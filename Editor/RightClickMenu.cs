using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{
    public static class RightClickMenu
    {
        public static void Show(Rect rect, GenericMenu menu)
        {
            var evt = Event.current;
            if (rect.Contains(evt.mousePosition) && evt.type == EventType.ContextClick)
            {
                menu.ShowAsContext();
                evt.Use();
            }
        }
    }
}// namespace
