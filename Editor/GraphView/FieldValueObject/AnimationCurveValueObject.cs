using UnityEngine;

namespace MomomaAssets
{
    internal sealed class AnimationCurveValueObject : FieldValueObjectBase<AnimationCurve> { }
    [System.Serializable]
    internal sealed class AnimationCurveValue : FieldValue<AnimationCurve> { public AnimationCurveValue(AnimationCurve defaultValue) : base(defaultValue) { } }
}
