using ArchiveData;
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

        public string DataPath_Realtime;
        public string DataPath_Instrument;

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

        private void UnionAndUpdate(SortedDictionary<int,FileInfo> sd_old,SortedDictionary<int, FileInfo> sd_new)
        {
            // 用新的覆盖旧的
            foreach(var kv in sd_new)
            {
                sd_old[kv.Key] = kv.Value;
            }
        }

        // 从分类好的目录中取中所有合约
        private SortedDictionary<int, FileInfo> GetData_Instrument(Instrument inst)
        {
            SortedDictionary<int, FileInfo> resultList = new SortedDictionary<int, FileInfo>();

            if (string.IsNullOrEmpty(DataPath_Instrument))
                return resultList;            

            // 直接查找某一目录是否存在
            string instrument = inst.Symbol;
            int i = inst.Symbol.IndexOf('.');
            if(i>=0)
            {
                instrument = instrument.Substring(0,i);
            }

            var di = new DirectoryInfo(DataPath_Instrument);

            if (!di.Exists)
                return resultList;

            var list = di.GetDirectories(instrument, System.IO.SearchOption.AllDirectories);
            foreach(var l in list)
            {
                UnionAndUpdate(resultList, GetFiles(l, inst.Symbol));
            }

            return resultList;
        }

        // 从实时目录中取
        private SortedDictionary<int, FileInfo> GetData_Realtime(Instrument inst)
        {
            // 直接查找某一类的文件是否存在
            SortedDictionary<int, FileInfo> resultList = new SortedDictionary<int, FileInfo>();

            if (string.IsNullOrEmpty(DataPath_Realtime))
                return resultList;   

            var di = new DirectoryInfo(DataPath_Realtime);

            if (!di.Exists)
                return resultList;

            // 这下会找到很多相同名字开头的合约，需要再处理一下，需要得到匹配度最高的
            UnionAndUpdate(resultList, GetFiles(di, inst.Symbol));

            return resultList;
        }

        private SortedDictionary<int, FileInfo> GetFiles(DirectoryInfo di, string symbol)
        {
            SortedDictionary<int, FileInfo> resultList = new SortedDictionary<int,FileInfo>();

            var list = di.GetFiles(symbol + "*").ToList();
            foreach (var l in list)
            {
                string _exchange = string.Empty;
                string _product = string.Empty;
                string _instrument = string.Empty;
                string _symbol = string.Empty;
                string _date = string.Empty;
                if (PathHelper.SplitFileName(l.Name, out _exchange, out _product, out _instrument, out _date))
                {
                    int __date = int.Parse(_date);
                    // 合约名中带交易所
                    if (symbol.IndexOf('.') >= 0)
                    {
                        if (symbol == string.Format("{0}.{1}", _instrument, _exchange))
                        {
                            resultList.Add(__date, l);
                        }
                    }
                    else
                    {
                        if (symbol == _instrument)
                        {
                            resultList.Add(__date, l);
                        }
                    }
                }
            }

            return resultList;
        }

        public void GetDataSeries(Instrument instrument, DateTime dateTime1, DateTime dateTime2)
        {
            _InstrumentId = instrument.Id;

            SortedDictionary<int, FileInfo> resultList = new SortedDictionary<int, FileInfo>();

            // 先取归档的文件，再取实时的，这样实时的会覆盖归档的，即同一日期的文件优先取实时的
            UnionAndUpdate(resultList, GetData_Instrument(instrument));
            UnionAndUpdate(resultList, GetData_Realtime(instrument));

            int dt1 = dateTime1.Year * 10000 + dateTime1.Month * 100 + dateTime1.Day;
            int dt2 = dateTime2.Year * 10000 + dateTime2.Month * 100 + dateTime2.Day;

            foreach (var kv in resultList)
            {
                if (kv.Key >= dt1
                    && kv.Key <= dt2)
                {
                    Console.WriteLine("{0} {1}::Load {2}", DateTime.Now, "QBDataSimulator", kv.Value.FullName);
                    Series.AddRange(ReadOneFile(kv.Value));
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
                    marketData.PreClosePrice = tickView.Static.PreClosePrice;
                    marketData.PreSettlementPrice = tickView.Static.PreSettlementPrice;
                    marketData.PreOpenInterest = tickView.Static.PreOpenInterest;
                }
            }

            int count = tickView.DepthList == null ? 0 : tickView.DepthList.Count;
            if (count > 0)
            {
                int AskPos = DepthListHelper.FindAsk1Position(tickView.DepthList, tickView.AskPrice1);
                int BidPos = AskPos - 1;
                int BidCount = BidPos + 1;
                int AskCount = count - AskPos;

                marketData.Bids = new DepthField[0];
                marketData.Asks = new DepthField[0];

                if(SubscribeBid)
                {
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
