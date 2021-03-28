namespace MomomaAssets
{
    internal sealed class FloatValueObject : FieldValueObjectBase<float> { }
    [System.Serializable]
    internal sealed class FloatValue : FieldValue<float> { public FloatValue(float defaultValue) : base(defaultValue) { } }
}
