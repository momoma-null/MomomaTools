using UnityEngine;

namespace MomomaAssets
{
    internal sealed class ColorValueObject : FieldValueObjectBase<Color> { }
    [System.Serializable]
    internal sealed class ColorValue : FieldValue<Color> { public ColorValue(Color defaultValue) : base(defaultValue) { } }
}
