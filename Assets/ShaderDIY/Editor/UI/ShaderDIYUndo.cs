using UnityEditor;
using UnityEngine;

namespace ShaderDIY.Editor
{
    public static class ShaderDIYUndo
    {
        static int _hot = -999;
        static bool _recorded;

        public static void Record(Object obj, string name)
        {
            if (obj == null)
                return;
            if (!Begin(name))
                return;
            Undo.RecordObject(obj, name);
        }

        public static void Record(Object a, Object b, string name)
        {
            if (!Begin(name))
                return;
            if (a != null)
                Undo.RecordObject(a, name);
            if (b != null)
                Undo.RecordObject(b, name);
        }

        static bool Begin(string name)
        {
            int hot = GUIUtility.hotControl;
            bool discrete = hot == 0;
            bool newDrag = hot != _hot;
            if (!discrete && !newDrag && _recorded)
                return false;

            if (discrete || newDrag)
            {
                Undo.IncrementCurrentGroup();
                Undo.SetCurrentGroupName(name);
                _hot = hot;
            }
            _recorded = !discrete;
            return true;
        }
    }
}
