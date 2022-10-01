using System;


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


    public class NumberUtils
    {
        // interpret a word 16-bit signed int, as per spec 2.2
        public static Int16 toInt16(uint val)
        {
            if (val > Int16.MaxValue)
                return (Int16)(val - 0x10000);

            return (Int16)val;
        }

        public static MemWord fromInt16(Int16 val)
        {
            if (val < 0)
                return (MemWord)(0x10000 + val);

            return (MemWord)val;
        }


    }
}