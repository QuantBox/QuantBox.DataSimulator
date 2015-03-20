using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using SmartQuant;

namespace QuantBox
{
    public class BacktestInstrumentServer : InstrumentServer
    {
        public string Path { get; set; }
        public BacktestInstrumentServer(Framework framework,string path)
            : base(framework)
        {
            Path = path;
        }

        public static void AddDirectoryInstrument(Framework framework, string path)
        {
            var list = new BacktestInstrumentServer(framework, path).Load();
        }

        public override InstrumentList Load()
        {
            if (!Directory.Exists(Path))
            {
                return instruments;
            }

            var dir = new DirectoryInfo(Path);
            foreach (var path in dir.GetDirectories("*", System.IO.SearchOption.AllDirectories))
            {
                //var match = Regex.Match(path.Name, @"([a-zA-Z]+)\d+");
                //if (!match.Success)
                //    continue;

                //string symbol = match.Groups[0].Value;
                string symbol = path.Name;

                Instrument inst = framework.InstrumentManager.Get(symbol);
                if(inst == null)
                {
                    inst = new Instrument(InstrumentType.Synthetic, symbol);
                    framework.InstrumentManager.Add(inst, false);
                }
                instruments.Add(inst);
            }
            return instruments;
        }
    }
}
