using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SmartQuant;

using QuantBox.Data.Serializer.V2;
using QuantBox.XAPI;
using Ideafixxxer.Generics;
using QuantBox.Extensions;

namespace QuantBox
{
    class DataSeriesObject_bak
    {
        readonly SubscribeInfo _info;
        readonly System.Collections.Generic.LinkedList<FileInfo> _files = new System.Collections.Generic.LinkedList<FileInfo>();

        int _count;
        int _completed;
        int _progressDelta;
        int _progressCount;
        int _progressPercent;
        TickReader _current;
        int _lastTradeSize;

        internal bool EndOfSeries;
        internal readonly EventQueue EventQueue;

        void Init()
        {
            if (!Directory.Exists(_info.DatePath)) {
                return;
            }

            var list = new DirectoryInfo(_info.DatePath).GetFiles().ToList();
            list.Sort((x, y) => String.Compare(x.Name, y.Name, StringComparison.Ordinal));

            foreach (var file in list) {
                var macth = Regex.Match(file.Name, @"\d{8}");
                if (macth.Success) {
                    DateTime date;
                    if (DateTime.TryParseExact(macth.Groups[0].Value, "yyyyMMdd", null, DateTimeStyles.None, out date)
                        && date >= _info.DateTime1
                        && date <= _info.DateTime2) {
                        _files.AddLast(file);
                        _count += (int)file.Length;
                    }
                }
            }

            if (_count > 0) {
                EndOfSeries = false;
                _progressDelta = (int)Math.Ceiling(((double)_count / 100));
                _progressCount = _progressDelta;
                _progressPercent = 0;
                OpenReader();
            }

            if (_current == null)
                EndOfSeries = true;
        }

        private void OpenReader()
        {
            _current = null;
            while (_files.Count > 0) {
                var file = _files.First.Value;
                _files.RemoveFirst();
                if (file.Length > 0) {
                    _current = new TickReader(file);
                    break;
                }
            }
        }

        public DataSeriesObject_bak(SubscribeInfo info, EventQueue queue)
        {
            EventQueue = queue;
            _info = info;
            Init();
        }

        //private DepthMarketDataField PbTick2DepthMarketDataField(PbTickCodec codec, PbTick tick)
        //{
        //    PbTickView tickView = codec.Data2View(tick, false);

        //    DepthMarketDataField marketData = default(DepthMarketDataField);
        //    codec.GetUpdateTime(tick, out marketData.UpdateTime, out marketData.UpdateMillisec);

        //    marketData.TradingDay = tickView.TradingDay;
        //    marketData.ActionDay = tickView.ActionDay;
        //    marketData.LastPrice = tickView.LastPrice;
        //    marketData.Volume = tickView.Volume;
        //    marketData.Turnover = tickView.Turnover;
        //    marketData.OpenInterest = tickView.OpenInterest;
        //    marketData.AveragePrice = tickView.AveragePrice;
        //    if(tickView.Bar != null)
        //    {
        //        marketData.OpenPrice = tickView.Bar.Open;
        //        marketData.HighestPrice = tickView.Bar.High;
        //        marketData.LowestPrice = tickView.Bar.Low;
        //        marketData.ClosePrice = tickView.Bar.Close;
        //    }
        //    if(tickView.Static != null)
        //    {
        //        marketData.LowerLimitPrice = tickView.Static.LowerLimitPrice;
        //        marketData.UpperLimitPrice = tickView.Static.UpperLimitPrice;
        //        marketData.SettlementPrice = tickView.Static.SettlementPrice;
        //        marketData.Symbol = tickView.Static.Symbol;
        //        if (!string.IsNullOrWhiteSpace(tickView.Static.Exchange))
        //        {
        //            marketData.Exchange = Enum<ExchangeType>.Parse(tickView.Static.Exchange);
        //        }
        //    }

        //    int count = tickView.DepthList == null ? 0 : tickView.DepthList.Count;
        //    if (count > 0)
        //    {
        //        int AskPos = DepthListHelper.FindAsk1Position(tickView.DepthList, tickView.AskPrice1);
        //        int BidPos = AskPos - 1;
        //        int _BidPos = BidPos;
        //        if (_BidPos >= 0)
        //        {
        //            marketData.BidPrice1 = tickView.DepthList[_BidPos].Price;
        //            marketData.BidVolume1 = tickView.DepthList[_BidPos].Size;
        //            --_BidPos;
        //            if (_BidPos >= 0)
        //            {
        //                marketData.BidPrice2 = tickView.DepthList[_BidPos].Price;
        //                marketData.BidVolume2 = tickView.DepthList[_BidPos].Size;
        //                --_BidPos;
        //                if (_BidPos >= 0)
        //                {
        //                    marketData.BidPrice3 = tickView.DepthList[_BidPos].Price;
        //                    marketData.BidVolume3 = tickView.DepthList[_BidPos].Size;
        //                    --_BidPos;
        //                    if (_BidPos >= 0)
        //                    {
        //                        marketData.BidPrice4 = tickView.DepthList[_BidPos].Price;
        //                        marketData.BidVolume4 = tickView.DepthList[_BidPos].Size;
        //                        --_BidPos;
        //                        if (_BidPos >= 0)
        //                        {
        //                            marketData.BidPrice5 = tickView.DepthList[_BidPos].Price;
        //                            marketData.BidVolume5 = tickView.DepthList[_BidPos].Size;
        //                        }
        //                    }
        //                }
        //            }
        //        }

        //        int _AskPos = AskPos;
        //        if (_AskPos < count)
        //        {
        //            marketData.AskPrice1 = tickView.DepthList[_AskPos].Price;
        //            marketData.AskVolume1 = tickView.DepthList[_AskPos].Size;
        //            ++_AskPos;
        //            if (_AskPos < count)
        //            {
        //                marketData.AskPrice2 = tickView.DepthList[_AskPos].Price;
        //                marketData.AskVolume2 = tickView.DepthList[_AskPos].Size;
        //                ++_AskPos;
        //                if (_AskPos < count)
        //                {
        //                    marketData.AskPrice3 = tickView.DepthList[_AskPos].Price;
        //                    marketData.AskVolume3 = tickView.DepthList[_AskPos].Size;
        //                    ++_AskPos;
        //                    if (_AskPos < count)
        //                    {
        //                        marketData.AskPrice4 = tickView.DepthList[_AskPos].Price;
        //                        marketData.AskVolume4 = tickView.DepthList[_AskPos].Size;
        //                        ++_AskPos;
        //                        if (_AskPos < count)
        //                        {
        //                            marketData.AskPrice5 = tickView.DepthList[_AskPos].Price;
        //                            marketData.AskVolume5 = tickView.DepthList[_AskPos].Size;
        //                        }
        //                    }
        //                }
        //            }
        //        }
        //    }
        //    return marketData;
        //}

        public bool Enqueue()
        {
            if (EventQueue.IsFull() || EndOfSeries) {
                return false;
            }

            if (_current == null || !_current.HasNext && _files.Count > 0) {
                OpenReader();
                _lastTradeSize = 0;
            }

            // 这种每次只取一个数据方式比较慢，下一阶段可以改成一次读完一个文件
            if (_current != null && _current.HasNext) {
                var tick = _current.Next();
                var dateTime = _current.Codec.GetDateTime(tick.ActionDay == 0?tick.TradingDay:tick.ActionDay).Add(_current.Codec.GetUpdateTime(tick));
                
                //var marketData = PbTick2DepthMarketDataField(_current.Codec, tick);

                //// 在这个地方式可以试着生成自己的TradeEx，这样在策略中，模拟与实盘的效果就完全一样了
                //if (_info.SubscribeTrade) {
                //    var trade = new TradeEx(dateTime, 0, _info.InstrumentId, marketData.LastPrice, (int)marketData.Volume);
                //    trade.Size -= _lastTradeSize;
                //    trade.DepthMarketData = marketData;
                //    EventQueue.Enqueue(trade);
                //    _lastTradeSize = (int)marketData.Volume;
                //}

                
                //if (_info.SubscribeBidAsk)
                //{
                //    if (marketData.BidVolume1 > 0)
                //    {
                //        var bid = new Bid(dateTime, 0, _info.InstrumentId, marketData.BidPrice1, marketData.BidVolume1);
                //        EventQueue.Write(bid);
                //    }
                //    if (marketData.AskVolume1 > 0)
                //    {
                //        var ask = new Ask(dateTime, 0, _info.InstrumentId, marketData.AskPrice1, marketData.AskVolume1);
                //        EventQueue.Write(ask);
                //    }
                //}

                //_completed += _current.Setp;
                //if (_completed >= _progressCount) {
                //    _progressCount += _progressDelta;
                //    _progressPercent++;
                //    EventQueue.Enqueue(new OnSimulatorProgress(_progressCount, _progressPercent));
                //}
                return true;
            }
            else {
                EndOfSeries = true;
                return false;
            }
        }
    }
}
