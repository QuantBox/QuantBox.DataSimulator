using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using SmartQuant;

using QuantBox.Data.Serializer.V2;

namespace QuantBox
{
    class DataSeriesObject
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

        public DataSeriesObject(SubscribeInfo info, EventQueue queue)
        {
            EventQueue = queue;
            _info = info;
            Init();
        }

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

                // 在这个地方式可以试着生成自己的TradeEx，这样在策略中，模拟与实盘的效果就完全一样了
                if (_info.SubscribeBidAsk)
                {
                    int count = tick.DepthList == null ? 0 : tick.DepthList.Count;
                    if(count>0)
                    {
                        int AskPos = DepthListHelper.FindAsk1Position(tick.DepthList, tick.AskPrice1);
                        int BidPos = AskPos - 1;
                        if (BidPos >= 0 && BidPos < count)
                        {
                            var bid = new Bid(dateTime, 0, _info.InstrumentId, _current.Codec.TickToPrice(tick.DepthList[BidPos].Price), tick.DepthList[BidPos].Size);
                            EventQueue.Enqueue(bid);
                        }
                        if (AskPos >= 0 && AskPos < count)
                        {
                            var ask = new Ask(dateTime, 0, _info.InstrumentId, _current.Codec.TickToPrice(tick.DepthList[AskPos].Price), tick.DepthList[AskPos].Size);
                            EventQueue.Enqueue(ask);
                        }
                    }
                }

                if (_info.SubscribeTrade) {
                    var trade = new Trade(dateTime, 0, _info.InstrumentId, _current.Codec.GetLastPrice(tick), (int)_current.Codec.GetVolume(tick));
                    trade.Size -= _lastTradeSize;
                    EventQueue.Enqueue(trade);
                    _lastTradeSize = (int)_current.Codec.GetVolume(tick);
                }

                _completed += _current.Setp;
                if (_completed >= _progressCount) {
                    _progressCount += _progressDelta;
                    _progressPercent++;
                    EventQueue.Enqueue(new OnSimulatorProgress(_progressCount, _progressPercent));
                }
                return true;
            }
            else {
                EndOfSeries = true;
                return false;
            }
        }
    }
}
