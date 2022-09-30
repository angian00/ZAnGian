using System;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Reflection.Metadata;

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

}