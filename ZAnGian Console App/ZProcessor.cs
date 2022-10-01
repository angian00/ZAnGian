using System;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Text;

namespace ZAnGian
{
    public class ZProcessor
    {
        private static Logger _logger = Logger.GetInstance();


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
                        if (operands[0] is MemWord packedAddr)
                        {
                            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
                            _pc++;
                            OpcodeCall(nOps, operands);
                        }
                        else
                        {
                            Debug.Assert(false, "Inconsistent operand type");
                        }
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


                            case 0x0E:
                                OpcodeInsertObj(operands);
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
            _logger.All($"call {FormatOperands(nOps, operands)}");

            if (!(operands[0] is MemWord))
                Debug.Assert(false, "Inconsistent operand type");


            MemWord packedAddr = (MemWord)operands[0];
            MemWord routineAddr = packedAddr * 2;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;


            for (int i = 0; i < nOps - 1; i++)
            {
                //TODO: push argument operands[i+1]
            }

            //TODO: store return address somewhere

            //jump to routine
            _pc = routineAddr;

            byte nLocalVars = _memory.ReadByte(_pc).Value;
            _pc++;
            MemWord _localVarsAddr = _pc;

            _pc += (2 * nLocalVars); //skip local vars
        }


        //-------------------------------------------------
        //--- 0OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodePop()
        {
            _logger.All("pop");
            _stack.Pop();
        }

        private void OpcodePrint()
        {
            _logger.All("print");

            ushort nBytesRead;
            string msg = Zscii.DecodeText(_memory.Data, _pc, out nBytesRead);
            _pc += nBytesRead;

            _screen.Print(msg);
        }

        private void OpcodeRTrue()
        {
            _logger.All("rtrue");
            MemWord returnAddress = (MemWord)_stack.Pop();
        }


        //-------------------------------------------------
        //--- 1OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodeJump(MemValue[] operands)
        {
            _logger.All($"jump {operands[0]}");

            if (!(operands[0] is MemWord))
                Debug.Assert(false, "Inconsistent operand type");

            MemWord targetOffset = (MemWord)operands[0];


            _pc += targetOffset.SignedValue - 2;
        }


        private void OpcodePrintAddr(MemValue[] operands)
        {
            _logger.All($"print_addr {operands[0]}");

            if (!(operands[0] is MemWord))
                Debug.Assert(false, "Inconsistent operand type");
            

            MemWord targetAddr = (MemWord)operands[0];
            string msg = Zscii.DecodeText(_memory.Data, targetAddr, out _);

            _screen.Print(msg);
        }


        private void OpcodePrintPAddr(MemValue[] operands)
        {
            _logger.All($"print_paddr {operands[0]}");

            if (!(operands[0] is MemWord))
                Debug.Assert(false, "Inconsistent operand type");

            MemWord packedAddr = (MemWord)operands[0];
            
            MemWord targetAddr = packedAddr * 2;

            string msg = Zscii.DecodeText(_memory.Data, targetAddr, out _);
            _screen.Print(msg);
        }


        //-------------------------------------------------
        //--- 2OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodeIncChk(MemValue[] operands)
        {
            _logger.All($"inc_chk {operands[0]} {operands[1]}");
            
            if (!(operands[0] is MemByte) || !(operands[1] is MemWord))
                Debug.Assert(false, "Inconsistent operand types");

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
            _logger.All($"insert_obj {operands[0]} {operands[1]}");

            if (!(operands[0] is MemByte) || !(operands[1] is MemByte))
                Debug.Assert(false, "Inconsistent operand types");

            GameObjectId objId = ((MemByte)operands[0]).Value;
            GameObjectId destId = ((MemByte)operands[0]).Value;

            GameObject obj = _memory.FindObject(objId);

            obj.DetachFromParent();
            obj.AttachToParent(destId);
        }

        private void OpcodeJE(MemValue[] operands)
        {
            _logger.All($"je {operands[0]} {operands[1]}");

            if (!(operands[0] is MemWord) || !(operands[1] is MemWord))
                Debug.Assert(false, "Inconsistent operand types");

            MemWord a = (MemWord)operands[0];
            MemWord b = (MemWord)operands[1];


            MemWord targetAddr = _memory.ReadWord(_pc);
            _pc += 2;
            if (a.Value == b.Value)
                _pc = targetAddr;
        }

        private void OpcodeJIn(MemValue[] operands)
        {
            _logger.All($"jin {operands[0]} {operands[1]}");

            if (!(operands[0] is MemByte) || !(operands[1] is MemByte))
                Debug.Assert(false, "Inconsistent operand types");

            GameObjectId objId = ((MemByte)operands[0]).Value;
            GameObjectId parentId = ((MemByte)operands[1]).Value;


            MemWord targetAddr = _memory.ReadWord(_pc);
            _pc += 2;
            
            GameObject obj = _memory.FindObject(objId);

            if (obj != null && obj.ParentId == parentId)
                _pc = targetAddr;
        }

        private void OpcodeJL(MemValue[] operands)
        {
            _logger.All($"jl {operands[0]} {operands[1]}");

            if (!(operands[0] is MemWord) || !(operands[1] is MemWord))
                Debug.Assert(false, "Inconsistent operand types");

            MemWord a = (MemWord)operands[0];
            MemWord b = (MemWord)operands[1];


            MemWord targetAddr = _memory.ReadWord(_pc);
            _pc += 2;
            if (a.SignedValue < b.SignedValue)
                _pc = targetAddr;
        }

        private void OpcodeMod(MemValue[] operands)
        {
            _logger.All($"mod {operands[0]} {operands[1]}");

            if (!(operands[0] is MemWord) || !(operands[1] is MemWord))
                Debug.Assert(false, "Inconsistent operand types");

            MemWord a = (MemWord)operands[0];
            MemWord b = (MemWord)operands[1];


            GameVariableId varId = _memory.ReadByte(_pc).Value;
            _pc++;

            if (b.SignedValue == 0)
            {
                Abort("Division by zero");
            }
            else
            {
                WriteVariable(varId, MemWord.fromSignedValue(a.SignedValue % b.SignedValue));
            }
        }

        //-------------------------------------------------
        //-------------------------------------------------

        private void Abort(string msg)
        {
            _logger.Error(msg);
            Environment.Exit(1);
        }

        private MemWord ReadVariable(GameVariableId varId)
        {
            _logger.Debug($"\t ReadVariable[{varId}]");

            if (varId <= 0x0f)
            {
                //local variable
                throw new NotImplementedException();
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

            if (varId <= 0x0f)
            {
                //local variable
                throw new NotImplementedException();
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