using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using SevenZip;
using SmartQuant;
using System.Reflection;
using System.ComponentModel;

namespace QuantBox
{
    public class FileDataSimulator : Provider, IDataSimulator
    {
        [Editor(typeof(System.Windows.Forms.Design.FolderNameEditor), typeof(System.Drawing.Design.UITypeEditor))]
        public string DataPath { get; set; }

        private readonly BarFilter _barFilter;
        private bool _isExiting;
        private bool _isRunning;
        private Thread _thread;
        private readonly SmartQuant.LinkedList<DataSeriesObject> _dataSeries = new SmartQuant.LinkedList<DataSeriesObject>();

        void Run()
        {
            _thread = new Thread(ThreadRun) {
                Name = "QuantBox Data Simulator Thread",
                IsBackground = true
            };
            _thread.Start();
        }

        void ThreadRun()
        {
            Console.WriteLine(string.Join(" ", DateTime.Now, _thread.Name, @"started"));
            if (!IsConnected) {
                Connect();
            }
            EventQueue queue = new EventQueue(1, 0, 2, 10) {
                IsSynched = true
            };
            queue.Enqueue(new OnQueueOpened(queue));
            queue.Enqueue(new OnSimulatorStart(DateTime1, DateTime2, 0L));
            queue.Enqueue(new OnQueueClosed(queue));
            framework.EventBus.DataPipe.Add(queue);
            _isRunning = true;
            _isExiting = false;
            while (!_isExiting) {
                var currentNode = _dataSeries.First;
                SmartQuant.LinkedListNode<DataSeriesObject> prevNode = null;

                while (currentNode != null) {
                    var serie = currentNode.Data;
                    if (!serie.EndOfSeries) {
                        serie.Enqueue();
                        prevNode = currentNode;
                    }
                    else {
                        if (prevNode == null) {
                            _dataSeries.First = currentNode.Next;
                        }
                        else {
                            prevNode.Next = currentNode.Next;
                        }
                        _dataSeries.Count--;
                        serie.EventQueue.Enqueue(new OnQueueClosed(serie.EventQueue));
                    }
                    currentNode = currentNode.Next;
                }
            }
            _isExiting = false;
            _isRunning = false;
            Console.WriteLine(DateTime.Now + _thread.Name + " stopped");
        }

        void Subscribe(Instrument instrument, DateTime dateTime1, DateTime dateTime2)
        {
            Console.WriteLine("{0} {1}::Subscribe {2}", DateTime.Now, this.Name, instrument.Symbol);
            var info = new SubscribeInfo();
            info.DatePath = Path.Combine(DataPath, instrument.Symbol);
            info.DateTime1 = dateTime1;
            info.DateTime2 = dateTime2;
            info.InstrumentId = instrument.Id;
            info.SubscribeBidAsk = SubscribeBid && SubscribeAsk;
            info.SubscribeTrade = SubscribeTrade;

            var queue = new EventQueue(1, 0, 2, 0x61a8) {
                IsSynched = true
            };
            queue.Enqueue(new OnQueueOpened(queue));
            framework.EventBus.DataPipe.Add(queue);
            _dataSeries.Add(new DataSeriesObject(info, queue));
        }

        public FileDataSimulator(Framework framework)
            : base(framework)
        {
            if (Environment.Is64BitProcess)
            {
                SevenZipBase.SetLibraryPath("7z64.dll");
            }
            else
            {
                SevenZipBase.SetLibraryPath("7z.dll");
            }

            id = 50;
            name = "QBDataSimulator";
            description = "QuantBox Data Simulator";
            url = "www.smartquant.cn";
            _barFilter = new BarFilter();
            SubscribeAsk = true;
            SubscribeBid = true;
            SubscribeTrade = true;
            Series = new List<IDataSeries>();
            Processor = new DataProcessor();
            DateTime1 = DateTime.MinValue;
            DateTime2 = DateTime.MaxValue;
        }

        public override void Subscribe(InstrumentList instruments)
        {
            if (!_isRunning) {
                foreach (var inst in instruments) {
                    Subscribe(inst, DateTime1, DateTime2);
                }
                Run();
            }
            else {
                foreach (var inst in instruments) {
                    Subscribe(inst, framework.Clock.DateTime, DateTime2);
                }
            }
        }

        public override void Subscribe(Instrument instrument)
        {
            if (!_isRunning) {
                Subscribe(instrument, DateTime1, DateTime2);
                Run();
            }
            else {
                Subscribe(instrument, framework.Clock.DateTime, DateTime2);
            }
        }

        public override void Connect()
        {
            if (!IsConnected) {
                Status = ProviderStatus.Connected;
            }
        }

        public override void Disconnect()
        {
            if (!IsDisconnected) {
                _isExiting = true;
                while (_isRunning) {
                    Thread.Sleep(1);
                }
                Clear();
                Status = ProviderStatus.Disconnected;
            }
        }

        public BarFilter BarFilter
        {
            get { return _barFilter; }
        }

        public void Clear()
        {
            base.Clear();
            _dataSeries.Clear();
            DateTime1 = DateTime.MinValue;
            DateTime2 = DateTime.MaxValue;
        }

        public DateTime DateTime1 { get; set; }
        public DateTime DateTime2 { get; set; }
        public DataProcessor Processor { get; set; }
        public List<IDataSeries> Series { get; set; }
        public bool SubscribeAll { get; set; }
        public bool SubscribeAsk { get; set; }
        public bool SubscribeBar { get; set; }
        public bool SubscribeBid { get; set; }
        public bool SubscribeFundamental { get; set; }
        public bool SubscribeLevelII { get; set; }
        public bool SubscribeNews { get; set; }
        public bool SubscribeTrade { get; set; }
    }
}
