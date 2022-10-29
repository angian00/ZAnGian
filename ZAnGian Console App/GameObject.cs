﻿using System;
using System.Diagnostics;

namespace ZAnGian
{
    public class GameObject
    {
        private static Logger _logger = Logger.GetInstance();


        public GameObjectId Id;
        private ZMemory _memory;

        private readonly MemWord _attrAddr;
        private readonly MemWord _parentAddr;
        private readonly MemWord _siblingAddr;
        private readonly MemWord _childAddr;
        private readonly MemWord _propPAddr;
        private readonly int _nAttrs;


        public uint Attributes
        {
            get => _memory.ReadDWord(_attrAddr);
            set => _memory.WriteDWord(_attrAddr, value);
        }

        public GameObjectId ParentId
        {
            get => (_memory.ZVersion < 5 ? _memory.ReadByte(_parentAddr) : _memory.ReadWord(_parentAddr));
            set {
                if (_memory.ZVersion < 5)
                    _memory.WriteByte(_parentAddr, (byte)value.FullValue);
                else
                    _memory.WriteWord(_parentAddr, value.FullValue);
            }
        }

        public GameObjectId SiblingId
        {
            get => (_memory.ZVersion < 5 ? _memory.ReadByte(_siblingAddr) : _memory.ReadWord(_siblingAddr));
            set {
                if (_memory.ZVersion < 5)
                    _memory.WriteByte(_siblingAddr, (byte)value.FullValue);
                else
                    _memory.WriteWord(_siblingAddr, value.FullValue);
            }
        }

        public GameObjectId ChildId
        {
            get => (_memory.ZVersion < 5 ? _memory.ReadByte(_childAddr) : _memory.ReadWord(_siblingAddr));
            set {
                if (_memory.ZVersion < 5)
                    _memory.WriteByte(_childAddr, (byte)value.FullValue);
                else
                    _memory.WriteWord(_childAddr, value.FullValue);
            }
        }

        public string ShortName
        {
            get
            {
                MemWord snAddr = _memory.ReadWord(_propPAddr);
                byte textLen = _memory.ReadByte(snAddr).Value;
                string value = Zscii.DecodeText(_memory.Data, snAddr + 1, out _, (ushort)(2 * textLen));

                return value;
            }
        }


        public GameObject(GameObjectId iObj, MemWord baseAddr, ZMemory memory)
        {
            Id = iObj;
            _memory = memory;

            _attrAddr = baseAddr;
            if (memory.ZVersion == 3)
            {
                _parentAddr = baseAddr + 4;
                _siblingAddr = baseAddr + 5;
                _childAddr = baseAddr + 6;
                _propPAddr = baseAddr + 7;
                _nAttrs = 32;
            }
            else if (memory.ZVersion == 5)
            {
                _parentAddr = baseAddr + 6;
                _siblingAddr = baseAddr + 8;
                _childAddr = baseAddr + 10;
                _propPAddr = baseAddr + 12;
                _nAttrs = 64;
            }
            else
            {
                Debug.Assert(false, "Unreachable");
            }
        }

        public void Dump()
        {
            _logger.Info($"{this}:");
            _logger.Debug($"\t  Parent [{ParentId}]");
            _logger.Debug($"\t  Sibling [{SiblingId}]");
            _logger.Debug($"\t  Child [{ChildId}]");
            _logger.Debug($"\t  Attributes [0x{Attributes:x8}]");
        }


        public override string ToString()
        {
            return $"GameObject [{Id}] [{ShortName}]";
        }


        public bool HasAttribute(ushort iAttr)
        {
            int iAttrByte = iAttr / 8;
            int iAttrBit = (_nAttrs - iAttr - 1) % 8;

            byte attrByte = _memory.ReadByte(_attrAddr + iAttrByte).Value;

            return (attrByte & (1 << iAttrBit)) == (1 << iAttrBit);
        }

        public void SetAttribute(ushort iAttr)
        {
            int iAttrByte = iAttr / 8;
            int iAttrBit = (_nAttrs - iAttr - 1) % 8;

            MemByte attrByte = _memory.ReadByte(_attrAddr + iAttrByte);

            attrByte |= (1 << iAttrBit);
            _memory.WriteByte(_attrAddr + iAttrByte, attrByte);
        }

        public void ClearAttribute(ushort iAttr)
        {
            int iAttrByte = iAttr / 8;
            int iAttrBit = (_nAttrs - iAttr - 1) % 8;

            MemByte attrByte = _memory.ReadByte(_attrAddr + iAttrByte);

            attrByte ^= (1 << iAttrBit);
            _memory.WriteByte(_attrAddr + iAttrByte, attrByte);
        }

        /**
         * As per spec 12.4
         */
        public MemWord? GetPropertyAddress(ushort targetPropId)
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

                propAddr += propLen;
            }
        }

        public static MemByte GetPropertyLength(ZMemory memory, MemWord propAddr)
        {
            MemByte sizeByte = memory.ReadByte(propAddr - 1);
            Debug.Assert(sizeByte != 0x00);

            return ((sizeByte >> 5) + 1);
        }

        public MemValue? GetPropertyValue(ushort targetPropId)
        {
            MemWord? propAddr = GetPropertyAddress(targetPropId);
            if (propAddr == null)
                return null;

            MemByte sizeByte = _memory.ReadByte(propAddr - 1); //go back 1 to size header
            ushort propLen = ((sizeByte >> 5) + 1).Value;

            if (propLen == 1)
                return _memory.ReadByte(propAddr);
            else if (propLen == 2)
                return _memory.ReadWord(propAddr);
            else
                throw new ArgumentException("GetPropertyValue called when propLen > 2");
        }

        public void PutPropertyValue(ushort targetPropId, MemValue propValue)
        {
            MemWord? propAddr = GetPropertyAddress(targetPropId);
            if (propAddr == null)
                throw new ArgumentException("PutPropertyValue called for nonexistent property");

            MemByte sizeByte = _memory.ReadByte(propAddr - 1); //go back 1 to size header
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
        
        
        public MemWord? GetNextPropertyId(ushort startPropId)
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
                if (propAddr == null)
                    throw new ArgumentException("GetNextPropertyId called for nonexistent property"); //as per spec

                sizeByte = _memory.ReadByte(propAddr - 1); //go back 1 to size header
                ushort propLen = ((sizeByte >> 5) + 1).Value;

                propAddr += propLen; //skip to next property
            }


            sizeByte = _memory.ReadByte(propAddr);
            if (sizeByte == 0x00)
                return null;

            ushort targetPropId = (sizeByte & 0b00011111).Value;
            return new MemWord(targetPropId);
        }



        public void DetachFromParent()
        {
            if (this.ParentId.FullValue == 0x00)
            {
                Debug.Assert(this.SiblingId.FullValue == 0x00, "Object is in inconsistent state");
                return;
            }

            GameObject? oldParent = _memory.FindObject(this.ParentId);
            Debug.Assert(oldParent != null);
            if (oldParent.ChildId == this.Id)
                oldParent.ChildId = this.SiblingId;
            else
            {
                GameObjectId candSiblingId = oldParent.ChildId;
                while (candSiblingId.FullValue != 0x00)
                {
                    GameObject? candSibling = _memory.FindObject(candSiblingId);
                    Debug.Assert(candSibling != null);
                    if (candSibling.SiblingId == this.Id)
                    {
                        candSibling.SiblingId = this.SiblingId;
                        break;
                    }
                    else
                    {
                        candSiblingId = candSibling.SiblingId;
                    }
                }
            }

            this.ParentId = _memory.MakeObjectId(0x00);
            this.SiblingId = _memory.MakeObjectId(0x00);
        }


        public void AttachToParent(GameObjectId targetParentId)
        {
            Debug.Assert(ParentId.FullValue == 0x00 && SiblingId.FullValue == 0x00, 
                "Object must be DetachedFromParent before it can be attached to another parent/sibiling");

            GameObject? targetParent = _memory.FindObject(targetParentId);
            Debug.Assert(targetParent != null);

            this.SiblingId = targetParent.ChildId;
            targetParent.ChildId = this.Id;
            this.ParentId = targetParentId;
        }
    }


    public record ObjProperty
    {
        public GameObjectId Id;
        public byte Size;
        public uint Data;

        public ObjProperty(GameObjectId id, byte size, uint data)
        {
            Id = id;
            Size = size;
            Data = data;
        }

        public override string ToString()
        {
            return $"[{Id}] [{Data:x}]";
        }
    }


}