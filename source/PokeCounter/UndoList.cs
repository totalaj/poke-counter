using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PokeCounter
{
    interface IUndoObject<T>
    {
        public T GetCopy();
    }

    class UndoList<T> where T : IUndoObject<T>
    {
        List<T> list = new List<T>();
        int backtrace = 0;
        int undoCap = 100;

        int BacktraceIndex => list.Count - backtrace - 1;

        public bool CanUndo => BacktraceIndex > 0;
        public bool CanRedo => backtrace > 0;

        public void Clear()
        {
            backtrace = 0;
            list.Clear();
        }

        public void PushChange(T newState)
        {
            if (backtrace > 0)
            {
                list.RemoveRange(BacktraceIndex + 1, backtrace);
                backtrace = 0;
            }

            list.Add(newState.GetCopy());
            if (list.Count - 1 > undoCap)
            {
                list.RemoveAt(0);
            }
        }

        public bool Undo(ref T state)
        {
            if (backtrace >= list.Count) return false;

            backtrace++;

            state = list[BacktraceIndex].GetCopy();

            return true;
        }

        public bool Redo(ref T state)
        {
            if (backtrace <= 0) return false;

            backtrace--;

            state = list[BacktraceIndex].GetCopy();

            return true;
        }

    }
}
