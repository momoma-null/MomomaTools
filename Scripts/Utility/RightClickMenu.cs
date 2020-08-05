#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace MomomaAssets.Utility
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

}// namespace MomomaAssets.Utility
#endif
