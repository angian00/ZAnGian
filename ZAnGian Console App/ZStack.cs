using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace ZAnGian
{
    public class ZStack
    {
        private static Logger _logger = Logger.GetInstance();

        private Stack<RoutineData> _routineStack = new();

        public RoutineData CurrRoutineData { get => _routineStack.Peek(); }
        public Stack<RoutineData> RoutineStack { get => _routineStack; }

        public ZStack(bool addFirstFrame = true)
        {
            if (addFirstFrame)
                _routineStack.Push(new RoutineData());
        }


        public void Dump()
        {
            _logger.Debug($"-- routineStack");
            _logger.Debug("<Top>");
            int i = 0;
            foreach (RoutineData rData in _routineStack)
            {
                _logger.Debug($"\troutineStack item #{i}");
                rData.Dump();
                i++;
            }
            _logger.Debug("<Bottom>");
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
        private static Logger _logger = Logger.GetInstance();

        private List<MemWord> _localVariables = new();
        private Stack<MemWord> _valueStack = new();

        public List<MemWord> LocalVariables { get => _localVariables; }
        public Stack<MemWord> ValueStack { get => _valueStack; }

        public int NumArgs;
        public HighMemoryAddress ReturnAddress;
        public GameVariableId ReturnVariableId;
        public bool IgnoreReturnVariable = false;


        public void Dump()
        {
            _logger.Debug($"\t-- ReturnAddress: {ReturnAddress}");
            _logger.Debug($"\t-- ReturnVariableId: {ReturnVariableId}");

            _logger.Debug("\t-- localVariables");
            int i=0;
            foreach (MemWord localVar in _localVariables)
            {
                _logger.Debug($"\t\tlocalVar #{i}");
                _logger.Debug($"\t\t{localVar}");
                i++;
            }

            _logger.Debug("\t-- valueStack");
            _logger.Debug("\t<Top>");
            i=0;
            foreach (MemWord value in _valueStack)
            {
                _logger.Debug($"\t\tvalueStack item #{i}");
                _logger.Debug($"\t\t{value}");
                i++;
            }
            _logger.Debug("\t<Bottom>");
        }

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