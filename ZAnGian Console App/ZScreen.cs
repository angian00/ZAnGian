using System;
using System.Collections.Generic;

namespace ZAnGian
{
    public enum WindowId
    {
        LowerWindow = 0,
        UpperWindow = 1,
    }

    [Flags]
    public enum TextStyle
    {
        Normal = 0,
        Reverse = 1,
        Bold = 2,
        Italic = 4,
        Monospace = 8,
    }

    public class ZScreen
    {

        private const string esc = "\u001b";
        
        private static Logger _logger = Logger.GetInstance();

        private TextStyle _currTextStyle = TextStyle.Normal;
        private WindowId _currWindow = WindowId.LowerWindow;
        private Dictionary<WindowId, CursorPosition> CursorPositions;
        private ushort _upperWindowSize = 0;


        public ZScreen()
        {
            //absolute screen positions
            CursorPositions = new Dictionary<WindowId, CursorPosition>();
            CursorPositions[WindowId.UpperWindow] = new CursorPosition() { Line = 1, Column = 1 };
            CursorPositions[WindowId.LowerWindow] = new CursorPosition() { Line = 2, Column = 1 };

            Console.Clear();
            Console.WriteLine(); //leave space for top status bar
        }


        public void Print(string msg)
        {
            Console.Write(msg);

            StoreCursorPos();
        }

        public void PrintStatusLine(StatusInfo statusInfo)
        {
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
            for (int i = 0; i < termWidth - leftSideMsg.Length - rightSideMsg.Length - 2; i++)
                Console.Write(" ");

            Console.Write(rightSideMsg);
            Console.Write(" ");

            Console.Write($"{esc}[m"); //reset styling
            Console.Write($"{esc}8"); //restore cursor position
        }


        public void SetColour(int fgColour, int bgColour)
        {
            _logger.Warn("TODO: implement SetColour");
        }

        public void SetTextStyle(ushort newTextStyle)
        {
            if (newTextStyle == 0x00)
                _currTextStyle = TextStyle.Normal;
            else
                _currTextStyle |= (TextStyle)newTextStyle;

            //apply the new text style to next prints

            //reset style attributes
            Console.Write($"{esc}[0m");

            if ((_currTextStyle & TextStyle.Italic) == TextStyle.Italic)
                Console.Write($"{esc}[4m");

            if ((_currTextStyle & TextStyle.Bold) == TextStyle.Bold)
                Console.Write($"{esc}[1m");

            if ((_currTextStyle & TextStyle.Reverse) == TextStyle.Reverse)
                Console.Write($"{esc}[7m");
        }


        public void SetCurrWindow(short windowId)
        {
            _currWindow = (WindowId)windowId;
            ApplyCursorPos();
        }

        public void SplitWindow(ushort nLines)
        {
            _upperWindowSize = nLines;
        }

        public void SetWindowStyle(ushort windowId, ushort bitmask, ushort operation)
        {
            _logger.Warn("TODO: implement SetWindowStyle");
        }

        public void SetCursorPos(int line, int col)
        {
            if (_currWindow == WindowId.UpperWindow)
                CursorPositions[_currWindow] = new CursorPosition() { Line = line, Column = col };
            else
                CursorPositions[_currWindow] = new CursorPosition() { Line = line + _upperWindowSize, Column = col };

            ApplyCursorPos();
        }



        private void StoreCursorPos()
        {
            CursorPositions[_currWindow].Line = Console.CursorTop + 1;
            CursorPositions[_currWindow].Column = Console.CursorLeft + 1;
        }

        private void ApplyCursorPos()
        {
            Console.CursorTop = CursorPositions[_currWindow].Line - 1;
            Console.CursorLeft = CursorPositions[_currWindow].Column - 1;
        }

    }


    internal record CursorPosition
    {
        public int Line { get; set; } = 0;
        public int Column { get; set; } = 0;
    }
}