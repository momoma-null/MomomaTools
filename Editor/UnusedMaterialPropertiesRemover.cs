using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

    public class UnusedMaterialPropertiesRemover : Editor
    {
        [MenuItem("MomomaTools/RemoveUnusedProperties")]
        static void Remove()
        {
            var mats = Resources.FindObjectsOfTypeAll<Material>();
            foreach (var mat in mats)
            {
                var so = new SerializedObject(mat);
                so.Update();

                var savedProp = so.FindProperty("m_SavedProperties");
                RemoveProperties(savedProp.FindPropertyRelative("m_TexEnvs"), mat);
                RemoveProperties(savedProp.FindPropertyRelative("m_Floats"), mat);
                RemoveProperties(savedProp.FindPropertyRelative("m_Colors"), mat);

                so.ApplyModifiedProperties();
            }
        }

        static void RemoveProperties(SerializedProperty props, Material mat)
        {
            for (int i = props.arraySize - 1; i >= 0; i--)
            {
                var name = props.GetArrayElementAtIndex(i).FindPropertyRelative("first").stringValue;
                if (!mat.HasProperty(name))
                    props.DeleteArrayElementAtIndex(i);
            }
        }
    }

}// namespace MomomaAssets
