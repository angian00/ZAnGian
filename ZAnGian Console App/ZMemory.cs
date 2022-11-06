global using GameObjectId = ZAnGian.MemValue;
global using GameVariableId = System.Byte;


using System;
using System.Diagnostics;
using System.Reflection.Emit;

namespace ZAnGian
{
    public delegate void GameObjDelegate(GameObject gameObj, int depth);


    public class ZMemory : ICloneable
    {
        private static Logger _logger = Logger.GetInstance();

        private readonly int MaxNObjects;
        private readonly ushort ObjEntrySize;
        private readonly int MaxNObjProps;
        public readonly ushort DictEntryTextLen;


        public byte[] Data;

        public int ZVersion;
        private MemWord BaseHighMem;
        public MemWord StartPC { get; private set; }
        public MemWord DictionaryLoc { get; private set; }
        private MemWord ObjectTableLoc;
        public MemWord GlobalVarTableLoc;
        public MemWord BaseStaticMem { get; private set; }
        private MemWord AbbrTableLoc;
        private ushort FileLen;
        public MemWord GameRelease { get; private init; }
        public string GameSerialNum { get; private init; }
        public MemWord Checksum { get; private init; }
        public MemWord StdRevision { get; private init; }
        public MemByte Flags1;
        public MemByte Flags2;


        public ZMemory(byte[] rawData)
        {
            this.Data = rawData;

            this.ZVersion = rawData[0x00];
            if (ZVersion != 3 && ZVersion != 5)
                throw new NotImplementedException($"Unsupported story file version: {ZVersion}");


            this.Flags1 = ReadByte(0x01);

            this.GameRelease = ReadWord(0x02);

            this.BaseHighMem = ReadWord(0x04);
            this.StartPC = ReadWord(0x06);
            this.DictionaryLoc = ReadWord(0x08);
            this.ObjectTableLoc = ReadWord(0x0A);
            this.GlobalVarTableLoc = ReadWord(0x0C);
            this.BaseStaticMem = ReadWord(0x0E);

            this.Flags2 = ReadByte(0x10);
            this.GameSerialNum = StringUtils.BytesToAsciiString(Data, 0x12, 6);

            this.AbbrTableLoc = ReadWord(0x18);
            this.FileLen = ReadWord(0x1A).Value;
            if (ZVersion == 3)
            {
                FileLen *= 2;
                MaxNObjects = 255;
                ObjEntrySize = 9;
                MaxNObjProps = 31;
                DictEntryTextLen = 4;
            }
            else if (ZVersion == 5)
            {
                FileLen *= 4;
                MaxNObjects = 65535;
                ObjEntrySize = 14;
                MaxNObjProps = 63;
                DictEntryTextLen = 6;
            }
            this.Checksum = ReadWord(0x1C);
            this.StdRevision = ReadWord(0x32);


            //FIXME: Unicode table address in header extension table
            //specific entry from "header extension table"
            //if (ReadWord(0x36).Value >= 3)
            //    Zscii.SetUnicodeTable(this, ReadWord(0x36 + 6));

            Zscii.SetTranslationTable(this, ReadWord(0x34));


            //-- update writeable fields with interpreter-dependent info
            WriteByte(0x1e, (byte)'A');
            WriteByte(0x1f, (byte)'Z');

            //specify missing interpreter features
            this.Flags2 &= 0b11101111; //no undo
            WriteByte(0x01, Flags2);
        }


        public void DumpMetadata()
        {
            _logger.Info($"version: {ZVersion}");
            _logger.Info($"revision: {StdRevision}");
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

        public object Clone()
        {
            var newMem = (ZMemory)MemberwiseClone();

            return newMem;
        }

        public void DumpDynamicMem()
        {
            _logger.Debug("-- Dynamic memory image");
            for (UInt16 addr = 0; addr < BaseStaticMem.Value; addr++)
            {
                _logger.Debug($"[0x{addr:x4}] 0x{Data[addr]:x2}");
            }
        }

        public void WalkObjTree(GameObjectId startObjId, GameObjDelegate objDelegate)
        {
            WalkObjNode(startObjId, 0, objDelegate);
        }

        private void WalkObjNode(GameObjectId objId, ushort currDepth, GameObjDelegate objDelegate)
        {
            GameObject? currObj = FindObject(objId);
            Debug.Assert(currObj != null);

            objDelegate(currObj, currDepth);

            if (currObj.ChildId.FullValue != 0x00)
                WalkObjNode(currObj.ChildId, (ushort)(currDepth + 1), objDelegate);

            if (currObj.SiblingId.FullValue != 0x00)
                WalkObjNode(currObj.SiblingId, currDepth, objDelegate);
        }


        public void ReadObjList()
        {
            GameObjectId iObj = MakeObjectId(1);
            bool isLast = false;

            while (!isLast)
            {
                Debug.Assert(iObj.FullValue <= MaxNObjects);

                GameObject? obj = FindObject(iObj);
                Debug.Assert(obj != null);
                obj.Dump();

                iObj.next();

                //DEBUG
                if (iObj.FullValue >= 10)
                    break;
            }
        }


        public GameObject? FindObject(GameObjectId iObj)
        {
            if (iObj == null || iObj.FullValue == 0x00)
                return null;

            MemWord memPos = (MemWord)(ObjectTableLoc + 2 * MaxNObjProps + (iObj.FullValue - 1) * ObjEntrySize);
            //Console.WriteLine($"DEBUG: FindObject[{memPos}]");

            GameObject gameObj = MakeObject(iObj, memPos);

            return gameObj;

        }

        public MemWord GetDefaultPropertyValue(ushort propId)
        {
            return ReadWord(ObjectTableLoc + (propId - 1) * 2);
        }


        public GameObjectId MakeObjectId(uint val)
        {
            if (ZVersion < 5)
                return new MemByte(val);
            else
                return new MemWord(val);
        }

        public GameObject MakeObject(GameObjectId iObj, MemWord baseAddr)
        {
            if (ZVersion < 5)
                return new GameObjectV3(iObj, baseAddr, this);
            else
                return new GameObjectV5(iObj, baseAddr, this);
        }

        public MemByte GetPropertyLength(MemWord propAddr)
        {
            if (ZVersion < 5)
                return GameObjectV3.GetPropertyLength(this, propAddr);
            else
                return GameObjectV5.GetPropertyLength(this, propAddr);
        }



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
            if (!IsWritable(targetAddr))
                throw new ArgumentException("Illegal write on memory");

            Data[targetAddr] = value;
        }

        public void WriteByte(int targetAddr, MemByte memByte)
        {
            WriteByte(targetAddr, memByte.Value);
        }

        public void WriteByte(MemWord targetAddr, MemByte memByte)
        {
            WriteByte(targetAddr.Value, memByte.Value);
        }

        public void WriteByte(MemWord targetAddr, byte value)
        {
            WriteByte(targetAddr.Value, value);
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
            if (!IsWritable(targetAddr) || !IsWritable(targetAddr + 1))
                throw new ArgumentException("Illegal write on memory");

            Data[targetAddr] = data.HighByte;
            Data[targetAddr + 1] = data.LowByte;
        }

        public void WriteWord(MemWord targetAddr, MemWord data)
        {
            WriteWord(targetAddr.Value, data);
        }

        public void WriteWord(MemWord targetAddr, ushort data)
        {
            WriteWord(targetAddr.Value, new MemWord(data));
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
                ushort writeIndex = (ushort) (targetAddr + nBytes - i - 1);

                if (!IsWritable(writeIndex))
                    throw new ArgumentException("Illegal write on memory");

                Data[writeIndex] = (byte)(val & 0xff);
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

        public bool CompareBytes(MemWord targetAddr, byte[] data, ushort dataLen)
        {
            for (int i = 0; i < dataLen; i++)
            {
                if (Data[targetAddr.Value + i] != data[i])
                    return false;
            }

            return true;
        }

        public void CopyBytes(MemWord targetAddr, byte[] data, ushort dataLen=0xffff)
        {
            if (dataLen == 0xffff)
                dataLen = (ushort)data.Length;

            for (int i = 0; i < dataLen; i++)
                Data[targetAddr.Value + i] = data[i];
        }



        private bool IsWritable(int targetAddr)
        {
            return (targetAddr < BaseStaticMem.Value);
        }

    }
}