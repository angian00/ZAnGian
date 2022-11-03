using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ZAnGian
{
    public class StringUtils
    {

        public static string ByteToBinaryString(byte val)
        {
            string result = "";
            for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                result += ((val & (1 << (7 - bitIndex))) > 0) ? '1' : '0';

            return result;
        }

        public static string BytesToAsciiString(byte[] buf, uint start, uint len)
        {
            return System.Text.Encoding.ASCII.GetString(buf, (int)start, (int)len).Split("\u0000")[0];
        }


        
        public static void AsciiStringToBytes(string msg, byte[] buf, uint start, uint len)
        {
            byte[] decoded = System.Text.Encoding.ASCII.GetBytes(msg);

            Debug.Assert(decoded.Length >= len);

            for (int i = 0; i < len; i++)
            {
                buf[start + i] = decoded[i];
            }
        }

    }

    public class CollectionUtils
    {
        public static Dictionary<T, T> FlipDict<T>(Dictionary<T, T> orig)
        {
            Dictionary<T, T> res = new Dictionary<T, T>();

            foreach (T key in orig.Keys)
            {
                T val = orig[key];
                res[val] = key;
            }

            return res;
        }
    }

    public class NumberUtils
    {
        public static short BitShift(short val, short places, bool preserveSign)
        {
            if (!preserveSign)
                val = (short)(val & 0b0111_1111_1111_1111);

            return (short)(val << places);
        }

        public static short Signed14Bits(int value14bits)
        {
            bool isNegative = ((value14bits & 0b0010_0000_0000_0000) == 0b0010_0000_0000_0000);

            if (isNegative)
                return (short)(value14bits - (1 << 14));
            else
                return (short)value14bits;
        }
    }
}