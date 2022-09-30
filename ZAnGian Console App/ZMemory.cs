global using MemWord = System.UInt16;

using System;
using System.Diagnostics;

namespace ZAnGian
{
    public delegate void GameObjDelegate(GameObject gameObj, int depth);


    public class ZMemory
    {
        private static Logger _logger = Logger.GetInstance();

        public const MemWord MAX_N_OBJS = 255;
        private const ushort OBJ_ENTRY_SIZE = 9;


        public byte[] Data;

        public  int ZVersion;
        private MemWord BaseHighMem;
        public  MemWord StartPC { get; private set; }
        private MemWord DictionaryLoc;
        private MemWord ObjectTableLoc;
        public  MemWord GlobalVarTableLoc;
        private MemWord BaseStaticMem;
        private MemWord AbbrTableLoc;
        private int FileLen;
        private int Checksum;
        private int Revision;
        
        private MemWord[] ObjPropDefaults = new MemWord[GameObject.MAX_N_OBJ_PROPS];


        public ZMemory(byte[] rawData)
        {
            this.Data = rawData;

            this.ZVersion          = rawData[0x00];

            byte flags1            = rawData[0x01]; //TODO: use flags

            this.BaseHighMem       = ReadWord(0x04);
            this.StartPC           = ReadWord(0x06);
            this.DictionaryLoc     = ReadWord(0x08);
            this.ObjectTableLoc    = ReadWord(0x0A);
            this.GlobalVarTableLoc = ReadWord(0x0C);
            //this.BaseStaticMem     = rawData[0x0E];
            this.BaseStaticMem     = ReadWord(0x0E);

            byte flags2            = rawData[0x10]; //TODO: use flags

            this.AbbrTableLoc      = ReadWord(0x18);
            this.FileLen           = ReadWord(0x1A) * 2; //as per spec
            this.Checksum          = ReadWord(0x1C);
            this.Revision          = ReadWord(0x32);
        }


        public void DumpMetadata()
        {
            _logger.Info($"version: {ZVersion}");
            _logger.Info($"revision: {Revision}");
            _logger.Debug("");
            _logger.Debug($"baseHighMem: {BaseHighMem} [0x{BaseHighMem:x}]");
            _logger.Debug($"baseStaticMem: {BaseStaticMem} [0x{BaseStaticMem:x}]");
            _logger.Debug($"startPC: {StartPC} [0x{StartPC:x}]");
            _logger.Debug("");
            _logger.Debug($"dictionaryLoc: {DictionaryLoc} [0x{DictionaryLoc:x}]");
            _logger.Debug($"objTableLoc: {ObjectTableLoc}  [0x{ObjectTableLoc:x}]");
            _logger.Debug($"globalVarTableLoc: {GlobalVarTableLoc} [0x{GlobalVarTableLoc:x}]");
            _logger.Debug($"abbrTableLoc: {AbbrTableLoc} [0x{AbbrTableLoc:x}]");
            _logger.Debug("");
            _logger.Debug($"file length: {FileLen}");
            _logger.Debug($"checksum: {Checksum}");
            _logger.Debug("");
        }


        public void WalkObjTree(ushort startObjId, GameObjDelegate objDelegate)
        {
            WalkObjNode(startObjId, 0, objDelegate);
        }

        private void WalkObjNode(ushort objId, ushort currDepth, GameObjDelegate objDelegate)
        {
            GameObject currObj = ReadGameObject(objId);
            
            objDelegate(currObj, currDepth);

            if (currObj.Child != 0x00)
                WalkObjNode(currObj.Child, (ushort)(currDepth + 1), objDelegate);

            if (currObj.Sibling != 0x00)
                WalkObjNode(currObj.Sibling, currDepth, objDelegate);
        }


        public void ReadObjList()
        {
            ushort iObj = 1;
            bool isLast = false;

            while (!isLast)
            {
                Debug.Assert(iObj <= MAX_N_OBJS);

                GameObject obj = ReadGameObject(iObj);
                obj.Dump();

                iObj++;

                //DEBUG
                if (iObj >= 40)
                    break;
            }
        }


        private GameObject ReadGameObject(ushort iObj)
        {
            MemWord memPos = (MemWord)(ObjectTableLoc + 2 * GameObject.MAX_N_OBJ_PROPS + (iObj-1)*OBJ_ENTRY_SIZE);
            //Console.WriteLine($"DEBUG: ReadGameObject[{memPos}]");

            GameObject gameObj = new();

            gameObj.Id = iObj;
            //gameObj.IsLast = ; 
            gameObj.Attributes = ReadNBytes(4, memPos);
            gameObj.Parent     = ReadByte(memPos + 4);
            gameObj.Sibling    = ReadByte(memPos + 5);
            gameObj.Child      = ReadByte(memPos + 6);
            MemWord propAddr   = ReadWord(memPos + 7);
            
            ReadProperties(ref gameObj, propAddr);
            
            return gameObj;

        }

        public MemWord ReadPropDefault(short iProperty)
        {
            return ReadWord(ObjectTableLoc + iProperty);
        }


        private void ReadProperties(ref GameObject gameObj, MemWord propTableAddr)
        {
            //Console.WriteLine($"DEBUG: ReadProperties[{gameObj.Id}] [{propTableAddr}]");

            MemWord memPos = propTableAddr;
            
            //read shortName header
            byte textLen = ReadByte(memPos);
            //Console.WriteLine($"DEBUG: ReadProperties textLen: [{textLen}]");

            gameObj.ShortName = Zscii.DecodeText(Data, (MemWord)(memPos + 1), out _, (ushort)(2*textLen));
            memPos += (MemWord) (2 * textLen + 1);

            while (true)
            {
                byte sizeByte = ReadByte(memPos);
                if (sizeByte == 0)
                    break;

                byte pId = (byte)(sizeByte & 0b00011111);
                byte pSize = (byte)((sizeByte + 1) / 32);
                uint pData = ReadNBytes(pSize, memPos + 1);

                gameObj.Properties[pId] = new ObjProperty(pId, pSize, pData);
                memPos += (MemWord)(pSize + 1);
            }
        }


        public byte ReadByte(int offset)
        {
            return Data[offset];
        }

        public MemWord ReadWord(int offset)
        {
            return (MemWord)((Data[offset] << 8) + Data[offset + 1]);
        }

        public void WriteWord(int offset, MemWord data)
        {
            Data[offset]   = (byte)(data >> 8);
            Data[offset+1] = (byte)(data & 0xff);
        }

        public uint ReadNBytes(int nBytes, int offset)
        {
            uint res = 0;

            for (int i=0; i < nBytes; i++)
            {
                res += (res << 8) + Data[offset+i];
            }

            return res;
        }
    }
}