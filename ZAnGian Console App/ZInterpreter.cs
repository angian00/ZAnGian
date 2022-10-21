using System;
using System.Diagnostics;
using System.IO;

namespace ZAnGian
{
    public class ZInterpreter
    {
        private static Logger _logger = Logger.GetInstance();

        private string _gamePath;


        public void StartGame(string gamePath)
        {
            byte[] rawData;
            ZMemory memory;
            ZProcessor processor;

            _logger.Info($"Loading gamefile [{gamePath}]");

            _gamePath = gamePath;
            rawData = File.ReadAllBytes(gamePath);
            memory = new ZMemory(rawData);


            //--- DEBUG ---
            //memory.DumpMetadata();

            //memory.ReadObjList();

            //memory.WalkObjTree(1, (GameObject gameObj, int depth) =>
            //{
            //    for (int i=0; i < depth; i++)
            //        Console.Write("\t");

            //    Console.WriteLine(gameObj);
            //});
            //-------------

            processor = new ZProcessor(this, memory);
            processor.Run();
        }

        public void RestartGame()
        {
            StartGame(_gamePath);
        }

        public ZMemory GetInitialMemoryState()
        {
            byte[] rawData = File.ReadAllBytes(_gamePath);
            return new ZMemory(rawData);
        }
    }
}