using System;
using System.Diagnostics;
using System.IO;

namespace ZAnGian
{
    public class ZInterpreter
    {
        private static Logger _logger = Logger.GetInstance();

        private string _gamePath;
        private ZProcessor _processor;

        public void StartGame(string gamePath)
        {
            byte[] rawData;
            ZMemory memory;
            ZProcessor processor;

            _logger.Info($"Loading gamefile [{gamePath}]");

            _gamePath = gamePath;
            rawData = File.ReadAllBytes(gamePath);
            memory = new ZMemory(rawData);

            _processor = new ZProcessor(this, memory);
            _processor.Run();
        }

        public void RestartGame()
        {
            _processor.Dispose();
            StartGame(_gamePath);
        }

        public ZMemory GetInitialMemoryState()
        {
            byte[] rawData = File.ReadAllBytes(_gamePath);
            return new ZMemory(rawData);
        }
    }
}