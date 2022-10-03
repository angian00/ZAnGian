using System;
using System.Diagnostics;
using System.Drawing;
using System.Security.Principal;

namespace ZAnGian
{
    public abstract class MemValue {
        public virtual ushort FullValue { get => ushort.MaxValue; }
        public virtual short SignedValue { get => short.MinValue; }
    }


    public class MemByte : MemValue
    {
        private byte _value;
        public byte Value { get => _value; }
        public override ushort FullValue { get => _value; }
        public override short SignedValue
        {
            get => _value; //CHECK
        }


        public MemByte(byte val)
        {
            _value = val;
        }

        public MemByte(int val)
        {
            Debug.Assert(val >= 0 && val <= 0xff, "MemByte overflow");
            _value = (byte)val;
        }

        public MemByte(uint val)
        {
            Debug.Assert(val <= 0xff, "MemByte overflow");
            _value = (byte)val;
        }

        public override string ToString()
        {
            return $"0x{_value:x2}";
        }

        public string ToDecimalString()
        {
            return $"{_value}";
        }

        public static bool operator ==(MemByte v1, int v2)
        {
            return (v1._value == v2);
        }
        public static bool operator !=(MemByte v1, int v2)
        {
            return (v1._value != v2);
        }
        public static MemByte operator +(MemByte v1, MemByte v2)
        {
            return new MemByte(v1._value + v2._value);
        }
        public static MemByte operator +(MemByte v1, int v2)
        {
            return new MemByte(v1._value + v2);
        }
        public static MemByte operator +(MemByte v1, uint v2)
        {
            return new MemByte(v1._value + v2);
        }
        public static MemByte operator -(MemByte v1, int v2)
        {
            return new MemByte(v1._value - v2);
        }
        public static MemByte operator -(MemByte v1, uint v2)
        {
            return new MemByte(v1._value - v2);
        }
        public static MemByte operator *(MemByte v1, int v2)
        {
            return new MemByte(v1._value * v2);
        }
        public static MemByte operator &(MemByte v, uint bitMask)
        {
            return new MemByte(v._value & bitMask);
        }
        public static MemByte operator >>(MemByte v, int offset)
        {
            return new MemByte(v._value >> (offset));
        }
    }


    public class MemWord : MemValue
    {
        private ushort _value;
        public ushort Value { get => _value; }
        public override ushort FullValue { get => _value; }
        public override short SignedValue { 
            get => (_value > short.MaxValue) ? (short)(_value - 0x10000) : (short)_value;
        }

        public byte HighByte { get => (byte)(_value >> 8); }
        public byte LowByte { get => (byte)(_value & 0xff); }


        public MemWord(byte highVal, byte lowVal)
        {
            _value = (ushort)((highVal << 8) + lowVal);
        }

        public MemWord(int val)
        {
            Debug.Assert(val >= 0 && val <= 0xffff, "MemWord overflow");
            _value = (ushort)val;
        }

        public MemWord(uint val)
        {
            Debug.Assert(val <= 0xffff, "MemWord overflow");
            _value = (ushort)val;
        }


        public static MemWord fromSignedValue(int val)
        {
            if (val < 0)
                return new MemWord(0x10000 + val);
            else
                return new MemWord(val);

        }


        public override string ToString()
        {
            return $"0x{_value:x4}";
        }

        public string ToDecimalString()
        {
            return $"{_value}";
        }


        public static bool operator ==(MemWord v1, int v2)
        {
            return (v1._value == v2);
        }
        public static bool operator !=(MemWord v1, int v2)
        {
            return (v1._value != v2);
        }
        public static MemWord operator ++(MemWord v)
        {
            v._value += 1;
            return v;
        }
        public static MemWord operator --(MemWord v)
        {
            v._value -= 1;
            return v;
        }
        public static MemWord operator +(MemWord v1, MemWord v2)
        {
            return new MemWord(v1._value + v2._value);
        }
        public static MemWord operator +(MemWord v1, MemByte v2)
        {
            return new MemWord(v1._value + v2.Value);
        }
        public static MemWord operator +(MemByte v1, MemWord v2)
        {
            return new MemWord(v1.Value + v2._value);
        }
        public static MemWord operator +(MemWord v1, int v2)
        {
            return new MemWord(v1._value + v2);
        }
        public static MemWord operator +(MemWord v1, uint v2)
        {
            return new MemWord(v1._value + v2);
        }
        public static MemWord operator -(MemWord v1, int v2)
        {
            return new MemWord(v1._value - v2);
        }
        public static MemWord operator -(MemWord v1, uint v2)
        {
            return new MemWord(v1._value - v2);
        }
        public static MemWord operator *(MemWord v1, int v2)
        {
            return new MemWord(v1._value * v2);
        }
        public static MemWord operator ~(MemWord v)
        {
            return new MemWord((UInt16)(~v._value));
        }
        public static MemWord operator &(MemWord v1, MemWord v2)
        {
            return new MemWord(v1._value & v2._value);
        }
        public static MemWord operator &(MemWord v, uint bitMask)
        {
            return new MemWord(v._value & bitMask);
        }
        public static MemWord operator >>(MemWord v, int offset)
        {
            return new MemWord(v._value >> (offset));
        }

    }
}