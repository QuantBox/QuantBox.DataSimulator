using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SmartQuant;
using System.Linq;
using ArchiveData;

namespace QuantBox
{
    public class BacktestInstrumentServer : InstrumentServer
    {
        public string Path { get; set; }
        public bool Save { get; set; }

        public bool UseFileName { get; set; }
        public BacktestInstrumentServer(Framework framework, string path, bool save, bool useFileName)
            : base(framework)
        {
            Path = path;
            Save = save;
            UseFileName = useFileName;
        }

        public static void AddDirectoryInstrument(Framework framework, string path, bool save = false,bool useFileName = true)
        {
            var list = new BacktestInstrumentServer(framework, path, save, useFileName).LoadDirectories();
        }

        public static void AddFileInstrument(Framework framework, string path, bool save = false, bool useFileName = true)
        {
            var list = new BacktestInstrumentServer(framework, path, save, true).LoadFiles();
        }

        public InstrumentList LoadFiles()
        {
            if (!Directory.Exists(Path))
            {
                return instruments;
            }

            var dir = new DirectoryInfo(Path);
            foreach (var fi in dir.GetFiles("*", System.IO.SearchOption.AllDirectories))
            {
                // 以最深层的文件名做为合约名
                string _exchange = string.Empty;
                string _product = string.Empty;
                string _instrument = string.Empty;
                string _symbol = string.Empty;
                string _date = string.Empty;
                if (PathHelper.SplitFileName(fi.Name, out _exchange, out _product, out _instrument, out _date))
                {
                    if (!string.IsNullOrEmpty(_exchange))
                    {
                        _symbol = string.Format("{0}.{1}", _instrument, _exchange);
                    }
                    else
                        _symbol = _instrument;

                    Instrument inst = framework.InstrumentManager.Get(_symbol);
                    if (inst == null)
                    {
                        inst = new Instrument(SmartQuant.InstrumentType.Synthetic, _symbol);
                        framework.InstrumentManager.Add(inst, Save);
                    }
                    instruments.Add(inst);
                }
            }
            return instruments;
        }

        public InstrumentList LoadDirectories()
        {
            if (!Directory.Exists(Path))
            {
                return instruments;
            }

            var dir = new DirectoryInfo(Path);
            foreach (var path in dir.GetDirectories("*", System.IO.SearchOption.AllDirectories))
            {
                // 以最深层的目录名做为合约名
                var subdir = path.GetDirectories("*", System.IO.SearchOption.TopDirectoryOnly);
                if(subdir.Length>0)
                {
                    continue;
                }

                string symbol = path.Name;
                if(UseFileName)
                {
                    var fis = path.GetFiles();
                    foreach(var fi in fis)
                    {
                        string _exchange = string.Empty;
                        string _product = string.Empty;
                        string _instrument = string.Empty;
                        string _symbol = string.Empty;
                        string _date = string.Empty;
                        if (PathHelper.SplitFileName(fi.Name, out _exchange, out _product, out _instrument, out _date))
                        {
                            if (!string.IsNullOrEmpty(_exchange))
                            {
                                symbol = string.Format("{0}.{1}", _instrument, _exchange);
                            }
                        }

                        Instrument inst = framework.InstrumentManager.Get(symbol);
                        if (inst == null)
                        {
                            inst = new Instrument(SmartQuant.InstrumentType.Synthetic, symbol);
                            framework.InstrumentManager.Add(inst, Save);
                        }
                        instruments.Add(inst);
                    }
                }
                else
                {
                    Instrument inst = framework.InstrumentManager.Get(symbol);
                    if (inst == null)
                    {
                        inst = new Instrument(SmartQuant.InstrumentType.Synthetic, symbol);
                        framework.InstrumentManager.Add(inst, Save);
                    }
                    instruments.Add(inst);
                }
            }
            return instruments;
        }

        public override InstrumentList Load()
        {
            return instruments;
        }
    }
}
