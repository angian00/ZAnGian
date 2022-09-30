using System;
using System.Diagnostics;
using System.Net;
using System.Reflection.Emit;
using System.Reflection.Metadata;

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



        private ZMemory Memory;
        private MemWord _pc; //Program Counter
        private ZScreen Screen;


        public ZProcessor(ZMemory memory)
        {
            this.Memory = memory;
            _pc = memory.StartPC;
            this.Screen = new ZScreen();
        }

        public void Run()
        {
            byte[] memData = Memory.Data;

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
                RunOpCode(isVAR, opcode, nOps, operands);
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
                        operands[iOp] = Memory.ReadWord(_pc);
                        _pc += 2;
                        break;
                    case OperandType.SmallConstant:
                        operands[iOp] = Memory.ReadByte(_pc);
                        _pc++;
                        break;
                    case OperandType.Variable:
                        byte varId = Memory.ReadByte(_pc);
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


        private void RunOpCode(bool isVAR, byte opcode, byte nOps, uint[] operands)
        {
            string opCodeStr = (isVAR ? "VAR" : $"{nOps}OP") + $":{opcode:X}";
            _logger.Debug($"RunOpCode [{opCodeStr}]");

            MemWord targetAddr;
            byte storeVar;

            if (isVAR)
            {
                //TODO: VAR opcodes
                throw new NotImplementedException($"Unimplemented opcode: {opCodeStr}");

            }
            else
            {
                switch (nOps)
                {
                    case 0:
                        switch (opcode)
                        {
                            case 0x02:
                                OpcodePrint();
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
                            case 0x08:
                                throw new ArgumentException($"Invalid opcode (for ZVersion == 3): {opCodeStr}");

                            case 0x0C:
                                targetAddr = Memory.ReadWord(_pc);
                                _pc += 2;
                                OpcodeJump(targetAddr);
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
                                targetAddr = Memory.ReadWord(_pc);
                                _pc += 2;
                                OpcodeJE(operands[0], operands[1], targetAddr);
                                break;

                            case 0x02:
                                targetAddr = Memory.ReadWord(_pc);
                                _pc += 2;
                                OpcodeJL(operands[0], operands[1], targetAddr);
                                break;


                            case 0x0E:
                                OpcodeInsertObj((byte)operands[0], (byte)operands[1]);
                                break;


                            case 0x18:
                                storeVar = Memory.ReadByte(_pc);
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
        //--- 2OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodeInsertObj(byte objId, byte dest)
        {
            //TODO
        }

        private void OpcodeJE(uint a, uint b, MemWord targetAddr)
        {
            if (a == b)
                _pc = targetAddr;
        }

        private void OpcodeJL(uint a, uint b, MemWord targetAddr)
        {
            if (toInt16(a) < toInt16(b))
                _pc = targetAddr;
        }

        private void OpcodeMod(uint a, uint b, byte varId)
        {
            if (b == 0)
            {
                Panic("Division by zero");
            }
            else
            {
                WriteVariable(varId, fromInt16((Int16)(toInt16(a) % toInt16(b))));
            }
        }

        //-------------------------------------------------
        //--- 1OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodeJump(uint targetOffset)
        {
            _pc += (MemWord) (toInt16(targetOffset) - 2);
        }

        //-------------------------------------------------
        //--- 0OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodePrint()
        {
            ushort nBytesRead;

            string msg = Zscii.DecodeText(Memory.Data, _pc, out nBytesRead);
            _pc += nBytesRead;

            Screen.print(msg);
        }

        //-------------------------------------------------
        //-------------------------------------------------

        private void Panic(string msg)
        {
            _logger.Error(msg);
            Environment.Exit(1);
        }

        private MemWord ReadVariable(byte varId)
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
                MemWord varAddr = (MemWord) (Memory.GlobalVarTableLoc + 2 * (varId - 0x10));

                return Memory.ReadWord(varAddr);
            }
        }


        private void WriteVariable(byte varId, MemWord value)
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
                MemWord varAddr = (MemWord)(Memory.GlobalVarTableLoc + 2 * (varId - 0x10));

                Memory.WriteWord(varAddr, value);
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

        // interpret a word 16-bit signed int, as per spec 2.2
        private static Int16 toInt16(uint val)
        {
            if (val > Int16.MaxValue)
                return (Int16)(0x10000 - val);

            return (Int16)val;
        }

        private static MemWord fromInt16(Int16 val)
        {
            if (val < 0)
                return (MemWord)(0x10000 - val);

            return (MemWord)val;
        }

    }
}