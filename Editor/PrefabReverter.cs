using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Experimental.SceneManagement;

namespace MomomaAssets
{
    sealed class PrefabReverter : EditorWindow
    {
        static class Styles
        {
            public static GUIContent revertEqual = EditorGUIUtility.TrTextContent("Revert (Equal)");
            public static GUIContent revert = EditorGUIUtility.TrTextContent("Revert");
        }

        [SerializeField]
        Vector2 _scrollPos;
        [SerializeField]
        Object _targetObject;

        SortedDictionary<Object, IEnumerable<SerializedProperty>> _properties;

        [MenuItem("MomomaTools/Prefab Reverter", false, 800)]
        static void ShowWindow()
        {
            GetWindow<PrefabReverter>(ObjectNames.NicifyVariableName(nameof(PrefabReverter)));
        }

        void OnEnable()
        {
            GetModifiedProperties();
        }

        void OnDisable()
        {
            _properties = null;
            _targetObject = null;
        }

        void OnHierarchyChange()
        {
            GetModifiedProperties();
            Repaint();
        }

        void OnGUI()
        {
            using (var scrollView = new EditorGUILayout.ScrollViewScope(_scrollPos))
            {
                _scrollPos = scrollView.scrollPosition;
                foreach (var props in _properties)
                {
                    var key = props.Key;
                    var isFoldOut = EditorGUILayout.BeginFoldoutHeaderGroup(key == _targetObject, EditorGUIUtility.ObjectContent(key, key.GetType()));
                    EditorGUILayout.EndFoldoutHeaderGroup();
                    if (isFoldOut)
                    {
                        _targetObject = key;
                        if (_targetObject != null)
                        {
                            var sourceObject = PrefabUtility.GetCorrespondingObjectFromSource(_targetObject);
                            if (sourceObject != null)
                            {
                                using (var srcSO = new SerializedObject(sourceObject))
                                {
                                    foreach (var prop in props.Value)
                                    {
                                        using (new EditorGUILayout.HorizontalScope())
                                        {
                                            prop.serializedObject.Update();
                                            EditorGUILayout.PropertyField(prop);
                                            using (var srcSP = srcSO.FindProperty(prop.propertyPath))
                                            {
                                                var label = SerializedProperty.DataEquals(prop, srcSP) ? Styles.revertEqual : Styles.revert;
                                                if (GUILayout.Button(label))
                                                {
                                                    prop.prefabOverride = false;
                                                    prop.serializedObject.ApplyModifiedProperties();
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    else if (_targetObject == key)
                    {
                        _targetObject = null;
                    }
                }
            }
        }

        void GetModifiedProperties()
        {
            _properties = new SortedDictionary<Object, IEnumerable<SerializedProperty>>(new TypeComparer());
            var prefabStage = PrefabStageUtility.GetCurrentPrefabStage();
            var scene = EditorSceneManager.GetActiveScene();
            var rootGOs = prefabStage != null ? new GameObject[] { prefabStage.prefabContentsRoot } : scene.GetRootGameObjects();
            foreach (var go in rootGOs)
            {
                CollectModifications(go);
            }
        }

        void CollectModifications(GameObject target)
        {
            if (PrefabUtility.IsOutermostPrefabInstanceRoot(target))
            {
                var overrides = PrefabUtility.GetObjectOverrides(target);
                foreach (var objectOverride in overrides)
                {
                    var so = new SerializedObject(objectOverride.instanceObject);
                    var sp = so.GetIterator();
                    var properties = new List<SerializedProperty>();
                    var enterChildrren = true;
                    while (sp.Next(enterChildrren))
                    {
                        if (sp.prefabOverride && !sp.isDefaultOverride)
                        {
                            properties.Add(sp.Copy());
                            enterChildrren = false;
                        }
                        else
                        {
                            enterChildrren = true;
                        }
                    }
                    if (properties.Count > 0)
                        _properties[objectOverride.instanceObject] = properties;
                }
            }
            foreach (Transform child in target.transform)
                CollectModifications(child.gameObject);
        }

        class TypeComparer : IComparer<Object>
        {
            static string GetCode(Object o) => $"{o.GetType().Name}.{o.name}.{o.GetInstanceID()}";

            public int Compare(Object x, Object y)
            {
                return -string.Compare(GetCode(x), GetCode(y));
            }
        }
    }
}
