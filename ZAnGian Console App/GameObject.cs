using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;

namespace ZAnGian
{
    public class GameObject
    {
        public const MemWord MAX_N_OBJ_PROPS = 31;

        private static Logger _logger = Logger.GetInstance();


        public GameObjectId Id;
        private ZMemory _memory;

        private readonly MemWord _attrAddr;
        private readonly MemWord _parentAddr;
        private readonly MemWord _siblingAddr;
        private readonly MemWord _childAddr;
        private readonly MemWord _propPAddr;


        public uint Attributes
        {
            get => _memory.ReadNBytes(4, _attrAddr);
            set => _memory.WriteNBytes(4, _attrAddr, value);
        }

        public GameObjectId ParentId
        {
            get => _memory.ReadByte(_parentAddr);
            set => _memory.WriteByte(_parentAddr, value);
        }

        public GameObjectId SiblingId
        {
            get => _memory.ReadByte(_siblingAddr);
            set => _memory.WriteByte(_siblingAddr, value);
        }

        public GameObjectId ChildId
        {
            get => _memory.ReadByte(_childAddr);
            set => _memory.WriteByte(_childAddr, value);
        }

        public string ShortName
        {
            get
            {
                MemWord snAddr = _memory.ReadWord(_propPAddr);
                byte textLen = _memory.ReadByte(snAddr);
                string value = Zscii.DecodeText(_memory.Data, (MemWord)(snAddr + 1), out _, (ushort)(2 * textLen));

                return value;
            }
        }


        public GameObject(GameObjectId iObj, MemWord baseAddr, ZMemory memory)
        {
            Id = iObj;
            _memory = memory;

            _attrAddr = baseAddr;
            _parentAddr = (MemWord)(baseAddr + 4);
            _siblingAddr = (MemWord)(baseAddr + 5);
            _childAddr = (MemWord)(baseAddr + 6);
            _propPAddr = (MemWord)(baseAddr + 7);

            //TODO: properties
        }

        public void Dump()
        {
            _logger.Info($"{this}:");
            _logger.Debug($"\t  Parent [{ParentId}]");
            _logger.Debug($"\t  Sibling [{SiblingId}]");
            _logger.Debug($"\t  Child [{ChildId}]");
            _logger.Debug($"\t  Attributes [0x{Attributes:x8}]");
            /*
            _logger.Debug($"\t  Properties");
            List<byte> sortedPKeys = new List<byte>(Properties.Keys);
            sortedPKeys.Sort();
            foreach (byte pId in sortedPKeys)
                Console.WriteLine($"\t\t  {Properties[pId]}");
            */
        }


        public override string ToString()
        {
            return $"GameObject [{Id}] [{ShortName}]";
        }


        public void DetachFromParent()
        {
            if (this.ParentId == 0x00)
            {
                Debug.Assert(this.SiblingId == 0x00, "Object is in inconsistent state");
                return;
            }

            //CHECK
            GameObject oldParent = _memory.FindObject(this.ParentId);
            if (oldParent.ChildId == this.Id)
                oldParent.ChildId = this.SiblingId;
            else
            {
                GameObjectId candSiblingId = oldParent.ChildId;
                while (candSiblingId != 0x00)
                {
                    GameObject candSibling = _memory.FindObject(candSiblingId);
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

            this.ParentId = 0x00;
            this.SiblingId = 0x00;
        }


        public void AttachToParent(GameObjectId targetParentId)
        {
            Debug.Assert(ParentId == 0x00 && SiblingId == 0x00, 
                "Object must be DetachedFromParent before it can be attached to another parent/sibiling");

            GameObject targetParent = _memory.FindObject(targetParentId);

            this.SiblingId = targetParent.ChildId;
            targetParent.ChildId = this.Id;
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