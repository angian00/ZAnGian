using System;
using System.Diagnostics;
using System.Text;

namespace ZAnGian
{
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

        private void ReturnRoutine(ushort value)
        {
            ReturnRoutine(new MemWord(value));
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
    }
}