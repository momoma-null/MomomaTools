using UnityEngine;

namespace MomomaAssets
{
    internal sealed class Vector4ValueObject : FieldValueObjectBase<Vector4> { }
    [System.Serializable]
    internal sealed class Vector4Value : FieldValue<Vector4> { public Vector4Value(Vector4 defaultValue) : base(defaultValue) { } }
}
