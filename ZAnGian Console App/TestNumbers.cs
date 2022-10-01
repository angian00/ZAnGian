using System;
using System.Drawing;

namespace ZAnGian
{

    public struct TestNumber
    {
        public UInt16 Value { get; set; }


        public TestNumber(UInt16 val) { Value = val; }


        public static TestNumber operator +(TestNumber v1, TestNumber v2)
        {
            Console.WriteLine("Applying operator overload!");
            return new TestNumber() { Value = (UInt16)(v1.Value + v2.Value) };
        }

        public static TestNumber operator +(TestNumber v1, int v2)
        {
            Console.WriteLine("Applying (int) operator overload!");
            return new TestNumber() { Value = (UInt16)(v1.Value + v2) };
        }

        public static TestNumber operator +(TestNumber v1, uint v2)
        {
            Console.WriteLine("Applying (uint) operator overload!");
            return new TestNumber() { Value = (UInt16)(v1.Value + v2) };
        }

        public override string ToString()
        {
            return Value.ToString();
        }


        public static void Main(string[] args)
        {
            TestNumber tn1 = new TestNumber(1);
            TestNumber tn2 = new TestNumber(3);
            int myInt = 123;
            Console.WriteLine("Testing operator overload");
            Console.WriteLine($"tn1: {tn1} + tn2: {tn2} = {tn1+tn2}");
            Console.WriteLine($"tn1: {tn1} + int: {myInt} = {tn1+ myInt}");
        }
    }


    public class MyMemWord
    {
        private UInt16 _value;

        public MyMemWord(UInt16 val)
        {
            _value = val;
        }

        public MyMemWord(int val)
        {
            _value = (UInt16)val;
        }

        public static MyMemWord operator +(MyMemWord v1, int v2)
        {
            Console.WriteLine("Applying MyMemWord operator overload!");
            return new MyMemWord(v1._value + v2);
        }
    }
 }