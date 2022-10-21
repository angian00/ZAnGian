using System;
using System.IO;

namespace ZAnGian { 
    public class Program
    {
        private static Logger _logger = Logger.GetInstance();

        public static void Main(string[] args)
        {
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine(" ZAnGian");
            Console.WriteLine("an Interactive Fiction Z-Machine Interpreter");
            Console.WriteLine("----------------------------------------------------");
            Console.WriteLine("");


            if (args.Length != 1)
            {
                Console.Error.WriteLine("  Usage: ZAnGian <gamefile.z3>");
                Environment.Exit(1);
            }

            //_logger.Configure(LogLevel.DEBUG, "zangian.log");
            _logger.Configure(LogLevel.ALL, "zangian.log");


            string gamePath = args[0];
            if (!File.Exists(gamePath))
            {
                Console.Error.WriteLine("  Usage: ZAnGian <gamefile.z3>");
                Environment.Exit(1);
            }



            ZInterpreter interpr = new();
            interpr.StartGame(gamePath);

            /*
            //DEBUG
            GameSave gs = new ();
            gs.LoadFile("games/dejavu_001.sav");
            //
            */
        }
    }
}
