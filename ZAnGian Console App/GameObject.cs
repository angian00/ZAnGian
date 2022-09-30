using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;

namespace ZAnGian
{
    public class GameObject
    {
        public const MemWord MAX_N_OBJ_PROPS = 31;
 
        
        public ushort Id;
        public uint Attributes;
        public ushort Parent;
        public ushort Sibling;
        public ushort Child;
        public Dictionary<byte, ObjProperty> Properties = new ();
        public string ShortName;


        public void Dump()
        {
            Console.WriteLine($"{this}:");
            Console.WriteLine($"\t  Parent [{Parent}]");
            Console.WriteLine($"\t  Sibling [{Sibling}]");
            Console.WriteLine($"\t  Child [{Child}]");
            Console.WriteLine("");
            Console.WriteLine($"\t  Attributes [0x{Attributes:x8}]");
            Console.WriteLine($"\t  Properties");
            
            List<byte> sortedPKeys = new List<byte>(Properties.Keys);
            sortedPKeys.Sort();
            foreach (byte pId in sortedPKeys)
                Console.WriteLine($"\t\t  {Properties[pId]}");
        }


        public override string ToString()
        {
            return $"GameObject [{Id}] [{ShortName}]";
        }
    }

    public record ObjProperty
    {
        public byte Id;
        public byte Size;
        public uint Data;

        public ObjProperty(byte id, byte size, uint data)
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