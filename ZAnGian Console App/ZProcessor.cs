using System;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Text;

namespace ZAnGian
{
    public partial class ZProcessor
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


        private ZInterpreter _interpreter;
        private ZMemory _memory;
        private MemWord _pc; //Program Counter
        private ZStack _stack;
        private ZParser _parser;
        private Random _rndGen;
        private ZScreen _screen;
        private ZInput _input;


        public ZProcessor(ZInterpreter interpreter, ZMemory memory)
        {
            _interpreter = interpreter;
            _memory = memory;
            _pc = memory.StartPC;
            _stack = new ZStack();
            _parser = new ZParser(_memory);
            _rndGen = new Random();
            _screen = new ZScreen();
            _input = new ZInput(_screen);
        }

        public void Run()
        {
            byte[] memData = _memory.Data;

            while (true)
            //for (int iInstr = 0; iInstr < 10000; iInstr++)
            {
                _logger.All($"sp={_pc.Value} [{_pc}]: {StringUtils.ByteToBinaryString(_memory.ReadByte(_pc).Value)}");

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

                MemValue[] operands = ReadOperands(operandTypes);
                if (!UsesIndirectRefs(isVAR, nOps, opcode))
                    DereferenceVariables(operandTypes, ref operands);
                    
                RunOpCode(isVAR, opcode, nOps, operandTypes, operands);
            }
        }


        private MemValue[] ReadOperands(OperandType[] operandTypes)
        {
            MemValue[] operands = new MemValue[4];


            for (int iOp = 0; iOp < 4; iOp++)
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
                        operands[iOp] = _memory.ReadByte(_pc);
                        _pc++;
                        break;
                    default:
                        break;
                }
            }

            return operands;
        }

        private bool UsesIndirectRefs(bool isVAR, byte nOps, byte opcode)
        {
            // as per spec 6.3.4
            //pull
            if (isVAR && (opcode == 0x09))
                return true;

            //inc, dec, load
            if ((!isVAR) && (nOps == 1) &&  (opcode == 0x05 || opcode == 0x06 || opcode == 0x0E))
                    return true;

            //inc_chk, dec_chk, store
            if ((!isVAR) && (nOps == 2) && (opcode == 0x04 || opcode == 0x05 || opcode == 0x0D))
                return true;

            return false;
        }

        private void DereferenceVariables(OperandType[] operandTypes, ref MemValue[] operands)
        {
            for (int i=0; i < 4; i++)
            {
                if (operandTypes[i] == OperandType.Variable)
                    operands[i] = ReadVariable((GameVariableId)operands[i].FullValue);
            }
        }

        private void RunOpCode(bool isVAR, byte opcode, byte nOps, OperandType[] operandTypes, MemValue[] operands)
        {
            string opCodeStr = (isVAR ? "VAR" : $"{nOps}OP") + $":0x{opcode:X}";
            _logger.All($"RunOpCode [{opCodeStr}]");

            if (isVAR)
            {
                switch (opcode)
                {
                    case 0x00:
                        OpcodeCall(nOps, operands);
                        break;

                    case 0x01:
                        OpcodeStoreW(nOps, operands);
                        break;

                    case 0x02:
                        OpcodeStoreB(nOps, operands);
                        break;

                    case 0x03:
                        OpcodePutProp(nOps, operands);
                        break;

                    case 0x04:
                        OpcodeSRead(nOps, operands);
                        break;

                    case 0x05:
                        OpcodePrintChar(nOps, operands);
                        break;

                    case 0x06:
                        OpcodePrintNum(nOps, operands);
                        break;

                    case 0x07:
                        OpcodeRandom(nOps, operands);
                        break;

                    case 0x08:
                        OpcodePush(nOps, operands);
                        break;

                    case 0x09:
                        OpcodePull(nOps, operands);
                        break;

                    case 0x0A:
                        OpcodeSplitWindow(nOps, operands);
                        throw new NotImplementedException($"Unimplemented opcode: {opCodeStr}");
                        //break;

                    case 0x0B:
                        OpcodeSetWindow(nOps, operands);
                        throw new NotImplementedException($"Unimplemented opcode: {opCodeStr}");
                        //break;

                    case 0x13:
                        OpcodeOutputStream(nOps, operands);
                        throw new NotImplementedException($"Unimplemented opcode: {opCodeStr}");
                        //break;

                    case 0x14:
                        OpcodeInputStream(nOps, operands);
                        throw new NotImplementedException($"Unimplemented opcode: {opCodeStr}");
                        //break;

                    default:
                        //throw new NotImplementedException($"Unimplemented opcode: {opCodeStr}");
                        throw new ArgumentException($"Invalid opcode (for ZVersion == 3): {opCodeStr}");
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

                            case 0x01:
                                OpcodeRFalse();
                                break;

                            case 0x02:
                                OpcodePrint();
                                break;

                            case 0x03:
                                OpcodePrintRet();
                                break;

                            case 0x04:
                                OpcodeNop();
                                break;

                            case 0x05:
                                OpcodeSave();
                                break;

                            case 0x06:
                                OpcodeRestore();
                                break;

                            case 0x07:
                                OpcodeRestart();
                                break;

                            case 0x08:
                                OpcodeRetPopped();
                                break;

                            case 0x09:
                                OpcodePop();
                                break;

                            case 0x0A:
                                OpcodeQuit();
                                break;

                            case 0x0B:
                                OpcodeNewLine();
                                break;

                            case 0x0C:
                                OpcodeShowStatus();
                                break;

                            case 0x0D:
                                OpcodeVerify();
                                break;

                            case 0x0E:
                            case 0x0F:
                                throw new ArgumentException($"Invalid opcode (for ZVersion == 3): {opCodeStr}");

                            default:
                                Debug.Assert(false, "Unreachable if all opcodes are accounted for");
                                break;
                        }

                        break;

                    case 1:
                        switch (opcode)
                        {
                            case 0x00:
                                OpcodeJZ(operands);
                                break;

                            case 0x01:
                                OpcodeGetSibling(operands);
                                break;

                            case 0x02:
                                OpcodeGetChild(operands);
                                break;

                            case 0x03:
                                OpcodeGetParent(operands);
                                break;

                            case 0x04:
                                OpcodeGetPropLen(operands);
                                break;

                            case 0x05:
                                OpcodeInc(operandTypes, operands);
                                break;
                            case 0x06:
                                OpcodeDec(operandTypes, operands);
                                break;

                            case 0x07:
                                OpcodePrintAddr(operands);
                                break;

                            case 0x08:
                                throw new ArgumentException($"Invalid opcode (for ZVersion == 3): {opCodeStr}");

                            case 0x09:
                                OpcodeRemoveObj(operands);
                                break;

                            case 0x0A:
                                OpcodePrintObj(operands);
                                break;

                            case 0x0B:
                                OpcodeRet(operands);
                                break;

                            case 0x0C:
                                OpcodeJump(operands);
                                break;

                            case 0x0D:
                                OpcodePrintPAddr(operands);
                                break;

                            case 0x0E:
                                OpcodeLoad(operandTypes, operands);
                                break;

                            case 0x0F:
                                OpcodeNot(operands);
                                break;

                            default:
                                Debug.Assert(false, "Unreachable if all opcodes are accounted for");
                                break;
                        }

                        break;

                    case 2:
                        switch (opcode)
                        {
                            case 0x00:
                                throw new ArgumentException($"Invalid opcode: {opCodeStr}");

                            case 0x01:
                                OpcodeJE(operandTypes, operands);
                                break;

                            case 0x02:
                                OpcodeJL(operands);
                                break;

                            case 0x03:
                                OpcodeJG(operands);
                                break;

                            case 0x04:
                                OpcodeDecChk(operandTypes, operands);
                                break;

                            case 0x05:
                                OpcodeIncChk(operandTypes, operands);
                                break;

                            case 0x06:
                                OpcodeJIn(operands);
                                break;

                            case 0x07:
                                OpcodeTest(operands);
                                break;

                            case 0x08:
                                OpcodeOr(operands);
                                break;

                            case 0x09:
                                OpcodeAnd(operands);
                                break;

                            case 0x0A:
                                OpcodeTestAttr(operands);
                                break;

                            case 0x0B:
                                OpcodeSetAttr(operands);
                                break;

                            case 0x0C:
                                OpcodeClearAttr(operands);
                                break;

                            case 0x0D:
                                OpcodeStore(operandTypes, operands);
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

                            case 0x11:
                                OpcodeGetProp(operands);
                                break;

                            case 0x12:
                                OpcodeGetPropAddr(operands);
                                break;

                            case 0x13:
                                OpcodeGetNextProp(operands);
                                break;

                            case 0x14:
                                OpcodeAdd(operands);
                                break;

                            case 0x15:
                                OpcodeSub(operands);
                                break;

                            case 0x16:
                                OpcodeMul(operands);
                                break;

                            case 0x17:
                                OpcodeDiv(operands);
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
                                Debug.Assert(false, "Unreachable if all opcodes are accounted for");
                                break;
                        }

                        break;

                    default:
                        Debug.Assert(false, "Unreachable if all opcodes are accounted for");
                        break;
                }
            }
        }
    }

}