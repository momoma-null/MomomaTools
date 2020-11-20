using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

    class ExtendedProperty
    {
        static Object s_Object;
        static Dictionary<string[], int> s_Enums = new Dictionary<string[], int>(new SequenceEqualityComparer<string>());
        static Vector2? s_Vector2;
        static Vector3? s_Vector3;
        static Vector4? s_Vector4;
        static Rect? s_Rect;
        static AnimationCurve s_AnimationCurve;
        static Bounds? s_Bounds;
        static Quaternion? s_Quaternion;

        [InitializeOnLoadMethod]
        static void Initialize()
        {
            EditorApplication.contextualPropertyMenu += OnMenu;
        }

        static void OnMenu(GenericMenu menu, SerializedProperty property)
        {
            property = property.Copy();
            switch (property.propertyType)
            {
                case SerializedPropertyType.ObjectReference:
                    menu.AddItem(new GUIContent("Copy Object"), false, () => s_Object = property.objectReferenceValue);
                    menu.AddItem(new GUIContent("Paste Object"), false, () =>
                    {
                        property.serializedObject.Update();
                        property.objectReferenceValue = s_Object;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                    break;
                case SerializedPropertyType.Enum:
                    menu.AddItem(new GUIContent("Copy Enum Value"), false, () => s_Enums[property.enumNames] = property.enumValueIndex);
                    if (s_Enums.ContainsKey(property.enumNames))
                    {
                        menu.AddItem(new GUIContent("Paste Enum Value"), false, () =>
                        {
                            property.serializedObject.Update();
                            property.enumValueIndex = s_Enums[property.enumNames];
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Paste Enum Value"));
                    }
                    break;
                case SerializedPropertyType.Vector2:
                    menu.AddItem(new GUIContent("Copy Vector2"), false, () => s_Vector2 = property.vector2Value);
                    if (s_Vector2 != null)
                    {
                        menu.AddItem(new GUIContent("Paste Vector2"), false, () =>
                        {
                            property.serializedObject.Update();
                            property.vector2Value = s_Vector2 ?? property.vector2Value;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Paste Vector2"));
                    }
                    break;
                case SerializedPropertyType.Vector3:
                    menu.AddItem(new GUIContent("Copy Vector3"), false, () => s_Vector3 = property.vector3Value);
                    if (s_Vector3 != null)
                    {
                        menu.AddItem(new GUIContent("Paste Vector3"), false, () =>
                        {
                            property.serializedObject.Update();
                            property.vector3Value = s_Vector3 ?? property.vector3Value;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Paste Vector3"));
                    }
                    break;
                case SerializedPropertyType.Vector4:
                    menu.AddItem(new GUIContent("Copy Vector4"), false, () => s_Vector4 = property.vector4Value);
                    if (s_Vector4 != null)
                    {
                        menu.AddItem(new GUIContent("Paste Vector4"), false, () =>
                        {
                            property.serializedObject.Update();
                            property.vector4Value = s_Vector4 ?? property.vector4Value;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Paste Vector4"));
                    }
                    break;
                case SerializedPropertyType.Rect:
                    menu.AddItem(new GUIContent("Copy Rect"), false, () => s_Rect = property.rectValue);
                    if (s_Rect != null)
                    {
                        menu.AddItem(new GUIContent("Paste Rect"), false, () =>
                        {
                            property.serializedObject.Update();
                            property.rectValue = s_Rect ?? property.rectValue;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Paste Rect"));
                    }
                    break;
                case SerializedPropertyType.AnimationCurve:
                    menu.AddItem(new GUIContent("Copy Animation Curve"), false, () => s_AnimationCurve = property.animationCurveValue);
                    menu.AddItem(new GUIContent("Paste Animation Curve"), false, () =>
                    {
                        property.serializedObject.Update();
                        property.animationCurveValue = s_AnimationCurve;
                        property.serializedObject.ApplyModifiedProperties();
                    });
                    break;
                case SerializedPropertyType.Bounds:
                    menu.AddItem(new GUIContent("Copy Bounds"), false, () => s_Bounds = property.boundsValue);
                    if (s_Bounds != null)
                    {
                        menu.AddItem(new GUIContent("Paste Bounds"), false, () =>
                        {
                            property.serializedObject.Update();
                            property.boundsValue = s_Bounds ?? property.boundsValue;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Paste Bounds"));
                    }
                    break;
                case SerializedPropertyType.Quaternion:
                    menu.AddItem(new GUIContent("Copy Quaternion"), false, () => s_Quaternion = property.quaternionValue);
                    if (s_Quaternion != null)
                    {
                        menu.AddItem(new GUIContent("Paste Quaternion"), false, () =>
                        {
                            property.serializedObject.Update();
                            property.quaternionValue = s_Quaternion ?? property.quaternionValue;
                            property.serializedObject.ApplyModifiedProperties();
                        });
                    }
                    else
                    {
                        menu.AddDisabledItem(new GUIContent("Paste Quaternion"));
                    }
                    break;
            }
        }

        class SequenceEqualityComparer<T> : IEqualityComparer<IEnumerable<T>>
        {
            public bool Equals(IEnumerable<T> x, IEnumerable<T> y)
            {
                return x?.SequenceEqual(y) ?? false;
            }

            public int GetHashCode(IEnumerable<T> obj)
            {
                return obj?.Max(t => t.GetHashCode()) ?? 0;
            }
        }
    }

}// namespace MomomaAssets
