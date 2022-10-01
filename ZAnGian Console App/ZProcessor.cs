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
                _logger.Debug($"sp={_pc} [0x{_pc:x}]: {StringUtils.ByteToBinaryString(memData[_pc])}");

                byte formBits = (byte)(memData[_pc] >> 6);
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
                        opcode = (byte)(memData[_pc] & 0b00001111);

                        opTypeBits = (byte)((memData[_pc] & 0b00110000) >> 4);
                        operandTypes[0] = ParseOperandType(opTypeBits);
                        if (operandTypes[0] == OperandType.Omitted)
                            nOps = 0;
                        else
                            nOps = 1;

                        _pc++;
                        break;

                    case OpCodeForm.Long:
                        opcode = (byte)(memData[_pc] & 0b00011111);
                        nOps = 2;

                        if ((memData[_pc] & 0b01000000) == 0b01000000)
                            operandTypes[0] = OperandType.Variable;
                        else
                            operandTypes[0] = OperandType.SmallConstant;

                        if ((memData[_pc] & 0b00100000) == 0b00100000)
                            operandTypes[1] = OperandType.Variable;
                        else
                            operandTypes[1] = OperandType.SmallConstant;

                        _pc++;
                        break;

                    case OpCodeForm.Variable:
                        opcode = (byte)(memData[_pc] & 0b00011111);
                        operandTypes[0] = ParseOperandType((byte)((memData[_pc + 1] & 0b11000000) >> 6));
                        operandTypes[1] = ParseOperandType((byte)((memData[_pc + 1] & 0b00110000) >> 4));
                        operandTypes[2] = ParseOperandType((byte)((memData[_pc + 1] & 0b00001100) >> 2));
                        operandTypes[3] = ParseOperandType((byte)(memData[_pc + 1] & 0b00000011));

                        if ((memData[_pc] & 0b00100000) == 0x00)
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

                uint[] operands = ReadOperands(nOps, operandTypes);
                RunOpCode(isVAR, opcode, nOps, operandTypes, operands);
            }
        }


        private uint[] ReadOperands(byte nOps, OperandType[] operandTypes)
        {
            uint[] operands = new uint[4];


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
                        GameVariableId varId = _memory.ReadByte(_pc);
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


        private void RunOpCode(bool isVAR, byte opcode, byte nOps, OperandType[] operandTypes, uint[] operands)
        {
            string opCodeStr = (isVAR ? "VAR" : $"{nOps}OP") + $":0x{opcode:X}";
            _logger.Debug($"RunOpCode [{opCodeStr}]");

            MemWord targetAddr;
            byte storeVar;

            if (isVAR)
            {
                switch (opcode)
                {
                    case 0x00:
                        storeVar = (GameVariableId)_memory.ReadByte(_pc);
                        _pc++;
                        OpcodeCall(nOps, operandTypes, operands, storeVar);
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
                                OpcodePrintAddr((MemWord)operands[0]);
                                break;

                            case 0x08:
                                throw new ArgumentException($"Invalid opcode (for ZVersion == 3): {opCodeStr}");

                            case 0x0C:
                                OpcodeJump(operands[0]);
                                break;

                            case 0x0D:
                                OpcodePrintPAddr((MemWord)operands[0]);
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
                                targetAddr = _memory.ReadWord(_pc);
                                _pc += 2;
                                OpcodeJE(operands[0], operands[1], targetAddr);
                                break;

                            case 0x02:
                                targetAddr = _memory.ReadWord(_pc);
                                _pc += 2;
                                OpcodeJL(operands[0], operands[1], targetAddr);
                                break;

                            case 0x05:
                                targetAddr = _memory.ReadWord(_pc);
                                _pc += 2;
                                OpcodeIncChk((GameVariableId)operands[0], operands[1], targetAddr);
                                break;

                            case 0x06:
                                targetAddr = _memory.ReadWord(_pc);
                                _pc += 2;
                                OpcodeJIn((GameObjectId)operands[0], (GameObjectId)operands[1], targetAddr);
                                break;


                            case 0x0E:
                                OpcodeInsertObj((GameObjectId)operands[0], (GameObjectId)operands[1]);
                                break;


                            case 0x18:
                                storeVar = (GameVariableId)_memory.ReadByte(_pc);
                                _pc ++;
                                OpcodeMod(operands[0], operands[1], storeVar);
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

        private void OpcodeCall(int nOps, OperandType[] operandTypes, uint[] operands, GameVariableId varId)
        {
            _logger.All($"call {FormatOperands(nOps, operandTypes, operands)}");

            MemWord routineAddr = (MemWord)(operands[0] * 2); //from packed address to byte address

            for (int i = 0; i < nOps - 1; i++)
            {
                //TODO: push argument operands[i+1]
            }

            //TODO: store return address somewhere

            //jump to routine
            _pc = routineAddr;

            byte nLocalVars = _memory.ReadByte(_pc);
            _pc++;
            MemWord _localVarsAddr = _pc;

            _pc += (MemWord) (2 * nLocalVars); //skip local vars


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
            ushort nBytesRead;

            _logger.All("print");
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

        private void OpcodeJump(uint targetOffset)
        {
            _logger.All($"jump 0x{targetOffset:x}");
            _pc += (MemWord) (NumberUtils.toInt16(targetOffset) - 2);
            //_pc += (MemWord)(NumberUtils.toInt16(targetOffset)); //DEBUG
        }

        private void OpcodePrintAddr(MemWord targetAddr)
        {
            _logger.All("print_addr");
            string msg = Zscii.DecodeText(_memory.Data, targetAddr, out _);

            _screen.Print(msg);
        }

        private void OpcodePrintPAddr(MemWord packedAddr)
        {
            _logger.All("print_paddr");

            MemWord targetAddr = (MemWord)(2*packedAddr);

            string msg = Zscii.DecodeText(_memory.Data, targetAddr, out _);
            _screen.Print(msg);
        }


        //-------------------------------------------------
        //--- 2OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodeIncChk(GameVariableId varId, uint cmpValue, MemWord targetAddr)
        {
            ushort varValue = ReadVariable(varId);
            varValue++;
            WriteVariable(varId, varValue);

            if (NumberUtils.toInt16(varValue) > NumberUtils.toInt16(cmpValue))
                _pc = targetAddr;
        }


        private void OpcodeInsertObj(GameObjectId objId, GameObjectId destId)
        {
            GameObject obj = _memory.FindObject(objId);

            obj.DetachFromParent();
            obj.AttachToParent(destId);
        }

        private void OpcodeJE(uint a, uint b, MemWord targetAddr)
        {
            if (a == b)
                _pc = targetAddr;
        }

        private void OpcodeJIn(GameObjectId objId, GameObjectId parentId, MemWord targetAddr)
        {
            _logger.All($"jin {objId} {parentId} 0x{targetAddr:x}");
            GameObject obj = _memory.FindObject(objId);

            if (obj != null && obj.ParentId == parentId)
                _pc = targetAddr;
        }

        private void OpcodeJL(uint a, uint b, MemWord targetAddr)
        {
            if (NumberUtils.toInt16(a) < NumberUtils.toInt16(b))
                _pc = targetAddr;
        }

        private void OpcodeMod(uint a, uint b, GameVariableId varId)
        {
            if (b == 0)
            {
                Abort("Division by zero");
            }
            else
            {
                WriteVariable(varId, NumberUtils.fromInt16((Int16)(NumberUtils.toInt16(a) % NumberUtils.toInt16(b))));
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
                MemWord varAddr = (MemWord)(_memory.GlobalVarTableLoc + 2 * (varId - 0x10));

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


        private string FormatOperands(int nOps, OperandType[] operandTypes, uint[] operands)
        {
            StringBuilder sb = new();

            for (int i = 0; i < nOps; i++)
            {
                switch (operandTypes[i])
                {
                    case OperandType.SmallConstant:
                        sb.Append($"{(byte)operands[i]:x2}");
                        break;
                    case OperandType.LargeConstant:
                        sb.Append($"{(MemWord)operands[i]:x4}");
                        break;
                    case OperandType.Variable:
                        sb.Append("...");
                        break;
                    default:
                        sb.Append("--");
                        break;
                }

                sb.Append(' ');
            }

            return sb.ToString();
        }
    }
}