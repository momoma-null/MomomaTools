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

    public class SliderWithFloatField : VisualElement, INotifyValueChanged<float>
    {
        readonly FloatField floatField;
        readonly Slider slider;

        public float value
        {
            get { return slider.value; }
            set { slider.value = value; }
        }

        public void OnValueChanged(EventCallback<ChangeEvent<float>> callback)
        {
            slider.OnValueChanged(callback);
        }

        public void RemoveOnValueChanged(EventCallback<ChangeEvent<float>> callback)
        {
            slider.RemoveOnValueChanged(callback);
        }

        public void SetValueAndNotify(float newValue)
        {
            value = newValue;
        }

        public void SetValueWithoutNotify(float newValue)
        {
            floatField.SetValueWithoutNotify(newValue);
            slider.SetValueWithoutNotify(newValue);
        }

        public void BindProperty(SerializedProperty property)
        {
            floatField.BindProperty(property);
            slider.BindProperty(property);
        }

        internal SliderWithFloatField(float start, float end, float initial)
        {
            slider = new Slider(start, end) { style = { flexGrow = 1f } };
            floatField = new FloatField() { style = { flexGrow = 0.8f, flexShrink = 1f } };
            slider.OnValueChanged(e => floatField.value = e.newValue);
            floatField.OnValueChanged(e => slider.value = e.newValue);
            slider.value = initial;
            floatField.value = initial;
            style.flexDirection = FlexDirection.Row;
            Add(slider);
            Add(floatField);
        }
    }

    public class EnumPopupField<T> : BaseField<int> where T : struct, Enum
    {
        internal string[] m_Choices;
        protected TextElement m_TextElement;

        public override int value
        {
            get { return base.value; }
            set
            {
                if (value < 0 || m_Choices.Length <= value)
                {
                    throw new ArgumentException(string.Format("Value {0} is not present in the list of possible values", value));
                }
                base.value = value;
            }
        }

        public override void SetValueWithoutNotify(int newValue)
        {
            if (newValue < 0 || m_Choices.Length <= newValue)
            {
                throw new ArgumentException(string.Format("Value {0} is not present in the list of possible values", newValue));
            }
            base.SetValueWithoutNotify(newValue);
            m_TextElement.text = m_Choices[newValue];
        }

        public T enumValue
        {
            get
            {
                Enum.TryParse<T>(m_Choices[value], out T result);
                return result;
            }
            set
            {
                this.value = Array.IndexOf(m_Choices, Enum.GetName(typeof(T), enumValue));
            }
        }

        protected EnumPopupField()
        {
            m_TextElement = new TextElement();
            m_TextElement.pickingMode = PickingMode.Ignore;
            Add(m_TextElement);
            AddToClassList("popupField");
            m_Choices = Enum.GetNames(typeof(T));
        }

        public EnumPopupField(T defaultValue) : this()
        {
            if (!Enum.IsDefined(typeof(T), defaultValue))
                throw new ArgumentException(string.Format("Default value {0} is not present in the list of possible values", defaultValue));
            SetValueWithoutNotify(Array.IndexOf(m_Choices, Enum.GetName(typeof(T), defaultValue)));
        }

        public EnumPopupField(List<T> choices, int defaultIndex) : this()
        {
            if (defaultIndex < 0 || m_Choices.Length <= defaultIndex)
                throw new ArgumentException(string.Format("Default Index {0} is beyond the scope of possible value", defaultIndex));
            value = defaultIndex;
        }

        protected override void ExecuteDefaultActionAtTarget(EventBase evt)
        {
            base.ExecuteDefaultActionAtTarget(evt);

            if (((evt as MouseDownEvent)?.button == (int)MouseButton.LeftMouse) ||
                ((evt.GetEventTypeId() == KeyDownEvent.TypeId()) && ((evt as KeyDownEvent)?.character == '\n') || ((evt as KeyDownEvent)?.character == ' ')))
            {
                ShowMenu();
                evt.StopPropagation();
            }
        }

        void ShowMenu()
        {
            var menu = new GenericMenu();
            foreach (var name in m_Choices)
            {
                menu.AddItem(new GUIContent(name), name == m_Choices[value], () => value = Array.IndexOf(m_Choices, name));
            }
            menu.DropDown(worldBound);
        }
    }

}
