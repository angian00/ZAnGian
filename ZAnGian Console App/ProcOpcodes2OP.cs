using System;
using System.Diagnostics;

namespace ZAnGian
{
    public partial class ZProcessor
    {
        private void OpcodeAdd(MemValue[] operands)
        {
            _logger.Debug($"ADD {operands[0]} {operands[1]}");
            short a = operands[0].SignedValue;
            short b = operands[1].SignedValue;

            GameVariableId varId = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(varId, MemWord.FromSignedValue(a + b));
        }

        private void OpcodeAnd(MemValue[] operands)
        {
            _logger.Debug($"AND {operands[0]} {operands[1]}");

            MemWord a = new MemWord(operands[0].FullValue);
            MemWord b = new MemWord(operands[1].FullValue);

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, a & b);
        }


        private void OpcodeCall2N(MemValue[] operands)
        {
            _logger.Debug($"CALL_2N {operands[0]} {operands[1]}");

            MemValue packedAddr = operands[0];
            MemValue[] args = new MemValue[] { operands[1] };

            CallRoutine(packedAddr, args, null);
        }

        private void OpcodeCall2S(MemValue[] operands)
        {
            _logger.Debug($"CALL_2S {operands[0]} {operands[1]}");

            MemValue packedAddr = operands[0];
            MemValue[] args = new MemValue[] { operands[1] };

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            CallRoutine(packedAddr, args, storeVar);
        }


       private void OpcodeClearAttr(MemValue[] operands)
        {
            _logger.Debug($"CLEAR_ATTR {operands[0]} {operands[1]}");

            GameObjectId objId = operands[0];
            if (objId.FullValue == 0x00)
                return;

            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            ushort iAttr = operands[1].FullValue;

            obj.ClearAttribute(iAttr);
        }


        private void OpcodeDecChk(OperandType[] operandTypes, MemValue[] operands)
        {
            _logger.Debug($"DEC_CHK {operands[0]} {operands[1]}");

            Debug.Assert(operandTypes[0] == OperandType.Variable);
            GameVariableId varId = ((MemByte)operands[0]).Value;

            MemWord cmpValue;
            if (operandTypes[1] == OperandType.Variable)
                cmpValue = ReadVariable(((MemByte)operands[1]).Value);
            else
                cmpValue = new MemWord(operands[1].FullValue);


            MemWord varValue = ReadVariable(varId);
            varValue--;
            WriteVariable(varId, varValue);

            Branch(varValue.SignedValue < cmpValue.SignedValue);
        }


        private void OpcodeDiv(MemValue[] operands)
        {
            _logger.Debug($"DIV {operands[0]} {operands[1]}");
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
                WriteVariable(varId, MemWord.FromSignedValue(a / b));
            }
        }

        private void OpcodeGetNextProp(MemValue[] operands)
        {
            _logger.Debug($"GET_NEXT_PROP {operands[0]} {operands[1]}");

            GameObjectId objId = operands[0];
            ushort propId = operands[1].FullValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            MemWord? value = null;
            if (objId.FullValue != 0x00 && propId != 0x00)
            {
                GameObject? obj = _memory.FindObject(objId);
                Debug.Assert(obj != null);

                value = obj.GetNextPropertyId(propId);
            }

            if (value == (MemWord)null)
                value = new MemWord(0x00);

            WriteVariable(storeVar, value);
        }

        private void OpcodeGetProp(MemValue[] operands)
        {
            _logger.Debug($"GET_PROP {operands[0]} {operands[1]}");

            GameObjectId objId = operands[0];
            ushort propId = operands[1].FullValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            
            MemValue? pValue = null;
            if (objId.FullValue != 0x00)
            {
                GameObject? obj = _memory.FindObject(objId);
                Debug.Assert(obj != null);
                pValue = obj.GetPropertyValue(propId);
            }
    
            if (pValue == null)
                pValue = _memory.GetDefaultPropertyValue(propId);

            WriteVariable(storeVar, new MemWord(pValue.FullValue));
        }

        private void OpcodeGetPropAddr(MemValue[] operands)
        {
            _logger.Debug($"GET_PROP_ADDR {operands[0]} {operands[1]}");

            GameObjectId objId = operands[0];

            ushort propId = operands[1].FullValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            MemWord? pAddr = null;

            if (objId.FullValue != 0x00)
            {
                GameObject? obj = _memory.FindObject(objId);
                Debug.Assert(obj != null);
                pAddr = obj.GetPropertyAddress(propId);
            }

            if (pAddr == (MemWord)null)
                pAddr = new MemWord(0x00);

            WriteVariable(storeVar, pAddr);
        }

        private void OpcodeIncChk(OperandType[] operandTypes, MemValue[] operands)
        {
            _logger.Debug($"INC_CHK {operands[0]} {operands[1]}");

            Debug.Assert(operandTypes[0] == OperandType.Variable);
            GameVariableId varId = ((MemByte)operands[0]).Value;

            MemWord cmpValue;
            if (operandTypes[1] == OperandType.Variable)
                cmpValue = ReadVariable(((MemByte)operands[1]).Value);
            else
                cmpValue = new MemWord(operands[1].FullValue);


            MemWord varValue = ReadVariable(varId);
            varValue++;
            WriteVariable(varId, varValue);

            Branch(varValue.SignedValue > cmpValue.SignedValue);
        }


        private void OpcodeInsertObj(MemValue[] operands)
        {
            _logger.Debug($"INSERT_OBJ {operands[0]} {operands[1]}");

            GameObjectId objId = operands[0];
            GameObjectId destId = operands[1];

            if (objId.FullValue == 0x00 || destId.FullValue == 0x00)
                return;

            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            obj.DetachFromParent();
            obj.AttachToParent(destId);
        }

        private void OpcodeJE(OperandType[] operandTypes, MemValue[] operands)
        {
            _logger.Debug($"JE {operands[0]} {operands[1]}");

            //JE is strange, it has up to 4 operands
            bool condition = false;
            for (int i = 1; i < operandTypes.Length; i++)
            {
                if (operandTypes[i] == OperandType.Omitted)
                    break;
                else
                    condition = (condition || (operands[0].FullValue == operands[i].FullValue));
            }

            Branch(condition);
        }

        private void OpcodeJIn(MemValue[] operands)
        {
            _logger.Debug($"JIN {operands[0]} {operands[1]}");

            GameObjectId objId = operands[0];
            GameObjectId parentId = operands[1];

            GameObject? obj = _memory.FindObject(objId);

            Branch((objId.FullValue == 0x00 && parentId.FullValue == 0x00) ||
                (obj != null) && (obj.ParentId.FullValue == parentId.FullValue));
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

        private void OpcodeMul(MemValue[] operands)
        {
            _logger.Debug($"MUL {operands[0]} {operands[1]}");
            short a = operands[0].SignedValue;
            short b = operands[1].SignedValue;

            GameVariableId varId = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(varId, MemWord.FromSignedValue(a * b));
        }

        private void OpcodeOr(MemValue[] operands)
        {
            _logger.Debug($"OR {operands[0]} {operands[1]}");

            MemWord a = new MemWord(operands[0].FullValue);
            MemWord b = new MemWord(operands[1].FullValue);

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, a | b);
        }

        private void OpcodeSetAttr(MemValue[] operands)
        {
            _logger.Debug($"SET_ATTR {operands[0]} {operands[1]}");

            GameObjectId objId = operands[0];
            if (objId.FullValue == 0x00)
                return;

            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            ushort iAttr = operands[1].FullValue;

            obj.SetAttribute(iAttr);
        }


        private void OpcodeSetColour(MemValue[] operands)
        {
            _logger.Debug($"SET_COLOUR {operands[0]} {operands[1]}");
            //TODO: set_colour foreground background
        }

        private void OpcodeStore(OperandType[] operandTypes, MemValue[] operands)
        {
            _logger.Debug($"STORE {operands[0]} {operands[1]}");

            GameVariableId varId = ((MemByte)operands[0]).Value;

            ushort value;
            if (operandTypes[1] == OperandType.Variable)
                value = ReadVariable(((MemByte)operands[1]).Value).FullValue;
            else
                value = operands[1].FullValue;

            WriteVariable(varId, value);
        }

        private void OpcodeSub(MemValue[] operands)
        {
            _logger.Debug($"SUB {operands[0]} {operands[1]}");
            short a = operands[0].SignedValue;
            short b = operands[1].SignedValue;

            GameVariableId varId = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(varId, MemWord.FromSignedValue(a - b));
        }

        private void OpcodeTest(MemValue[] operands)
        {
            _logger.Debug($"TEST {operands[0]} {operands[1]}");

            MemWord a = new MemWord(operands[0].FullValue);
            MemWord b = new MemWord(operands[1].FullValue);

            Branch((a & b) == b);
        }

        private void OpcodeTestAttr(MemValue[] operands)
        {
            _logger.Debug($"TEST_ATTR {operands[0]} {operands[1]}");

            GameObjectId objId = operands[0];
            ushort iAttr = operands[1].FullValue;

            bool condition = false;
            
            if (objId.FullValue != 0x00)
            {
                GameObject? obj = _memory.FindObject(objId);
                Debug.Assert(obj != null);

                condition = obj.HasAttribute(iAttr);
            }

            Branch(condition);
        }

        private void OpcodeThrow(MemValue[] operands)
        {
            _logger.Debug($"THROW {operands[0]} {operands[1]}");
            //TODO: throw
        }

    }
}