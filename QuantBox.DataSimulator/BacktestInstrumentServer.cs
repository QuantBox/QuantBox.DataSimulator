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
        public bool Save { get; set; }
        public BacktestInstrumentServer(Framework framework,string path,bool save)
            : base(framework)
        {
            Path = path;
            Save = save;
        }

        public static void AddDirectoryInstrument(Framework framework, string path, bool save = false)
        {
            var list = new BacktestInstrumentServer(framework, path, save).Load();
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
                // 以最深层的目录名做为合约名
                var subdir = path.GetDirectories("*", System.IO.SearchOption.TopDirectoryOnly);
                if(subdir.Length>0)
                {
                    continue;
                }
                string symbol = path.Name;

                Instrument inst = framework.InstrumentManager.Get(symbol);
                if(inst == null)
                {
                    inst = new Instrument(SmartQuant.InstrumentType.Synthetic, symbol);
                    framework.InstrumentManager.Add(inst, Save);
                }
                instruments.Add(inst);
            }
            return instruments;
        }
    }
}
