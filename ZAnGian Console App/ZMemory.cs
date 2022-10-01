﻿global using GameObjectId = System.Byte;
global using GameVariableId = System.Byte;


using System;
using System.Diagnostics;

namespace ZAnGian
{
    public delegate void GameObjDelegate(GameObject gameObj, int depth);


    public class ZMemory
    {
        private static Logger _logger = Logger.GetInstance();

        public const int MAX_N_OBJS = 255;
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
        private MemWord FileLen;
        private MemWord Checksum;
        private MemWord Revision;
        
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
            _logger.Debug($"baseHighMem: {BaseHighMem.ToDecimalString()} [{BaseHighMem}]");
            _logger.Debug($"baseStaticMem: {BaseStaticMem.ToDecimalString()} [{BaseStaticMem}]");
            _logger.Debug($"startPC: {StartPC.ToDecimalString()} [{StartPC}]");
            _logger.Debug("");
            _logger.Debug($"dictionaryLoc: {DictionaryLoc.ToDecimalString()} [{DictionaryLoc}]");
            _logger.Debug($"objTableLoc: {ObjectTableLoc.ToDecimalString()}  [{ObjectTableLoc}]");
            _logger.Debug($"globalVarTableLoc: {GlobalVarTableLoc.ToDecimalString()} [{GlobalVarTableLoc}]");
            _logger.Debug($"abbrTableLoc: {AbbrTableLoc.ToDecimalString()} [{AbbrTableLoc}]");
            _logger.Debug("");
            _logger.Debug($"file length: {FileLen}");
            _logger.Debug($"checksum: {Checksum}");
            _logger.Debug("");
        }


        public void WalkObjTree(GameObjectId startObjId, GameObjDelegate objDelegate)
        {
            WalkObjNode(startObjId, 0, objDelegate);
        }

        private void WalkObjNode(GameObjectId objId, ushort currDepth, GameObjDelegate objDelegate)
        {
            GameObject currObj = FindObject(objId);
            
            objDelegate(currObj, currDepth);

            if (currObj.ChildId != 0x00)
                WalkObjNode(currObj.ChildId, (ushort)(currDepth + 1), objDelegate);

            if (currObj.SiblingId != 0x00)
                WalkObjNode(currObj.SiblingId, currDepth, objDelegate);
        }


        public void ReadObjList()
        {
            GameObjectId iObj = 1;
            bool isLast = false;

            while (!isLast)
            {
                Debug.Assert(iObj <= MAX_N_OBJS);

                GameObject obj = FindObject(iObj);
                obj.Dump();

                iObj++;

                //DEBUG
                if (iObj >= 10)
                    break;
            }
        }


        public GameObject FindObject(GameObjectId iObj)
        {
            if (iObj == 0x00)
                return null;

            MemWord memPos = (MemWord)(ObjectTableLoc + 2 * GameObject.MAX_N_OBJ_PROPS + (iObj-1)*OBJ_ENTRY_SIZE);
            //Console.WriteLine($"DEBUG: FindObject[{memPos}]");

            GameObject gameObj = new GameObject(iObj, memPos, this);
            
            //ReadProperties(ref gameObj, propAddr);
            
            return gameObj;

        }

        public MemWord ReadPropDefault(short iProperty)
        {
            return ReadWord(ObjectTableLoc + iProperty);
        }


        /*
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
        */

        public MemByte ReadByte(ushort targetAddr)
        {
            return new MemByte(Data[targetAddr]);
        }

        public MemByte ReadByte(MemWord targetAddr)
        {
            return new MemByte(Data[targetAddr.Value]);
        }

        public void WriteByte(int targetAddr, byte value)
        {
            Data[targetAddr] = value;
        }

        public void WriteByte(MemWord targetAddr, MemByte memByte)
        {
            Data[targetAddr.Value] = memByte.Value;
        }

        public void WriteByte(MemWord targetAddr, byte value)
        {
            Data[targetAddr.Value] = value;
        }

        public MemWord ReadWord(ushort targetAddr)
        {
            return new MemWord(Data[targetAddr], Data[targetAddr + 1]);
        }

        public MemWord ReadWord(MemWord targetAddr)
        {
            return ReadWord(targetAddr.Value);
        }

        public void WriteWord(ushort targetAddr, MemWord data)
        {
            Data[targetAddr] = data.HighByte;
            Data[targetAddr + 1] = data.LowByte;
        }

        public void WriteWord(MemWord targetAddr, MemWord data)
        {
            WriteWord(targetAddr.Value, data);
        }

        public uint ReadNBytes(int nBytes, uint targetAddr)
        {
            Debug.Assert(nBytes <= 4, "uint overflow");

            uint res = 0;

            for (int i = 0; i < nBytes; i++)
            {
                res += (res << 8) + Data[targetAddr + i];
            }

            return res;
        }

        public uint ReadNBytes(int nBytes, MemWord targetAddr)
        {
            return ReadNBytes(nBytes, targetAddr.Value);
        }

        public void WriteNBytes(int nBytes, int targetAddr, uint val)
        {
            Debug.Assert(nBytes <= 4, "uint overflow");

            for (int i = 0; i < nBytes; i++)
            {
                Data[targetAddr + nBytes - i - 1] = (byte) (val & 0xff);
                val >>= 8;
            }
        }

        public uint ReadDWord(MemWord targetAddr)
        {
            return ReadNBytes(4, targetAddr.Value);
        }

        public void WriteDWord(MemWord targetAddr, uint val)
        {
            WriteNBytes(4, targetAddr.Value, val);
        }
    }
}