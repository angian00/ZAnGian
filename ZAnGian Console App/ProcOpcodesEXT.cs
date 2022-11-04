using System;
using System.Diagnostics;
using System.Numerics;

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


        private void OpcodeLogShift(int nOps, MemValue[] operands)
        {
            _logger.Debug($"LOG_SHIFT {operands[0]} {operands[1]}");

            short val = operands[0].SignedValue;
            short places = operands[1].SignedValue;

            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            WriteVariable(storeVar.Value, MemWord.FromSignedValue(NumberUtils.BitShift(val, places, false)));
        }


        private void OpcodePictureTable(int nOps, MemValue[] operands)
        {
            _logger.Debug($"PICTURE_TABLE {operands[0]}");
            //TODO: implement PICTURE_TABLE
        }


        private void OpcodeSaveExt(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SAVE {FormatOperands(nOps, operands)}");
            _logger.Debug($"defaulting to 0OP save -->");

            //TODO: use extra save arguments

            OpcodeSave();
        }


        private void OpcodeSaveUndo(int nOps, MemValue[] operands)
        {
            _logger.Debug($"SAVE_UNDO {FormatOperands(nOps, operands)}");

            //TODO: OpcodeSaveUndo


            MemByte storeVar = _memory.ReadByte(_pc);
            _pc++;

            WriteVariable(storeVar.Value, new MemWord(0xffff));
        }


        private void OpcodeWindowStyle(int nOps, MemValue[] operands)
        {
            _logger.Debug($"WINDOW_STYLE {operands[0]} {operands[1]} {operands[2]}");

            ushort windowId = operands[0].FullValue;
            ushort bitmask = operands[1].FullValue;
            ushort operation = nOps > 2 ? operands[2].FullValue : (ushort)0x00;

            _screen.SetWindowStyle(windowId, bitmask, operation);
        }

    }
}