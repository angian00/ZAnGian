using System;
using System.Diagnostics;

namespace ZAnGian
{
    public class GameObjectV5: GameObject
    {
        public GameObjectV5(GameObjectId iObj, MemWord baseAddr, ZMemory memory) : base(iObj, baseAddr, memory) { }


        public override void SetAddresses(MemWord baseAddr)
        {
            _parentAddr = baseAddr + 6;
            _siblingAddr = baseAddr + 8;
            _childAddr = baseAddr + 10;
            _propPAddr = baseAddr + 12;
            _nAttrs = 64;
        }


        public override GameObjectId ParentId
        {
            get => (_memory.ZVersion < 5 ? _memory.ReadByte(_parentAddr) : _memory.ReadWord(_parentAddr));
            set {
                if (_memory.ZVersion < 5)
                    _memory.WriteByte(_parentAddr, (byte)value.FullValue);
                else
                    _memory.WriteWord(_parentAddr, value.FullValue);
            }
        }

        public override GameObjectId SiblingId
        {
            get => (_memory.ZVersion < 5 ? _memory.ReadByte(_siblingAddr) : _memory.ReadWord(_siblingAddr));
            set {
                if (_memory.ZVersion < 5)
                    _memory.WriteByte(_siblingAddr, (byte)value.FullValue);
                else
                    _memory.WriteWord(_siblingAddr, value.FullValue);
            }
        }

        public override GameObjectId ChildId
        {
            get => (_memory.ZVersion < 5 ? _memory.ReadByte(_childAddr) : _memory.ReadWord(_childAddr));
            set {
                if (_memory.ZVersion < 5)
                    _memory.WriteByte(_childAddr, (byte)value.FullValue);
                else
                    _memory.WriteWord(_childAddr, value.FullValue);
            }
        }


        public override MemWord? GetPropertyAddress(ushort targetPropId)
        {
            MemWord propAddr = _memory.ReadWord(_propPAddr);

            //skip shortname
            byte propLen = _memory.ReadByte(propAddr).Value;
            propAddr += propLen * 2 + 1;

            while (true)
            {
                MemByte sizeByte = _memory.ReadByte(propAddr);
                propAddr++;

                if (sizeByte == 0x00)
                    return null;

                byte propId;
                if ((sizeByte & 0b10000000).Value == 0b10000000)
                {
                    propId = (sizeByte & 0b00111111).Value;
                    MemByte sizeByte2 = _memory.ReadByte(propAddr);
                    propAddr++;
                    propLen = (sizeByte2 & 0b00111111).Value;
                    if (propLen == 0x00)
                        propLen = 64; //as per spec 12.4.2.1.1
                }
                else
                {
                    propId = (sizeByte & 0b00011111).Value;
                    propLen = (byte) ( (sizeByte & 0b01000000).Value == 0b01000000 ? 2 : 1 );
                }

                if (propId == targetPropId)
                {
                    return propAddr;
                }

                propAddr += propLen;
            }
        }

        public static MemByte GetPropertyLength(ZMemory memory, MemWord propAddr)
        {
            MemByte sizeByte = memory.ReadByte(propAddr-1);

            byte propLen;
            if ((sizeByte & 0b10000000).Value == 0b10000000)
            {
                propLen = (sizeByte & 0b00111111).Value;
                if (propLen == 0x00)
                    propLen = 64;
            }
            else
            {
                propLen = (byte)((sizeByte & 0b01000000).Value == 0b01000000 ? 2 : 1);
            }

            return new MemByte(propLen);
        }


        public override MemValue? GetPropertyValue(ushort targetPropId)
        {
            MemWord? propAddr = GetPropertyAddress(targetPropId);
            if (propAddr == (MemWord)null)
                return null;

            MemByte sizeByte = _memory.ReadByte(propAddr-1);

            byte propLen;
            if ((sizeByte & 0b10000000).Value == 0b10000000)
            {
                propLen = (sizeByte & 0b00111111).Value;
                if (propLen == 0x00)
                    propLen = 64; //as per spec 12.4.2.1.1
            }
            else
            {
                propLen = (byte)((sizeByte & 0b01000000).Value == 0b01000000 ? 2 : 1);
            }

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
            if (propValue == (MemWord)null)
                throw new ArgumentException("PutPropertyValue called for nonexistent property");

            MemByte sizeByte = _memory.ReadByte(propAddr - 1);

            byte propLen;
            if ((sizeByte & 0b10000000).Value == 0b10000000)
            {
                propLen = (sizeByte & 0b00111111).Value;
                if (propLen == 0x00)
                    propLen = 64; //as per spec 12.4.2.1.1
            }
            else
            {
                propLen = (byte)((sizeByte & 0b01000000).Value == 0b01000000 ? 2 : 1);
            }

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

                byte propLen;
                if ((sizeByte & 0b10000000).Value == 0b10000000)
                {
                    propLen = (sizeByte & 0b00111111).Value;
                    if (propLen == 0x00)
                        propLen = 64; //as per spec 12.4.2.1.1
                }
                else
                {
                    propLen = (byte)((sizeByte & 0b01000000).Value == 0b01000000 ? 2 : 1);
                }

                propAddr += propLen;
            }


            byte targetPropId;

            sizeByte = _memory.ReadByte(propAddr);

            if ((sizeByte & 0b10000000).Value == 0b10000000)
                targetPropId = (sizeByte & 0b00111111).Value;
            else
                targetPropId = (sizeByte & 0b00011111).Value;
            
            return new MemWord(targetPropId);
        }

    }


}