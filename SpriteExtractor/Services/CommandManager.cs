using System;
using System.Collections.Generic;

namespace SpriteExtractor.Services
{
    public interface ICommand
    {
        void Execute();
        void Undo();
        string Description { get; }
    }

    public class DelegateCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Action _undo;
        public string Description { get; }

        public DelegateCommand(Action execute, Action undo, string description = "")
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _undo = undo ?? throw new ArgumentNullException(nameof(undo));
            Description = description;
        }

        public void Execute() => _execute();
        public void Undo() => _undo();
    }

    public class CommandManager
    {
        private readonly Stack<ICommand> _undoStack = new Stack<ICommand>();
        private readonly Stack<ICommand> _redoStack = new Stack<ICommand>();
        private readonly int _maxHistory;

        public event Action StateChanged;

        // new: detailed operation event so listeners know what happened (Execute/Undo/Redo/Clear)
        public enum OperationType { Execute, Undo, Redo, Clear }
        public event Action<OperationType> OperationPerformed;

        public CommandManager(int maxHistory = 100)
        {
            _maxHistory = Math.Max(1, maxHistory);
        }

        public bool CanUndo => _undoStack.Count > 0;
        public bool CanRedo => _redoStack.Count > 0;

        public void ExecuteCommand(ICommand cmd)
        {
            if (cmd == null) return;
            cmd.Execute();
            _undoStack.Push(cmd);
            _redoStack.Clear();
            TrimHistory();
            StateChanged?.Invoke();
            OperationPerformed?.Invoke(OperationType.Execute);
        }

        public void Undo()
        {
            if (!CanUndo) return;
            var cmd = _undoStack.Pop();
            cmd.Undo();
            _redoStack.Push(cmd);
            StateChanged?.Invoke();
            OperationPerformed?.Invoke(OperationType.Undo);
        }

        public void Redo()
        {
            if (!CanRedo) return;
            var cmd = _redoStack.Pop();
            cmd.Execute();
            _undoStack.Push(cmd);
            StateChanged?.Invoke();
            OperationPerformed?.Invoke(OperationType.Redo);
        }

        public void Clear()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke();
            OperationPerformed?.Invoke(OperationType.Clear);
        }

        public void ClearHistoryOnly()
        {
            _undoStack.Clear();
            _redoStack.Clear();
            StateChanged?.Invoke();
            OperationPerformed?.Invoke(OperationType.Clear);
        }

        private void TrimHistory()
        {
            while (_undoStack.Count > _maxHistory)
                _undoStack.Pop();
        }
    }
}
