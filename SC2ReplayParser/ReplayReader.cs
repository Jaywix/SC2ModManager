
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace ReplayParser.SC2
{
    public class ReplayReader : IDisposable
    {
        private MemoryStream _buffer;
        private BinaryReader _reader;
        private long _bufferSize;

        public ReplayReader(Stream inputStream)
        {
            SetData(inputStream);
        }

        public ReplayReader(byte[] data)
        {
            SetData(new MemoryStream(data));
        }

        public void SetData(Stream inputStream)
        {
            _bufferSize = inputStream.Length;
            var position = inputStream.Position;
            inputStream.Position = 0;
            
            _buffer = new MemoryStream();
            inputStream.CopyTo(_buffer);
            _buffer.Position = 0;
            
            inputStream.Position = position;
            _reader = new BinaryReader(_buffer);
        }

        public string ReadString()
        {
            var result = new List<byte>();
            byte b;
            while ((b = _reader.ReadByte()) != 0)
            {
                result.Add(b);
            }
            return Encoding.UTF8.GetString(result.ToArray());
        }

        public int ReadInt32() => _reader.ReadInt32();
        public uint ReadUInt32() => _reader.ReadUInt32();
        public short ReadInt16() => _reader.ReadInt16();
        public ushort ReadUInt16() => _reader.ReadUInt16();
        public float ReadSingle() => _reader.ReadSingle();
        public byte ReadByte() => _reader.ReadByte();
        public bool ReadBoolean() => ReadByte() != 0;
        public void ReadNil() => ReadByte();

        public Dictionary<object, object> ReadDictionary()
        {
            var result = new Dictionary<object, object>();
            while (true)
            {
                if (IsEndOfStream) break;
                var type = ReadByte();
                if (type == DataType.END) break;
                var key = ReadLua(type);
                var value = ReadLua();
                if (key != null)
                    result[key] = value;
            }
            return result;
        }

        public object ReadLua(byte? type = null)
        {
            if (IsEndOfStream) return null;
            var dataType = type ?? ReadByte();
            return ReadLuaValue(dataType);
        }

        private object ReadLuaValue(byte type)
        {
            switch (type)
            {
                case DataType.NUMBER: return ReadSingle();
                case DataType.STRING: return ReadString();
                case DataType.NIL: ReadNil(); return null;
                case DataType.BOOL: return ReadBoolean();
                case DataType.TABLE: return ReadDictionary();
                default: return null;
            }
        }

        public byte[] ReadBytes(int count)
        {
            if (count <= 0) return new byte[0];
            if (_buffer.Position + count > _bufferSize)
                count = (int)(_bufferSize - _buffer.Position);
            if (count <= 0) return new byte[0];
            return _reader.ReadBytes(count);
        }
        
        public long Position => _buffer.Position;
        public long Length => _buffer.Length;
        public bool IsEndOfStream => _buffer.Position >= _bufferSize;
        
        public void Seek(long offset, SeekOrigin origin) => _buffer.Seek(offset, origin);

        public void Dispose()
        {
            _reader?.Dispose();
            _buffer?.Dispose();
        }
    }
}