using System;
using System.Collections.Generic;

namespace ZAnGian
{
    public class ZParser
    {
        private const int DICT_ENTRY_TEXT_LEN = 4; //depends on version

        ZMemory _memory;
        List<char> _wordSeparators;
        byte _dictEntrySize;
        ushort _numDictEntries;
        MemWord _dictEntriesAddr;


        public ZParser(ZMemory memory)
        {
            _memory = memory;
            MemWord memAddr = _memory.DictionaryLoc;

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

            _dictEntriesAddr = memAddr;
        }


        /**
         * See spec 13.*
         */
        public void ParseInput(string inputStr, MemWord textBufferAddr, MemWord parseBufferAddr)
        {
            int nMaxChars = _memory.ReadByte(textBufferAddr).Value; //plus 0x00 string terminator

            inputStr = inputStr.ToLower();
            if (inputStr.Length > nMaxChars)
                inputStr = inputStr.Substring(0, nMaxChars);

            _memory.WriteByte(textBufferAddr + 1, new MemByte(inputStr.Length));
            for (byte iChar = 0; iChar < inputStr.Length; iChar++)
                _memory.WriteByte(textBufferAddr + 2 + iChar, new MemByte(Zscii.Ascii2Zscii(inputStr[iChar]))); 

            if (inputStr.Length == 0)
                return;


            int nMaxParsedWords = _memory.ReadByte(parseBufferAddr).Value;

            byte nParsedWords = 0;
            int wordStart = -1;

            bool isAlphaNum;
            bool isSep = false;
            bool isSpace;
            bool insideWord = false;
            bool wasSep;

            for (byte iChar = 0; iChar < inputStr.Length; iChar++)
            {
                wasSep = isSep;
                isAlphaNum = false;
                isSep = false;
                isSpace = false;

                if (inputStr[iChar] == ' ')
                    isSpace = true;
                else if (_wordSeparators.Contains(inputStr[iChar]))
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

        private void WriteParsedWord(byte iWord, string inputStr, int wordStart, int wordEnd, MemWord parseBufferAddr)
        {
            // as per 'read' opcode spec
            byte nWordLetters = (byte)(wordEnd - wordStart);
            string word = inputStr.Substring(wordStart, nWordLetters);
            MemWord dictWordAddr = SearchDict(word);

            _memory.WriteWord(parseBufferAddr + 2 + (iWord * 4), dictWordAddr);
            _memory.WriteByte(parseBufferAddr + 2 + (iWord * 4) + 2, nWordLetters);
            _memory.WriteByte(parseBufferAddr + 2 + (iWord * 4) + 3, (byte)(wordStart + 2));
        }

        private MemWord SearchDict(string word)
        {
            MemWord memAddr = _dictEntriesAddr;

            for (ushort iDictEntry=0; iDictEntry < _numDictEntries; iDictEntry ++)
            {
                string dictWord = Zscii.DecodeText(_memory.Data, memAddr, out _, DICT_ENTRY_TEXT_LEN);
                if (dictWord == word)
                    return memAddr;

                memAddr += _dictEntrySize;
            }

            return new MemWord(0x00);
        }
    }

}