using System;
using System.Diagnostics;
using System.IO;

namespace ZAnGian
{
    public class ZInterpreter
    {
        private static Logger _logger = Logger.GetInstance();



        public ZInterpreter()
        {
            //_logger.Configure(LogLevel.ALL, null);
            _logger.Configure(LogLevel.ALL, "zangian.log");
        }


        public void StartGame(string gamePath)
        {
            byte[] rawData;
            ZMemory memory;
            ZProcessor processor;

            _logger.Info($"Loading gamefile [{gamePath}]");

            rawData = File.ReadAllBytes(gamePath);
            memory = new ZMemory(rawData);


            //--- DEBUG ---
            memory.DumpMetadata();
            Console.WriteLine("");

            memory.ReadObjList();

            //memory.WalkObjTree(1, (GameObject gameObj, int depth) =>
            //{
            //    for (int i=0; i < depth; i++)
            //        Console.Write("\t");

            //    Console.WriteLine(gameObj);
            //});
            //-------------

            processor = new ZProcessor(memory);
            processor.Run();
        }
    }
}