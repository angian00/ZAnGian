using System;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Reflection.Metadata;
using static System.Formats.Asn1.AsnWriter;

namespace ZAnGian
{
    public class ZScreen
    {
        public ZScreen()
        {
            Console.Clear();
            Console.WriteLine(); //leave space for top status bar
        }


        public void Print(string msg)
        {
            Console.Write(msg);
        }

        public void PrintStatusLine(StatusInfo statusInfo)
        {
            string esc = "\u001b";


            Console.Write($"{esc}7"); //save cursor position

            Console.Write($"{esc}[1m");  //bold
            Console.Write($"{esc}[7m");  //reverse fg and bg colors

            Console.Write($"{esc}[99A"); //go all up

            string leftSideMsg = statusInfo.CurrObjName;
            if (leftSideMsg.Length > 20)
                leftSideMsg = leftSideMsg.Substring(0, 30) + "...";

            Console.Write($"{esc}[999D"); //go all left
            Console.Write(" ");
            Console.Write(leftSideMsg);

            string rightSideMsg;
            if (statusInfo.IsScoreGame)
            {
                rightSideMsg = $"Score: {statusInfo.Score} Moves: {statusInfo.Turns}";
            }
            else
            {
                rightSideMsg = $"Time: {statusInfo.Hours:d02}:{statusInfo.Minutes:d02}";
            }

            //fill middle section of top line with whitespace
            int termWidth = Console.BufferWidth;
            for (int i=0; i < termWidth-leftSideMsg.Length-rightSideMsg.Length-2; i++)
                Console.Write(" ");

            Console.Write(rightSideMsg);
            Console.Write(" ");

            Console.Write($"{esc}[m"); //reset styling
            Console.Write($"{esc}8"); //restore cursor position
        }
    }

}