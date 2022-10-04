using System;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Text;

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
        private Random _rndGen;

        public ZProcessor(ZMemory memory)
        {
            _memory = memory;
            _pc = memory.StartPC;
            _screen = new ZScreen();
            _stack = new ZStack();
            _rndGen = new Random();
        }

        public void Run()
        {
            byte[] memData = _memory.Data;

            //while (true)
            for (int iInstr = 0; iInstr < 1000; iInstr++)
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

                MemValue[] operands = ReadOperands(nOps, operandTypes);
                if (!UsesIndirectRefs(isVAR, nOps, opcode))
                    DereferenceVariables(operandTypes, ref operands);

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
                        operands[iOp] = _memory.ReadByte(_pc);
                        _pc++;
                        break;
                    default:
                        Debug.Assert(false, "Should never be reached");
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

                            case 0x01:
                                OpcodeRFalse();
                                break;

                            case 0x02:
                                OpcodePrint();
                                break;

                            case 0x09:
                                OpcodePop();
                                break;

                            case 0x0B:
                                OpcodeNewLine();
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
                            case 0x01:
                                OpcodeGetSibling(operands);
                                break;

                            case 0x02:
                                OpcodeGetChild(operands);
                                break;

                            case 0x03:
                                OpcodeGetParent(operands);
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
                                throw new ArgumentException($"Invalid opcode (for ZVersion == 3): {opCodeStr}");

                            case 0x0A:
                                OpcodePrintObj(operands);
                                break;

                            case 0x0C:
                                OpcodeJump(operands);
                                break;

                            case 0x0D:
                                OpcodePrintPAddr(operands);
                                break;

                            case 0x0F:
                                OpcodeNot(operands);
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

                            case 0x03:
                                OpcodeJG(operands);
                                break;

                            case 0x05:
                                OpcodeIncChk(operands);
                                break;

                            case 0x06:
                                OpcodeJIn(operands);
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

                            case 0x11:
                                OpcodeGetProp(operands);
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
            _logger.Debug($"CALL {FormatOperands(nOps, operands)}");

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

            _stack.PushRoutine(newRoutine);
        }


        private void OpcodePrintChar(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PRINT_CHAR {FormatOperands(nOps, operands)}");

            byte zchar = (byte)operands[0].FullValue;
            _screen.Print($"{Zscii.Zscii2Utf(zchar)}");
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
            int nMaxChars = operands[0].FullValue - 1;

            //_memory.WriteByte(new MemWord(arr + index), value);
            //TODO: lexical analysis
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
            _stack.Pop();
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


        //-------------------------------------------------
        //-------------------------------------------------

        /** Branching logic, as per spec 4.7
             The branch information is stored in one or two bytes, indicating what to do with the result of the test.
            If bit 7 of the first byte is 0, a branch occurs when the condition was false; 
            if 1, then branch is on true.If bit 6 is set, then the branch occupies 1 byte only, 
                and the "offset" is in the range 0 to 63, given in the bottom 6 bits.If bit 6 is clear, 
            then the offset is a signed 14 - bit number given in bits 0 to 5 of the first byte followed by all 8 of the second.
        */
        private void Branch(bool condition)
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
                targetOffset = new MemWord(((addr1.Value & 0b00111111) << 8) + _memory.ReadByte(_pc + 1).Value);
                _pc += 2;
            }

            if ((condition && branchOnTrue) || (!condition && !branchOnTrue))
            {
                //jump

                if (targetOffset.SignedValue == 0x00)
                {
                    ReturnRoutine(MemWord.FromBool(false));
                }
                else if (targetOffset.SignedValue == 0x01)
                {
                    ReturnRoutine(MemWord.FromBool(true));
                }
                else
                {
                    _pc += targetOffset.SignedValue - 2;
                }
            }

        }

        private void ReturnRoutine(MemWord value)
        {
            RoutineData currRoutine = _stack.PopRoutine();
            WriteVariable(currRoutine.ReturnVariableId, value);
            
            _pc = currRoutine.ReturnAddress;
        }


        private MemWord ReadVariable(GameVariableId varId)
        {
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
                MemWord varAddr = _memory.GlobalVarTableLoc + 2 * (varId - 0x10);

                return _memory.ReadWord(varAddr);
            }
        }

        private void WriteVariable(GameVariableId varId, ushort value)
        {
            WriteVariable(varId, new MemWord(value));
        }

        private void WriteVariable(GameVariableId varId, MemWord value)
        {
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