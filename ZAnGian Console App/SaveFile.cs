using System;
using System.Diagnostics;

namespace ZAnGian
{
    public class SaveFile
    {
        private static Logger _logger = Logger.GetInstance();


        private string _filepath;



        public SaveFile(string filepath)
        {
            _filepath = filepath;
        }


        public static void Restore()
        {

        }
   }


}