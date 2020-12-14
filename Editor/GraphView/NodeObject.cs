using System.Collections.Generic;
using UnityEngine;

namespace MomomaAssets
{

    public class NodeObject : ScriptableObject
    {
        [SerializeField]
        string m_Guid = "";
        public string guid => m_Guid;

        [SerializeField]
        Rect m_Position = Rect.zero;
        public Rect position => m_Position;

        [SerializeField]
        List<string> m_InputPortGuids = new List<string>();
        public string[] inputPortGuids => m_InputPortGuids.ToArray();

        [SerializeField]
        List<string> m_OutputPortGuids = new List<string>();
        public string[] outputPortGuids => m_OutputPortGuids.ToArray();

        [SerializeField]
        List<float> m_FloatValues = new List<float>();
        public float[] floatValues => m_FloatValues.ToArray();

        [SerializeField]
        List<int> m_IntegerValues = new List<int>();
        public int[] integerValues => m_IntegerValues.ToArray();

        [SerializeField]
        List<bool> m_BoolValues = new List<bool>();
        public bool[] boolValues => m_BoolValues.ToArray();

        [SerializeField]
        List<string> m_StringValues = new List<string>();
        public string[] stringValues => m_StringValues.ToArray();

        [SerializeField]
        List<Object> m_ObjectRederenceValues = new List<Object>();
        public Object[] objectReferenceValues => m_ObjectRederenceValues.ToArray();

        [SerializeField]
        List<Vector4> m_Vector4Values = new List<Vector4>();
        public Vector4[] vector4Values => m_Vector4Values.ToArray();

        [SerializeField]
        List<AnimationCurve> m_AnimationCurveValues = new List<AnimationCurve>();
        public AnimationCurve[] animationCurveValues => m_AnimationCurveValues.ToArray();

        void Awake()
        {
            hideFlags = HideFlags.HideAndDontSave;
        }
    }

}
