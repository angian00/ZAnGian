﻿using System;
using System.Diagnostics;
using System.Text;

namespace ZAnGian
{
    public partial class ZProcessor
    {
        //-------------------------------------------------
        //--- VAR -----------------------------------------
        //-------------------------------------------------

        private void OpcodeCall(int nOps, MemValue[] operands)
        {
            _logger.Debug($"CALL {FormatOperands(nOps, operands)}");

            MemWord packedAddr = (MemWord)operands[0];
            MemWord routineAddr = packedAddr * 2;

            RoutineData newRoutine = new RoutineData();

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            newRoutine.ReturnVariableId = storeVar;
            newRoutine.ReturnAddress = _pc;

            //jump to routine instructions
            _pc = routineAddr;

            byte nLocalVars = _memory.ReadByte(_pc).Value;
            _pc++;

            for (byte i = 0; i < nLocalVars; i++)
            {
                newRoutine.AddLocalVariable(_memory.ReadWord(_pc));
                _pc += 2;
            }

            for (byte i = 1; i < nOps; i++)
            {
                //CHECK: push argument operands[i+1] into first local variables?
                newRoutine.SetLocalVariable(i, operands[i].FullValue);
            }


            _stack.PushRoutine(newRoutine);
            //TODO manage case (routineAddr == 0x00) as per opcode spec 
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

        private void OpcodeSRead(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SREAD {operands[0]} {operands[1]}");

            //PrintStatusLine(); //TODO
            MemWord textBufferAddr = new MemWord(operands[0].FullValue);
            MemWord parseBufferAddr = new MemWord(operands[1].FullValue);

            string inputStr = _input.ReadLine();
            _logger.All($"input string: {inputStr}");
            _parser.ParseInput(inputStr, textBufferAddr, parseBufferAddr);
        }


        private void OpcodeStoreB(int nOps, MemValue[] operands)
        {
            _logger.Debug($"STOREB {operands[0]} {operands[1]}");
            ushort arr = operands[0].FullValue;
            ushort index = operands[1].FullValue;
            byte value = (byte)operands[2].FullValue;
            _memory.WriteByte(new MemWord(arr + index), value);
        }
        private void OpcodeStoreW(int nOps, MemValue[] operands)
        {
            _logger.Debug($"STOREW {operands[0]} {operands[1]}");
            ushort arr = operands[0].FullValue;
            ushort index = operands[1].FullValue;
            ushort value = (byte)operands[2].FullValue;

            _memory.WriteWord(new MemWord(arr + index), new MemWord(value));
        }

        //-------------------------------------------------
        //--- 0OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodeNewLine()
        {
            _logger.Debug("NEW_LINE");
            _screen.Print("\n");
        }

        private void OpcodePop()
        {
            _logger.Debug("POP");
            _stack.PopValue();
        }

        private void OpcodePrint()
        {
            _logger.Debug("PRINT");

            ushort nBytesRead;
            string msg = Zscii.DecodeText(_memory.Data, _pc, out nBytesRead);
            _pc += nBytesRead;

            _screen.Print(msg);
        }

        private void OpcodeRFalse()
        {
            _logger.Debug("RFALSE");
            ReturnRoutine(MemWord.FromBool(false));
        }

        private void OpcodeRTrue()
        {
            _logger.Debug("RTRUE");
            ReturnRoutine(MemWord.FromBool(true));
        }


        //-------------------------------------------------
        //--- 1OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodeDec(MemValue[] operands)
        {
            _logger.Debug($"DEC {operands[0]}");

            GameVariableId varId = ((MemByte)operands[0]).Value;

            MemWord varValue = ReadVariable(varId);
            varValue--;
            WriteVariable(varId, varValue);
        }

        private void OpcodeGetChild(MemValue[] operands)
        {
            _logger.Debug($"GET_CHILD {operands[0]}");

            GameObjectId objId = (GameObjectId)operands[0].FullValue;
            GameObject obj = _memory.FindObject(objId);

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, obj.ChildId);

            Branch(obj.ChildId != 0x00);
        }

        private void OpcodeGetParent(MemValue[] operands)
        {
            _logger.Debug($"GET_PARENT {operands[0]}");

            GameObjectId objId = (GameObjectId)operands[0].FullValue;
            GameObject obj = _memory.FindObject(objId);

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, obj.ParentId);
        }

        private void OpcodeGetSibling(MemValue[] operands)
        {
            _logger.Debug($"GET_SIBLING {operands[0]}");

            GameObjectId objId = (GameObjectId)operands[0].FullValue;
            GameObject obj = _memory.FindObject(objId);

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, obj.SiblingId);
        }


        private void OpcodeInc(MemValue[] operands)
        {
            _logger.Debug($"INC {operands[0]}");

            GameVariableId varId = ((MemByte)operands[0]).Value;

            MemWord varValue = ReadVariable(varId);
            varValue++;
            WriteVariable(varId, varValue);
        }

        private void OpcodeJump(MemValue[] operands)
        {
            _logger.Debug($"JUMP {operands[0]}");

            short targetOffset = operands[0].SignedValue;
            _pc += targetOffset - 2;
        }


        private void OpcodeNot(MemValue[] operands)
        {
            _logger.Debug($"NOT {operands[0]}");

            MemWord value = new MemWord(operands[0].FullValue); //force to MemWord even if it is a MemByte

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, ~value);
        }


        private void OpcodePrintAddr(MemValue[] operands)
        {
            _logger.Debug($"PRINT_ADDR {operands[0]}");

            MemWord targetAddr = new MemWord(operands[0].FullValue);
            string msg = Zscii.DecodeText(_memory.Data, targetAddr, out _);

            _screen.Print(msg);
        }


        private void OpcodePrintObj(MemValue[] operands)
        {
            _logger.Debug($"PRINT_OBJ {operands[0]}");

            GameObjectId objId = (GameObjectId)operands[0].FullValue;
            GameObject obj = _memory.FindObject(objId);

            _screen.Print(obj.ShortName);
        }


        private void OpcodePrintPAddr(MemValue[] operands)
        {
            _logger.Debug($"PRINT_PADDR {operands[0]}");

            MemWord targetAddr = new MemWord(operands[0].FullValue * PACKED_ADDR_FACTOR);
            string msg = Zscii.DecodeText(_memory.Data, targetAddr, out _);

            _screen.Print(msg);
        }

        private void OpcodeRet(MemValue[] operands)
        {
            _logger.Debug($"RET {operands[0]}");

            ReturnRoutine(operands[0].FullValue);
        }

        //-------------------------------------------------
        //--- 2OP -----------------------------------------
        //-------------------------------------------------
        private void OpcodeAnd(MemValue[] operands)
        {
            _logger.Debug($"AND {operands[0]} {operands[1]}");

            MemWord a = new MemWord(operands[0].FullValue);
            MemWord b = new MemWord(operands[1].FullValue);

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, a & b);
        }

        private void OpcodeClearAttr(MemValue[] operands)
        {
            _logger.Debug($"CLEAR_ATTR {operands[0]} {operands[1]}");

            GameObjectId objId = (GameObjectId)operands[0].FullValue;
            GameObject obj = _memory.FindObject(objId);

            ushort iAttr = operands[1].FullValue;

            obj.ClearAttribute(iAttr);
        }

        private void OpcodeGetProp(MemValue[] operands)
        {
            _logger.Debug($"GET_PROP {operands[0]} {operands[1]}");

            GameObjectId objId = (GameObjectId)operands[0].FullValue;
            GameObject obj = _memory.FindObject(objId);

            ushort propId = operands[1].FullValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            MemValue pValue = obj.GetPropertyValue(propId);
            if (pValue == null)
                pValue = _memory.GetDefaultPropertyValue(propId);

            WriteVariable(storeVar, new MemWord(pValue.FullValue));
        }

        private void OpcodeIncChk(MemValue[] operands)
        {
            _logger.Debug($"INC_CHK {operands[0]} {operands[1]}");

            GameVariableId varId = ((MemByte)operands[0]).Value;
            MemWord cmpValue = (MemWord)operands[1];

            //CHECK ReadOperands in this case
            MemWord varValue = ReadVariable(varId);
            varValue++;
            WriteVariable(varId, varValue);

            Branch(varValue.SignedValue > cmpValue.SignedValue);
        }


        private void OpcodeInsertObj(MemValue[] operands)
        {
            _logger.Debug($"INSERT_OBJ {operands[0]} {operands[1]}");

            GameObjectId objId = (GameObjectId)operands[0].FullValue;
            GameObjectId destId = (GameObjectId)operands[1].FullValue;

            GameObject obj = _memory.FindObject(objId);

            obj.DetachFromParent();
            obj.AttachToParent(destId);
        }

        private void OpcodeJE(MemValue[] operands)
        {
            _logger.Debug($"JE {operands[0]} {operands[1]}");

            Branch(operands[0].FullValue == operands[1].FullValue);
        }

        private void OpcodeJIn(MemValue[] operands)
        {
            _logger.Debug($"JIN {operands[0]} {operands[1]}");

            GameObjectId objId = (GameObjectId)operands[0].FullValue;
            GameObjectId parentId = (GameObjectId)operands[1].FullValue;

            GameObject obj = _memory.FindObject(objId);

            Branch(obj != null && obj.ParentId == parentId);
        }

        private void OpcodeJG(MemValue[] operands)
        {
            _logger.Debug($"JG {operands[0]} {operands[1]}");

            Branch(operands[0].SignedValue > operands[1].SignedValue);
        }

        private void OpcodeJL(MemValue[] operands)
        {
            _logger.Debug($"JL {operands[0]} {operands[1]}");

            Branch(operands[0].SignedValue < operands[1].SignedValue);
        }

        private void OpcodeLoadB(MemValue[] operands)
        {
            _logger.Debug($"LOADB {operands[0]} {operands[1]}");

            ushort arr = operands[0].FullValue;
            ushort index = operands[1].FullValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            MemByte val = _memory.ReadByte((ushort)(arr + index));
            WriteVariable(storeVar, val.FullValue);
        }

        private void OpcodeLoadW(MemValue[] operands)
        {
            _logger.Debug($"LOADW {operands[0]} {operands[1]}");

            ushort arr = operands[0].FullValue;
            ushort index = operands[1].FullValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            MemWord val = _memory.ReadWord((ushort)(arr + index * 2));
            WriteVariable(storeVar, val);
        }

        private void OpcodeMod(MemValue[] operands)
        {
            _logger.Debug($"MOD {operands[0]} {operands[1]}");
            short a = operands[0].SignedValue;
            short b = operands[1].SignedValue;

            GameVariableId varId = _memory.ReadByte(_pc).Value;
            _pc++;

            if (b == 0)
            {
                Debug.Assert(false, "Division by zero");
            }
            else
            {
                WriteVariable(varId, MemWord.FromSignedValue(a % b));
            }
        }

        private void OpcodeSetAttr(MemValue[] operands)
        {
            _logger.Debug($"SET_ATTR {operands[0]} {operands[1]}");

            GameObjectId objId = (GameObjectId)operands[0].FullValue;
            GameObject obj = _memory.FindObject(objId);

            ushort iAttr = operands[1].FullValue;

            obj.SetAttribute(iAttr);
        }

        private void OpcodeStore(MemValue[] operands)
        {
            _logger.Debug($"STORE {operands[0]} {operands[1]}");

            GameVariableId varId = ((MemByte)operands[0]).Value; //CHECK ReadOperands
            
            ushort value = operands[1].FullValue;
            WriteVariable(varId, value);
        }

        private void OpcodeTestAttr(MemValue[] operands)
        {
            _logger.Debug($"TEST_ATTR {operands[0]} {operands[1]}");

            GameObjectId objId = (GameObjectId)operands[0].FullValue;
            GameObject obj = _memory.FindObject(objId);

            ushort iAttr = operands[1].FullValue;

            Branch(obj.HasAttribute(iAttr));
        }
    }
}