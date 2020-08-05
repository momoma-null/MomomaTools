#if UNITY_EDITOR
using System.Linq;
using UnityEditor;

namespace MomomaAssets.Utility
{

public static class SerializedObjectExtension
{
	public static void CopySerializedObject(this SerializedObject dst, SerializedObject src)
	{
		dst.CopySerializedObject(src, null);
	}

	public static void CopySerializedObject(this SerializedObject dst, SerializedObject src, string[] exclusions, bool canUndo = true)
	{
		src.Update();
		dst.Update();

		var sp = src.GetIterator();
		sp.Next(true);
		while(true)
		{
			if (exclusions == null || !exclusions.Contains(sp.name))
				dst.CopyFromSerializedProperty(sp);
			if(!sp.Next(false))
				break;
		}

		if (canUndo)
			dst.ApplyModifiedProperties();
		else
			dst.ApplyModifiedPropertiesWithoutUndo();
	}
}

}// namespace MomomaAssets.Utility
#endif
