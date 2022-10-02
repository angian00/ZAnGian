using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Runtime.ConstrainedExecution;
using System.Text;
using static System.Net.Mime.MediaTypeNames;

namespace ZAnGian
{
    public class ZProcessor
    {
        private static Logger _logger = Logger.GetInstance();

        private const int PACKED_ADDR_FACTOR = 2; //depends on game version

        private enum OpCodeForm
        {
            Short,
            Variable,
            Long,
            Extended //unused if ZVersion < 5
        }

        private enum OperandType
        {
            Omitted,
            LargeConstant,
            SmallConstant,
            Variable
        }



        private ZMemory _memory;
        private MemWord _pc; //Program Counter
        private ZScreen _screen;
        private ZStack _stack;


        public ZProcessor(ZMemory memory)
        {
            this._memory = memory;
            _pc = memory.StartPC;
            this._screen = new ZScreen();
            this._stack = new ZStack();
        }

        public void Run()
        {
            byte[] memData = _memory.Data;

            //while (true)
            for (int iInstr = 0; iInstr < 100; iInstr++)
            {
                _logger.Debug($"sp={_pc} [0x{_pc:x}]: {StringUtils.ByteToBinaryString(_memory.ReadByte(_pc).Value)}");

                byte formBits = (byte)(_memory.ReadByte(_pc).Value >> 6);
                OpCodeForm form;

                switch (formBits)
                {
                    case 0b10:
                        form = OpCodeForm.Short;
                        break;
                    case 0b11:
                        form = OpCodeForm.Variable;
                        break;
                    default:
                        form = OpCodeForm.Long;
                        break;
                }

                bool isVAR = false;
                byte opcode = 0x00; //nonexistent opcode as default
                byte opTypeBits;
                byte nOps = 0;
                OperandType[] operandTypes = new OperandType[4];

                switch (form)
                {
                    case OpCodeForm.Short:
                        opcode = (_memory.ReadByte(_pc) & 0b00001111).Value;

                        opTypeBits = ((_memory.ReadByte(_pc) & 0b00110000) >> 4).Value;
                        operandTypes[0] = ParseOperandType(opTypeBits);
                        if (operandTypes[0] == OperandType.Omitted)
                            nOps = 0;
                        else
                            nOps = 1;

                        _pc++;
                        break;

                    case OpCodeForm.Long:
                        opcode = (_memory.ReadByte(_pc) & 0b00011111).Value;
                        nOps = 2;

                        if ((_memory.ReadByte(_pc) & 0b01000000) == 0b01000000)
                            operandTypes[0] = OperandType.Variable;
                        else
                            operandTypes[0] = OperandType.SmallConstant;

                        if ((_memory.ReadByte(_pc).Value & 0b00100000) == 0b00100000)
                            operandTypes[1] = OperandType.Variable;
                        else
                            operandTypes[1] = OperandType.SmallConstant;

                        _pc++;
                        break;

                    case OpCodeForm.Variable:
                        opcode = (_memory.ReadByte(_pc) & 0b00011111).Value;
                        operandTypes[0] = ParseOperandType(((_memory.ReadByte(_pc + 1) & 0b11000000) >> 6).Value);
                        operandTypes[1] = ParseOperandType(((_memory.ReadByte(_pc + 1) & 0b00110000) >> 4).Value);
                        operandTypes[2] = ParseOperandType(((_memory.ReadByte(_pc + 1) & 0b00001100) >> 2).Value);
                        operandTypes[3] = ParseOperandType((_memory.ReadByte(_pc + 1) & 0b00000011).Value);

                        if ((_memory.ReadByte(_pc) & 0b00100000) == 0x00)
                        {
                            nOps = 2;
                        }
                        else
                        {
                            isVAR = true;
                            //nOps determined by "Omitted" operand types
                            nOps = 4;
                            for (int iOp = 0; iOp < 4; iOp++)
                            {
                                if (operandTypes[3 - iOp] == OperandType.Omitted)
                                    nOps--;
                                else
                                    break;
                            }
                        }

                        _pc += 2;
                        break;

                    default:
                        Debug.Assert(false, "Should never be reached");
                        break;
                }

                MemValue[] operands = ReadOperands(nOps, operandTypes);
                RunOpCode(isVAR, opcode, nOps, operandTypes, operands);
            }
        }


        private MemValue[] ReadOperands(byte nOps, OperandType[] operandTypes)
        {
            MemValue[] operands = new MemValue[4];


            for (int iOp = 0; iOp < nOps; iOp++)
            {
                switch (operandTypes[iOp])
                {
                    case OperandType.LargeConstant:
                        operands[iOp] = _memory.ReadWord(_pc);
                        _pc += 2;
                        break;
                    case OperandType.SmallConstant:
                        operands[iOp] = _memory.ReadByte(_pc);
                        _pc++;
                        break;
                    case OperandType.Variable:
                        GameVariableId varId = _memory.ReadByte(_pc).Value;
                        _pc++;
                        operands[iOp] = ReadVariable(varId);
                        break;
                    default:
                        Debug.Assert(false, "Should never be reached");
                        break;
                }
            }

            return operands;
        }


        private void RunOpCode(bool isVAR, byte opcode, byte nOps, OperandType[] operandTypes, MemValue[] operands)
        {
            string opCodeStr = (isVAR ? "VAR" : $"{nOps}OP") + $":0x{opcode:X}";
            _logger.Debug($"RunOpCode [{opCodeStr}]");

            if (isVAR)
            {
                switch (opcode)
                {
                    case 0x00:
                        OpcodeCall(nOps, operands);
                        break;

                    case 0x05:
                        OpcodePrintChar(nOps, operands);
                        break;

                    case 0x06:
                        OpcodePrintNum(nOps, operands);
                        break;

                    default:
                        throw new NotImplementedException($"Unimplemented opcode: {opCodeStr}");
                }

            }
            else
            {
                switch (nOps)
                {
                    case 0:
                        switch (opcode)
                        {
                            case 0x00:
                                OpcodeRTrue();
                                break;

                            case 0x02:
                                OpcodePrint();
                                break;

                            case 0x09:
                                OpcodePop();
                                break;

                            case 0x0E:
                            case 0x0F:
                                throw new ArgumentException($"Invalid opcode (for ZVersion == 3): {opCodeStr}");

                            default:
                                throw new NotImplementedException($"Unimplemented opcode: {opCodeStr}");
                        }

                        break;

                    case 1:
                        switch (opcode)
                        {
                            case 0x05:
                                OpcodeInc(operands);
                                break;

                            case 0x06:
                                OpcodeDec(operands);
                                break;

                            case 0x07:
                                OpcodePrintAddr(operands);
                                break;

                            case 0x08:
                                throw new ArgumentException($"Invalid opcode (for ZVersion == 3): {opCodeStr}");

                            case 0x0C:
                                OpcodeJump(operands);
                                break;

                            case 0x0D:
                                OpcodePrintPAddr(operands);
                                break;

                            case 0x0F:
                                OpcodeNOT(operands);
                                break;
                                
                            default:
                                throw new NotImplementedException($"Unimplemented opcode: {opCodeStr}");
                        }

                        break;

                    case 2:
                        switch (opcode)
                        {
                            case 0x00:
                                throw new ArgumentException($"Invalid opcode: {opCodeStr}");

                            case 0x01:
                                OpcodeJE(operands);
                                break;

                            case 0x02:
                                OpcodeJL(operands);
                                break;

                            case 0x05:
                                OpcodeIncChk(operands);
                                break;

                            case 0x06:
                                OpcodeJIn(operands);
                                break;

                            case 0x09:
                                OpcodeAND(operands);
                                break;


                            case 0x0D:
                                OpcodeStore(operands);
                                break;

                            case 0x0E:
                                OpcodeInsertObj(operands);
                                break;

                            case 0x0F:
                                OpcodeLoadW(operands);
                                break;

                            case 0x10:
                                OpcodeLoadB(operands);
                                break;


                            case 0x18:
                                OpcodeMod(operands);
                                break;

                            case 0x19:
                            case 0x1A:
                            case 0x1B:
                            case 0x1C:
                                throw new ArgumentException($"Invalid opcode (for ZVersion == 3): {opCodeStr}");

                            case 0x1D:
                            case 0x1E:
                            case 0x1F:
                                throw new ArgumentException($"Invalid opcode: {opCodeStr}");

                            default:
                                throw new NotImplementedException($"Unimplemented opcode: {opCodeStr}");
                        }

                        break;

                    default:
                        throw new NotImplementedException($"Unimplemented opcode: {opCodeStr}");
                        break;
                }
            }
        }

        //-------------------------------------------------
        //--- VAR -----------------------------------------
        //-------------------------------------------------

        private void OpcodeCall(int nOps, MemValue[] operands)
        {
            _logger.All($"CALL {FormatOperands(nOps, operands)}");

            MemWord packedAddr = (MemWord)operands[0];
            MemWord routineAddr = packedAddr * 2;


            RoutineData newRoutine = new RoutineData();
            for (int i = 0; i < nOps - 1; i++)
            {
                //TODO: push argument operands[i+1]
            }



            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            newRoutine.ReturnVariableId = storeVar;
            newRoutine.ReturnAddress = _pc;

            //jump to routine instructions
            _pc = routineAddr;

            byte nLocalVars = _memory.ReadByte(_pc).Value;
            _pc++;

            for (int i = 0; i < nLocalVars; i++)
            {
                newRoutine.AddLocalVariable(_memory.ReadWord(_pc));
                _pc += 2;
            }
            
            _stack.AddRoutine(newRoutine);
        }


        private void OpcodePrintChar(int nOps, MemValue[] operands)
        {
            _logger.All($"PRINT_CHAR {FormatOperands(nOps, operands)}");

            //Zscii.DecodeText();
            byte zchar = (byte)operands[0].FullValue;
            _screen.Print($"{Zscii.ZChar2Zscii(zchar)}");
        }


        private void OpcodePrintNum(int nOps, MemValue[] operands)
        {
            _logger.All($"PRINT_NUM {FormatOperands(nOps, operands)}");

            int val = ((MemWord)operands[0]).SignedValue;

            _screen.Print($"{val}");
        }

        //-------------------------------------------------
        //--- 0OP -----------------------------------------
        //-------------------------------------------------

            private void OpcodePop()
        {
            _logger.All("POP");
            _stack.Pop();
        }

        private void OpcodePrint()
        {
            _logger.All("PRINT");

            ushort nBytesRead;
            string msg = Zscii.DecodeText(_memory.Data, _pc, out nBytesRead);
            _pc += nBytesRead;

            _screen.Print(msg);
        }

        private void OpcodeRTrue()
        {
            _logger.All("RTRUE");
            //MemWord returnAddress = (MemWord)_stack.Pop(); //FIXME: use routinedata
        }


        //-------------------------------------------------
        //--- 1OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodeDec(MemValue[] operands)
        {
            _logger.All($"DEC {operands[0]}");

            GameVariableId varId = ((MemByte)operands[0]).Value;

            MemWord varValue = ReadVariable(varId);
            varValue--;
            WriteVariable(varId, varValue);
        }

        private void OpcodeInc(MemValue[] operands)
        {
            _logger.All($"INC {operands[0]}");

            GameVariableId varId = ((MemByte)operands[0]).Value;

            MemWord varValue = ReadVariable(varId);
            varValue++;
            WriteVariable(varId, varValue);
        }

        private void OpcodeJump(MemValue[] operands)
        {
            _logger.All($"JUMP {operands[0]}");

            MemWord targetOffset = (MemWord)operands[0];
            _pc += targetOffset.SignedValue - 2;
        }


        private void OpcodeNOT(MemValue[] operands)
        {
            _logger.All($"NOT {operands[0]}");

            MemWord value = (MemWord)operands[0];

            MemWord targetAddr = _memory.ReadWord(_pc);
            _pc += 2;

            _memory.WriteWord(targetAddr, ~value);
        }


        private void OpcodePrintAddr(MemValue[] operands)
        {
            _logger.All($"PRINT_ADDR {operands[0]}");

            MemWord targetAddr = (MemWord)operands[0];
            string msg = Zscii.DecodeText(_memory.Data, targetAddr, out _);

            _screen.Print(msg);
        }


        private void OpcodePrintPAddr(MemValue[] operands)
        {
            _logger.All($"PRINT_PADDR {operands[0]}");

            MemWord packedAddr = (MemWord)operands[0] * PACKED_ADDR_FACTOR; 
            MemWord targetAddr = packedAddr;

            string msg = Zscii.DecodeText(_memory.Data, targetAddr, out _);
            _screen.Print(msg);
        }


        //-------------------------------------------------
        //--- 2OP -----------------------------------------
        //-------------------------------------------------
        private void OpcodeAND(MemValue[] operands)
        {
            _logger.All($"AND {operands[0]} {operands[1]}");

            MemWord a = (MemWord)operands[0];
            MemWord b = (MemWord)operands[1];

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, a & b);
        }

        private void OpcodeLoadB(MemValue[] operands)
        {
            _logger.All($"LOADB {operands[0]} {operands[1]}");

            uint arr = operands[0].FullValue;
            uint index = operands[1].FullValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            //MemWord val = _memory.ReadWord((ushort)(arr + index));
            MemByte val = _memory.ReadByte((ushort)(arr + index));
            WriteVariable(storeVar, new MemWord(val.Value));
        }

        private void OpcodeLoadW(MemValue[] operands)
        {
            _logger.All($"LOADW {operands[0]} {operands[1]}");

            uint arr = operands[0].FullValue;
            uint index = operands[1].FullValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            MemWord val = _memory.ReadWord((ushort)(arr + index * 2));
            WriteVariable(storeVar, val);
        }

        private void OpcodeIncChk(MemValue[] operands)
        {
            _logger.All($"INC_CHK {operands[0]} {operands[1]}");
            
            GameVariableId varId = ((MemByte)operands[0]).Value;
            MemWord cmpValue = (MemWord)operands[1];

            MemWord targetAddr = _memory.ReadWord(_pc);
            _pc += 2;

            MemWord varValue = ReadVariable(varId);
            varValue++;
            WriteVariable(varId, varValue);

            if (varValue.SignedValue > cmpValue.SignedValue)
                _pc = targetAddr;
        }


        private void OpcodeInsertObj(MemValue[] operands)
        {
            _logger.All($"INSERT_OBJ {operands[0]} {operands[1]}");

            GameObjectId objId = ((MemByte)operands[0]).Value;
            GameObjectId destId = ((MemByte)operands[0]).Value;

            GameObject obj = _memory.FindObject(objId);

            obj.DetachFromParent();
            obj.AttachToParent(destId);
        }

        private void OpcodeJE(MemValue[] operands)
        {
            _logger.All($"JE {operands[0]} {operands[1]}");

            MemWord a = (MemWord)operands[0];
            MemWord b = (MemWord)operands[1];

            MemWord targetAddr = _memory.ReadWord(_pc);
            _pc += 2;
            if (a.Value == b.Value)
                _pc = targetAddr;
        }

        private void OpcodeJIn(MemValue[] operands)
        {
            _logger.All($"JIN {operands[0]} {operands[1]}");

            GameObjectId objId = ((MemByte)operands[0]).Value;
            GameObjectId parentId = ((MemByte)operands[1]).Value;

            MemWord targetAddr = _memory.ReadWord(_pc);
            _pc += 2;
            
            GameObject obj = _memory.FindObject(objId);

            if (obj != null && obj.ParentId == parentId)
                _pc = targetAddr; //FIXME: use branch logic
        }

        private void OpcodeJL(MemValue[] operands)
        {
            _logger.All($"JL {operands[0]} {operands[1]}");

            int a = operands[0].SignedValue;
            int b = operands[1].SignedValue;

            BranchJump(a < b);
        }

        private void OpcodeMod(MemValue[] operands)
        {
            _logger.All($"MOD {operands[0]} {operands[1]}");
            MemWord a = (MemWord)operands[0];
            MemWord b = (MemWord)operands[1];
            GameVariableId varId = _memory.ReadByte(_pc).Value;
            _pc++;
            if (b.SignedValue == 0)
            {
                Debug.Assert(false, "Division by zero");
            }
            else
            {
                WriteVariable(varId, MemWord.fromSignedValue(a.SignedValue % b.SignedValue));
            }
        }
        private void OpcodeStore(MemValue[] operands)
        {
            _logger.All($"STORE {operands[0]} {operands[1]}");
            GameVariableId varId = ((MemByte)operands[0]).Value;
            MemByte val = (MemByte)operands[1];
            WriteVariable(varId, new MemWord(val.Value));
        }

        //-------------------------------------------------
        //-------------------------------------------------

        /** Branching logic, as per spec 4.7
             The branch information is stored in one or two bytes, indicating what to do with the result of the test.
            If bit 7 of the first byte is 0, a branch occurs when the condition was false; 
            if 1, then branch is on true.If bit 6 is set, then the branch occupies 1 byte only, 
                and the "offset" is in the range 0 to 63, given in the bottom 6 bits.If bit 6 is clear, 
            then the offset is a signed 14 - bit number given in bits 0 to 5 of the first byte followed by all 8 of the second.
        */
        private void BranchJump(bool condition)
        {
            MemByte addr1 = _memory.ReadByte(_pc);
            bool branchOnTrue = ((addr1.Value & 0b10000000) == 0b10000000);
            bool addrOn1Byte = ((addr1.Value & 0b01000000) == 0b01000000);


            MemWord targetOffset;
            if (addrOn1Byte)
            {
                targetOffset = new MemWord(addr1.Value & 0b00111111);
                _pc++;
            }
            else
            {
                targetOffset = new MemWord( ((addr1.Value & 0b00111111) << 8) + _memory.ReadByte(_pc + 1).Value );
                _pc += 2;
            }

            if ((condition && branchOnTrue) || (!condition && !branchOnTrue))
            {
                //jump
                if (targetOffset.SignedValue == 0x00)
                {
                    //return false from current routine
                    throw new NotImplementedException();
                }
                else if (targetOffset.SignedValue == 0x01)
                {
                    //return false from current routine
                    throw new NotImplementedException();
                }
                else
                    _pc += targetOffset.SignedValue - 2;
                //_pc += targetOffset.SignedValue + 1;
            }

        }

        private void Abort(string msg)
        {
            _logger.Error(msg);
            Environment.Exit(1);
        }
        private MemWord ReadVariable(GameVariableId varId)
        {
            _logger.Debug($"\t ReadVariable[{varId}]");
            if (varId == 0x00)
            {
                return _stack.Pop();
            }
            else if (varId <= 0x0f)
            {
                //local variable
                return _stack.ReadLocalVariable(varId);
            }
            else
            {
                //global variable
                MemWord varAddr = (MemWord) (_memory.GlobalVarTableLoc + 2 * (varId - 0x10));

                return _memory.ReadWord(varAddr);
            }
        }


        private void WriteVariable(GameVariableId varId, MemWord value)
        {
            _logger.Debug($"\t WriteVariable[{varId}]");

            if (varId == 0x00)
            {
                _stack.Push(value);
            }
            else if (varId <= 0x0f)
            {
                //local variable
                _stack.WriteLocalVariable(varId, value);
            }
            else
            {
                //global variable
                MemWord varAddr = _memory.GlobalVarTableLoc + 2 * (varId - 0x10);

                _memory.WriteWord(varAddr, value);
            }
        }



        private static OperandType ParseOperandType(byte opTypeBits)
        {
            switch (opTypeBits)
            {
                case 0b00: return OperandType.LargeConstant;
                case 0b01: return OperandType.SmallConstant;
                case 0b10: return OperandType.Variable;
                case 0b11: return OperandType.Omitted;

                default:
                    Debug.Assert(false, "Should never be reached");
                    return OperandType.Omitted; //whatever
            }
        }


        private string FormatOperands(int nOps, MemValue[] operands)
        {
            StringBuilder sb = new();

            for (int i = 0; i < nOps; i++)
            {
                sb.Append($"{operands[i]}");
                sb.Append(' ');
            }

            return sb.ToString();
        }
    }
}