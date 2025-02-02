namespace StockSharp.Algo.Strategies.Testing
{
	using System;
	using System.Collections.Generic;
	using System.Linq;
	using System.Threading;

	using Ecng.Collections;
	using Ecng.Common;
	using Ecng.Serialization;

	using StockSharp.Algo.Storages;
	using StockSharp.Algo.Testing;
	using StockSharp.BusinessEntities;
	using StockSharp.Messages;
	using StockSharp.Logging;
	using StockSharp.Localization;

	/// <summary>
	/// The batch emulator of strategies.
	/// </summary>
	public class BatchEmulation : BaseLogReceiver
	{
		private readonly List<HistoryEmulationConnector> _currentConnectors = new();
		private bool _cancelEmulation;
		private int _nextTotalProgress;
		private DateTime _startedAt;

		private readonly SyncObject _sync = new();

		private readonly ISecurityProvider _securityProvider;
		private readonly IPortfolioProvider _portfolioProvider;
		private readonly IExchangeInfoProvider _exchangeInfoProvider;

		/// <summary>
		/// Initializes a new instance of the <see cref="BatchEmulation"/>.
		/// </summary>
		/// <param name="securities">Instruments, the operation will be performed with.</param>
		/// <param name="portfolios">Portfolios, the operation will be performed with.</param>
		/// <param name="storageRegistry">Market data storage.</param>
		public BatchEmulation(IEnumerable<Security> securities, IEnumerable<Portfolio> portfolios, IStorageRegistry storageRegistry)
			: this(new CollectionSecurityProvider(securities), new CollectionPortfolioProvider(portfolios), storageRegistry.CheckOnNull(nameof(storageRegistry)).ExchangeInfoProvider, storageRegistry, StorageFormats.Binary, storageRegistry.DefaultDrive)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BatchEmulation"/>.
		/// </summary>
		/// <param name="securityProvider">The provider of information about instruments.</param>
		/// <param name="portfolioProvider">The portfolio to be used to register orders. If value is not given, the portfolio with default name Simulator will be created.</param>
		/// <param name="storageRegistry">Market data storage.</param>
		public BatchEmulation(ISecurityProvider securityProvider, IPortfolioProvider portfolioProvider, IStorageRegistry storageRegistry)
			: this(securityProvider, portfolioProvider, storageRegistry.CheckOnNull(nameof(storageRegistry)).ExchangeInfoProvider, storageRegistry, StorageFormats.Binary, storageRegistry.DefaultDrive)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="BatchEmulation"/>.
		/// </summary>
		/// <param name="securityProvider">The provider of information about instruments.</param>
		/// <param name="portfolioProvider">The portfolio to be used to register orders. If value is not given, the portfolio with default name Simulator will be created.</param>
		/// <param name="exchangeInfoProvider">Exchanges and trading boards provider.</param>
		/// <param name="storageRegistry">Market data storage.</param>
		/// <param name="storageFormat">The format of market data. <see cref="StorageFormats.Binary"/> is used by default.</param>
		/// <param name="drive">The storage which is used by default. By default, <see cref="IStorageRegistry.DefaultDrive"/> is used.</param>
		public BatchEmulation(ISecurityProvider securityProvider, IPortfolioProvider portfolioProvider, IExchangeInfoProvider exchangeInfoProvider, IStorageRegistry storageRegistry, StorageFormats storageFormat = StorageFormats.Binary, IMarketDataDrive drive = null)
		{
			_securityProvider = securityProvider ?? throw new ArgumentNullException(nameof(securityProvider));
			_portfolioProvider = portfolioProvider ?? throw new ArgumentNullException(nameof(portfolioProvider));
			_exchangeInfoProvider = exchangeInfoProvider ?? throw new ArgumentNullException(nameof(exchangeInfoProvider));

			EmulationSettings = new();

			StorageSettings = new()
			{
				StorageRegistry = storageRegistry,
				Drive = drive,
				Format = storageFormat,
			};
		}

		/// <summary>
		/// Storage settings.
		/// </summary>
		public StorageCoreSettings StorageSettings { get; }

		/// <summary>
		/// Emulation settings.
		/// </summary>
		public EmulationSettings EmulationSettings { get; }

		/// <summary>
		/// <see cref="HistoryMessageAdapter.AdapterCache"/>.
		/// </summary>
		public MarketDataStorageCache AdapterCache { get; set; }

		/// <summary>
		/// <see cref="HistoryMessageAdapter.StorageCache"/>.
		/// </summary>
		public MarketDataStorageCache StorageCache { get; set; }

		/// <summary>
		/// Has the emulator ended its operation due to end of data, or it was interrupted through the <see cref="Stop"/> method.
		/// </summary>
		public bool IsCancelled { get; private set; }

		private ChannelStates _state = ChannelStates.Stopped;
		
		/// <summary>
		/// The emulator state.
		/// </summary>
		public ChannelStates State
		{
			get => _state;
			private set
			{
				if (_state == value)
					return;

				var oldState = _state;
				_state = value;
				StateChanged?.Invoke(oldState, _state);
			}
		}

		/// <summary>
		/// The event on change of paper trade state.
		/// </summary>
		public event Action<ChannelStates, ChannelStates> StateChanged;

		/// <summary>
		/// The event of total progress change.
		/// </summary>
		public event Action<int, TimeSpan, TimeSpan> TotalProgressChanged;

		/// <summary>
		/// The event of single progress change.
		/// </summary>
		public event Action<Strategy, int> SingleProgressChanged;

		/// <summary>
		/// Start emulation.
		/// </summary>
		/// <param name="strategies">The strategies.</param>
		/// <param name="iterationCount">Iteration count.</param>
		public void Start(IEnumerable<Strategy> strategies, int iterationCount)
		{
			if (strategies is null)
				throw new ArgumentNullException(nameof(strategies));

			if (iterationCount <= 0)
				throw new ArgumentOutOfRangeException(nameof(iterationCount), iterationCount, LocalizedStrings.Str1219);

			_cancelEmulation = false;
			_nextTotalProgress = 0;
			_startedAt = DateTime.UtcNow;

			if (EmulationSettings.MaxIterations > 0 && iterationCount > EmulationSettings.MaxIterations)
			{
				iterationCount = EmulationSettings.MaxIterations;
				strategies = strategies.Take(iterationCount);
			}

			State = ChannelStates.Starting;

			var totalBatches = (int)((decimal)iterationCount / EmulationSettings.BatchSize).Ceiling();

			if (totalBatches == 0)
				throw new ArgumentOutOfRangeException(nameof(iterationCount), "totalBatches == 0");

			var batchSize = EmulationSettings.BatchSize;

			var adapterCaches = AdapterCache is null ? null : new Dictionary<int, MarketDataStorageCache>();
			var storageCaches = StorageCache is null ? null : new Dictionary<int, MarketDataStorageCache>();

			if (adapterCaches is not null)
			{
				for (int i = 0; i < batchSize; i++)
					adapterCaches.Add(i, new());
			}

			if (storageCaches is not null)
			{
				for (int i = 0; i < batchSize; i++)
					storageCaches.Add(i, new());
			}

			var batchWeight = 100.0 / totalBatches;

			TryStartNextBatch(
				strategies.Batch(batchSize).GetEnumerator(), 
				-1, totalBatches, batchWeight,
				adapterCaches, storageCaches);
		}

		private void TryStartNextBatch(
			IEnumerator<IEnumerable<Strategy>> batches,
			int currentBatch, int totalBatches, double batchWeight,
			IDictionary<int, MarketDataStorageCache> adapterCaches,
			IDictionary<int, MarketDataStorageCache> storageCaches)
		{
			if (batches is null)
				throw new ArgumentNullException(nameof(batches));

			lock (_sync)
			{
				if (_cancelEmulation || !batches.MoveNext())
				{
					IsCancelled = _cancelEmulation;

					State = ChannelStates.Stopping;
					State = ChannelStates.Stopped;

					return;
				}

				var batch = batches.Current.ToArray();
				currentBatch++;

				if (currentBatch == 0)
				{
					State = ChannelStates.Starting;
					State = ChannelStates.Started;
				}

				var progress = new SynchronizedDictionary<HistoryEmulationConnector, int>();
				var left = batch.Length;

				var nextProgress = 1;

				_currentConnectors.Clear();

				foreach (var strategy in batch)
				{
					var idx = _currentConnectors.Count;

					var connector = new HistoryEmulationConnector(_securityProvider, _portfolioProvider, _exchangeInfoProvider)
					{
						Parent = this,

						HistoryMessageAdapter =
						{
							StorageRegistry = StorageSettings.StorageRegistry,
							Drive = StorageSettings.Drive,
							StorageFormat = StorageSettings.Format,
							StartDate = EmulationSettings.StartTime,
							StopDate = EmulationSettings.StopTime,

							AdapterCache = adapterCaches?.TryGetValue(idx),
							StorageCache = storageCaches?.TryGetValue(idx),
						},
					};
					connector.EmulationAdapter.Settings.Load(EmulationSettings.Save());

					strategy.Connector = connector;

					strategy.Reset();
					strategy.Start();

					progress.Add(connector, 0);

					connector.ProgressChanged += step =>
					{
						SingleProgressChanged?.Invoke(strategy, step);

						var avgStep = 0;

						lock (progress.SyncRoot)
						{
							progress[connector] = step;
							avgStep = (int)progress.Values.Average();
						}

						if (avgStep < nextProgress)
							return;

						nextProgress++;

						var currTotalProgress = (int)(currentBatch * batchWeight + ((avgStep * batchWeight) / 100));

						if (_nextTotalProgress >= currTotalProgress)
							return;

						var now = DateTime.UtcNow;
						var duration = now - _startedAt;

						TotalProgressChanged?.Invoke(currTotalProgress, duration, TimeSpan.FromTicks((duration.Ticks * 100) / currTotalProgress));
						_nextTotalProgress = currTotalProgress + 1;
					};

					connector.StateChanged += () =>
					{
						if (connector.State == ChannelStates.Stopped)
						{
							if (progress[connector] != 100)
								SingleProgressChanged?.Invoke(strategy, 100);

							if (Interlocked.Decrement(ref left) == 0)
								TryStartNextBatch(batches, currentBatch, totalBatches, batchWeight, adapterCaches, storageCaches);
						}
					};

					_currentConnectors.Add(connector);
				}

				foreach (var connector in _currentConnectors)
				{
					connector.Connect();
					connector.Start();
				}
			}
		}

		/// <summary>
		/// To suspend the emulation.
		/// </summary>
		public void Suspend()
		{
			ThreadingHelper.Thread(() =>
			{
				lock (_sync)
				{
					if (State != ChannelStates.Started)
						return;

					State = ChannelStates.Suspending;

					foreach (var connector in _currentConnectors)
					{
						if (connector.State == ChannelStates.Started)
							connector.Suspend();
					}

					State = ChannelStates.Suspended;
				}
			}).Launch();
		}

		/// <summary>
		/// To resume the emulation.
		/// </summary>
		public void Resume()
		{
			ThreadingHelper.Thread(() =>
			{
				lock (_sync)
				{
					if (State != ChannelStates.Suspended)
						return;

					State = ChannelStates.Starting;

					foreach (var connector in _currentConnectors)
					{
						if (connector.State == ChannelStates.Suspended)
							connector.Start();
					}

					State = ChannelStates.Started;
				}
			}).Launch();
		}

		/// <summary>
		/// To stop paper trading.
		/// </summary>
		public void Stop()
		{
			ThreadingHelper.Thread(() =>
			{
				lock (_sync)
				{
					if (!(State is ChannelStates.Started or ChannelStates.Suspended))
						return;

					State = ChannelStates.Stopping;

					_cancelEmulation = true;

					foreach (var connector in _currentConnectors)
					{
						if (connector.State is
							ChannelStates.Started or
							ChannelStates.Starting or
							ChannelStates.Suspended or
							ChannelStates.Suspending)
							connector.Disconnect();
					}
				}
			}).Launch();
		}

		/// <inheritdoc />
		protected override void DisposeManaged()
		{
			base.DisposeManaged();
			Stop();
		}
	}
}