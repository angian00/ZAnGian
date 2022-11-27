using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ZAnGian
{
    public class ZParser
    {
        private ZMemory _memory;
        private List<char> _wordSeparators;
        private byte _dictEntrySize;
        private ushort _numDictEntries;
        private MemWord _currDictAddr;


        public ZParser(ZMemory memory)
        {
            _memory = memory;
        }

        /**
         * Returns normalized (lowercase, truncated) inputStr
         */
        public string StoreInput(string inputStr, MemWord textBufferAddr)
        {
            int nMaxChars = _memory.ReadByte(textBufferAddr).Value; //plus 0x00 string terminator

            inputStr = inputStr.ToLower();
            if (inputStr.Length > nMaxChars)
                inputStr = inputStr.Substring(0, nMaxChars);

            _memory.WriteByte(textBufferAddr + 1, new MemByte(inputStr.Length));
            if (inputStr.Length == 0)
                return "";

            for (byte iChar = 0; iChar < inputStr.Length; iChar++)
                _memory.WriteByte(textBufferAddr + 2 + iChar, new MemByte(Zscii.Ascii2Zscii(inputStr[iChar])));

            return inputStr;
        }

        public void ParseInput(MemWord textBufferAddr, MemWord parseBufferAddr, string inputStr)
        {
            ParseInput(textBufferAddr, parseBufferAddr, inputStr, null, false);
        }

        public void ParseInput(MemWord textBufferAddr, MemWord parseBufferAddr, MemWord dictAddr, bool skipUnrecognizedWords)
        {
            ParseInput(textBufferAddr, parseBufferAddr, null, dictAddr, skipUnrecognizedWords);
        }


        /**
         * See spec 13.*
         */
        private void ParseInput(MemWord textBufferAddr, MemWord parseBufferAddr, string? inputStr, MemWord? dictAddr, bool skipUnrecognizedWords)
        {
            byte inputLen = _memory.ReadByte(textBufferAddr + 1).Value;

            if (inputStr == null)
            {
                //inputStr is not pre-compute, compute it from textBuffer
                inputStr = "";
                for (byte iChar = 0; iChar < inputLen; iChar++)
                    inputStr += Zscii.Zscii2Ascii(_memory.ReadByte(textBufferAddr + 2 + iChar).Value);
            }

            if (dictAddr == (MemWord)null || dictAddr.Value == 0x00)
                dictAddr = new MemWord(_memory.DictionaryLoc.Value);
            ComputeDictMetrics(dictAddr);

            int nMaxParsedWords = _memory.ReadByte(parseBufferAddr).Value;

            byte nParsedWords = 0;
            int wordStart = -1;

            bool isAlphaNum;
            bool isSep = false;
            bool isSpace;
            bool insideWord = false;
            bool wasSep;

            for (byte iChar = 0; iChar < inputLen; iChar++)
            {
                wasSep = isSep;
                isAlphaNum = false;
                isSep = false;
                isSpace = false;

                char currChar = Zscii.Zscii2Ascii(_memory.ReadByte(textBufferAddr + 2 + iChar).Value);

                if (currChar == ' ')
                    isSpace = true;
                else if (_wordSeparators.Contains(currChar))
                    isSep = true;
                else
                    isAlphaNum = true;


                if ((isSpace && insideWord) || (isSep && insideWord) || wasSep)
                {
                    byte wordEnd = (byte)iChar;
                    byte iWord = (byte)(nParsedWords - 1);
                    WriteParsedWord(iWord, inputStr, wordStart, wordEnd, parseBufferAddr);
                    insideWord = false;
                }

                if ((isAlphaNum && !insideWord) || isSep)
                {
                    nParsedWords++;
                    if (nParsedWords > nMaxParsedWords)
                        break;

                    wordStart = iChar;
                    insideWord = true;
                }
            }

            if (insideWord)
            {
                byte wordEnd = (byte)(inputStr.Length);
                byte iWord = (byte)(nParsedWords - 1);
                WriteParsedWord(iWord, inputStr, wordStart, wordEnd, parseBufferAddr);
            }

            _memory.WriteByte(parseBufferAddr + 1, nParsedWords);
        }


        private void ComputeDictMetrics(MemWord dictAddr)
        {
            MemWord memAddr = new MemWord(dictAddr.Value);

            byte nWordSeps = _memory.ReadByte(memAddr).Value;
            memAddr++;

            _wordSeparators = new List<char>();
            for (int i = 0; i < nWordSeps; i++)
            {
                char wordSep = Zscii.Zscii2Ascii(_memory.ReadByte(memAddr).Value);
                _wordSeparators.Add(wordSep);

                memAddr++;
            }

            _dictEntrySize = _memory.ReadByte(memAddr).Value;
            memAddr++;

            _numDictEntries = _memory.ReadWord(memAddr).Value;
            memAddr += 2;

            _currDictAddr = memAddr;
        }


        private void WriteParsedWord(byte iWord, string inputStr, int wordStart, int wordEnd, MemWord parseBufferAddr)
        {
            byte nWordLetters = (byte)(wordEnd - wordStart);
            string word = inputStr.Substring(wordStart, nWordLetters);
            MemWord dictWordAddr = SearchDict(word);

            _memory.WriteWord(parseBufferAddr + 2 + (iWord * 4), dictWordAddr);
            _memory.WriteByte(parseBufferAddr + 2 + (iWord * 4) + 2, nWordLetters);
            _memory.WriteByte(parseBufferAddr + 2 + (iWord * 4) + 3, (byte)(wordStart + 2));
        }


        private MemWord SearchDict(string word)
        {
            MemWord memAddr = _currDictAddr;

            byte[] wordEncoded = Zscii.EncodeText(word, (short)_memory.DictEntryTextLen);

            //FIXME: use binary search for dictionary entries
            for (ushort iDictEntry=0; iDictEntry < _numDictEntries; iDictEntry ++)
            {
                bool match = _memory.CompareBytes(memAddr, wordEncoded, _memory.DictEntryTextLen);
                if (match)
                    return memAddr;

                memAddr += _dictEntrySize;
            }

            return new MemWord(0x00);
        }
    }

}