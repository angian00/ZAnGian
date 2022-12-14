using System;
using System.Diagnostics;

namespace ZAnGian
{
    public abstract class MemValue {
        public virtual ushort FullValue { get => ushort.MaxValue; }
        public virtual short SignedValue { get => short.MinValue; }
        public abstract void next();


        public static bool operator ==(MemValue? v1, MemValue? v2)
        {
            if (v1 is null)
                return (v2 is null);
            else if (v2 is null)
                return false;

            return (v1.FullValue == v2.FullValue);
        }

        public static bool operator !=(MemValue? v1, MemValue? v2)
        {
            if (v1 is null)
                return (v2 is not null);
            else if (v2 is null)
                return true;

            return (v1.FullValue != v2.FullValue);
        }

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

        public override bool Equals(object? obj)
        {
            return obj is MemByte other && _value == other._value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_value);
        }


        public override string ToString()
        {
            return $"0x{_value:x2}";
        }

        public string ToDecimalString()
        {
            return $"{_value}";
        }

        public override void next()
        {
            _value++;
        }


        public static bool operator ==(MemByte? v1, MemByte? v2)
        {
            if (v1 is null)
                return (v2 is null);
            else if (v2 is null)
                return false;

            return (v1._value == v2._value);
        }
        public static bool operator ==(MemByte? v1, int v2)
        {
            if (v1 is null)
                return false;

            return (v1._value == v2);
        }
        public static bool operator ==(MemByte? v1, MemWord? v2)
        {
            if (v1 is null)
                return (v2 is null);
            else if (v2 is null)
                return false;

            return (v1.FullValue == v2.FullValue);
        }
        public static bool operator !=(MemByte? v1, MemByte? v2)
        {
            if (v1 is null)
                return (v2 is not null);
            else if (v2 is null)
                return true;

            return (v1._value != v2._value);
        }
        public static bool operator !=(MemByte? v1, int v2)
        {
            if (v1 is null)
                return true;

            return (v1._value != v2);
        }
        public static bool operator !=(MemByte? v1, MemWord? v2)
        {
            if (v1 is null)
                return (v2 is not null);
            else if (v2 is null)
                return true;

            return (v1.FullValue != v2.FullValue);
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
        public static MemByte operator |(MemByte v, uint bitMask)
        {
            return new MemByte(v._value | bitMask);
        }
        public static MemByte operator |(MemByte v, int bitMask)
        {
            return new MemByte(v._value | bitMask);
        }
        public static MemByte operator ^(MemByte v, uint bitMask)
        {
            return new MemByte(v._value ^ bitMask);
        }
        public static MemByte operator ^(MemByte v, int bitMask)
        {
            return new MemByte(v._value ^ bitMask);
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

        public override bool Equals(object? obj)
        {
            return obj is MemWord other && _value == other._value;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_value);
        }


        public static MemWord FromSignedValue(int val)
        {
            if (val < 0)
                return new MemWord(0x10000 + val);
            else
                return new MemWord(val);

        }

        public static MemWord FromBool(bool val)
        {
                return new MemWord(val ? 0x01 : 0x00);
        }


        public override string ToString()
        {
            return $"0x{_value:x4}";
        }

        public string ToDecimalString()
        {
            return $"{_value}";
        }


        public override void next()
        {
            _value++;
        }

        public static bool operator ==(MemWord? v1, MemWord? v2)
        {
            if (v1 is null)
                return (v2 is null);
            else if (v2 is null)
                return false;

            return (v1._value == v2._value);
        }
        public static bool operator ==(MemWord? v1, MemByte? v2)
        {
            if (v1 is null)
                return (v2 is null);
            else if (v2 is null)
                return false;

            return (v1._value == v2.FullValue);
        }
        public static bool operator ==(MemWord? v1, int v2)
        {
            if (v1 is null)
                return false;

            return (v1._value == v2);
        }
        public static bool operator !=(MemWord? v1, MemWord? v2)
        {
            if (v1 is null)
                return v2 is not null;
            else if (v2 is null)
                return false;

            return (v1._value != v2._value);
        }
        public static bool operator !=(MemWord? v1, int v2)
        {
            if (v1 is null)
                return true;

            return (v1._value != v2);
        }
        public static bool operator !=(MemWord? v1, MemByte? v2)
        {
            if (v1 is null)
                return (v2 is not null);
            else if (v2 is null)
                return true;

            return (v1._value != v2.FullValue);
        }
        public static bool operator <(MemWord v1, MemWord v2)
        {
            return v1._value < v2._value;
        }
        public static bool operator >(MemWord v1, MemWord v2)
        {
            return v1._value > v2._value;
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
        public static MemWord operator |(MemWord v, uint bitMask)
        {
            return new MemWord(v._value | bitMask);
        }
        public static MemWord operator |(MemWord v1, MemWord v2)
        {
            return new MemWord(v1._value | v2._value);
        }
        public static MemWord operator ^(MemWord v, uint bitMask)
        {
            return new MemWord(v._value ^ bitMask);
        }
        public static MemWord operator >>(MemWord v, int offset)
        {
            return new MemWord(v._value >> (offset));
        }

    }
}