using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

public class HideFlagsEditor : EditorWindow
{
	Object targetObj;
	
	// Generate menu tab
	[MenuItem("MomomaTools/HideFlagsEditor")]
    static void ShowWindow()
    {
        EditorWindow.GetWindow<HideFlagsEditor>("HideFlagsEditor");
    }

	void OnGUI()
    {
        targetObj = EditorGUILayout.ObjectField(targetObj, typeof(Object), true);
		if (!targetObj)
			return;
		
		var so = new SerializedObject(targetObj);
		var hideFlagsSP = so.FindProperty("m_ObjectHideFlags");
		hideFlagsSP.intValue = (int)(HideFlags)EditorGUILayout.EnumPopup("Hide Flags", (HideFlags)hideFlagsSP.intValue);
		switch(hideFlagsSP.intValue)
		{
			case (int)HideFlags.None :
			case (int)HideFlags.DontSaveInBuild :
			case (int)HideFlags.NotEditable :
				so.ApplyModifiedProperties();
				break;
		}

		if (targetObj is GameObject)
		{
			if (GUILayout.Button("Reset all Components' Hide Flags"))
			{
				var comps = ((GameObject)targetObj).GetComponents<Component>();
				foreach (var comp in comps)
				{
					var compSO = new SerializedObject(comp);
					compSO.FindProperty("m_ObjectHideFlags").intValue = (int)HideFlags.None;
					compSO.ApplyModifiedProperties();
				}
			}
		}
	}
}

}// namespace MomomaAssets
