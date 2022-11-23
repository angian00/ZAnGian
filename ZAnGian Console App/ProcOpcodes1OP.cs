using System;
using System.Diagnostics;

namespace ZAnGian
{
    public partial class ZProcessor
    {
        private void OpcodeCall1N(MemValue[] operands)
        {
            _logger.Debug($"CALL_1N {operands[0]}");

            MemValue packedAddr = operands[0];

            MemValue[] args = new MemValue[0];

            CallRoutine(packedAddr, args, null);
        }

        private void OpcodeCall1S(MemValue[] operands)
        {
            _logger.Debug($"CALL_1S {operands[0]}");

            MemValue packedAddr = operands[0];

            MemValue[] args = new MemValue[0];

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            CallRoutine(packedAddr, args, storeVar);
        }


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
            UInt16 retValue;

            GameObjectId objId = operands[0];
            if (objId.FullValue == 0x00)
            {
                retValue = 0x00;
            }
            else
            {
                GameObject? obj = _memory.FindObject(objId);
                Debug.Assert(obj != null);
                retValue = obj.ChildId.FullValue;
            }

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, retValue);

            Branch(retValue != 0x00);
        }

        private void OpcodeGetParent(MemValue[] operands)
        {
            _logger.Debug($"GET_PARENT {operands[0]}");
            UInt16 retValue;

            GameObjectId objId = (GameObjectId)operands[0];
            if (objId.FullValue == 0x00)
            {
                retValue = 0x00;
            }
            else
            {
                GameObject? obj = _memory.FindObject(objId);
                Debug.Assert(obj != null);
                retValue = obj.ParentId.FullValue;
            }

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, retValue);
        }

        private void OpcodeGetPropLen(MemValue[] operands)
        {
            _logger.Debug($"GET_PROP_LEN {operands[0]}");

            MemWord propAddr = new MemWord(operands[0].FullValue);

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            MemByte pSize;
            if (propAddr == 0x00)
                pSize = new MemByte(0x00);
            else
                pSize = _memory.GetPropertyLength(propAddr);

            WriteVariable(storeVar, pSize.FullValue);
        }

        private void OpcodeGetSibling(MemValue[] operands)
        {
            _logger.Debug($"GET_SIBLING {operands[0]}");
            UInt16 retValue;

            GameObjectId objId = operands[0];
            if (objId.FullValue == 0x00)
            {
                retValue = 0x00;
            }
            else
            {
                GameObject? obj = _memory.FindObject(objId);
                Debug.Assert(obj != null);
                retValue = obj.SiblingId.FullValue;
            }

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, retValue);

            Branch(retValue != 0x00);
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
            _pc = (HighMemoryAddress)(_pc + targetOffset - 2);
        }


        private void OpcodeJZ(MemValue[] operands)
        {
            _logger.Debug($"JZ {operands[0]}");

            Branch(operands[0].FullValue == 0x00);
        }


        private void OpcodeLoad(OperandType[] operandTypes, MemValue[] operands)
        {
            _logger.Debug($"INC {operands[0]}");

            //Debug.Assert(operandTypes[0] == OperandType.Variable);
            GameVariableId sourceVarId = ((MemByte)operands[0]).Value;

            MemWord varValue = ReadVariable(sourceVarId);

            GameVariableId targetVarId = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(targetVarId, varValue);
        }

        private void OpcodeNotV3(MemValue[] operands)
        {
            _logger.Debug($"NOT {operands[0]}");

            MemWord value = new MemWord(operands[0].FullValue);

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, ~value);
        }


        private void OpcodePrintAddr(MemValue[] operands)
        {
            _logger.Debug($"PRINT_ADDR {operands[0]}");

            MemWord targetAddr = new MemWord(operands[0].FullValue);
            string msg = Zscii.DecodeText(_memory.Data, targetAddr.Value, out _, memory: _memory);

            _screen.Print(msg);
        }


        private void OpcodePrintObj(MemValue[] operands)
        {
            _logger.Debug($"PRINT_OBJ {operands[0]}");

            GameObjectId objId = operands[0];
            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            _screen.Print(obj.ShortName);
        }


        private void OpcodePrintPAddr(MemValue[] operands)
        {
            _logger.Debug($"PRINT_PADDR {operands[0]}");

            HighMemoryAddress targetAddr = UnpackAddress(operands[0]);
            string msg = Zscii.DecodeText(_memory.Data, targetAddr, out _, memory: _memory);

            _screen.Print(msg);
        }

        private void OpcodeRemoveObj(MemValue[] operands)
        {
            _logger.Debug($"REMOVE_OBJ {operands[0]}");

            GameObjectId objId = operands[0];
            if (objId.FullValue == 0x00)
                return;

            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            obj.DetachFromParent();
        }

        private void OpcodeRet(MemValue[] operands)
        {
            _logger.Debug($"RET {operands[0]}");

            ReturnRoutine(operands[0].FullValue);
        }

    }
}