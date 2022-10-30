using System;

namespace ZAnGian
{
    public class ZInput
    {

        private ZScreen _screen;


        public ZInput(ZScreen screen)
        {
            this._screen = screen;
        }


        public string ReadLine()
        {
            return Console.ReadLine();
        }

        public char ReadChar()
        {
            return Console.ReadKey().KeyChar;
        }


        public string GetFilePath(bool mustExist)
        {
            _screen.Print("Please enter the path to the saved file\n");
            string filePath = null;

            while (true)
            {
                _screen.Print(">> ");
                filePath = Console.ReadLine().Trim();

                if (filePath == "")
                    return null; //cancel operation


                if (filePath.Contains(" ") || filePath.Contains("\t"))
                {
                    _screen.Print("!! not a valid path\n ");
                    continue;
                }

                if (mustExist)
                {
                    if (!GameSave.FileExists(filePath))
                    {
                        _screen.Print("!! file does not exist \n");
                        continue;
                    }
                    if (!GameSave.IsValidFile(filePath))
                    {
                        _screen.Print("!! not a valid savegame file \n");
                        continue;
                    }

                    _screen.Print($"loading savegame file [{filePath}] \n");
                    break;
                }

                //mustExist == false
                if (GameSave.FileExists(filePath))
                {
                    _screen.Print("!! file already exists, overwrite it? (y/n) \n");
                    string confirmStr = Console.ReadLine().ToLower();
                    if (confirmStr != "y" && confirmStr != "yes")
                        continue;
                }

                _screen.Print($"saving savegame file [{filePath}] \n");
                break;
            }

            return filePath;
        }
    }

}