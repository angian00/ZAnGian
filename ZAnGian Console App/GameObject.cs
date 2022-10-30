using System;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;

namespace ZAnGian
{
    public abstract class GameObject
    {
        private static Logger _logger = Logger.GetInstance();


        public GameObjectId Id;
        protected ZMemory _memory;

        protected MemWord _attrAddr;
        protected MemWord _parentAddr;
        protected MemWord _siblingAddr;
        protected MemWord _childAddr;
        protected MemWord _propPAddr;
        protected int _nAttrs;


        public uint Attributes
        {
            get => _memory.ReadDWord(_attrAddr);
            set => _memory.WriteDWord(_attrAddr, value);
        }

        public abstract GameObjectId ParentId { get; set; }
        public abstract GameObjectId ChildId { get; set; }
        public abstract GameObjectId SiblingId { get; set; }

        public string ShortName
        {
            get
            {
                MemWord snAddr = _memory.ReadWord(_propPAddr);
                byte textLen = _memory.ReadByte(snAddr).Value;
                string value = Zscii.DecodeText(_memory, snAddr + 1, out _, (ushort)(2 * textLen));

                return value;
            }
        }

        public abstract MemWord? GetPropertyAddress(ushort targetPropId);
        //public static abstract MemByte GetPropertyLength(ZMemory memory, MemWord propAddr);

        public abstract MemValue? GetPropertyValue(ushort targetPropId);
        public abstract void PutPropertyValue(ushort targetPropId, MemValue propValue);
        public abstract MemWord? GetNextPropertyId(ushort startPropId);


        [MemberNotNull(nameof(_parentAddr))]
        [MemberNotNull(nameof(_childAddr))]
        [MemberNotNull(nameof(_siblingAddr))]
        [MemberNotNull(nameof(_propPAddr))]
        public abstract void SetAddresses(MemWord baseAddr);


        protected GameObject(GameObjectId iObj, MemWord baseAddr, ZMemory memory)
        {
            Id = iObj;
            _memory = memory;
            _attrAddr = baseAddr;
            SetAddresses(baseAddr);
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

}