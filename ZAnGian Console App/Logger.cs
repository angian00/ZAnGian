using System;
using System.Collections.Generic;
using System.IO;


namespace ZAnGian
{
    public enum LogLevel
    {
        ERROR,
        WARN,
        INFO,
        DEBUG,
        ALL,
    }

    public class Logger
    {
        private static Logger _the;
        private static Dictionary<LogLevel, string> Colors = new();
        static Logger()
        {
            Colors[LogLevel.ERROR] = "31";
            Colors[LogLevel.WARN]  = "33";
            Colors[LogLevel.INFO]  = "32";
            Colors[LogLevel.DEBUG] = "37";
            Colors[LogLevel.ALL]   = "37";
        }


        public static Logger GetInstance()
        {
            if (_the == null)
                _the = new Logger();

            return _the;
        }


        private LogLevel _level;
        private TextWriter _writer;


        private Logger()
        {
            this._level = LogLevel.INFO;
            this._writer = Console.Error;
        }

        public void Configure(LogLevel level, string filePath)
        {
            this._level = level;

            if (filePath == null)
                _writer = Console.Error;
            else
                _writer = File.CreateText(filePath);
        }

        public void Log(LogLevel msgLevel, string msg)
        {
            if (_level >= msgLevel)
            {
                _writer.WriteLine($"\u001b[{Colors[msgLevel]}m{msg}\u001b[0m");
                _writer.Flush();
            }
        }

        public void Error(string msg) => Log(LogLevel.ERROR, msg);
        public void Warn(string msg) => Log(LogLevel.WARN, msg);
        public void Info(string msg) => Log(LogLevel.INFO, msg);
        public void Debug(string msg) => Log(LogLevel.DEBUG, msg);
        public void All(string msg) => Log(LogLevel.ALL, msg);

    }
}