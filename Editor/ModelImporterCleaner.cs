using System.Linq;
using System.Collections.Generic;
using UnityEditor;

namespace MomomaAssets
{
    sealed class ModelImporterCleaner
    {
        [MenuItem("MomomaTools/Cleanup Model Importer")]
        static void Remove()
        {
            var guids = AssetDatabase.FindAssets("t:Model", new[] { "Assets/" });
            foreach (var guid in guids)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path);
                using (var so = new SerializedObject(importer))
                using (var m_ExternalObjects = so.FindProperty("m_ExternalObjects"))
                using (var m_Materials = so.FindProperty("m_Materials"))
                {
                    var externalObjects = new Dictionary<(string, string), int>();
                    for (var i = 0; i < m_ExternalObjects.arraySize; ++i)
                    {
                        using (var element = m_ExternalObjects.GetArrayElementAtIndex(i))
                            externalObjects.Add((element.FindPropertyRelative("first.name").stringValue, element.FindPropertyRelative("first.type").stringValue), i);
                    }
                    for (var i = 0; i < m_Materials.arraySize; ++i)
                    {
                        using (var element = m_Materials.GetArrayElementAtIndex(i))
                            externalObjects.Remove((element.FindPropertyRelative("name").stringValue, element.FindPropertyRelative("type").stringValue));
                    }
                    foreach (var i in externalObjects.Values.OrderByDescending(val => val))
                    {
                        m_ExternalObjects.DeleteArrayElementAtIndex(i);
                    }
                    if (so.ApplyModifiedPropertiesWithoutUndo())
                        importer.SaveAndReimport();
                }
            }
        }
    }
}
