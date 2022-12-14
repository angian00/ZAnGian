using System;
using System.Collections;
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
        private static Dictionary<int, int> _extendedAscii2ZsciiTable = new Dictionary<int, int>();
        private static Dictionary<int, int> _extendedZscii2AsciiTable = new Dictionary<int, int>();
        private static Dictionary<int, int> DefaultExtendedZscii2AsciiTable = new Dictionary<int, int>() {
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
            SetTranslationTable(null, null);
            SetUnicodeTable(null, null);
        }

        public static void SetTranslationTable(ZMemory memory, MemWord translationTableAddr)
        {
            _alphabetChars.Clear();

            if (translationTableAddr == (MemWord)null || translationTableAddr.Value == 0x00)
                SetDefaultTranslationTable();
            else
            {
                //Such an alphabet table consists of 78 bytes arranged as 3 blocks of 26 ZSCII values, 
                //    translating Z-characters 6 to 31 for alphabets A0, A1 and A2.
                //    Z-characters 6 and 7 of A2, however, are still translated as escape and newline codes(as above).

                MemWord currAddr = translationTableAddr;

                foreach (Alphabet alph in new Alphabet[] { Alphabet.Lowercase, Alphabet.Uppercase, Alphabet.Punctuation })
                {
                    StringBuilder sb = new();
                    for (int i = 0; i < 26; i++)
                    {
                        sb.Append(Zscii2Ascii(memory.ReadByte(currAddr).Value));
                        currAddr++;
                    }

                    _alphabetChars[alph] = sb.ToString();
                }
            }
        }
        public static void SetUnicodeTable(ZMemory memory, MemWord unicodeTableAddr)
        {
            if (unicodeTableAddr == (MemWord)null || unicodeTableAddr.Value == 0x00)
                _extendedZscii2AsciiTable = DefaultExtendedZscii2AsciiTable;
            else
            {
                _extendedZscii2AsciiTable = new Dictionary<int, int>();

                MemWord currAddr = unicodeTableAddr;
                byte nChars = memory.ReadByte(currAddr++).Value;
                for (int i = 0; i < nChars; i++)
                {
                    _extendedZscii2AsciiTable[i + 155] = memory.ReadWord(currAddr).Value;
                    currAddr += 2;
                }
            }

            _extendedAscii2ZsciiTable = CollectionUtils.FlipDict(_extendedZscii2AsciiTable);
        }


        private static void SetDefaultTranslationTable()
        {
            _alphabetChars[Alphabet.Lowercase] = "abcdefghijklmnopqrstuvwxyz";
            _alphabetChars[Alphabet.Uppercase] = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
            _alphabetChars[Alphabet.Punctuation] = " \n0123456789.,!?_#'\"/\\-:()";
        }


        public static string DecodeText(byte[] data, HighMemoryAddress startAddr, out ushort nBytesRead, ushort nBytesToRead = ushort.MaxValue, ZMemory? memory = null)
        {
            StringBuilder sb = new();
            Alphabet currAlphabet = Alphabet.Lowercase;

            UInt32 i = startAddr;
            int abbrIndex = -1;
            bool isMultiChar = false; //multi-char: see spec 3.4
            byte multiChar1 = 0xff;

            while (i < startAddr + nBytesToRead)
            {
                //Console.WriteLine($"decoding data 0x{data[iStr]:x2} 0x{data[iStr+1]:x2}");
                byte ch1 = (byte)((data[i] & 0b01111100) >> 2);
                byte ch2 = (byte)(((data[i] & 0b00000011) << 3) + ((data[i + 1] & 0b11100000) >> 5));
                byte ch3 = (byte)(data[i + 1] & 0b00011111);

                foreach (byte ch in new byte[] { ch1, ch2, ch3 })
                {
                    if (abbrIndex != -1)
                    {
                        if (memory != null)
                        {
                            ushort abbrTableAddr = memory.ReadWord(0x18).Value;

                            //lookup abbreviation
                            HighMemoryAddress abbrStringAddr = memory.ReadWord((ushort)(abbrTableAddr + 2*(32 * abbrIndex + ch))).Value;
                            abbrStringAddr *= 2; //abbr table addresses are "word addresses"

                            //HighMemoryAddress abbrStringAddr = (HighMemoryAddress)(0x20 + 32 * abbrIndex + ch);
                            //abbrStringAddr *= 2;
                            sb.Append(DecodeText(data, abbrStringAddr, out _));
                        }
                        abbrIndex = -1;
                        continue;
                    }

                    if (isMultiChar)
                    {
                        if (multiChar1 == 0xff) //multiChar1 not set yet?
                            multiChar1 = ch;
                        else
                        {
                            sb.Append(Zscii2Ascii((multiChar1 << 5) + ch));
                            isMultiChar = false;
                            multiChar1 = 0xff;
                        }

                        continue;
                    }

                    if (ch == 0x00)
                    {
                        sb.Append(" ");
                    }
                    else if (ch == 0x01 || ch == 0x02 || ch == 0x03)
                    {
                        //abbreviation
                        abbrIndex = ch - 0x01;
                    }
                    else if (ch == 0x04)
                    {
                        currAlphabet = Alphabet.Uppercase;
                    }
                    else if (ch == 0x05)
                    {
                        currAlphabet = Alphabet.Punctuation;
                    }
                    else if (ch == 0x06 && currAlphabet == Alphabet.Punctuation)
                    {
                        isMultiChar = true;
                        currAlphabet = Alphabet.Lowercase;
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

            nBytesRead = (ushort)(i - startAddr);

            return sb.ToString();
        }


        public static byte[] EncodeText(string asciiText, short nBytesToWrite=-1)
        {
            ushort inputLen = (ushort)asciiText.Length;
            byte[] inputBuffer = Ascii2Zscii(asciiText);

            return EncodeText(inputBuffer, 0, inputLen, nBytesToWrite);
        }


        public static byte[] EncodeText(byte[] inputBuffer, ushort startOffset, ushort inputLen, short nBytesToWrite=-1)
        {
            List<byte> textData = new();

            for (int iStr=0; iStr < inputLen; iStr++)
            {
                char ch = (char)inputBuffer[startOffset + iStr];
                if (_alphabetChars[Alphabet.Lowercase].Contains(ch))
                {
                    textData.Add(Zscii2ZChar(ch, Alphabet.Lowercase));
                }
                else if (_alphabetChars[Alphabet.Uppercase].Contains(ch))
                {
                    textData.Add(0x04); //switch next char to Uppercase
                    textData.Add(Zscii2ZChar(ch, Alphabet.Uppercase));
                }
                else if (_alphabetChars[Alphabet.Punctuation].Contains(ch))
                {
                    textData.Add(0x05); //switch next char to Punctuation
                    textData.Add(Zscii2ZChar(ch, Alphabet.Punctuation));
                }
                else
                {
                    //multi-zchar encoding: see spec 3.4
                    textData.Add(0x05);
                    textData.Add(0x06);
                    textData.Add((byte)((ch >> 5) & 0b0001_1111));
                    textData.Add((byte)(ch & 0b0001_1111));
                }
            }

            int textDataLen = textData.Count;
            byte[] encodedData = new byte[nBytesToWrite > 0 ? nBytesToWrite : UInt16.MaxValue];

            int iDataIn = 0;
            int iDataOut = 0;
            while (true)
            {
                byte ch1 = iDataIn     < textDataLen ? textData[iDataIn]     : (byte)0x05;
                byte ch2 = iDataIn + 1 < textDataLen ? textData[iDataIn + 1] : (byte)0x05;
                byte ch3 = iDataIn + 2 < textDataLen ? textData[iDataIn + 2] : (byte)0x05;

                encodedData[iDataOut]    = (byte)(ch1 << 2);
                encodedData[iDataOut]   |= (byte)(ch2 >> 3);
                encodedData[iDataOut+1]  = (byte)((ch2 & 0b0111) << 5);
                encodedData[iDataOut+1] |= (byte)(ch3);

                if ((nBytesToWrite > 0 && iDataOut >= nBytesToWrite - 2) || (nBytesToWrite == -1 && iDataIn >= textDataLen - 3))
                {
                    encodedData[iDataOut] |= 0b1000_0000; //set MSB for first byte of last couple
                    break;
                }

                iDataIn += 3;
                iDataOut += 2;
            }

            return encodedData[..(iDataOut+2)];
        }

        public static char ZChar2Zscii(byte zchar, Alphabet alphabet = Alphabet.Lowercase)
        {
            if ((zchar < 0x06) || (zchar > 0x2f))
                return '?';

            return _alphabetChars[alphabet][zchar - 0x06];
        }

        public static byte Zscii2ZChar(char ch, Alphabet alphabet = Alphabet.Lowercase)
        {
            int pos = _alphabetChars[alphabet].IndexOf(ch);
            Debug.Assert(pos >= 0);

            return (byte)(pos + 0x06);
        }


        public static string Zscii2Ascii(byte[] zsciiText, ushort start, ushort textLen)
        {
            char[] asciiChars = new char[textLen];

            for (int i = 0; i < textLen; i++)
            {
                asciiChars[i] = Zscii2Ascii(zsciiText[start + i]);
            }

            return String.Concat(asciiChars);
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

                case >= 155 and <= 255: return _extendedZscii2AsciiTable.ContainsKey(zsciiCode) ? (char)_extendedZscii2AsciiTable[zsciiCode] : '?';

                default: throw new ArgumentException($"Invalid zscii code: {zsciiCode}");
            }
        }

        public static byte[] Ascii2Zscii(string asciiStr)
        {
            List<byte> res = new();

            foreach (char ch in asciiStr)
            {
                Debug.Assert(Ascii2Zscii(ch) <= 0xff); //extended chars not supported here
                res.Add((byte)Ascii2Zscii(ch));
            }

            return res.ToArray();
        }

        public static int Ascii2Zscii(char ch)
        {
            switch (ch)
            {
                case (char)0x00: return 0;
                case '\t': return 9;
                case ' ': return 11;
                case '\r': return 13;
                case '\n': return 13;

                case >= (char)32 and <= (char)126: return ch;
                    
                default: 
                    if (_extendedAscii2ZsciiTable.ContainsKey((int)ch))
                        return _extendedAscii2ZsciiTable[ch];
                    else
                        throw new ArgumentException($"Invalid ascii char: {ch}");
            }
        }
    }
}