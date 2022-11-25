using System;

namespace ZAnGian
{
    public partial class ZProcessor
    {
        private void OpcodeCatch()
        {
            _logger.Debug("CATCH");

            throw new NotImplementedException("TODO: implement CATCH");
        }

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

        private void OpcodePiracy()
        {
            _logger.Debug("PIRACY");
            Branch(true);
        }

        private void OpcodePrint()
        {
            _logger.Debug("PRINT");

            ushort nBytesRead;
            string msg = Zscii.DecodeText(_memory.Data, _pc, out nBytesRead, memory: _memory);
            _pc += nBytesRead;

            _screen.Print(msg);
        }

        private void OpcodePrintRet()
        {
            _logger.Debug("PRINT_RET");

            ushort nBytesRead;
            string msg = Zscii.DecodeText(_memory.Data, _pc, out nBytesRead, memory: _memory);
            _pc += nBytesRead;

            _screen.Print(msg);
            _screen.Print("\n");
            ReturnRoutine(MemWord.FromBool(true));
        }

        private void OpcodeQuit()
        {
            _logger.Debug("QUIT");

            this.Dispose();
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

                    if (_memory.ZVersion >= 5)
                    {
                        MemByte storeVar = _memory.ReadByte(_pc-1);
                        WriteVariable(storeVar.Value, 0x02);
                    }

                    //DEBUG
                    _logger.Debug("After restore");
                    _logger.Debug($"pc={_pc}");
                    _stack.Dump();
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
            MemByte storeVar = null;

            string filepath = _input.GetFilePath(false);
            if (filepath == null)
                return;

            if (_memory.ZVersion < 5)
            {
                Branch(true);
            }
            else
            {
                storeVar = _memory.ReadByte(_pc);
                _pc++;
            }

            _logger.Debug("Before save");
            _logger.Debug($"pc={_pc}");
            _stack.Dump();
            //_memory.DumpDynamicMem();

            ZMemory initialMemState = _interpreter.GetInitialMemoryState();
            GameSave gameSave = new GameSave(_memory, _stack, _pc, initialMemState);
            bool savedOk = gameSave.SaveFile(filepath);

            if (_memory.ZVersion >= 5)
            {
                WriteVariable(storeVar.Value, (ushort) (savedOk ? 0x01 : 0x00));
            }
        }

        private void OpcodeShowStatus()
        {
            _logger.Debug("SHOW_STATUS");

            _screen.PrintStatusLine(GetStatusInfo());
        }

        private void OpcodeVerify()
        {
            _logger.Debug("VERIFY");

            _logger.Warn("TODO: implement OpcodeVerify");
            Branch(true);
        }
    }
}