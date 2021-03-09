using UnityEngine;
using UnityEditor;

namespace MomomaAssets.Extensions
{
    public static class SerializedPropertyExtension
    {
        public static void AddArrayElement(this SerializedProperty dst, SerializedProperty src)
        {
            if (!src.isArray || !dst.isArray)
            {
                Debug.LogAssertion("Serialized Property is not array");
                return;
            }

            var dstSO = dst.serializedObject;
            dstSO.Update();

            for (int k = 0; k < src.arraySize; k++)
            {
                var srcSP = src.GetArrayElementAtIndex(k);
                dst.arraySize += 1;
                dst.MoveArrayElement(dst.arraySize - 1, k);
                dstSO.CopyFromSerializedProperty(srcSP);
                dst.MoveArrayElement(k, dst.arraySize - 1);
            }

            dstSO.ApplyModifiedProperties();
        }

        public static void AddArrayElementRelative(this SerializedProperty dst, SerializedProperty src)
        {
            if (!src.isArray || !dst.isArray)
            {
                Debug.LogAssertion("Serialized Property is not array");
                return;
            }

            var dstSO = dst.serializedObject;
            dstSO.Update();

            for (int k = 0; k < src.arraySize; k++)
            {
                var srcSP = src.GetArrayElementAtIndex(k);
                var endProperty = srcSP.GetEndProperty();
                dst.arraySize += 1;
                var dstSP = dst.GetArrayElementAtIndex(dst.arraySize - 1);
                while (srcSP.NextVisible(true) && dstSP.NextVisible(true))
                {
                    switch (srcSP.propertyType)
                    {
                        case SerializedPropertyType.Boolean:
                            dstSP.boolValue = srcSP.boolValue;
                            break;
                        case SerializedPropertyType.Integer:
                            dstSP.intValue = srcSP.intValue;
                            break;
                        case SerializedPropertyType.Float:
                            dstSP.floatValue = srcSP.floatValue;
                            break;
                        case SerializedPropertyType.String:
                            dstSP.stringValue = srcSP.stringValue;
                            break;
                        case SerializedPropertyType.Color:
                            dstSP.colorValue = srcSP.colorValue;
                            break;
                        case SerializedPropertyType.ObjectReference:
                            dstSP.objectReferenceValue = srcSP.objectReferenceValue;
                            break;
                        case SerializedPropertyType.Enum:
                            dstSP.enumValueIndex = srcSP.enumValueIndex;
                            break;
                        default:
                            break;
                    }
                    if (SerializedProperty.EqualContents(srcSP, endProperty))
                        break;
                }
            }

            dstSO.ApplyModifiedProperties();
        }

        public static void AddEmptyArrayElement(this SerializedProperty sp)
        {
            using (var eSO = new SerializedObject(sp.serializedObject.targetObject))
            using (var eSP = eSO.FindProperty(sp.propertyPath))
            {
                eSP.arraySize = 0;
                ++sp.arraySize;
                eSP.arraySize = sp.arraySize;
                sp.serializedObject.CopyFromSerializedProperty(eSP.GetArrayElementAtIndex(sp.arraySize - 1));
            }
        }
    }
}// namespace
