using System;
using System.Diagnostics;
using System.Reflection.Emit;
using static System.Net.Mime.MediaTypeNames;

namespace ZAnGian
{
    public partial class ZProcessor
    {
        private void OpcodeARead(int nOps, MemValue[] operands)
        {
            _logger.Debug($"AREAD {FormatOperands(nOps, operands)}");

            MemWord textBufferAddr = new MemWord(operands[0].FullValue);
            MemWord parseBufferAddr = new MemWord(operands[1].FullValue);

            if (nOps >= 4)
            {
                MemWord routineAddr = new MemWord(operands[2].FullValue);
                ushort time = operands[3].FullValue;
                //TODO: input timeout mechanism
            }

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            string inputStr = _input.ReadLine(); //TODO: check any terminating char for v5

            ushort retValue = 0x00;
            if (parseBufferAddr != 0x00)
            {
                inputStr = _parser.StoreInput(inputStr, textBufferAddr);
                _parser.ParseInput(textBufferAddr, parseBufferAddr, inputStr);
                retValue = 13; //input-terminating char
            }

            WriteVariable(storeVar.Value, retValue);
        }


        private void OpcodeBufferMode(int nOps, MemValue[] operands)
        {
            _logger.Debug($"BUFFER_MODE {FormatOperands(nOps, operands)}");
            ushort flag = operands[0].FullValue;

            //FIXME: ignoring BUFFER_MODE, since we don't word-wrap anyway
        }


        private void OpcodeCall(int nOps, MemValue[] operands)
        {
            _logger.Debug($"CALL {FormatOperands(nOps, operands)}");

            MemValue packedAddr = operands[0];

            MemValue[] args = new MemValue[nOps - 1];
            for (byte i = 1; i < nOps; i++)
            {
                args[i - 1] = operands[i];
            }

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            CallRoutine(packedAddr, args, storeVar);
        }

        private void OpcodeCallVN(int nOps, MemValue[] operands)
        {
            _logger.Debug($"CALL_VN {FormatOperands(nOps, operands)}");

            MemValue packedAddr = operands[0];

            MemValue[] args = new MemValue[nOps - 1];
            for (byte i = 1; i < nOps; i++)
            {
                args[i - 1] = operands[i];
            }

            CallRoutine(packedAddr, args, null);
        }

        private void OpcodeCallVN2(int nOps, MemValue[] operands)
        {
            _logger.Debug($"CALL_VN2 {FormatOperands(nOps, operands)}");

            MemValue packedAddr = operands[0];

            MemValue[] args = new MemValue[nOps - 1];
            for (byte i = 1; i < nOps; i++)
            {
                args[i - 1] = operands[i];
            }

            CallRoutine(packedAddr, args, null);
        }

        private void OpcodeCallVS2(int nOps, MemValue[] operands)
        {
            _logger.Debug($"CALL_VS2 {FormatOperands(nOps, operands)}");

            MemValue packedAddr = operands[0];

            MemValue[] args = new MemValue[nOps - 1];
            for (byte i = 1; i < nOps; i++)
            {
                args[i - 1] = operands[i];
            }

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            CallRoutine(packedAddr, args, storeVar);
        }


        private void OpcodeCheckArgCount(int nOps, MemValue[] operands)
        {
            _logger.Debug($"CHECK_ARG_COUNT {FormatOperands(nOps, operands)}");

            ushort argNum = operands[0].FullValue;

            Branch(argNum <= _stack.CurrRoutineData.NumArgs);
        }


        private void OpcodeCopyTable(int nOps, MemValue[] operands)
        {
            _logger.Debug($"COPY_TABLE {FormatOperands(nOps, operands)}");

            MemWord sourceAddr = (MemWord)operands[0];
            MemWord destAddr = (MemWord)operands[1];
            short dataLen = operands[2].SignedValue;

            if (destAddr.Value == 0x00)
            {
                for (int i = 0; i < Math.Abs(dataLen); i++)
                    _memory.Data[sourceAddr.Value + i] = 0x00;
            }
            else
            {
                bool forwardDir = true;
                if ((dataLen > 0) && (sourceAddr + dataLen > destAddr))
                    forwardDir = false;

                if (forwardDir)
                {
                    for (int i = 0; i < Math.Abs(dataLen); i++)
                        _memory.Data[destAddr.Value + i] = _memory.Data[sourceAddr.Value + i];
                }
                else
                {
                    //CHECK: copy backwards to avoid corruption of source table
                    for (int i = Math.Abs(dataLen) - 1; i >= 0; i--)
                        _memory.Data[destAddr.Value + i] = _memory.Data[sourceAddr.Value + i];
                }
            }

        }


        private void OpcodeEncodeText(int nOps, MemValue[] operands)
        {
            _logger.Debug($"ENCODE_TEXT {FormatOperands(nOps, operands)}");

            MemWord zsciiTextAddr = (MemWord)operands[0];
            ushort textLen = operands[1].FullValue;
            ushort fromOffset = operands[2].FullValue;
            MemWord destAddr = (MemWord)operands[3];

            byte[] encodedData = Zscii.EncodeText(_memory.Data, (zsciiTextAddr + fromOffset).Value, textLen);

            _memory.CopyBytes(destAddr, encodedData);
        }


        private void OpcodeEraseLine(int nOps, MemValue[] operands)
        {
            _logger.Debug($"ERASE_LINE {FormatOperands(nOps, operands)}");

            ushort val = operands[0].FullValue;
            _screen.EraseLine(val);
        }


        private void OpcodeEraseWindow(int nOps, MemValue[] operands)
        {
            _logger.Debug($"ERASE_WINDOW {FormatOperands(nOps, operands)}");

            short windowId = operands[0].SignedValue;

            _screen.EraseWindow(windowId);
        }


        private void OpcodeGetCursor(int nOps, MemValue[] operands)
        {
            _logger.Debug($"GET_CURSOR {FormatOperands(nOps, operands)}");

            MemWord destAddr = (MemWord)operands[0];

            var cursorPos = _screen.GetCursorPos();
        }


        private void OpcodeInputStream(int nOps, MemValue[] operands)
        {
            _logger.Warn("TODO: implement INPUT_STREAM");
        }


        private void OpcodeOutputStream(int nOps, MemValue[] operands)
        {
            _logger.Warn("TODO: implement OUTPUT_STREAM");
        }


        private void OpcodeNotV5(int nOps, MemValue[] operands)
        {
            _logger.Debug($"NOT {operands[0]}");

            MemWord value = new MemWord(operands[0].FullValue);

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, ~value);
        }

        private void OpcodePrintChar(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PRINT_CHAR {FormatOperands(nOps, operands)}");

            byte zchar = (byte)operands[0].FullValue;
            _screen.Print($"{Zscii.Zscii2Ascii(zchar)}");
        }


        private void OpcodePrintNum(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PRINT_NUM {FormatOperands(nOps, operands)}");

            short val = operands[0].SignedValue;

            _screen.Print($"{val}");
        }

        private void OpcodePrintTable(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PRINT_TABLE {FormatOperands(nOps, operands)}");

            MemWord zsciiTextAddr = (MemWord)operands[0];
            ushort width = operands[1].FullValue;
            ushort height = operands[2].FullValue;
            ushort skip = 0;
            if (nOps > 3)
                skip = operands[3].FullValue;

            string asciiText = Zscii.Zscii2Ascii(_memory.Data, zsciiTextAddr.Value, (ushort)(width*height));

            _screen.PrintTable(asciiText, width, height, skip);
        }


        private void OpcodePull(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PULL {FormatOperands(nOps, operands)}");

            GameVariableId storeVar = (GameVariableId)operands[0].FullValue;

            MemWord value = _stack.PopValue();
            WriteVariable(storeVar, value);
        }

        private void OpcodePush(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PUSH {FormatOperands(nOps, operands)}");
            MemWord value = new MemWord(operands[0].FullValue);

            _stack.PushValue(value);
        }


        private void OpcodePutProp(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PUT_PROP {FormatOperands(nOps, operands)}");

            GameObjectId objId = operands[0];
            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            ushort propId = operands[1].FullValue;

            MemValue propValue = operands[2];

            obj.PutPropertyValue(propId, propValue);
        }


        private void OpcodeRandom(int nOps, MemValue[] operands)
        {
            _logger.Debug($"RANDOM {FormatOperands(nOps, operands)}");

            short randomRange = operands[0].SignedValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;


            ushort value;
            if (randomRange < 0)
            {
                _rndGen = new Random(-randomRange);
                value = 0;
            }
            else
            {
                value = (ushort)_rndGen.Next(randomRange);
            }

            WriteVariable(storeVar, value);
        }


        private void OpcodeReadChar(int nOps, MemValue[] operands)
        {
            _logger.Debug($"READ_CHAR {FormatOperands(nOps, operands)}");

            //operands[0] == 1 always

            if (nOps >= 3)
            {
                MemWord routineAddr = new MemWord(operands[1].FullValue);
                ushort time = operands[2].FullValue;
                //TODO: input timeout mechanism
            }

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;


            ushort ch  = (ushort)_input.ReadChar();

            WriteVariable(storeVar.Value, ch);
        }


        private void OpcodeScanTable(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SCAN_TABLE {FormatOperands(nOps, operands)}");

            MemValue value = operands[0];
            MemWord tableAddr = (MemWord)operands[1];
            ushort tableLen = operands[2].FullValue;

            ushort form = 0x82; //default as per opcode spec 
            if (nOps > 3)
                form = operands[3].FullValue;

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;


            bool useWords = ((form & 0b1000_0000) == 0b1000_0000);
            ushort tableEntryLen = (ushort)(form & 0b0111_1111);

            MemWord matchAddr = new MemWord(0x00);

            //CHECK if when useWords == false value operand is actually a MemByte
            for (int i=0; i < tableLen; i++)
            {
                MemWord tableEntryAddr = tableAddr +i * tableEntryLen;

                MemValue tableValue;
                if (useWords)
                {
                    Debug.Assert(value is MemWord);
                    tableValue = _memory.ReadWord(tableEntryAddr);
                }
                else
                {
                    Debug.Assert(value is MemByte);
                    tableValue = _memory.ReadByte(tableEntryAddr);
                }

                if (value == tableValue)
                {
                    matchAddr = tableEntryAddr;
                    break;
                }

            }


            WriteVariable(storeVar.Value, matchAddr);

            Branch(matchAddr > new MemWord(0x00));
        }


        private void OpcodeSetCursor(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SET_CURSOR {FormatOperands(nOps, operands)}");

            short line = operands[0].SignedValue;
            short col  = operands[1].SignedValue;

            _screen.SetCursorPos(line, col);
        }

        private void OpcodeSetTextStyle(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SET_TEXT_STYLE {FormatOperands(nOps, operands)}");

            ushort textStyle = operands[0].FullValue;

            _screen.SetTextStyle(textStyle);

        }

        private void OpcodeSetWindow(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SET_WINDOW {FormatOperands(nOps, operands)}");

            short windowId = operands[0].SignedValue;

            _screen.SetCurrWindow(windowId);
        }


        private void OpcodeSoundEffect(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SOUND_EFFECT {FormatOperands(nOps, operands)}");
            _logger.Warn("TODO: implement SOUND_EFFECT");
        }


        private void OpcodeSplitWindow(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SPLIT_WINDOW {FormatOperands(nOps, operands)}");

            ushort nLines = operands[0].FullValue;

            _screen.SplitWindow(nLines);
        }

        private void OpcodeSRead(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SREAD {operands[0]} {operands[1]}");

            _screen.PrintStatusLine(GetStatusInfo());
            MemWord textBufferAddr = new MemWord(operands[0].FullValue);
            MemWord parseBufferAddr = new MemWord(operands[1].FullValue);

            string inputStr = _input.ReadLine();
            inputStr = _parser.StoreInput(inputStr, textBufferAddr);
            _parser.ParseInput(textBufferAddr, parseBufferAddr, inputStr);
        }


        private void OpcodeStoreB(int nOps, MemValue[] operands)
        {
            ushort arr = operands[0].FullValue;
            ushort index = operands[1].FullValue;
            MemByte value = new MemByte(operands[2].FullValue);
            _logger.Debug($"STOREB {operands[0]} {operands[1]} {value}");

            _memory.WriteByte(new MemWord(arr + index), value);
        }

        private void OpcodeStoreW(int nOps, MemValue[] operands)
        {
            ushort arr = operands[0].FullValue;
            ushort index = operands[1].FullValue;
            MemWord value = new MemWord(operands[2].FullValue);
            _logger.Debug($"STOREW {operands[0]} {operands[1]} {value}");

            _memory.WriteWord(new MemWord(arr + 2 * index), value);
        }

        private void OpcodeTokenise(int nOps, MemValue[] operands)
        {
            _logger.Debug($"TOKENISE {FormatOperands(nOps, operands)}");

            MemWord textBufferAddr = (MemWord)operands[0];
            MemWord parseBufferAddr = (MemWord)operands[1];

            MemWord dictAddr = new MemWord(0x00);
            if (nOps > 2)
                dictAddr = (MemWord)operands[2];

            bool skipUnrecognizedWords = false;
            if (nOps > 3)
                skipUnrecognizedWords = (operands[3].FullValue > 0);

            _parser.ParseInput(textBufferAddr, parseBufferAddr, dictAddr, skipUnrecognizedWords);
        }
    }
}