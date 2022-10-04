using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ZAnGian
{
    public class ZStack
    {
        private Stack<RoutineData> _routineStack = new();
        private Stack<MemWord> _valueStack = new();

        private RoutineData CurrRoutineData { get => _routineStack.Peek(); }


        public void Push(MemWord value)
        {
            _valueStack.Push(value);
        }

        public MemWord Pop()
        {
            return _valueStack.Pop();
        }

        public void PushRoutine(RoutineData newRoutine)
        {
            _routineStack.Push(newRoutine);
        }

        public RoutineData PopRoutine()
        {
            return _routineStack.Pop();
        }

        public void WriteLocalVariable(GameVariableId varId, MemWord value)
        {
            CurrRoutineData.SetLocalVariable(varId, value);
        }

        public MemWord ReadLocalVariable(GameVariableId varId)
        {
            return CurrRoutineData.GetLocalVariable(varId);
        }
    }


    public class RoutineData
    {
        private List<MemWord> _localVariables = new();
        public MemWord ReturnAddress;
        public GameVariableId ReturnVariableId;


        public void AddLocalVariable(MemWord value)
        {
            _localVariables.Add(value);
        }

        public MemWord GetLocalVariable(GameVariableId varId)
        {
            return _localVariables[varId-1];
        }

        public void SetLocalVariable(GameVariableId varId, MemWord value)
        {
            _localVariables[varId-1] = value;        }
    }
}