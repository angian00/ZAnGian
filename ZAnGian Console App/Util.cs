using System;
using System.Collections.Generic;

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