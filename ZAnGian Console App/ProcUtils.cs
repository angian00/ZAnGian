using System.Diagnostics;
using System.Text;

namespace ZAnGian
{
    public record StatusInfo
    {
        public bool IsScoreGame;
        public string? CurrObjName;

        public short Score;
        public ushort Turns;

        public ushort Hours;
        public ushort Minutes;

    }

    public partial class ZProcessor
    {
        /** Branching logic, as per spec 4.7
             The branch information is stored in one or two bytes, indicating what to do with the result of the test.
            If bit 7 of the first byte is 0, a branch occurs when the condition was false; 
            if 1, then branch is on true.If bit 6 is set, then the branch occupies 1 byte only, 
                and the "offset" is in the range 0 to 63, given in the bottom 6 bits.If bit 6 is clear, 
            then the offset is a signed 14 - bit number given in bits 0 to 5 of the first byte followed by all 8 of the second.
        */
        private void Branch(bool condition)
        {
            short targetOffset;
            bool branchOnTrue;

            ComputeBranchOffset(out targetOffset, out branchOnTrue);
            BranchOnCondition(condition, targetOffset, branchOnTrue);
        }

        private void ComputeBranchOffset(out short targetOffset, out bool branchOnTrue)
        {
            MemByte addr1 = _memory.ReadByte(_pc);
            branchOnTrue = ((addr1.Value & 0b1000_0000) == 0b1000_0000);
            bool addrOn1Byte = ((addr1.Value & 0b0100_0000) == 0b0100_0000);

            if (addrOn1Byte)
            {
                targetOffset = (short)(addr1.Value & 0b0011_1111);
                _pc++;
            }
            else
            {
                targetOffset = NumberUtils.Signed14Bits(((addr1.Value & 0b0011_1111) << 8) + _memory.ReadByte(_pc + 1).Value);
                _pc += 2;
            }
        }

        private void BranchOnCondition(bool condition, short targetOffset, bool branchOnTrue)
        {
            if ((condition && branchOnTrue) || (!condition && !branchOnTrue))
            {
                //jump
                if (targetOffset == 0x00)
                {
                    ReturnRoutine(MemWord.FromBool(false));
                }
                else if (targetOffset == 0x01)
                {
                    ReturnRoutine(MemWord.FromBool(true));
                }
                else
                {
                    _pc += targetOffset - 2;
                }
            }
        }


        private void CallRoutine(MemValue packedAddr, MemValue[] args, MemByte storeVar)
        {
            MemWord routineAddr = UnpackAddress(packedAddr);

            RoutineData newRoutine = new RoutineData();
            newRoutine.ReturnAddress = _pc;


            if (storeVar != (MemByte)null)
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

            newRoutine.NumArgs = args.Length;
            for (byte i = 0; i < args.Length; i++)
                newRoutine.SetLocalVariable((byte)(i + 1), new MemWord(args[i].FullValue));

            _stack.PushRoutine(newRoutine);
            //TODO manage case (routineAddr == 0x00) as per opcode spec 
        }


        private void ReturnRoutine(ushort value)
        {
            ReturnRoutine(new MemWord(value));
        }

        private void ReturnRoutine(MemWord value)
        {
            RoutineData currRoutine = _stack.PopRoutine();
            if (!currRoutine.IgnoreReturnVariable)
                WriteVariable(currRoutine.ReturnVariableId, value);

            _pc = currRoutine.ReturnAddress;
        }


        private MemWord ReadVariable(GameVariableId varId)
        {
            if (varId == 0x00)
            {
                return _stack.PopValue();
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
                _stack.PushValue(value);
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

        private MemWord UnpackAddress(MemValue packedAddr)
        {
            if (_memory.ZVersion == 3)
                return new MemWord(packedAddr.FullValue * 2);
            else if (_memory.ZVersion == 5)
                return new MemWord(packedAddr.FullValue * 4);
            else
            {
                Debug.Assert(false, "Unreachable");
                return null;
            }
        }

        private StatusInfo GetStatusInfo()
        {
            // as per spec 8.2
            StatusInfo statusInfo = new StatusInfo();

            statusInfo.IsScoreGame = ((_memory.ZVersion < 3) || ((_memory.Flags1 & 0b00000010) != 0b00000010));

            GameObjectId currObjId = ReadVariable(0x10);

            if (currObjId.FullValue == 0x00)
            {
                statusInfo.CurrObjName = "--";
            }
            else
            {
                GameObject? currObj = _memory.FindObject(currObjId);
                Debug.Assert(currObj != null);

                statusInfo.CurrObjName = currObj.ShortName;
            }


            if (statusInfo.IsScoreGame)
            {
                statusInfo.Score = ReadVariable(0x11).SignedValue;
                statusInfo.Turns = ReadVariable(0x12).FullValue;
            }
            else
            {
                //time game
                statusInfo.Hours = ReadVariable(0x11).FullValue;
                statusInfo.Minutes = ReadVariable(0x12).FullValue;
            }

            return statusInfo;
        }
    }
}