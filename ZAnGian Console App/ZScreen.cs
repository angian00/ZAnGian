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

    public enum TermColor
    {
        ForegroundBlack = 30,
        ForegroundRed = 31,
        ForegroundGreen = 32,
        ForegroundYellow = 33,
        ForegroundBlue = 34,
        ForegroundMagenta = 35,
        ForegroundCyan = 36,
        ForegroundWhite = 37,

        BackgroundBlack = 30,
        BackgroundRed = 31,
        BackgroundGreen = 32,
        BackgroundYellow = 33,
        BackgroundBlue = 34,
        BackgroundMagenta = 35,
        BackgroundCyan = 36,
        BackgroundWhite = 37,
    }



    public class ZScreen
    {

        private const string esc = "\u001b";
        
        private static Logger _logger = Logger.GetInstance();

        private TextStyle _currTextStyle = TextStyle.Normal;
        private WindowId _currWindow = WindowId.LowerWindow;
        private Dictionary<WindowId, CursorPosition> CursorPositions;
        private ushort _upperWindowSize = 0;
        private TermColor _fgColour = TermColor.ForegroundWhite;
        private TermColor _bgColour = TermColor.BackgroundBlack;


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

        public void PrintTable(string asciiText, ushort width, ushort height, ushort skip)
        {
            var startPos = GetCursorPos();

            for (int iRow = 0; iRow < height; iRow++)
            {
                for (int iCol = 0; iCol < width; iCol++)
                {
                    string textLine = asciiText.Substring(iRow*width, width); //FIXME out of bounds for last line
                    Console.Write(textLine);
                }
                
                //next line
                SetCursorPos(startPos.Line + skip + 1, startPos.Column);
            }
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


        public void SetColour(int fgColourCode, int bgColourCode)
        {
            switch (fgColourCode)
            {
                case 0:
                    //keep current
                    break;
                case 1:
                    //default
                    _fgColour = TermColor.ForegroundWhite;
                    break;
                case 2:
                    _fgColour = TermColor.ForegroundBlack;
                    break;
                case 3:
                    _fgColour = TermColor.ForegroundRed;
                    break;
                case 4:
                    _fgColour = TermColor.ForegroundGreen;
                    break;
                case 5:
                    _fgColour = TermColor.ForegroundYellow;
                    break;
                case 6:
                    _fgColour = TermColor.ForegroundBlue;
                    break;
                case 7:
                    _fgColour = TermColor.ForegroundMagenta;
                    break;
                case 8:
                    _fgColour = TermColor.ForegroundCyan;
                    break;
                case 9:
                    _fgColour = TermColor.ForegroundWhite;
                    break;
                case 10:
                    _logger.Warn($"Unsupported color code: {fgColourCode}");
                    _fgColour = TermColor.ForegroundWhite;
                    break;
                case 11:
                    _logger.Warn($"Unsupported color code: {fgColourCode}");
                    _fgColour = TermColor.ForegroundWhite;
                    break;
                case 12:
                    _logger.Warn($"Unsupported color code: {fgColourCode}");
                    _fgColour = TermColor.ForegroundWhite;
                    break;
                default:
                    _logger.Warn($"Invalid color code: {fgColourCode}");
                    break;
            }

            int bgCode;
            switch (bgColourCode)
            {
                case 0:
                    //keep current
                    break;
                case 1:
                    //default
                    _bgColour = TermColor.BackgroundWhite;
                    break;
                case 2:
                    _bgColour = TermColor.BackgroundBlack;
                    break;
                case 3:
                    _bgColour = TermColor.BackgroundRed;
                    break;
                case 4:
                    _bgColour = TermColor.BackgroundGreen;
                    break;
                case 5:
                    _bgColour = TermColor.BackgroundYellow;
                    break;
                case 6:
                    _bgColour = TermColor.BackgroundBlue;
                    break;
                case 7:
                    _bgColour = TermColor.BackgroundMagenta;
                    break;
                case 8:
                    _bgColour = TermColor.BackgroundCyan;
                    break;
                case 9:
                    _bgColour = TermColor.BackgroundWhite;
                    break;
                case 10:
                    _logger.Warn($"Unsupported color code: {bgColourCode}");
                    _bgColour = TermColor.BackgroundWhite;
                    break;
                case 11:
                    _logger.Warn($"Unsupported color code: {bgColourCode}");
                    _bgColour = TermColor.BackgroundWhite;
                    break;
                case 12:
                    _logger.Warn($"Unsupported color code: {bgColourCode}");
                    _bgColour = TermColor.BackgroundWhite;
                    break;
                default:
                    _logger.Warn($"Invalid color code: {bgColourCode}");
                    break;
            }

            Console.Write($"{esc}[{(int)_fgColour}m");
            Console.Write($"{esc}[{(int)_bgColour}m");
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

        public CursorPosition GetCursorPos()
        {
            if (_currWindow == WindowId.UpperWindow)
                return new CursorPosition()
                {
                    Line = CursorPositions[_currWindow].Line,
                    Column = CursorPositions[_currWindow].Column
                };
            else
                return new CursorPosition() {
                    Line = CursorPositions[_currWindow].Line - _upperWindowSize,
                    Column = CursorPositions[_currWindow].Column
                };
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

        public void EraseLine(ushort val)
        {
            _logger.Warn("TODO: implement EraseLine");
        }

        public void EraseWindow(short windowId)
        {
            _logger.Warn("TODO: implement EraseWindow");
        }


    }


    public record CursorPosition
    {
        public int Line { get; set; } = 0;
        public int Column { get; set; } = 0;
    }
}