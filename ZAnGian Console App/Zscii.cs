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

        static Zscii()
        {
            _alphabetChars[Alphabet.Lowercase]   = "abcdefghijklmnopqrstuvwxyz";
            _alphabetChars[Alphabet.Uppercase]   = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            _alphabetChars[Alphabet.Punctuation] = " \n0123456789.,!?_#'\"/\\-:()";
        }


        public static string DecodeText(byte[] data, MemWord start, out ushort nBytesRead, ushort nBytesToRead=ushort.MaxValue)
        {
            StringBuilder sb = new();
            Alphabet currAlphabet = Alphabet.Lowercase;

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
                        sb.Append(ZChar2Zscii(ch, currAlphabet));
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


        public static char ZChar2Zscii(byte zchar, Alphabet alphabet=Alphabet.Lowercase)
        {
            //Debug.Assert(zchar >= 0x06 && zchar <= 0x2f, $"Invalid zchar 0x{zchar:x2}");
            //DEBUG
            if ((zchar < 0x06) || (zchar > 0x2f))
                return '?';
            //

            return _alphabetChars[alphabet][zchar-0x06];
        }
    }
}