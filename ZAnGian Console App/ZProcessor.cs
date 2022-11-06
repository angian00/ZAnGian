using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;

namespace ZAnGian
{
    public partial class ZProcessor
    {
        private static Logger _logger = Logger.GetInstance();

        private enum OpCodeForm
        {
            Short,
            Variable,
            Long,
            Extended
        }

        private enum OpCodeCategory
        {
            N_OPS_0,
            N_OPS_1,
            N_OPS_2,
            VAR,
            EXT,
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
                byte opcodeByte = _memory.ReadByte(_pc).Value;
                _logger.All($"pc={_pc.Value} [{_pc}]: {StringUtils.ByteToBinaryString(opcodeByte)}");
                _pc++;

                OpCodeForm form;

                if (opcodeByte == 0xbe)
                    form = OpCodeForm.Extended;
                else if ((opcodeByte >> 6) == 0b10)
                    form = OpCodeForm.Short;
                else if ((opcodeByte >> 6) == 0b11)
                    form = OpCodeForm.Variable;
                else
                    form = OpCodeForm.Long;


                byte opcode = 0x00; //nonexistent opcode as default
                byte opTypeBits;
                byte nOps = 0;
                OperandType[] operandTypes = new OperandType[4];
                OpCodeCategory opcodeCat = OpCodeCategory.EXT;

                switch (form)
                {
                    case OpCodeForm.Short:
                        opcode = (byte)(opcodeByte & 0b00001111);

                        opTypeBits = (byte)((opcodeByte & 0b00110000) >> 4);
                        operandTypes[0] = ParseOperandType(opTypeBits);
                        if (operandTypes[0] == OperandType.Omitted)
                        {
                            nOps = 0;
                            opcodeCat = OpCodeCategory.N_OPS_0;
                        }
                        else
                        {
                            nOps = 1;
                            opcodeCat = OpCodeCategory.N_OPS_1;
                        }

                        break;

                    case OpCodeForm.Long:
                        opcode = (byte)(opcodeByte & 0b00011111);
                        nOps = 2;
                        opcodeCat = OpCodeCategory.N_OPS_2;

                        if ((opcodeByte & 0b01000000) == 0b01000000)
                            operandTypes[0] = OperandType.Variable;
                        else
                            operandTypes[0] = OperandType.SmallConstant;

                        if ((opcodeByte & 0b00100000) == 0b00100000)
                            operandTypes[1] = OperandType.Variable;
                        else
                            operandTypes[1] = OperandType.SmallConstant;

                        break;

                    case OpCodeForm.Variable:
                        opcode = (byte)(opcodeByte & 0b00011111);

                        ReadOperandTypes(ref operandTypes);

                        if ((opcodeByte & 0b00100000) == 0x00)
                        {
                            nOps = 2;
                            opcodeCat = OpCodeCategory.N_OPS_2;
                        }
                        else
                        {
                            opcodeCat = OpCodeCategory.VAR;

                            if (opcode == 0x0C || opcode == 0x1A)
                            {
                                //call_vs2 and call_vn2 opcodes have an extra byte for additional operand types
                                OperandType[] operandTypes2 = new OperandType[4];
                                ReadOperandTypes(ref operandTypes2);

                                operandTypes = new List<OperandType>()
                                    .Concat(operandTypes)
                                    .Concat(operandTypes2)
                                    .ToArray();
                            }

                            nOps = ComputeNumOperands(operandTypes);

                        }

                        break;

                    case OpCodeForm.Extended:
                        opcode = _memory.ReadByte(_pc).Value;
                        _pc++;

                        opcodeCat = OpCodeCategory.EXT;

                        ReadOperandTypes(ref operandTypes);
                        nOps = ComputeNumOperands(operandTypes);
                        break;

                    default:
                        Debug.Assert(false, "Should never be reached");
                        break;
                }

                MemValue[] operands = ReadOperands(operandTypes);
                if (!UsesIndirectRefs((opcodeCat == OpCodeCategory.VAR), nOps, opcode))
                    DereferenceVariables(operandTypes, ref operands);
                    
                RunOpCode(opcodeCat, opcode, nOps, operandTypes, operands);
            }
        }


        private void ReadOperandTypes(ref OperandType[] operandTypes)
        {
            MemByte opTypesByte = _memory.ReadByte(_pc);
            _pc++;

            operandTypes[0] = ParseOperandType(((opTypesByte & 0b11000000) >> 6).Value);
            operandTypes[1] = ParseOperandType(((opTypesByte & 0b00110000) >> 4).Value);
            operandTypes[2] = ParseOperandType(((opTypesByte & 0b00001100) >> 2).Value);
            operandTypes[3] = ParseOperandType((opTypesByte & 0b00000011).Value);
        }

        private byte ComputeNumOperands(OperandType[] operandTypes)
        {
            byte nOps;

            //nOps determined by "Omitted" operand types
            nOps = (byte)operandTypes.Length;
            for (int iOp = 0; iOp < operandTypes.Length; iOp++)
            {
                if (operandTypes[operandTypes.Length - iOp - 1] == OperandType.Omitted)
                    nOps--;
                else
                    break;
            }

            return nOps;
        }

        private MemValue[] ReadOperands(OperandType[] operandTypes)
        {
            MemValue[] operands = new MemValue[operandTypes.Length];


            for (int iOp = 0; iOp < operandTypes.Length; iOp++)
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
            for (int i=0; i < operandTypes.Length; i++)
            {
                if (operandTypes[i] == OperandType.Variable)
                    operands[i] = ReadVariable((GameVariableId)operands[i].FullValue);
            }
        }

        private void RunOpCode(OpCodeCategory opcodeCat, byte opcode, byte nOps, OperandType[] operandTypes, MemValue[] operands)
        {
            string opCodeStr = $"{Enum.GetName(opcodeCat)} :0x{opcode:X}";
            _logger.All($"RunOpCode [{opCodeStr}]");

            switch (opcodeCat)
            {
                case OpCodeCategory.VAR:
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
                            if (_memory.ZVersion == 3)
                                OpcodeSRead(nOps, operands);
                            else if (_memory.ZVersion == 5)
                                OpcodeARead(nOps, operands);
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
                            break;

                        case 0x0B:
                            OpcodeSetWindow(nOps, operands);
                            break;

                        case 0x0C:
                            OpcodeCallVS2(nOps, operands);
                            break;

                        case 0x0D:
                            OpcodeEraseWindow(nOps, operands);
                            break;

                        case 0x0E:
                            OpcodeEraseLine(nOps, operands);
                            break;

                        case 0x0F:
                            OpcodeSetCursor(nOps, operands);
                            break;

                        case 0x10:
                            OpcodeGetCursor(nOps, operands);
                            break;

                        case 0x11:
                            OpcodeSetTextStyle(nOps, operands);
                            break;

                        case 0x12:
                            OpcodeBufferMode(nOps, operands);
                            break;

                        case 0x13:
                            OpcodeOutputStream(nOps, operands);
                            break;

                        case 0x14:
                            OpcodeInputStream(nOps, operands);
                            break;

                        case 0x15:
                            OpcodeSoundEffect(nOps, operands);
                            break;

                        case 0x16:
                            OpcodeReadChar(nOps, operands);
                            break;

                        case 0x17:
                            OpcodeScanTable(nOps, operands);
                            break;

                        case 0x18:
                            OpcodeNotV5(nOps, operands);
                            break;

                        case 0x19:
                            OpcodeCallVN(nOps, operands);
                            break;

                        case 0x1A:
                            OpcodeCallVN2(nOps, operands);
                            break;

                        case 0x1B:
                            OpcodeTokenise(nOps, operands);
                            break;

                        case 0x1C:
                            OpcodeEncodeText(nOps, operands);
                            break;

                        case 0x1D:
                            OpcodeCopyTable(nOps, operands);
                            break;

                        case 0x1E:
                            OpcodePrintTable(nOps, operands);
                            break;

                        case 0x1F:
                            OpcodeCheckArgCount(nOps, operands);
                            break;

                        default:
                            throw new ArgumentException($"Invalid opcode (for ZVersion == 3): {opCodeStr}");
                    }

                    break;

                case OpCodeCategory.N_OPS_0:
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
                            if (_memory.ZVersion < 5)
                                OpcodeSave();
                            else
                                throw new ArgumentException($"Invalid opcode for ZVersion=={_memory.ZVersion} : {opCodeStr}");
                            break;

                        case 0x06:
                            if (_memory.ZVersion < 5)
                                OpcodeRestore();
                            else
                                throw new ArgumentException($"Invalid opcode for ZVersion=={_memory.ZVersion} : {opCodeStr}");
                            break;

                        case 0x07:
                            OpcodeRestart();
                            break;

                        case 0x08:
                            OpcodeRetPopped();
                            break;

                        case 0x09:
                            if (_memory.ZVersion < 5)
                                OpcodePop();
                            else
                                OpcodeCatch();
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
                            Debug.Assert(false, "Unintercepted EXT opcode");
                            break;

                        case 0x0F:
                            OpcodePiracy();
                            break;

                        default:
                            Debug.Assert(false, "Unreachable");
                            break;
                    }

                    break;


                case OpCodeCategory.N_OPS_1:
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
                            OpcodeInc(operands);
                            break;

                        case 0x06:
                            OpcodeDec(operands);
                            break;

                        case 0x07:
                            OpcodePrintAddr(operands);
                            break;

                        case 0x08:
                            OpcodeCall1S(operands);
                            break;

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
                            if (_memory.ZVersion < 5)
                                OpcodeNotV3(operands);
                            else
                                OpcodeCall1N(operands);
                            break;

                        default:
                            Debug.Assert(false, "Unreachable");
                            break;
                    }

                    break;

                case OpCodeCategory.N_OPS_2:
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
                            OpcodeCall2S(operands);
                            break;

                        case 0x1A:
                            OpcodeCall2N(operands);
                            break;

                        case 0x1B:
                            OpcodeSetColour(operands);
                            break;

                        case 0x1C:
                            OpcodeThrow(operands);
                            break;

                        case 0x1D:
                        case 0x1E:
                        case 0x1F:
                            throw new ArgumentException($"Invalid opcode: {opCodeStr}");

                        default:
                            Debug.Assert(false, "Unreachable");
                            break;
                    }

                    break;

                case OpCodeCategory.EXT:
                    switch (opcode)
                    {
                        case 0x00:
                            OpcodeSaveExt(nOps, operands);
                            break;

                        case 0x01:
                            OpcodeRestoreExt(nOps, operands);
                            break;

                        case 0x02:
                            OpcodeLogShift(nOps, operands);
                            break;

                        case 0x03:
                            OpcodeArtShift(nOps, operands);
                            break;

                        case 0x04:
                            OpcodeSetFont(nOps, operands);
                            break;

                        case 0x05:
                        case 0x06:
                        case 0x07:
                        case 0x08:
                            throw new ArgumentException($"Invalid opcode (for ZVersion=={_memory.ZVersion}): {opcode}");

                        case 0x09:
                            OpcodeSaveUndo(nOps, operands);
                            break;

                        case 0x0A:
                            OpcodeRestoreUndo(nOps, operands);
                            break;

                        case 0x0B:
                            OpcodePrintUnicode(nOps, operands);
                            break;

                        case 0x0C:
                            OpcodeCheckUnicode(nOps, operands);
                            break;

                        case 0x0D:
                            OpcodeSetTrueColour(nOps, operands);
                            break;

                        case 0x0E:
                        case 0x0F:
                            throw new ArgumentException($"Invalid opcode: {opCodeStr}");


                        default:
                            throw new ArgumentException($"Invalid opcode (for ZVersion == {_memory.ZVersion}): {opCodeStr}");
                    }

                    break;

                default:
                    Debug.Assert(false, "Unreachable");
                    break;
            }
        }
    }

}