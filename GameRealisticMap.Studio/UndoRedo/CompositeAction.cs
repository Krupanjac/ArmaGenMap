using System.Collections.Generic;
using System.Linq;
using Gemini.Modules.UndoRedo;

namespace GameRealisticMap.Studio.UndoRedo
{
    public class CompositeAction : IUndoableAction
    {
        private readonly List<IUndoableAction> _actions;

        public CompositeAction(IEnumerable<IUndoableAction> actions, string name)
        {
            _actions = actions.ToList();
            Name = name;
        }

        public string Name { get; }

        public void Execute()
        {
            foreach (var action in _actions)
            {
                action.Execute();
            }
        }

        public void Undo()
        {
            for (int i = _actions.Count - 1; i >= 0; i--)
            {
                _actions[i].Undo();
            }
        }
    }
}
