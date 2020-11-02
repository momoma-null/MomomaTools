using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

public class MissingReferenceFinder : EditorWindow
{
	List<SerializedProperty> propertyList = new List<SerializedProperty>();
	HashSet<Object> objectHS = new HashSet<Object>();
	Vector2 scrollPos = Vector2.zero;

	// Generate menu tab
	[MenuItem("MomomaTools/MissingReferenceFinder")]
    static void ShowWindow()
    {
        EditorWindow.GetWindow<MissingReferenceFinder>("MissingReferenceFinder");
    }

	/*
	private void OnHierarchyChange()
	{
		propertyList = new List<SerializedProperty>();
	}
	*/

	private void OnGUI()
    {
        if (GUILayout.Button("Find Missing Reference"))
		{
			FindAllMissingReference();
		}

		EditorGUILayout.Space();

		using(var scrollView = new EditorGUILayout.ScrollViewScope(scrollPos))
		{
			scrollPos = scrollView.scrollPosition;
			foreach (var property in propertyList)
			{
				var obj = property.serializedObject.targetObject;
				if (obj == null)
					continue;

				using(new EditorGUILayout.HorizontalScope())
				{
					using(new EditorGUI.DisabledGroupScope(true))
					{
						EditorGUILayout.ObjectField("", obj, typeof(Object), true);
					}
					GUILayout.Label(property.propertyPath);
					GUILayout.FlexibleSpace();
				}
			}
		}
	}

	void FindAllMissingReference()
	{
		propertyList = new List<SerializedProperty>();
		objectHS = new HashSet<Object>();
		var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
		var gos = scene.GetRootGameObjects();
		try
		{
			foreach (var go in gos)
			{
				var comps = go.GetComponentsInChildren<Component>(true);
				foreach (var comp in comps)
				{
					if (comp != null)
						FindMissingReference(comp);
				}
			}
		}
		finally
		{
			EditorUtility.ClearProgressBar();
		}
	}

	void FindMissingReference(Object obj)
	{
		if (!objectHS.Add(obj))
			return;

		EditorUtility.DisplayProgressBar("Search Objects", objectHS.Count.ToString() + " " + obj.GetType().Name + " " + obj, 0);

		var sp = new SerializedObject(obj).GetIterator();
		while (sp.NextVisible(true))
		{
			if (sp.propertyType == SerializedPropertyType.ObjectReference)
			{
				var value = sp.objectReferenceValue;
				if (value == null && sp.objectReferenceInstanceIDValue != 0)
					propertyList.Add(sp.Copy());
				else if (value != null)
					FindMissingReference(value);
			}
		}
	}
}

}// namespace MomomaAssets
