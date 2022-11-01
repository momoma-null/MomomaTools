using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{
    sealed class ReflectionProbeRotator
    {
        [MenuItem("CONTEXT/ReflectionProbe/Turn Right")]
        static void TurnRight(MenuCommand menuCommand)
        {
            if(!(menuCommand.context is ReflectionProbe reflectionProbe))
                return;
            using(var so = new SerializedObject(reflectionProbe))
            using(var boxSizeProperty = so.FindProperty("m_BoxSize"))
            using(var boxOffsetProperty = so.FindProperty("m_BoxOffset"))
            {
                var bounds = new Bounds(boxOffsetProperty.vector3Value, boxSizeProperty.vector3Value);
                var min = bounds.min;
                var max = bounds.max;
                min = new Vector3(min.z, min.y, -min.x);
                max = new Vector3(max.z, max.y, -max.x);
                bounds.SetMinMax(min, max);
                boxSizeProperty.vector3Value = bounds.size;
                boxOffsetProperty.vector3Value = bounds.center;
                so.ApplyModifiedProperties();
            }
        }
    }
}
