using System;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;
using UnityEditor;
using UnityEditor.Experimental.UIElements;

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

    public sealed class SliderWithFloatField : BaseField<float>
    {
        readonly FloatField m_FloatField;
        readonly Slider m_Slider;

        public SliderWithFloatField(float start, float end, float initial)
        {
            m_Slider = new Slider(start, end) { style = { flexGrow = 1f } };
            m_FloatField = new FloatField() { style = { flexGrow = 0.8f, flexShrink = 1f } };
            m_Slider.OnValueChanged(e => e.target = this);
            m_Slider.OnValueChanged(e => value = e.newValue);
            m_FloatField.OnValueChanged(e => e.target = this);
            m_FloatField.OnValueChanged(e => value = e.newValue);
            OnValueChanged(e => m_Slider.value = e.newValue);
            OnValueChanged(e => m_FloatField.value = e.newValue);
            value = initial;
            style.flexDirection = FlexDirection.Row;
            Add(m_Slider);
            Add(m_FloatField);
        }

        public override void SetValueWithoutNotify(float newValue)
        {
            base.SetValueWithoutNotify(newValue);
            m_Slider.SetValueWithoutNotify(newValue);
            m_FloatField.SetValueWithoutNotify(newValue);
        }
    }

    public sealed class EnumPopupField<T> : BaseField<int> where T : struct, Enum
    {
        string[] m_Choices;
        TextElement m_TextElement;

        public override int value
        {
            get => base.value;
            set
            {
                if (value < 0 || m_Choices.Length <= value)
                    throw new ArgumentException(string.Format("Value {0} is not present in the list of possible values", value));
                base.value = value;
            }
        }

        public override void SetValueWithoutNotify(int newValue)
        {
            if (newValue < 0 || m_Choices.Length <= newValue)
                throw new ArgumentException(string.Format("Value {0} is not present in the list of possible values", newValue));
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
            set => this.value = Array.IndexOf(m_Choices, Enum.GetName(typeof(T), enumValue));
        }

        EnumPopupField()
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

        public EnumPopupField(int defaultIndex) : this()
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
