using System;
using System.Diagnostics;

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

            MemValue packedAddr = operands[0];

            MemValue[] args = new MemValue[nOps-1];
            for (byte i = 1; i < nOps; i++)
            {
                args[i-1] = operands[i];
            }

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            CallRoutine(packedAddr, args, storeVar);
        }


        private void OpcodeInputStream(int nOps, MemValue[] operands)
        {
            throw new NotImplementedException($"Unimplemented opcode: INPUT_STREAM"); //TODO
        }

        private void OpcodeOutputStream(int nOps, MemValue[] operands)
        {
            throw new NotImplementedException($"Unimplemented opcode: OUTPUT_STREAM"); //TODO
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

        private void OpcodePull(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PULL {FormatOperands(nOps, operands)}");

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            MemWord value = _stack.PopValue();
            WriteVariable(storeVar, value);
        }

        private void OpcodePush(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PUSH {FormatOperands(nOps, operands)}");
            MemWord value = new MemWord(operands[0].FullValue);

            _stack.PushValue(value);
        }


        private void OpcodePutProp(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PUT_PROP {FormatOperands(nOps, operands)}");

            GameObjectId objId = operands[0];
            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            ushort propId = operands[1].FullValue;

            MemValue propValue = operands[2];

            obj.PutPropertyValue(propId, propValue);
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

        private void OpcodeSetWindow(int nOps, MemValue[] operands)
        {
            throw new NotImplementedException($"Unimplemented opcode: SET_WINDOW"); //TODO
        }

        private void OpcodeSplitWindow(int nOps, MemValue[] operands)
        {
            throw new NotImplementedException($"Unimplemented opcode: SPLIT_WINDOW"); //TODO
        }

        private void OpcodeSRead(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SREAD {operands[0]} {operands[1]}");

            _screen.PrintStatusLine(GetStatusInfo());
            MemWord textBufferAddr = new MemWord(operands[0].FullValue);
            MemWord parseBufferAddr = new MemWord(operands[1].FullValue);

            string inputStr = _input.ReadLine();
            //_logger.All($"input string: {inputStr}");
            _parser.ParseInput(inputStr, textBufferAddr, parseBufferAddr);
        }


        private void OpcodeStoreB(int nOps, MemValue[] operands)
        {
            ushort arr = operands[0].FullValue;
            ushort index = operands[1].FullValue;
            MemByte value = new MemByte(operands[2].FullValue);
            _logger.Debug($"STOREB {operands[0]} {operands[1]} {value}");

            _memory.WriteByte(new MemWord(arr + index), value);
        }

        private void OpcodeStoreW(int nOps, MemValue[] operands)
        {
            ushort arr = operands[0].FullValue;
            ushort index = operands[1].FullValue;
            MemWord value = new MemWord(operands[2].FullValue);
            _logger.Debug($"STOREW {operands[0]} {operands[1]} {value}");

            _memory.WriteWord(new MemWord(arr + 2 * index), value);
        }

        //-------------------------------------------------
        //--- 0OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodeNewLine()
        {
            _logger.Debug("NEW_LINE");
            _screen.Print("\n");
        }

        private void OpcodeNop()
        {
            _logger.Debug("NOP");
            //does nothing, as expected
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

        private void OpcodePrintRet()
        {
            _logger.Debug("PRINT_RET");

            ushort nBytesRead;
            string msg = Zscii.DecodeText(_memory.Data, _pc, out nBytesRead);
            _pc += nBytesRead;

            _screen.Print(msg);
            _screen.Print("\n");
            ReturnRoutine(MemWord.FromBool(true));
        }

        private void OpcodeQuit()
        {
            _logger.Debug("QUIT");

            Environment.Exit(0);
        }

        private void OpcodeRestart()
        {
            _logger.Debug("RESTART");

            _interpreter.RestartGame();
        }

        private void OpcodeRestore()
        {
            _logger.Debug("RESTORE");

            string filepath = _input.GetFilePath(true);

            bool resOk = true;
            if (filepath != null)
            {
                GameSave savedGame = new GameSave();
                resOk = savedGame.LoadFile(filepath);
                if (resOk)
                {
                    ZMemory initialMemState = _interpreter.GetInitialMemoryState();
                    resOk = savedGame.Restore(ref _memory, ref _stack, ref _pc, initialMemState);

                    //NB: parser needs to refresh its copy of memory too
                    _parser = new ZParser(_memory);

                    //DEBUG
                    _logger.Debug("After restore");
                    _logger.Debug($"pc={_pc}");
                    //_stack.Dump();
                    //_memory.DumpDynamicMem();
                    //
                }
            }

            // NB: as per spec, "the branch is never actually made,
            // since either the game has successfully picked up again from where it was saved,
            // or it failed to load the save game file."
            //Branch(resOk);
        }

        private void OpcodeRetPopped()
        {
            _logger.Debug("RET_POPPED");
            ReturnRoutine(_stack.PopValue());
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

        private void OpcodeSave()
        {
            _logger.Debug("SAVE");

            string filepath = _input.GetFilePath(false);

            if (filepath == null)
                return;

            //MemWord targetOffset;
            //bool branchOnTrue;
            //ComputeBranchOffset(out targetOffset, out branchOnTrue);
            Branch(true); //FIXME

            _logger.Debug("Before save");
            _logger.Debug($"pc={_pc}");
            //_stack.Dump();
            //_memory.DumpDynamicMem();

            ZMemory initialMemState = _interpreter.GetInitialMemoryState();
            GameSave gameSave = new GameSave(_memory, _stack, _pc, initialMemState);
            bool savedOk = gameSave.SaveFile(filepath);

            //BranchOnCondition(savedOk, targetOffset, branchOnTrue); //CHECK
        }

        private void OpcodeShowStatus()
        {
            _screen.PrintStatusLine(GetStatusInfo());
        }

        private void OpcodeVerify()
        {
            //TODO: implement OpcodeVerify?
        }


        //-------------------------------------------------
        //--- 1OP -----------------------------------------
        //-------------------------------------------------

        private void OpcodeDec(OperandType[] operandTypes, MemValue[] operands)
        {
            _logger.Debug($"DEC {operands[0]}");

            //Debug.Assert(operandTypes[0] == OperandType.Variable);
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
                pSize = GameObject.GetPropertyLength(_memory, propAddr);

            WriteVariable(storeVar, pSize.FullValue);
        }

        private void OpcodeGetSibling(MemValue[] operands)
        {
            _logger.Debug($"GET_SIBLING {operands[0]}");
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
                retValue = obj.SiblingId.FullValue;
            }

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            WriteVariable(storeVar, retValue);

            Branch(retValue != 0x00);
        }


        private void OpcodeInc(OperandType[] operandTypes, MemValue[] operands)
        {
            _logger.Debug($"INC {operands[0]}");

            //Debug.Assert(operandTypes[0] == OperandType.Variable);
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

            GameObjectId objId = operands[0];
            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            _screen.Print(obj.ShortName);
        }


        private void OpcodePrintPAddr(MemValue[] operands)
        {
            _logger.Debug($"PRINT_PADDR {operands[0]}");

            MemWord targetAddr = UnpackAddress(operands[0]);
            string msg = Zscii.DecodeText(_memory.Data, targetAddr, out _);

            _screen.Print(msg);
        }

        private void OpcodeRemoveObj(MemValue[] operands)
        {
            _logger.Debug($"REMOVE_OBJ {operands[0]}");

            GameObjectId objId = operands[0];
            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            obj.DetachFromParent();
        }

        private void OpcodeRet(MemValue[] operands)
        {
            _logger.Debug($"RET {operands[0]}");

            ReturnRoutine(operands[0].FullValue);
        }

        //-------------------------------------------------
        //--- 2OP -----------------------------------------
        //-------------------------------------------------

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


        private void CallRoutine(MemValue packedAddr, MemValue[] args, MemByte storeVar)
        {
            MemWord routineAddr = UnpackAddress(packedAddr);

            RoutineData newRoutine = new RoutineData();

            if (storeVar != null)
                newRoutine.ReturnVariableId = storeVar.Value;
            else
                newRoutine.IgnoreReturnVariable = true;

            //jump to routine instructions
            _pc = routineAddr;

            byte nLocalVars = _memory.ReadByte(_pc).Value;
            _pc++;

            for (byte i = 0; i < nLocalVars; i++)
            {
                MemWord var;

                if (_memory.ZVersion < 5)
                {
                    var = _memory.ReadWord(_pc);
                    _pc += 2;
                }
                else
                    var = new MemWord(0x00);

                newRoutine.AddLocalVariable(var);
            }

            for (int i=0; i< args.Length; i++)
                newRoutine.SetLocalVariable(0, new MemWord(args[i].FullValue));

            _stack.PushRoutine(newRoutine);
            //TODO manage case (routineAddr == 0x00) as per opcode spec 
        }


        private void OpcodeClearAttr(MemValue[] operands)
        {
            _logger.Debug($"CLEAR_ATTR {operands[0]} {operands[1]}");

            GameObjectId objId = operands[0];
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
            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            ushort propId = operands[1].FullValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            if (propId == 0x00)
            {
                //FIXME
            }

            MemWord? value = obj.GetNextPropertyId(propId);
            if (value == null)
                value = new MemWord(0x00);

            WriteVariable(storeVar, value);
        }

        private void OpcodeGetProp(MemValue[] operands)
        {
            _logger.Debug($"GET_PROP {operands[0]} {operands[1]}");

            GameObjectId objId = operands[0];
            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            ushort propId = operands[1].FullValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            MemValue? pValue = obj.GetPropertyValue(propId);
            if (pValue == null)
                pValue = _memory.GetDefaultPropertyValue(propId);

            WriteVariable(storeVar, new MemWord(pValue.FullValue));
        }

        private void OpcodeGetPropAddr(MemValue[] operands)
        {
            _logger.Debug($"GET_PROP_ADDR {operands[0]} {operands[1]}");

            GameObjectId objId = operands[0];
            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            ushort propId = operands[1].FullValue;

            GameVariableId storeVar = _memory.ReadByte(_pc).Value;
            _pc++;

            MemWord? pAddr = obj.GetPropertyAddress(propId);
            if (pAddr == null)
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
            GameObject? obj = _memory.FindObject(objId);
            Debug.Assert(obj != null);

            ushort iAttr = operands[1].FullValue;

            Branch(obj.HasAttribute(iAttr));
        }

        private void OpcodeThrow(MemValue[] operands)
        {
            _logger.Debug($"THROW {operands[0]} {operands[1]}");
            //TODO: throw
        }


    }
}