using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ZAnGian
{
    public class ZStack
    {
        private Stack<RoutineData> _routineStack = new();

        private RoutineData CurrRoutineData { get => _routineStack.Peek(); }

        public ZStack()
        {
            _routineStack.Push(new RoutineData());
        }

        public void PushValue(MemWord value)
        {
            CurrRoutineData.PushValue(value);
        }

        public MemWord PopValue()
        {
            return CurrRoutineData.PopValue();
        }

        public void PushRoutine(RoutineData newRoutine)
        {
            _routineStack.Push(newRoutine);
        }

        public RoutineData PopRoutine()
        {
            //cannot pop main routine
            Debug.Assert(_routineStack.Count > 1);
                
            return _routineStack.Pop();
        }

        public void WriteLocalVariable(GameVariableId varId, MemWord value)
        {
            //cannot use local variables in main routine
            Debug.Assert(_routineStack.Count > 1);

            CurrRoutineData.SetLocalVariable(varId, value);
        }

        public MemWord ReadLocalVariable(GameVariableId varId)
        {
            //cannot use local variables in main routine
            Debug.Assert(_routineStack.Count > 1);

            return CurrRoutineData.GetLocalVariable(varId);
        }
    }


    public class RoutineData
    {
        private List<MemWord> _localVariables = new();
        private Stack<MemWord> _valueStack = new();

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

        public void SetLocalVariable(GameVariableId varId, ushort value)
        {
            SetLocalVariable(varId, new MemWord(value));
        }

        public void SetLocalVariable(GameVariableId varId, MemWord value)
        {
            _localVariables[varId - 1] = value;
        }

        public void PushValue(MemWord value)
        {
            _valueStack.Push(value);
        }

        public MemWord PopValue()
        {
            return _valueStack.Pop();
        }

    }
}