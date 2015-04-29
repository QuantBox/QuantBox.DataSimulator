using SmartQuant;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace QuantBox
{
    internal class DataSeriesObject
    {
        internal bool endOfSeries;
        internal DataObject obj;
        internal DataProcessor processor;
        internal EventQueue eventQueue;
        internal EventQueue dataQueue = new EventQueue(0, 0, 2, 0x80);
        internal int progressDelta;
        internal int progressCount;
        internal int progressPercent;
        internal long index1;
        internal long index2;
        internal long current;
        internal long count;
        internal IDataSeries series;

        internal DataSeriesObject(IDataSeries series, DateTime dateTime1, DateTime dateTime2, EventQueue queue, DataProcessor processor)
        {
            this.series = series;
            eventQueue = queue;
            if (processor == null)
            {
                this.processor = new DataProcessor();
            }
            else
            {
                this.processor = processor;
            }
            if (!(dateTime1 == DateTime.MinValue) && (dateTime1 >= series.DateTime1))
            {
                index1 = series.GetIndex(dateTime1, SearchOption.Next);
            }
            else
            {
                index1 = 0L;
            }
            if (!(dateTime2 == DateTime.MaxValue) && (dateTime2 <= series.DateTime2))
            {
                index2 = series.GetIndex(dateTime2);
            }
            else
            {
                index2 = series.Count - 1L;
            }
            current = index1;
            progressDelta = (int)Math.Ceiling(Count() / 100.0);
            progressCount = progressDelta;
            progressPercent = 0;
        }

        internal long Count()
        {
            return ((index2 - index1) + 1L);
        }

        internal bool Enqueue()
        {
            if (eventQueue.IsFull())
            {
                return false;
            }
            DataObject data = null;
            while (dataQueue.IsEmpty())
            {
                if (obj != null)
                {
                    data = obj;
                    obj = null;
                    break;
                }
                if (current > index2)
                {
                    endOfSeries = true;
                    return false;
                }
                obj = series[current];

                //-----------------------
                // DataProcessor其实只是Bar处理，由于读到的行情全是Tick，所以这部分可忽视
                //obj = processor.OnData(this);
                Emit(obj);
                //-----------------------

                current += 1L;
                count += 1L;
            }
            if (data == null)
                data = (DataObject)dataQueue.Read();
            eventQueue.Write(data);
            if (count == progressCount)
            {
                progressCount += progressDelta;
                progressPercent++;
                eventQueue.Enqueue(new OnSimulatorProgress(progressCount, progressPercent));
            }
            return true;
        }
        protected void Emit(DataObject obj)
        {
            if (!this.dataQueue.IsFull())
            {
                this.dataQueue.Write(obj);
            }
            else
            {
                Console.WriteLine("DataProcessor::Emit Can not write data object. Data queue is full. Data queue size = " + this.dataQueue.Size);
            }
        }
    }
}
