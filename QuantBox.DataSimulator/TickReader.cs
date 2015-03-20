using SevenZip;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using QuantBox.Data.Serializer;

namespace QuantBox
{
    class TickReader
    {
        readonly Stream _stream;
        public PbTickSerializer Serializer;
        public QuantBox.Data.Serializer.V2.PbTickCodec Codec;
        int _originalLength = 0;
        int _lastPosition = 0;
        
        void SetStep()
        {
            int orgPosition = _originalLength;
            if (HasNext) {
                orgPosition = (int)Math.Ceiling(((double)_stream.Position / _stream.Length) * _originalLength);                
            }
            Setp = orgPosition - _lastPosition;
            _lastPosition = orgPosition;
        }

        public TickReader(FileInfo file)
        {
            Serializer = new PbTickSerializer();
            Codec = new QuantBox.Data.Serializer.V2.PbTickCodec();
            _stream = new MemoryStream();
            _originalLength = (int)file.Length;

            // 加载文件，支持7z解压
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
                catch
                {
                    _stream = fileStream;
                    _stream.Seek(0, SeekOrigin.Begin);
                }
            }
        }
        public int Setp { get; set; }
        public bool HasNext { get { return _stream.Position != _stream.Length; } }
        public QuantBox.Data.Serializer.V2.PbTick Next()
        {
            if (HasNext) {
                var tick = Serializer.ReadOne(_stream);
                SetStep();
                Codec.Config = tick.Config;
                Codec.TickSize = tick.Config.GetTickSize();
                return tick;
            }
            return null;
        }
    }
}
