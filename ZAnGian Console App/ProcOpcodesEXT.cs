using System;
using System.Diagnostics;
using System.Numerics;
using System.Text;

namespace ZAnGian
{
    public partial class ZProcessor
    {
        private void OpcodeArtShift(int nOps, MemValue[] operands)
        {
            _logger.Debug($"ART_SHIFT {operands[0]} {operands[1]}");

            short val = operands[0].SignedValue;
            short places = operands[1].SignedValue;

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            WriteVariable(storeVar.Value, MemWord.FromSignedValue(NumberUtils.BitShift(val, places, true)));
        }


        private void OpcodeCheckUnicode(int nOps, MemValue[] operands)
        {
            _logger.Debug($"CHECK_UNICODE {FormatOperands(nOps, operands)}");

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            //we support all 2-byte unicode charCodes
            WriteVariable(storeVar.Value, new MemWord(0x01));
        }


        private void OpcodeLogShift(int nOps, MemValue[] operands)
        {
            _logger.Debug($"LOG_SHIFT {operands[0]} {operands[1]}");

            short val = operands[0].SignedValue;
            short places = operands[1].SignedValue;

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            WriteVariable(storeVar.Value, MemWord.FromSignedValue(NumberUtils.BitShift(val, places, false)));
        }


        private void OpcodePrintUnicode(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PRINT_UNICODE {FormatOperands(nOps, operands)}");

            ushort charCode = operands[0].FullValue;
            // CHECK unicode encoding
            byte[] rawUnicode = new byte[] { (byte)(charCode & 0xff), (byte)(charCode >> 8)};

            string unicodeStr = Encoding.Unicode.GetString(rawUnicode);
            _screen.Print(unicodeStr);
        }


        private void OpcodeRestoreExt(int nOps, MemValue[] operands)
        {
            _logger.Debug($"RESTORE {FormatOperands(nOps, operands)}");
            _logger.Warn($"defaulting to 0OP restore -->");

            //TODO: use extra restore arguments

            OpcodeRestore();
        }


        private void OpcodeRestoreUndo(int nOps, MemValue[] operands)
        {
            _logger.Debug($"RESTORE_UNDO {FormatOperands(nOps, operands)}");

            _logger.Warn($"TODO: implement RESTORE_UNDO");

        }


        private void OpcodeSaveExt(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SAVE {FormatOperands(nOps, operands)}");
            _logger.Warn($"defaulting to 0OP save -->");
            OpcodeSave();
        }


        private void OpcodeSaveUndo(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SAVE_UNDO {FormatOperands(nOps, operands)}");

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            _logger.Warn($"TODO: implement SAVE_UNDO");

            WriteVariable(storeVar.Value, new MemWord(0xffff));
        }


        private void OpcodeSetFont(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SET_FONT {FormatOperands(nOps, operands)}");
            
            //byte fontId = (byte)operands[0].FullValue;
            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            _logger.Warn($"TODO: implement SET_FONT");

            WriteVariable(storeVar.Value, new MemWord(0x00));
        }


        private void OpcodeSetTrueColour(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SET_TRUE_COLOUR {FormatOperands(nOps, operands)}");

            _logger.Warn($"TODO: implement SET_TRUE_COLOUR");
        }
    }
}