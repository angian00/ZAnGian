using System;
using System.Diagnostics;
using System.Reflection.Emit;
using System.Reflection.Metadata;

namespace ZAnGian
{
    public class ZScreen
    {
        public ZScreen()
        {
            Console.Clear();
        }


        public void Print(string msg)
        {
            Console.Write(msg);
        }
    }

}