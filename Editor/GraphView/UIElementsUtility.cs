using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{

    static class UIElementsUtility
    {
        internal static VisualElement CreateLabeledElement(string lablel, VisualElement element)
        {
            var horizontalElement = new VisualElement() { style = { flexDirection = FlexDirection.Row } };
            element.style.flexGrow = 1f;
            horizontalElement.Add(new Label(lablel));
            horizontalElement.Add(element);
            return horizontalElement;
        }
    }

    class SliderWithFloatField : VisualElement
    {
        readonly FloatField floatField;
        readonly Slider slider;

        internal float value
        {
            get { return slider.value; }
            set { slider.value = value; }
        }

        internal SliderWithFloatField(float start, float end, float initial, Action<float> valueChanged, Action<float> endValueChanged)
        {
            slider = new Slider(start, end, valueChanged) { style = { flexGrow = 1f } };
            floatField = new FloatField() { style = { flexGrow = 0.8f, flexShrink = 1f } };
            slider.OnValueChanged(e => floatField.value = e.newValue);
            floatField.OnValueChanged(e => slider.value = e.newValue);
            slider.RegisterCallback<MouseUpEvent>(e => { if (e.button == 0) endValueChanged.Invoke(value); });
            floatField.RegisterCallback<FocusOutEvent>(e => endValueChanged.Invoke(value));
            slider.value = initial;
            floatField.value = initial;
            style.flexDirection = FlexDirection.Row;
            Add(slider);
            Add(floatField);
        }
    }

}
