using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace ZAnGian
{
    public class Zscii
    {
        public enum Alphabet
        {
            Lowercase,
            Uppercase,
            Punctuation
        }


        private static Dictionary<Alphabet, string> _alphabetChars = new Dictionary<Alphabet, string>();
        private static Dictionary<int, int> _defaultExtendedTable = new Dictionary<int, int>() {
                { 155, 0x0e4 },
                { 156, 0x0f6 },
                { 157, 0x0fc },
                { 158, 0x0c4 },
                { 159, 0x0d6 },
                { 160, 0x0dc },
                { 161, 0x0df },
                { 162, 0x0bb },
                { 163, 0x0ab },
                { 164, 0x0eb },
                { 165, 0x0ef },
                { 166, 0x0ff },
                { 167, 0x0cb },
                { 168, 0x0cf },
                { 169, 0x0e1 },
                { 170, 0x0e9 },
                { 171, 0x0ed },
                { 172, 0x0f3 },
                { 173, 0x0fa },
                { 174, 0x0fd },
                { 175, 0x0c1 },
                { 176, 0x0c9 },
                { 177, 0x0cd },
                { 178, 0x0d3 },
                { 179, 0x0da },
                { 180, 0x0dd },
                { 181, 0x0e0 },
                { 182, 0x0e8 },
                { 183, 0x0ec },
                { 184, 0x0f2 },
                { 185, 0x0f9 },
                { 186, 0x0c0 },
                { 187, 0x0c8 },
                { 188, 0x0cc },
                { 189, 0x0d2 },
                { 190, 0x0d9 },
                { 191, 0x0e2 },
                { 192, 0x0ea },
                { 193, 0x0ee },
                { 194, 0x0f4 },
                { 195, 0x0fb },
                { 196, 0x0c2 },
                { 197, 0x0ca },
                { 198, 0x0ce },
                { 199, 0x0d4 },
                { 200, 0x0db },
                { 201, 0x0e5 },
                { 202, 0x0c5 },
                { 203, 0x0f8 },
                { 204, 0x0d8 },
                { 205, 0x0e3 },
                { 206, 0x0f1 },
                { 207, 0x0f5 },
                { 208, 0x0c3 },
                { 209, 0x0d1 },
                { 210, 0x0d5 },
                { 211, 0x0e6 },
                { 212, 0x0c6 },
                { 213, 0x0e7 },
                { 214, 0x0c7 },
                { 215, 0x0fe },
                { 216, 0x0f0 },
                { 217, 0x0de },
                { 218, 0x0d0 },
                { 219, 0x0a3 },
                { 220, 0x153 },
                { 221, 0x152 },
                { 222, 0x0a1 },
                { 223, 0x0bf },
        };

        static Zscii()
        {
            _alphabetChars[Alphabet.Lowercase] = "abcdefghijklmnopqrstuvwxyz";
            _alphabetChars[Alphabet.Uppercase] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            _alphabetChars[Alphabet.Punctuation] = " \n0123456789.,!?_#'\"/\\-:()";

        }

        public static string DecodeText(byte[] data, MemWord startAddr, out ushort nBytesRead, ushort nBytesToRead=ushort.MaxValue)
        {
            StringBuilder sb = new();
            Alphabet currAlphabet = Alphabet.Lowercase;

            ushort start = startAddr.Value;
            ushort i = start;
            while (i < start + nBytesToRead)
            {
                //Console.WriteLine($"decoding data 0x{data[i]:x2} 0x{data[i+1]:x2}");
                byte ch1 = (byte)((data[i] & 0b01111100) >> 2);
                byte ch2 = (byte)( ((data[i] & 0b00000011) << 3) + ((data[i + 1] & 0b11100000) >> 5) );
                byte ch3 = (byte)(data[i + 1] & 0b00011111);

                foreach (byte ch in new byte[] { ch1, ch2, ch3 })
                {
                    if (ch == 0x00)
                    {
                        sb.Append(" ");
                    }
                    else if (ch == 0x04)
                    {
                        currAlphabet = Alphabet.Uppercase;
                    }
                    else if (ch == 0x05)
                    {
                        currAlphabet = Alphabet.Punctuation;
                    }
                    else
                    {
                        sb.Append(ZChar2Ascii(ch, currAlphabet));
                        currAlphabet = Alphabet.Lowercase;
                    }
                }

                if ((data[i] & 0x80) == 0x80)
                {
                    //topmost bit set == last data bytes
                    i += 2;
                    break;
                }

                i += 2;
            }
            
            nBytesRead = (ushort) (i - start);

            return sb.ToString();
        }


        public static char ZChar2Ascii(byte zchar, Alphabet alphabet=Alphabet.Lowercase)
        {
            //Debug.Assert(zchar >= 0x06 && zchar <= 0x2f, $"Invalid zchar 0x{zchar:x2}");
            //DEBUG
            if ((zchar < 0x06) || (zchar > 0x2f))
                return '?';
            //

            return _alphabetChars[alphabet][zchar-0x06];
        }


        /**
         * As per spec 3.8
         */
        public static Char Zscii2Ascii(int zsciiCode)
        {
            switch (zsciiCode)
            {
                case  0: return (char)0x00;
                case  9: return '\t';
                case 11: return ' ';
                case 13: return '\n';

                case >= 32 and <= 126: return (char)zsciiCode;

                case >= 155 and <= 223: return (char)_defaultExtendedTable[zsciiCode];
                case >= 224 and <= 255: return '?'; // valid codepoint but not mapped in default table?

                default: throw new ArgumentException($"Invalid zscii code: {zsciiCode}");
            }
        }
    }
}