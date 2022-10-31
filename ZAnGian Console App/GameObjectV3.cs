using System;
using System.Diagnostics;

namespace ZAnGian
{
    public class GameObjectV3 : GameObject
    {
        public GameObjectV3(GameObjectId iObj, MemWord baseAddr, ZMemory memory) : base(iObj, baseAddr, memory) { }


        public override void SetAddresses(MemWord baseAddr)
        {
            _parentAddr = baseAddr + 4;
            _siblingAddr = baseAddr + 5;
            _childAddr = baseAddr + 6;
            _propPAddr = baseAddr + 7;
            _nAttrs = 32;
        }


        public override GameObjectId ParentId
        {
            get => _memory.ReadByte(_parentAddr);
            set => _memory.WriteByte(_parentAddr, (byte)value.FullValue);
        }

        public override GameObjectId SiblingId
        {
            get => _memory.ReadByte(_siblingAddr);
            set => _memory.WriteByte(_siblingAddr, (byte)value.FullValue);
        }

        public override GameObjectId ChildId
        {
            get => _memory.ReadByte(_childAddr);
            set => _memory.WriteByte(_childAddr, (byte)value.FullValue);
        }


        public override MemWord? GetPropertyAddress(ushort targetPropId)
        {
            MemWord propAddr = _memory.ReadWord(_propPAddr);

            //skip shortname
            ushort propLen = _memory.ReadByte(propAddr).Value;
            propAddr += propLen * 2 + 1;

            while (true)
            {
                MemByte sizeByte = _memory.ReadByte(propAddr);
                propAddr++;

                if (sizeByte == 0x00)
                    return null;

                propLen = ((sizeByte >> 5) + 1).Value;
                ushort propId = (sizeByte & 0b00011111).Value;
                if (propId == targetPropId)
                {
                    return propAddr;
                }

                propAddr = propAddr + propLen;
            }
        }

        public static MemByte GetPropertyLength(ZMemory memory, MemWord propAddr)
        {
            MemByte sizeByte = memory.ReadByte(propAddr-1);
            Debug.Assert(sizeByte != 0x00);

            return ((sizeByte >> 5) + 1);
        }

        public override MemValue? GetPropertyValue(ushort targetPropId)
        {
            MemWord? propAddr = GetPropertyAddress(targetPropId);
            if (propAddr == (MemWord)null)
                return null;

            MemByte sizeByte = _memory.ReadByte(propAddr-1);
            ushort propLen = ((sizeByte >> 5) + 1).Value;

            if (propLen == 1)
                return _memory.ReadByte(propAddr);
            else if (propLen == 2)
                return _memory.ReadWord(propAddr);
            else
                throw new ArgumentException("GetPropertyValue called when propLen > 2");
        }


        public override void PutPropertyValue(ushort targetPropId, MemValue propValue)
        {
            MemWord? propAddr = GetPropertyAddress(targetPropId);
            if (propAddr == (MemWord)null)
                throw new ArgumentException("PutPropertyValue called for nonexistent property");

            MemByte sizeByte = _memory.ReadByte(propAddr-1);
            ushort propLen = ((sizeByte >> 5) + 1).Value;

            if (propLen == 1)
            {
                //as per spec, when len == 1 save only least significant byte
                _memory.WriteByte(propAddr, (byte)(propValue.FullValue & 0xff));
            }
            else if (propLen == 2)
            {
                _memory.WriteWord(propAddr, propValue.FullValue);
            }
            else
                throw new ArgumentException("PutPropertyValue called when propLen > 2");
        }
        
        
        public override MemWord? GetNextPropertyId(ushort startPropId)
        {
            MemWord? propAddr;
            MemByte sizeByte;


            if (startPropId == 0x00)
            {
                //get the first property
                propAddr = _memory.ReadWord(_propPAddr);
            }
            else
            {
                //start from the given startPropId
                propAddr = GetPropertyAddress(startPropId);
                if (propAddr == (MemWord)null)
                    throw new ArgumentException("GetNextPropertyId called for nonexistent property"); //as per spec

                sizeByte = _memory.ReadByte(propAddr-1);
                ushort propLen = ((sizeByte >> 5) + 1).Value;

                propAddr += propLen; //skip to next property
            }


            sizeByte = _memory.ReadByte(propAddr);
            if (sizeByte == 0x00)
                return null;

            ushort targetPropId = (sizeByte & 0b00011111).Value;
            return new MemWord(targetPropId);
        }

    }
}