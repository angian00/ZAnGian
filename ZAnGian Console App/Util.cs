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

}