#region S# License
/******************************************************************************************
NOTICE!!!  This program and source code is owned and licensed by
StockSharp, LLC, www.stocksharp.com
Viewing or use of this code requires your acceptance of the license
agreement found at https://github.com/StockSharp/StockSharp/blob/master/LICENSE
Removal of this comment is a violation of the license agreement.

Project: StockSharp.Algo.Candles.Algo
File: CandleHelper.cs
Created: 2015, 11, 11, 2:32 PM

Copyright 2010 by StockSharp, LLC
*******************************************************************************************/
#endregion S# License
namespace StockSharp.Algo.Candles
{
	using System;
	using System.Collections;
	using System.Collections.Generic;
	using System.Linq;

	using Ecng.Collections;
	using Ecng.Common;
	using Ecng.ComponentModel;
	using Ecng.Configuration;

	using StockSharp.Algo.Candles.Compression;
	using StockSharp.Algo.Candles.Patterns;
	using StockSharp.BusinessEntities;
	using StockSharp.Localization;
	using StockSharp.Messages;

	/// <summary>
	/// Extension class for candles.
	/// </summary>
	public static class CandleHelper
	{
		/// <summary>
		/// Try get suitable market-data type for candles compression.
		/// </summary>
		/// <param name="adapter">Adapter.</param>
		/// <param name="subscription">Subscription.</param>
		/// <param name="provider">Candle builders provider.</param>
		/// <returns>Which market-data type is used as a source value. <see langword="null"/> is compression is impossible.</returns>
		public static DataType TryGetCandlesBuildFrom(this IMessageAdapter adapter, MarketDataMessage subscription, CandleBuilderProvider provider)
		{
			if (adapter == null)
				throw new ArgumentNullException(nameof(adapter));

			if (subscription == null)
				throw new ArgumentNullException(nameof(subscription));

			if (provider == null)
				throw new ArgumentNullException(nameof(provider));

			if (!provider.IsRegistered(subscription.DataType2.MessageType))
				return null;

			if (subscription.BuildMode == MarketDataBuildModes.Load)
				return null;

			var buildFrom = subscription.BuildFrom ?? adapter.SupportedMarketDataTypes.Intersect(DataType.CandleSources).OrderBy(t =>
			{
				// by priority
				if (t == DataType.Ticks)
					return 0;
				else if (t == DataType.Level1)
					return 1;
				else if (t == DataType.OrderLog)
					return 2;
				else if (t == DataType.MarketDepth)
					return 3;
				else
					return 4;
			}).FirstOrDefault();

			if (buildFrom == null || !adapter.SupportedMarketDataTypes.Contains(buildFrom))
				return null;

			return buildFrom;
		}

		/// <summary>
		/// Determines whether the specified type is derived from <see cref="Candle"/>.
		/// </summary>
		/// <param name="candleType">The candle type.</param>
		/// <returns><see langword="true"/> if the specified type is derived from <see cref="Candle"/>, otherwise, <see langword="false"/>.</returns>
		public static bool IsCandle(this Type candleType)
		{
			if (candleType == null)
				throw new ArgumentNullException(nameof(candleType));

			return candleType.IsSubclassOf(typeof(Candle));
		}

		/// <summary>
		/// To create <see cref="CandleSeries"/> for <see cref="TimeFrameCandle"/> candles.
		/// </summary>
		/// <param name="security">Security.</param>
		/// <param name="arg">The value of <see cref="TimeFrameCandle.TimeFrame"/>.</param>
		/// <returns>Candles series.</returns>
		public static CandleSeries TimeFrame(this Security security, TimeSpan arg)
		{
			return new CandleSeries(typeof(TimeFrameCandle), security, arg);
		}

		/// <summary>
		/// To create <see cref="CandleSeries"/> for <see cref="RangeCandle"/> candles.
		/// </summary>
		/// <param name="security">Security.</param>
		/// <param name="arg">The value of <see cref="RangeCandle.PriceRange"/>.</param>
		/// <returns>Candles series.</returns>
		public static CandleSeries Range(this Security security, Unit arg)
		{
			return new CandleSeries(typeof(RangeCandle), security, arg);
		}

		/// <summary>
		/// To create <see cref="CandleSeries"/> for <see cref="VolumeCandle"/> candles.
		/// </summary>
		/// <param name="security">Security.</param>
		/// <param name="arg">The value of <see cref="VolumeCandle.Volume"/>.</param>
		/// <returns>Candles series.</returns>
		public static CandleSeries Volume(this Security security, decimal arg)
		{
			return new CandleSeries(typeof(VolumeCandle), security, arg);
		}

		/// <summary>
		/// To create <see cref="CandleSeries"/> for <see cref="TickCandle"/> candles.
		/// </summary>
		/// <param name="security">Security.</param>
		/// <param name="arg">The value of <see cref="TickCandle.MaxTradeCount"/>.</param>
		/// <returns>Candles series.</returns>
		public static CandleSeries Tick(this Security security, decimal arg)
		{
			return new CandleSeries(typeof(TickCandle), security, arg);
		}

		/// <summary>
		/// To create <see cref="CandleSeries"/> for <see cref="PnFCandle"/> candles.
		/// </summary>
		/// <param name="security">Security.</param>
		/// <param name="arg">The value of <see cref="PnFCandle.PnFArg"/>.</param>
		/// <returns>Candles series.</returns>
		public static CandleSeries PnF(this Security security, PnFArg arg)
		{
			return new CandleSeries(typeof(PnFCandle), security, arg);
		}

		/// <summary>
		/// To create <see cref="CandleSeries"/> for <see cref="RenkoCandle"/> candles.
		/// </summary>
		/// <param name="security">Security.</param>
		/// <param name="arg">The value of <see cref="RenkoCandle.BoxSize"/>.</param>
		/// <returns>Candles series.</returns>
		public static CandleSeries Renko(this Security security, Unit arg)
		{
			return new CandleSeries(typeof(RenkoCandle), security, arg);
		}

		/// <summary>
		/// To start candles getting.
		/// </summary>
		/// <typeparam name="TCandle"><see cref="ICandleMessage"/></typeparam>
		/// <param name="manager">The candles manager.</param>
		/// <param name="series">Candles series.</param>
		public static void Start<TCandle>(this ICandleManager<TCandle> manager, CandleSeries series)
			where TCandle : ICandleMessage
		{
			manager.CheckOnNull(nameof(manager)).Start(series, series.From, series.To);
		}

		/// <summary>
		/// To get a candles series by the specified parameters.
		/// </summary>
		/// <typeparam name="TCandle">Candles type.</typeparam>
		/// <param name="candleManager">The candles manager.</param>
		/// <param name="security">The instrument by which trades should be filtered for the candles creation.</param>
		/// <param name="arg">Candle arg.</param>
		/// <returns>The candles series. <see langword="null" /> if this series is not registered.</returns>
		public static CandleSeries GetSeries<TCandle>(this ICandleManager<TCandle> candleManager, Security security, object arg)
			where TCandle : ICandleMessage
		{
			return candleManager.CheckOnNull(nameof(candleManager)).Series.FirstOrDefault(s => s.CandleType == typeof(TCandle) && s.Security == security && s.Arg.Equals(arg));
		}

		private static IEnumerable<CandleMessage> ToCandles<TSourceMessage>(this IEnumerable<TSourceMessage> messages, MarketDataMessage mdMsg, Func<TSourceMessage, ICandleBuilderValueTransform> createTransform, CandleBuilderProvider candleBuilderProvider = null)
			where TSourceMessage : Message
		{
			if (createTransform is null)
				throw new ArgumentNullException(nameof(createTransform));

			CandleMessage lastActiveCandle = null;

			using var builder = candleBuilderProvider.CreateBuilder(mdMsg.DataType2.MessageType);

			var subscription = new CandleBuilderSubscription(mdMsg);
			var isFinishedOnly = mdMsg.IsFinishedOnly;

			ICandleBuilderValueTransform transform = null;

			foreach (var message in messages)
			{
				transform ??= createTransform(message);

				if (!transform.Process(message))
					continue;

				foreach (var candle in builder.Process(subscription, transform))
				{
					if (candle.State == CandleStates.Finished)
					{
						lastActiveCandle = null;
						yield return candle;
					}
					else
					{
						if (!isFinishedOnly)
							lastActiveCandle = candle;
					}
				}
			}

			if (lastActiveCandle != null)
				yield return lastActiveCandle;
		}

		/// <summary>
		/// To create candles from the tick trades collection.
		/// </summary>
		/// <typeparam name="TCandle">Candles type.</typeparam>
		/// <param name="trades">Tick trades.</param>
		/// <param name="arg">Candle arg.</param>
		/// <param name="onlyFormed">Send only formed candles.</param>
		/// <returns>Candles.</returns>
		public static IEnumerable<TCandle> ToCandles<TCandle>(this IEnumerable<Trade> trades, object arg, bool onlyFormed = true)
			where TCandle : Candle
		{
			var firstTrade = trades.FirstOrDefault();

			if (firstTrade == null)
				return Enumerable.Empty<TCandle>();

			return trades.ToCandles(new CandleSeries(typeof(TCandle), firstTrade.Security, arg) { IsFinishedOnly = onlyFormed }).Cast<TCandle>();
		}

		/// <summary>
		/// To create candles from the tick trades collection.
		/// </summary>
		/// <param name="trades">Tick trades.</param>
		/// <param name="series">Candles series.</param>
		/// <returns>Candles.</returns>
		public static IEnumerable<Candle> ToCandles(this IEnumerable<Trade> trades, CandleSeries series)
		{
			return trades
				.ToMessages<Trade, ExecutionMessage>()
				.ToCandles(series)
				.ToCandles<Candle>(series.Security);
		}

		/// <summary>
		/// To create candles from the tick trades collection.
		/// </summary>
		/// <param name="trades">Tick trades.</param>
		/// <param name="series">Candles series.</param>
		/// <param name="candleBuilderProvider">Candle builders provider.</param>
		/// <returns>Candles.</returns>
		public static IEnumerable<CandleMessage> ToCandles(this IEnumerable<ExecutionMessage> trades, CandleSeries series, CandleBuilderProvider candleBuilderProvider = null)
		{
			return trades.ToCandles(series.ToMarketDataMessage(true), candleBuilderProvider);
		}

		private static ICandleBuilder CreateBuilder(this CandleBuilderProvider candleBuilderProvider, Type messageType)
		{
			if (messageType is null)
				throw new ArgumentNullException(nameof(messageType));

			candleBuilderProvider ??= ConfigManager.TryGetService<CandleBuilderProvider>() ?? new CandleBuilderProvider(ServicesRegistry.EnsureGetExchangeInfoProvider());

			return candleBuilderProvider.Get(messageType);
		}

		/// <summary>
		/// To create candles from the tick trades collection.
		/// </summary>
		/// <param name="executions">Tick data.</param>
		/// <param name="mdMsg">Market data subscription.</param>
		/// <param name="candleBuilderProvider">Candle builders provider.</param>
		/// <returns>Candles.</returns>
		public static IEnumerable<CandleMessage> ToCandles(this IEnumerable<ExecutionMessage> executions, MarketDataMessage mdMsg, CandleBuilderProvider candleBuilderProvider = null)
		{
			return executions.ToCandles(mdMsg, execMsg =>
			{
				if (execMsg.DataType == DataType.Ticks)
					return new TickCandleBuilderValueTransform();
				else if (execMsg.DataType == DataType.OrderLog)
					return new OrderLogCandleBuilderValueTransform();
				else
					throw new ArgumentOutOfRangeException(nameof(execMsg), execMsg.DataType, LocalizedStrings.Str1219);
			}, candleBuilderProvider);
		}

		/// <summary>
		/// To create candles from the order books collection.
		/// </summary>
		/// <param name="depths">Market depths.</param>
		/// <param name="series">Candles series.</param>
		/// <param name="type">Type of candle depth based data.</param>
		/// <param name="candleBuilderProvider">Candle builders provider.</param>
		/// <returns>Candles.</returns>
		public static IEnumerable<Candle> ToCandles(this IEnumerable<MarketDepth> depths, CandleSeries series, Level1Fields type = Level1Fields.SpreadMiddle, CandleBuilderProvider candleBuilderProvider = null)
		{
			return depths
				.ToMessages<MarketDepth, QuoteChangeMessage>()
				.ToCandles(series, type, candleBuilderProvider)
				.ToCandles<Candle>(series.Security);
		}

		/// <summary>
		/// To create candles from the order books collection.
		/// </summary>
		/// <param name="depths">Market depths.</param>
		/// <param name="series">Candles series.</param>
		/// <param name="type">Type of candle depth based data.</param>
		/// <param name="candleBuilderProvider">Candle builders provider.</param>
		/// <returns>Candles.</returns>
		public static IEnumerable<CandleMessage> ToCandles(this IEnumerable<QuoteChangeMessage> depths, CandleSeries series, Level1Fields type = Level1Fields.SpreadMiddle, CandleBuilderProvider candleBuilderProvider = null)
		{
			return depths.ToCandles(series.ToMarketDataMessage(true), type, candleBuilderProvider);
		}

		/// <summary>
		/// To create candles from the order books collection.
		/// </summary>
		/// <param name="depths">Market depths.</param>
		/// <param name="mdMsg">Market data subscription.</param>
		/// <param name="type">Type of candle depth based data.</param>
		/// <param name="candleBuilderProvider">Candle builders provider.</param>
		/// <returns>Candles.</returns>
		public static IEnumerable<CandleMessage> ToCandles(this IEnumerable<QuoteChangeMessage> depths, MarketDataMessage mdMsg, Level1Fields type = Level1Fields.SpreadMiddle, CandleBuilderProvider candleBuilderProvider = null)
		{
			return depths.ToCandles(mdMsg, quoteMsg => new QuoteCandleBuilderValueTransform { Type = type }, candleBuilderProvider);
		}

		/// <summary>
		/// To create ticks from candles.
		/// </summary>
		/// <param name="candles">Candles.</param>
		/// <returns>Trades.</returns>
		public static IEnumerable<Trade> ToTrades(this IEnumerable<Candle> candles)
		{
			var candle = candles.FirstOrDefault();

			if (candle == null)
				return Enumerable.Empty<Trade>();

			return candles
				.ToMessages<Candle, CandleMessage>()
				.ToTrades(candle.Security.VolumeStep ?? 1m)
				.ToEntities<ExecutionMessage, Trade>(candle.Security);
		}

		/// <summary>
		/// To create tick trades from candles.
		/// </summary>
		/// <param name="candles">Candles.</param>
		/// <param name="volumeStep">Volume step.</param>
		/// <returns>Tick trades.</returns>
		public static IEnumerable<ExecutionMessage> ToTrades(this IEnumerable<CandleMessage> candles, decimal volumeStep)
		{
			return new TradeEnumerable(candles, volumeStep);
		}

		/// <summary>
		/// To create tick trades from candle.
		/// </summary>
		/// <param name="candleMsg">Candle.</param>
		/// <param name="volumeStep">Volume step.</param>
		/// <param name="decimals">The number of decimal places for the volume.</param>
		/// <param name="ticks">Array to tick trades.</param>
		public static void ConvertToTrades(this CandleMessage candleMsg, decimal volumeStep, int decimals, (Sides? side, decimal price, decimal volume)[] ticks)
		{
			if (candleMsg is null)
				throw new ArgumentNullException(nameof(candleMsg));

			if (ticks is null)
				throw new ArgumentNullException(nameof(ticks));

			if (ticks.Length != 4)
				throw new ArgumentOutOfRangeException(nameof(ticks));

			var vol = (candleMsg.TotalVolume / 4).Round(volumeStep, decimals, MidpointRounding.AwayFromZero);
			var isUptrend = candleMsg.ClosePrice >= candleMsg.OpenPrice;

			if (candleMsg.OpenPrice == candleMsg.ClosePrice &&
				candleMsg.LowPrice == candleMsg.HighPrice &&
				candleMsg.OpenPrice == candleMsg.LowPrice ||
				candleMsg.TotalVolume == 1)
			{
				// все цены в свече равны или объем равен 1 - считаем ее за один тик
				ticks[0] = (Sides.Buy, candleMsg.OpenPrice, candleMsg.TotalVolume);

				ticks[1] = ticks[2] = ticks[3] = default;
			}
			else if (candleMsg.TotalVolume == 2)
			{
				ticks[0] = (Sides.Buy, candleMsg.HighPrice, 1);
				ticks[1] = (Sides.Sell, candleMsg.LowPrice, 1);

				ticks[2] = ticks[3] = default;
			}
			else if (candleMsg.TotalVolume == 3)
			{
				ticks[0] = (isUptrend ? Sides.Buy : Sides.Sell, candleMsg.OpenPrice, 1);
				ticks[1] = (Sides.Buy, candleMsg.HighPrice, 1);
				ticks[2] = (Sides.Sell, candleMsg.LowPrice, 1);

				ticks[3] = default;
			}
			else
			{
				ticks[0] = (isUptrend ? Sides.Buy : Sides.Sell, candleMsg.OpenPrice, vol);
				ticks[1] = (Sides.Buy, candleMsg.HighPrice, vol);
				ticks[2] = (Sides.Sell, candleMsg.LowPrice, vol);
				ticks[3] = (isUptrend ? Sides.Buy : Sides.Sell, candleMsg.ClosePrice, candleMsg.TotalVolume - 3 * vol);
			}
		}

		/// <summary>
		/// Convert tick info into <see cref="ExecutionMessage"/>.
		/// </summary>
		/// <param name="tick">Tick info.</param>
		/// <param name="securityId"><see cref="ExecutionMessage.SecurityId"/></param>
		/// <param name="localTime"><see cref="Message.LocalTime"/></param>
		/// <param name="serverTime"><see cref="ExecutionMessage.ServerTime"/></param>
		/// <returns><see cref="ExecutionMessage"/></returns>
		public static ExecutionMessage ToTickMessage(this (Sides? side, decimal price, decimal volume) tick, SecurityId securityId, DateTimeOffset localTime, DateTimeOffset serverTime)
		{
			return new()
			{
				LocalTime = localTime,
				SecurityId = securityId,
				ServerTime = serverTime,
				//TradeId = _tradeIdGenerator.Next,
				TradePrice = tick.price,
				TradeVolume = tick.volume,
				OriginSide = tick.side,
				DataTypeEx = DataType.Ticks,
				//OpenInterest = openInterest
			};
		}

		private sealed class TradeEnumerable : SimpleEnumerable<ExecutionMessage>//, IEnumerableEx<ExecutionMessage>
		{
			private sealed class TradeEnumerator : IEnumerator<ExecutionMessage>
			{
				private readonly decimal _volumeStep;
				private readonly IEnumerator<CandleMessage> _valuesEnumerator;
				private IEnumerator<ExecutionMessage> _currCandleEnumerator;
				private readonly int _decimals;
				private readonly (Sides? side, decimal price, decimal volume)[] _ticks = new (Sides?, decimal, decimal)[4];

				public TradeEnumerator(IEnumerable<CandleMessage> candles, decimal volumeStep)
				{
					_volumeStep = volumeStep;
					_decimals = volumeStep.GetCachedDecimals();
					_valuesEnumerator = candles.GetEnumerator();
				}

				private IEnumerable<ExecutionMessage> ToTicks(CandleMessage candleMsg)
				{
					candleMsg.ConvertToTrades(_volumeStep, _decimals, _ticks);

					foreach (var t in _ticks)
					{
						if (t == default)
							yield break;

						yield return t.ToTickMessage(candleMsg.SecurityId, candleMsg.LocalTime, candleMsg.OpenTime);
					}
				}

				public bool MoveNext()
				{
					if (_currCandleEnumerator == null)
					{
						if (_valuesEnumerator.MoveNext())
						{
							_currCandleEnumerator = ToTicks(_valuesEnumerator.Current).GetEnumerator();
						}
						else
						{
							Current = null;
							return false;
						}
					}

					if (_currCandleEnumerator.MoveNext())
					{
						Current = _currCandleEnumerator.Current;
						return true;
					}

					if (_valuesEnumerator.MoveNext())
					{
						_currCandleEnumerator = ToTicks(_valuesEnumerator.Current).GetEnumerator();

						_currCandleEnumerator.MoveNext();
						Current = _currCandleEnumerator.Current;

						return true;
					}
					
					Current = null;
					return false;
				}

				public void Reset()
				{
					_valuesEnumerator.Reset();
					Current = null;
				}

				public void Dispose()
				{
					Current = null;
					_valuesEnumerator.Dispose();
				}

				public ExecutionMessage Current { get; private set; }

				object IEnumerator.Current => Current;
			}

			public TradeEnumerable(IEnumerable<CandleMessage> candles, decimal volumeStep)
				: base(() => new TradeEnumerator(candles, volumeStep))
			{
				if (candles == null)
					throw new ArgumentNullException(nameof(candles));

				//_values = candles;
			}

			//private readonly IEnumerableEx<CandleMessage> _values;

			//public int Count => _values.Count * 4;
		}

		/// <summary>
		/// Whether the grouping of candles by the specified attribute is registered.
		/// </summary>
		/// <typeparam name="TCandle">Candles type.</typeparam>
		/// <param name="manager">The candles manager.</param>
		/// <param name="security">The instrument for which the grouping is registered.</param>
		/// <param name="arg">Candle arg.</param>
		/// <returns><see langword="true" /> if registered. Otherwise, <see langword="false" />.</returns>
		public static bool IsCandlesRegistered<TCandle>(this ICandleManager<TCandle> manager, Security security, object arg)
			where TCandle : ICandleMessage
		{
			return manager.GetSeries<TCandle>(security, arg) != null;
		}

		/// <summary>
		/// To get candle time frames relatively to the exchange working hours.
		/// </summary>
		/// <param name="timeFrame">The time frame for which you need to get time range.</param>
		/// <param name="currentTime">The current time within the range of time frames.</param>
		/// <param name="board">The information about the board from which <see cref="ExchangeBoard.WorkingTime"/> working hours will be taken.</param>
		/// <returns>The candle time frames.</returns>
		public static Range<DateTimeOffset> GetCandleBounds(this TimeSpan timeFrame, DateTimeOffset currentTime, ExchangeBoard board)
		{
			if (board == null)
				throw new ArgumentNullException(nameof(board));

			return timeFrame.GetCandleBounds(currentTime, board.TimeZone, board.WorkingTime);
		}

		/// <summary>
		/// To get the candle middle price.
		/// </summary>
		/// <param name="candle">The candle for which you need to get a length.</param>
		/// <returns>The candle length.</returns>
		public static decimal GetMiddlePrice(this ICandleMessage candle)
		{
			if (candle is null)
				throw new ArgumentNullException(nameof(candle));

			return candle.LowPrice + candle.GetLength() / 2;
		}

		/// <summary>
		/// To get the candle length.
		/// </summary>
		/// <param name="candle">The candle for which you need to get a length.</param>
		/// <returns>The candle length.</returns>
		public static decimal GetLength(this ICandleMessage candle)
		{
			if (candle == null)
				throw new ArgumentNullException(nameof(candle));

			return candle.HighPrice - candle.LowPrice;
		}

		/// <summary>
		/// To get the candle body.
		/// </summary>
		/// <param name="candle">The candle for which you need to get the body.</param>
		/// <returns>The candle body.</returns>
		public static decimal GetBody(this ICandleMessage candle)
		{
			if (candle == null)
				throw new ArgumentNullException(nameof(candle));

			return (candle.OpenPrice - candle.ClosePrice).Abs();
		}

		/// <summary>
		/// To get the candle upper shadow length.
		/// </summary>
		/// <param name="candle">The candle for which you need to get the upper shadow length.</param>
		/// <returns>The candle upper shadow length. If 0, there is no shadow.</returns>
		public static decimal GetTopShadow(this ICandleMessage candle)
		{
			if (candle == null)
				throw new ArgumentNullException(nameof(candle));

			return candle.HighPrice - candle.OpenPrice.Max(candle.ClosePrice);
		}

		/// <summary>
		/// To get the candle lower shadow length.
		/// </summary>
		/// <param name="candle">The candle for which you need to get the lower shadow length.</param>
		/// <returns>The candle lower shadow length. If 0, there is no shadow.</returns>
		public static decimal GetBottomShadow(this ICandleMessage candle)
		{
			if (candle == null)
				throw new ArgumentNullException(nameof(candle));

			return candle.OpenPrice.Min(candle.ClosePrice) - candle.LowPrice;
		}

		/// <summary>
		/// To get the number of time frames within the specified time range.
		/// </summary>
		/// <param name="range">The specified time range for which you need to get the number of time frames.</param>
		/// <param name="timeFrame">The time frame size.</param>
		/// <param name="board"><see cref="ExchangeBoard"/>.</param>
		/// <returns>The received number of time frames.</returns>
		public static long GetTimeFrameCount(this Range<DateTimeOffset> range, TimeSpan timeFrame, ExchangeBoard board)
		{
			if (board is null)
				throw new ArgumentNullException(nameof(board));

			return range.GetTimeFrameCount(timeFrame, board.WorkingTime, board.TimeZone);
		}

		/// <summary>
		/// To calculate the area for the candles group.
		/// </summary>
		/// <param name="candles">Candles.</param>
		/// <returns>The area.</returns>
		public static VolumeProfileBuilder GetValueArea(this IEnumerable<Candle> candles)
		{
			var area = new VolumeProfileBuilder(new List<CandlePriceLevel>());

			foreach (var candle in candles)
			{
				if (candle.PriceLevels == null)
					continue;

				foreach (var priceLevel in candle.PriceLevels)
				{
					area.Update(priceLevel);
				}
			}

			area.Calculate();
			return area;
		}

		/// <summary>
		/// Compress candles to bigger time-frame candles.
		/// </summary>
		/// <param name="source">Smaller time-frame candles.</param>
		/// <param name="compressor">Compressor of candles from smaller time-frames to bigger.</param>
		/// <param name="includeLastCandle">Output last active candle as finished.</param>
		/// <returns>Bigger time-frame candles.</returns>
		public static IEnumerable<CandleMessage> Compress(this IEnumerable<CandleMessage> source, BiggerTimeFrameCandleCompressor compressor, bool includeLastCandle)
		{
			if (source == null)
				throw new ArgumentNullException(nameof(source));

			if (compressor == null)
				throw new ArgumentNullException(nameof(compressor));

			CandleMessage lastActiveCandle = null;
			
			foreach (var message in source)
			{
				foreach (var candleMessage in compressor.Process(message))
				{
					if (candleMessage.State == CandleStates.Finished)
					{
						lastActiveCandle = null;
						yield return candleMessage;
					}
					else
						lastActiveCandle = candleMessage;
				}
			}

			if (!includeLastCandle || lastActiveCandle == null)
				yield break;

			lastActiveCandle.State = CandleStates.Finished;
			yield return lastActiveCandle;
		}

		/// <summary>
		/// Filter time-frames to find multiple smaller time-frames.
		/// </summary>
		/// <param name="timeFrames">All time-frames.</param>
		/// <param name="original">Original time-frame.</param>
		/// <returns>Multiple smaller time-frames.</returns>
		public static IEnumerable<TimeSpan> FilterSmallerTimeFrames(this IEnumerable<TimeSpan> timeFrames, TimeSpan original)
		{
			return timeFrames.Where(t => t < original && (original.Ticks % t.Ticks) == 0);
		}

		/// <summary>
		/// <see cref="ICandleMessage.PriceLevels"/> with minimum <see cref="CandlePriceLevel.TotalVolume"/>.
		/// </summary>
		/// <param name="candle"><see cref="ICandleMessage"/></param>
		/// <returns><see cref="ICandleMessage.PriceLevels"/> with minimum <see cref="CandlePriceLevel.TotalVolume"/>.</returns>
		public static CandlePriceLevel? MinPriceLevel(this ICandleMessage candle)
			=> candle.CheckOnNull(nameof(candle)).PriceLevels?.OrderBy(l => l.TotalVolume).FirstOr();

		/// <summary>
		/// <see cref="ICandleMessage.PriceLevels"/> with maximum <see cref="CandlePriceLevel.TotalVolume"/>.
		/// </summary>
		/// <param name="candle"><see cref="ICandleMessage"/></param>
		/// <returns><see cref="ICandleMessage.PriceLevels"/> with maximum <see cref="CandlePriceLevel.TotalVolume"/>.</returns>
		public static CandlePriceLevel? MaxPriceLevel(this ICandleMessage candle)
			=> candle.CheckOnNull(nameof(candle)).PriceLevels?.OrderByDescending(l => l.TotalVolume).FirstOr();

		/// <summary>
		/// Determines the specified candles are same.
		/// </summary>
		/// <typeparam name="TCandle"><see cref="ICandleMessage"/></typeparam>
		/// <param name="candle1">First.</param>
		/// <param name="candle2">Second.</param>
		/// <returns>Check result.</returns>
		public static bool IsSame<TCandle>(this TCandle candle1, TCandle candle2)
			where TCandle : ICandleMessage
			=> candle1 is not null && candle2 is not null && ((candle1 is Candle entity && ReferenceEquals(candle1, candle2)) || candle1.OpenTime == candle2.OpenTime);
	}
}