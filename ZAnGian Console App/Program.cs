using System;
using System.IO;

namespace ZAnGian { 
    public class Program
    {
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


            string gamePath = args[0];
            if (!File.Exists(gamePath))
            {
                Console.Error.WriteLine("  Usage: ZAnGian <gamefile.z3>");
                Environment.Exit(1);
            }

            ZInterpreter interpr = new();
            interpr.StartGame(gamePath);
        }
    }
}
