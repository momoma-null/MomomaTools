using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

public class UnityEventEditor : EditorWindow
{
	List<SerializedProperty> propertyList = new List<SerializedProperty>();
	
	// Generate menu tab
	[MenuItem("MomomaTools/UnityEventEditor")]
    static void ShowWindow()
    {
        EditorWindow.GetWindow<UnityEventEditor>("UnityEventEditor");
    }

	void OnEnable()
	{
		GetUnityEventProperty();
	}

	void OnSelectionChange()
	{
		GetUnityEventProperty();
	}

	void OnGUI()
    {
        foreach (var prop in propertyList)
		{
			if (!prop.serializedObject.targetObject)
				continue;
			prop.serializedObject.Update();
			EditorGUILayout.PropertyField(prop);
			prop.serializedObject.ApplyModifiedProperties();
		}
	}

	void GetUnityEventProperty()
	{
		propertyList = new List<SerializedProperty>();
		var go = Selection.activeGameObject;
		if (!go)
			return;
		
		var comps = go.GetComponents<Component>();
		foreach (var comp in comps)
		{
			var so = new SerializedObject(comp);
			var sp = so.GetIterator();
			while(sp.NextVisible(true))
			{
				if (sp.FindPropertyRelative("m_PersistentCalls.m_Calls") != null)
				{
					propertyList.Add(sp.Copy());
				}
			}
		}
	}
}

}// namespace MomomaAssets
