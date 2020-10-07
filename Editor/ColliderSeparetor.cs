#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

public class ColliderSeparator
{
	[MenuItem("CONTEXT/Collider/Separate")]
	private static void Separate(MenuCommand menuCommand)
	{
		var srcColl = menuCommand.context as Collider;
		
		var name = "Coll_" + srcColl.name;
		name = GameObjectUtility.GetUniqueNameForSibling(srcColl.transform, name);
		var childGO = new GameObject(name);
		childGO.transform.parent = srcColl.transform;
		childGO.transform.localPosition = Vector3.zero;
		childGO.transform.localRotation = Quaternion.identity;
		childGO.transform.localScale = Vector3.one;
		Undo.RegisterCreatedObjectUndo(childGO, "Create Collider Object");

		var dstColl = childGO.AddComponent(srcColl.GetType());
		EditorUtility.CopySerialized(srcColl, dstColl);
		Undo.DestroyObjectImmediate(srcColl);
	}

	[MenuItem("CONTEXT/Collider/Separate", validate = true)]
	private static bool SeparateValidation(MenuCommand menuCommand)
	{
		var srcColl = menuCommand.context as Collider;
		if (!srcColl.GetComponent<MeshRenderer>())
  			return false;
		var meshFilter = srcColl.GetComponent<MeshFilter>();
		if (!meshFilter || !(srcColl is MeshCollider))
			return true;
		return (meshFilter.sharedMesh != ((MeshCollider)srcColl).sharedMesh);
	}
}

}// namespace MomomaAssets
#endif
