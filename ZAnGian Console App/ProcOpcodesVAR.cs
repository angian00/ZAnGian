﻿using System;
using System.Diagnostics;
using System.Reflection.Emit;

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
            //_logger.All($"input string: {inputStr}");

            ushort retValue = 0x00;
            if (parseBufferAddr != 0x00)
            {
                _parser.ParseInput(inputStr, textBufferAddr, parseBufferAddr);
                retValue = 13; //input-terminating char
            }

            WriteVariable(storeVar.Value, retValue);
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


        private void OpcodeInputStream(int nOps, MemValue[] operands)
        {
            throw new NotImplementedException($"Unimplemented opcode: INPUT_STREAM"); //TODO
        }

        private void OpcodeOutputStream(int nOps, MemValue[] operands)
        {
            throw new NotImplementedException($"Unimplemented opcode: OUTPUT_STREAM"); //TODO
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
            //_logger.All($"input string: {inputStr}");
            _parser.ParseInput(inputStr, textBufferAddr, parseBufferAddr);
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
    }
}