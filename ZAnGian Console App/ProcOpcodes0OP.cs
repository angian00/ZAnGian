using System;

namespace ZAnGian
{
    public partial class ZProcessor
    {
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
            string msg = Zscii.DecodeText(_memory.Data, _pc.Value, out nBytesRead, memory: _memory);
            _pc += nBytesRead;

            _screen.Print(msg);
        }

        private void OpcodePrintRet()
        {
            _logger.Debug("PRINT_RET");

            ushort nBytesRead;
            string msg = Zscii.DecodeText(_memory.Data, _pc.Value, out nBytesRead, memory: _memory);
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
            Branch(true);

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
    }
}