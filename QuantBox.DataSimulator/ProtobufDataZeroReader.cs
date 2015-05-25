using Ideafixxxer.Generics;
using QuantBox.Data.Serializer;
using QuantBox.Data.Serializer.V2;
using QuantBox.Extensions;
using QuantBox.XAPI;
using SevenZip;
using SmartQuant;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace QuantBox
{
    public class ProtobufDataZeroReader
    {
        public bool SubscribeExternData = true;
        public bool SubscribeAsk = true;
        public bool SubscribeBid = true;

        int _InstrumentId;
        public QuantBox.Data.Serializer.PbTickSerializer Serializer = new QuantBox.Data.Serializer.PbTickSerializer();
        public List<QuantBox.Data.Serializer.V2.PbTickView> Series = new List<Data.Serializer.V2.PbTickView>();

        public List<QuantBox.Data.Serializer.V2.PbTickView> ReadOneFile(FileInfo file)
        {
            Stream _stream = new MemoryStream();
            var fileStream = file.Open(FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            {
                try
                {
                    using (var zip = new SevenZipExtractor(fileStream))
                    {
                        zip.ExtractFile(0, _stream);
                        _stream.Seek(0, SeekOrigin.Begin);
                    }
                }
                catch(Exception ex)
                {
                    _stream = fileStream;
                    _stream.Seek(0, SeekOrigin.Begin);
                }
            }
            return Serializer.Read2View(_stream);
        }
        public void GetDataSeries(int instrumentId,string DataPath, DateTime dateTime1, DateTime dateTime2)
        {
            _InstrumentId = instrumentId;
            if (!Directory.Exists(DataPath))
            {
                return;
            }

            var list = new DirectoryInfo(DataPath).GetFiles().ToList();
            //var list = new DirectoryInfo(DataPath).GetFiles("IF1506*",SearchOption.AllDirectories).ToList();
            list.Sort((x, y) => String.Compare(x.Name, y.Name, StringComparison.Ordinal));

            foreach (var file in list)
            {
                var macth = Regex.Match(file.Name, @"\d{8}");
                if (macth.Success)
                {
                    DateTime date;
                    if (DateTime.TryParseExact(macth.Groups[0].Value, "yyyyMMdd", null, DateTimeStyles.None, out date)
                        && date >= dateTime1
                        && date <= dateTime2)
                    {
                        Series.AddRange(ReadOneFile(file));
                    }
                }
            }
        }

        private DepthMarketDataNClass PbTick2DepthMarketDataNClass(PbTickCodec codec, PbTickView tickView)
        {
            DepthMarketDataNClass marketData = new DepthMarketDataNClass();
            codec.GetUpdateTime(tickView, out marketData.UpdateTime, out marketData.UpdateMillisec);

            marketData.TradingDay = tickView.TradingDay;
            marketData.ActionDay = tickView.ActionDay;
            marketData.LastPrice = tickView.LastPrice;
            marketData.Volume = tickView.Volume;
            if (SubscribeExternData)
            {
                marketData.Turnover = tickView.Turnover;
                marketData.OpenInterest = tickView.OpenInterest;
                marketData.AveragePrice = tickView.AveragePrice;
                if (tickView.Bar != null)
                {
                    marketData.OpenPrice = tickView.Bar.Open;
                    marketData.HighestPrice = tickView.Bar.High;
                    marketData.LowestPrice = tickView.Bar.Low;
                    marketData.ClosePrice = tickView.Bar.Close;
                }
                if (tickView.Static != null)
                {
                    marketData.LowerLimitPrice = tickView.Static.LowerLimitPrice;
                    marketData.UpperLimitPrice = tickView.Static.UpperLimitPrice;
                    marketData.SettlementPrice = tickView.Static.SettlementPrice;
                    marketData.Symbol = tickView.Static.Symbol;
                    if (!string.IsNullOrWhiteSpace(tickView.Static.Exchange))
                    {
                        marketData.Exchange = Enum<ExchangeType>.Parse(tickView.Static.Exchange);
                    }
                }
            }

            int count = tickView.DepthList == null ? 0 : tickView.DepthList.Count;
            if (count > 0)
            {
                int AskPos = DepthListHelper.FindAsk1Position(tickView.DepthList, tickView.AskPrice1);
                int BidPos = AskPos - 1;
                if(SubscribeBid)
                {
                    int BidCount = BidPos+1;
                    if(BidCount>0)
                    {
                        marketData.Bids = new DepthField[BidCount];
                        int j = 0;
                        for (int i = BidPos; i >= 0; --i)
                        {
                            marketData.Bids[j] = new DepthField()
                            {
                                Price = tickView.DepthList[i].Price,
                                Size = tickView.DepthList[i].Size,
                                Count = tickView.DepthList[i].Count,
                            };
                            ++j;
                        }
                    }
                }
                if (SubscribeAsk)
                {
                    int AskCount = count - AskPos;
                    if (AskCount > 0)
                    {
                        marketData.Asks = new DepthField[AskCount];

                        int j = 0;
                        for (int i = AskPos; i < count; ++i)
                        {
                            marketData.Asks[j] = new DepthField()
                            {
                                Price = tickView.DepthList[i].Price,
                                Size = tickView.DepthList[i].Size,
                                Count = tickView.DepthList[i].Count,
                            };
                            ++j;
                        }
                    }
                }
            }
            return marketData;
        }

        public void OutputSeries(out IDataSeries trades, out IDataSeries bids, out IDataSeries asks)
        {
            trades = new TickSeries();
            bids = new TickSeries();
            asks = new TickSeries();

            PbTickCodec codec = new PbTickCodec();
            int TradingDay = -1;
            int _lastTradeSize = 0;
            foreach (var s in Series)
            {
                if(TradingDay != s.TradingDay)
                {
                    _lastTradeSize = 0;
                    TradingDay = s.TradingDay;
                }
                var dateTime = codec.GetDateTime(s.ActionDay == 0 ? s.TradingDay : s.ActionDay).Add(codec.GetUpdateTime(s));
                var tick = PbTick2DepthMarketDataNClass(codec, s);

                if(SubscribeExternData)
                {
                    var trade = new TradeEx(dateTime, 0, _InstrumentId, tick.LastPrice, (int)tick.Volume);
                    trade.Size -= _lastTradeSize;
                    trade.DepthMarketData = tick;
                    trades.Add(trade);
                }
                else
                {
                    var trade = new Trade(dateTime, 0, _InstrumentId, tick.LastPrice, (int)tick.Volume);
                    trade.Size -= _lastTradeSize;
                    trades.Add(trade);
                }


                if (tick.Bids != null && tick.Bids.Length > 0)
                {
                    var bid = new Bid(dateTime, 0, _InstrumentId, tick.Bids[0].Price, tick.Bids[0].Size);
                    bids.Add(bid);
                }
                if (tick.Asks != null && tick.Asks.Length > 0)
                {
                    var ask = new Ask(dateTime, 0, _InstrumentId, tick.Asks[0].Price, tick.Asks[0].Size);
                    asks.Add(ask);
                }

                _lastTradeSize = (int)tick.Volume;
            }
        }
    }
}
