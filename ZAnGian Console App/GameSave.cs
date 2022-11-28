using System;
using System.Diagnostics;
using System.IO;

namespace ZAnGian
{
    public class GameSave
    {
        private const int MAX_SAVE_SIZE = 1 << 20; //CHECK

        private static Logger _logger = Logger.GetInstance();

        private UInt32 _currAddr;
        private byte[] _saveData;

        private HighMemoryAddress _pc = UInt32.MaxValue;
        private ZMemory _memory;
        private ZStack _stack;
        private ZMemory _initialMemory;


        public GameSave()
        {
            _memory = null;
            _stack = null;
        }

        public GameSave(ZMemory memory, ZStack stack, HighMemoryAddress pc, ZMemory initialMemory)
        {
            _memory = memory;
            _stack = stack;
            _pc = pc;
            _initialMemory = initialMemory;
        }


        public static bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }


        public static bool IsValidFile(string filePath)
        {
            GameSave gs = new GameSave();

            bool resOk = gs.LoadFile(filePath);
            return resOk;
        }


        public bool LoadFile(string filepath)
        {
            _logger.Info($"Loading game state on file [{filepath}]");

            _saveData = File.ReadAllBytes(filepath);
            _currAddr = 0x00;

            string mainId = ReadId();
            _currAddr += 4;
            
            if (mainId != "FORM")
                return false;
            

            UInt32 totLen = ReadNBytes(4);
            _currAddr += 4;

            if (_saveData.Length != totLen + 8)
            {
                _logger.Warn("!! Inconsistent savefile length");
                return false;
            }

            //FORM sub-id
            string subId = ReadId();
            _currAddr += 4;
            
            if (subId != "IFZS")
                return false;


            return true;
        }

        public bool Restore(ref ZMemory memory, ref ZStack stack, ref HighMemoryAddress pc, in ZMemory initialMemory)
        {
            if (_saveData == null || _saveData.Length == 0)
            {
                _logger.Error("GameSave not properly loaded before Restore");
                return false;
            }

            _initialMemory = initialMemory;

            //skip main headers
            _currAddr = 12;

            while (_currAddr < _saveData.Length)
            {
                ParseChunk();
            }

            if (_memory == null || _stack == null || _pc == UInt32.MaxValue)
                return false;

            memory = _memory;
            stack = _stack;

            pc = _pc;

            return true;
        }


        public bool SaveFile(string filepath)
        {
            _logger.Info($"Saving game state on file [{filepath}]");
            if (_pc == UInt32.MaxValue || _initialMemory == null || _memory == null || _stack == null)
            {
                _logger.Error("GameSave not properly assigned game state in constructor before SaveFile");
                return false;
            }

            _saveData = new byte[MAX_SAVE_SIZE];
            _currAddr = 0;

            StringUtils.AsciiStringToBytes("FORM", _saveData, _currAddr, 4); 
            _currAddr += 4;

            //leave space for totLen
            _currAddr += 4;

            StringUtils.AsciiStringToBytes("IFZS", _saveData, _currAddr, 4);
            _currAddr += 4;

            WriteChunk("IFhd");
            WriteChunk("UMem");
            WriteChunk("Stks");


            UInt32 totLen = _currAddr;
            _currAddr = 4;
            WriteNBytes(4, totLen-8);

            var fs = new FileStream(filepath, FileMode.Create, FileAccess.Write);
            fs.Write(new ReadOnlySpan<byte>(_saveData, 0, (int)totLen));
            fs.Close();

            return true;
        }




        private string ReadId()
        {
            return StringUtils.BytesToAsciiString(_saveData, _currAddr, 4);
        }


        private void ParseChunk()
        {
            string chunkId = ReadId();
            _currAddr += 4;

            _logger.Debug($"Found chunk of type [{chunkId}]");

            UInt32 chunkLen = ReadNBytes(4);
            _currAddr += 4;

            switch (chunkId)
            {
                case "IFhd":
                    ParseIFhd(chunkLen);
                    break;

                case "CMem":
                    ParseCMem(chunkLen);
                    break;

                case "UMem":
                    ParseUMem(chunkLen);
                    break;

                case "Stks":
                    ParseStks(chunkLen);
                    break;

                default:
                    _logger.Debug($"Skipping chunk");
                    _currAddr += chunkLen;
                    break;
            }

            // 2-byte alignment
            if (_currAddr % 2 != 0)
                _currAddr++;
        }


        private void WriteChunk(string chunkId)
        {
            _logger.Debug($"Writing chunk of type [{chunkId}]");

            UInt32 startChunkAddr = _currAddr;

            StringUtils.AsciiStringToBytes(chunkId, _saveData, _currAddr, 4);
            _currAddr += 4;

            //leave space for chunkLen
            _currAddr += 4;


            switch (chunkId)
            {
                case "IFhd":
                    WriteIFhd();
                    break;

                case "UMem":
                    WriteUMem();
                    break;

                case "Stks":
                    WriteStks();
                    break;

                default:
                    break;
            }

            UInt32 chunkLen = _currAddr - startChunkAddr - 8;
            WriteNBytes(4, chunkLen, startChunkAddr + 4);

            // 2-byte alignment
            if (_currAddr % 2 != 0)
                _currAddr++;

        }

        //-----------------------------------------------------
        //---- IFhd chunk -------------------------------------
        //-----------------------------------------------------

        private void ParseIFhd(UInt32 chunkLen)
        {
            Debug.Assert(chunkLen == 13);

            UInt16 relNum = (UInt16)ReadNBytes(2);
            _currAddr += 2;

            string serialNum = StringUtils.BytesToAsciiString(_saveData, _currAddr, 6);
            _currAddr += 6;

            //skip checksum
            _currAddr += 2;

            _pc = (HighMemoryAddress)ReadNBytes(3);
            _currAddr += 3;
        }


        private void WriteIFhd()
        {
            UInt16 relNum = 0x03;
            WriteNBytes(2, _memory.GameRelease.Value);
            _currAddr += 2;

            StringUtils.AsciiStringToBytes(_memory.GameSerialNum, _saveData, _currAddr, 6);
            _currAddr += 6;

            //skip checksum
            WriteNBytes(2, _memory.Checksum.Value);
            _currAddr += 2;

            WriteNBytes(3, _pc);
            _currAddr += 3;
        }


        //-----------------------------------------------------
        //---- CMem/UMem chunk --------------------------------
        //-----------------------------------------------------

        const UInt32 MAX_DYN_MEM_SIZE = 65534;

        private void ParseCMem(UInt32 chunkLen)
        {
            UInt32 startAddr = _currAddr;

            byte[] xoredMemory = new byte[MAX_DYN_MEM_SIZE];
            int uncompr_index = 0;

            while (_currAddr < startAddr + chunkLen)
            {
                byte compr_val = _saveData[_currAddr++];
                xoredMemory[uncompr_index++] = compr_val;

                if (compr_val == 0x00)
                {
                    byte runLen = _saveData[_currAddr++];
                    //Debug.Assert(runLen > 0); //CHECK

                    for (int i_unc = 0; i_unc < runLen; i_unc++)
                    {
                        xoredMemory[uncompr_index++] = 0x00;
                    }
                }
            }

            //DEBUG: dump reconstructed memory
            
        }

        private void ParseUMem(UInt32 chunkLen)
        {
            _memory = (ZMemory)_initialMemory.Clone();

            UInt32 startAddr = _currAddr;

            UInt32 memAddr = 0x00;
            while (_currAddr < startAddr + chunkLen)
            {
                _memory.Data[memAddr] = (byte)(_saveData[_currAddr++] ^ _initialMemory.Data[memAddr]);

                memAddr++;
            }
        }

        private void WriteUMem()
        {
            UInt32 memAddr = 0x00;
            UInt32 lastXor = 0x00;

            byte[] xoredMemory = new byte[MAX_DYN_MEM_SIZE];

            while (memAddr < _memory.BaseStaticMem.Value)
            {
                xoredMemory[memAddr] = (byte)(_memory.Data[memAddr] ^ _initialMemory.Data[memAddr]);
                if (xoredMemory[memAddr] != 0)
                    lastXor = memAddr;

                memAddr++;
            }

            for (memAddr = 0; memAddr <= lastXor; memAddr++)
            {
                _saveData[_currAddr++] = xoredMemory[memAddr];
            }
        }

        //-----------------------------------------------------
        //---- Stks chunk -------------------------------------
        //-----------------------------------------------------

        private void ParseStks(UInt32 chunkLen)
        {
            UInt32 startAddr = _currAddr;

            _stack = new ZStack(false);

            while (_currAddr - startAddr < chunkLen - 1)
            {
                ParseStackFrame();
            }
        }


        private void ParseStackFrame()
        {
            var rData = new RoutineData();

            UInt32 returnAddr = ReadNBytes(3);
            _currAddr += 3;
            rData.ReturnAddress = returnAddr;

            byte flags = _saveData[_currAddr];//000pvvvv
            _currAddr ++;

            rData.ReturnVariableId = _saveData[_currAddr];
            _currAddr ++;
            
            byte argFlags = _saveData[_currAddr];
            _currAddr++;

            UInt16 valueStackSize = (UInt16)ReadNBytes(2);
            _currAddr += 2;

            ushort nLocalVars = (ushort)(flags & 0b00001111);
            for (int iLocalVar=0; iLocalVar < nLocalVars; iLocalVar++)
            {
                rData.LocalVariables.Add(new MemWord((UInt16)ReadNBytes(2)));
                _currAddr += 2;
            }

            for (int iStackValue = 0; iStackValue < valueStackSize; iStackValue++)
            {
                rData.ValueStack.Push(new MemWord((UInt16)ReadNBytes(2)));
                _currAddr += 2;
            }

            _stack.PushRoutine(rData);
        }

        private void WriteStks()
        {
            var reverseStack = _stack.RoutineStack.ToArray();
            Array.Reverse(reverseStack);

            foreach (RoutineData rData in reverseStack)
                WriteStackFrame(rData);
        }

        private void WriteStackFrame(RoutineData rData)
        {
            WriteNBytes(3, rData.ReturnAddress);
            _currAddr += 3;

            byte flags = 0x00;
            flags |= (byte)(rData.LocalVariables.Count & 0b00001111); //"b" bit is not set in .z3 games
            _saveData[_currAddr++] = flags;

            _saveData[_currAddr++] = rData.ReturnVariableId;

            byte argFlags = 0x00;
            //TODO: set argFlags
            _saveData[_currAddr++] = argFlags;

            WriteNBytes(2, (UInt16)rData.ValueStack.Count);
            _currAddr += 2;

            foreach (MemWord localVarValue in rData.LocalVariables)
            {
                WriteNBytes(2, localVarValue.Value);
                _currAddr += 2;
            }

            MemWord[] stackValuesRev = rData.ValueStack.ToArray();
            Array.Reverse(stackValuesRev);
            foreach (MemWord stackValue in stackValuesRev)
            {
                WriteNBytes(2, stackValue.Value);
                _currAddr += 2;
            }
        }

        //-----------------------------------------------------
        //---- Memory Read/Write methods ----------------------
        //-----------------------------------------------------

        private UInt32 ReadNBytes(int nBytes)
        {
            Debug.Assert(nBytes > 0);

            UInt32 val = _saveData[_currAddr];

            for (int i = 1; i < nBytes; i++)
            {
                val <<= 8;
                val += _saveData[_currAddr + i];
            }

            return val;
        }

        private void WriteNBytes(int nBytes, UInt32 val, UInt32 targetAddr=UInt32.MaxValue)
        {
            Debug.Assert(nBytes > 0);

            if (targetAddr == UInt32.MaxValue)
                targetAddr = _currAddr;

            for (int i = 0; i < nBytes; i++)
            {
                _saveData[targetAddr + i] = (byte)( (val >> (8 * (nBytes - i - 1))) & 0xff );
            }
        }
    }


}