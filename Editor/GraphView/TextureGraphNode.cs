using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;
using UnityEngine.Experimental.UIElements.StyleSheets;
using MomomaAssets.Utility;

namespace MomomaAssets
{

    interface ISerializableNode : ISerializationCallbackReceiver
    {
        string guid { get; }
        Rect serializePosition { get; }
        string[] inputPortGuids { get; }
        string[] outputPortGuids { get; }

        void ReloadGuids();
    }

    [Serializable]
    class TextureGraphNode : Node, ISerializableNode
    {
        static readonly Color s_BackgroundColor = new Color(0.1803922f, 0.1803922f, 0.1803922f, 0.8039216f);

        TextureGraph m_Graph;
        protected TextureGraph graph => m_Graph ?? (m_Graph = GetFirstAncestorOfType<TextureGraph>());

        [SerializeField]
        string m_Guid;
        public string guid => m_Guid;
        [SerializeField]
        Rect m_SerializePosition;
        public Rect serializePosition => m_SerializePosition;
        [SerializeField]
        List<string> m_InputPortGuids = new List<string>();
        public string[] inputPortGuids => m_InputPortGuids.ToArray();
        [SerializeField]
        List<string> m_OutputPortGuids = new List<string>();
        public string[] outputPortGuids => m_OutputPortGuids.ToArray();

        protected TextureGraphNode() : base()
        {
            style.maxWidth = 150f;
            extensionContainer.style.backgroundColor = s_BackgroundColor;
            m_Guid = persistenceKey;
            var scheduleItem = schedule.Execute(() => m_SerializePosition = GetPosition());
            scheduleItem.Until(() => !float.IsNaN(m_SerializePosition.width));
        }

        public void ReloadGuids()
        {
            m_InputPortGuids = inputContainer.Query<Port>().ToList().Select(p => p.persistenceKey).ToList();
            m_OutputPortGuids = outputContainer.Query<Port>().ToList().Select(p => p.persistenceKey).ToList();
            m_Guid = persistenceKey;
        }

        public virtual void OnAfterDeserialize()
        {
            SetPosition(m_SerializePosition);
        }

        public virtual void OnBeforeSerialize()
        {

        }

        public override void BuildContextualMenu(ContextualMenuPopulateEvent evt)
        {
            evt.menu.AppendAction("Remove from Scope",
             (action) => this.GetContainingScope()?.RemoveElement(this),
             (action) => this.GetContainingScope() == null ? DropdownMenu.MenuAction.StatusFlags.Hidden : DropdownMenu.MenuAction.StatusFlags.Normal);
            base.BuildContextualMenu(evt);
        }

        public override void SetPosition(Rect newPos)
        {
            if (selected)
            {
                newPos.x = Mathf.Round(newPos.x * 0.1f) * 10f;
                newPos.y = Mathf.Round(newPos.y * 0.1f) * 10f;
            }
            base.SetPosition(newPos);
            m_SerializePosition = newPos;
        }

        public override void UpdatePresenterPosition()
        {
            base.UpdatePresenterPosition();
            m_SerializePosition = GetPosition();
        }

        internal virtual void Process() { }

        bool IsProcessed()
        {
            foreach (var port in outputContainer.Query<Port>().ToList())
            {
                if (graph.processData.ContainsKey(port))
                    return true;
            }
            foreach (var port in inputContainer.Query<Port>().ToList())
            {
                if (graph.processData.ContainsKey(port))
                    return true;
            }
            return false;
        }

        protected void GetInput<T>(ref T[] inputValue, string portName = null)
        {
            var port = inputContainer.Q<Port>(portName);
            foreach (var edge in port.connections)
            {
                if (edge.output == null)
                    continue;
                var outPort = edge.output;
                while (!(outPort.node is TextureGraphNode))
                {
                    var token = outPort.node as TokenNode;
                    foreach (var e in token.input.connections)
                    {
                        outPort = e.output;
                    }
                }
                var graphNode = outPort.node as TextureGraphNode;
                if (!graphNode.IsProcessed())
                    graphNode.Process();
                var rawData = graph.processData[outPort];
                if (rawData is T[] rawTData)
                {
                    inputValue = rawTData;
                }
                else
                {
                    var castedData = new List<T>();
                    foreach (var i in (rawData as IEnumerable))
                    {
                        castedData.Add(TextureGraph.AssignTo<T>(i));
                    }
                    inputValue = castedData.ToArray();
                }
            }
        }

        protected Port AddInputPort<T>(string portName = null)
        {
            var port = Port.Create<TextureGraphEdge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(T));
            port.portName = portName;
            inputContainer.Add(port);
            m_InputPortGuids.Add(port.persistenceKey);
            return port;
        }

        protected Port AddOutputPort<T>(string portName = null)
        {
            var port = Port.Create<TextureGraphEdge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(T));
            port.portName = portName;
            outputContainer.Add(port);
            m_OutputPortGuids.Add(port.persistenceKey);
            return port;
        }
    }

    [Serializable]
    class SerializableTokenNode : TokenNode, ISerializableNode
    {
        [SerializeField]
        string m_Guid;
        public string guid => m_Guid;
        [SerializeField]
        Rect m_SerializePosition;
        public Rect serializePosition => m_SerializePosition;
        [SerializeField]
        List<string> m_InputPortGuids = new List<string>();
        public string[] inputPortGuids => m_InputPortGuids.ToArray();
        [SerializeField]
        List<string> m_OutputPortGuids = new List<string>();
        public string[] outputPortGuids => m_OutputPortGuids.ToArray();

        [SerializeField]
        string m_InputType;
        [SerializeField]
        string m_OutputType;

        SerializableTokenNode() : this(Port.Create<TextureGraphEdge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, null), Port.Create<TextureGraphEdge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, null)) { }
        internal SerializableTokenNode(Port input, Port output) : base(input, output)
        {
            m_Guid = persistenceKey;
            m_InputPortGuids.Add(input.persistenceKey);
            m_OutputPortGuids.Add(output.persistenceKey);
            m_InputType = input.portType?.AssemblyQualifiedName;
            m_OutputType = output.portType?.AssemblyQualifiedName;
            if (input.portType == null || output.portType == null)
            {
                var typeScheduleItem = schedule.Execute(() => SetPortType());
                typeScheduleItem.Until(() => input.portType != null && output.portType != null);
            }
            var scheduleItem = schedule.Execute(() => m_SerializePosition = GetPosition());
            scheduleItem.Until(() => !float.IsNaN(m_SerializePosition.width));
        }

        void SetPortType()
        {
            if (!string.IsNullOrEmpty(m_InputType))
            {
                input.portType = Type.GetType(m_InputType);
            }
            if (!string.IsNullOrEmpty(m_OutputType))
            {
                output.portType = Type.GetType(m_OutputType);
            }
        }

        public void ReloadGuids()
        {
            m_InputPortGuids = new List<string>() { input.persistenceKey };
            m_OutputPortGuids = new List<string>() { output.persistenceKey };
            m_Guid = persistenceKey;
        }

        public virtual void OnAfterDeserialize()
        {
            SetPosition(m_SerializePosition);
        }

        public virtual void OnBeforeSerialize()
        {

        }

        public override void SetPosition(Rect newPos)
        {
            base.SetPosition(newPos);
            m_SerializePosition = newPos;
        }

        public override void UpdatePresenterPosition()
        {
            base.UpdatePresenterPosition();
            m_SerializePosition = GetPosition();
        }
    }

    class ImportTextureNode : TextureGraphNode
    {
        readonly ObjectField objectField;
        readonly Image image;

        int m_Width;
        int m_Height;

        [SerializeField]
        Texture2D m_ImageTexture;

        internal ImportTextureNode() : base()
        {
            title = "Import Texture";
            AddOutputPort<Vector4>("Color");
            RefreshPorts();
            objectField = new ObjectField() { objectType = typeof(Texture2D), style = { positionLeft = 0f, positionRight = 0f } };
            objectField[0].style.flexShrink = 1f;
            objectField[0][1].style.flexShrink = 1f;
            objectField.OnValueChanged(OnValueChanged);
            extensionContainer.Add(objectField);
            image = new Image() { scaleMode = ScaleMode.ScaleToFit, style = { positionLeft = 0f, positionRight = 0f, positionBottom = 0f } };
            extensionContainer.Add(image);
            RefreshExpandedState();
        }

        ~ImportTextureNode()
        {
            if (image.image.value != null)
                Texture.DestroyImmediate(image.image);
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            objectField.value = m_ImageTexture;
            schedule.Execute(() => ReloadTexture()).Until(() => graph != null);
        }

        void OnValueChanged(ChangeEvent<UnityEngine.Object> e)
        {
            m_ImageTexture = e.newValue as Texture2D;
            ReloadTexture();
            graph.MarkNordIsDirty();
        }

        void ReloadTexture()
        {
            if (graph == null)
                return;
            if (image.image.value != null)
                Texture.DestroyImmediate(image.image);
            m_Width = graph.width;
            m_Height = graph.height;
            var srcTexture = objectField.value as Texture2D;
            if (srcTexture == null)
            {
                image.image = null;
                return;
            }
            RenderTexture renderTexture;
            Material material;
            var newImage = new Texture2D(m_Width, m_Height, TextureFormat.RGBA32, false);
            using (var so = new SerializedObject(srcTexture))
            {
                var lightmapFormat = so.FindProperty("m_LightmapFormat").intValue;
                if (lightmapFormat == 3 || (lightmapFormat == 4 && srcTexture.format == TextureFormat.BC5)) // see TextureImporterEnums.cs and EditorGUI.cs at UnityCsReference
                {
                    material = s_NormalmapMaterial;
                    renderTexture = RenderTexture.GetTemporary(m_Width, m_Height, 0, RenderTextureFormat.ARGBFloat);
                }
                else
                {
                    material = s_TransparentMaterial;
                    material.SetColor("_ColorMask", Color.white);
                    renderTexture = RenderTexture.GetTemporary(m_Width, m_Height, 0, RenderTextureFormat.ARGB32);
                }
            }
            Graphics.Blit(srcTexture, renderTexture, material);
            newImage.ReadPixels(new Rect(0, 0, m_Width, m_Height), 0, 0, false);
            newImage.Apply();
            RenderTexture.ReleaseTemporary(renderTexture);
            image.image = newImage;
        }

        readonly static Material s_NormalmapMaterial = new Material(EditorGUIUtility.LoadRequired("Previews/PreviewEncodedNormals.shader") as Shader) { hideFlags = HideFlags.HideAndDontSave };
        readonly static Material s_TransparentMaterial = new Material(EditorGUIUtility.LoadRequired("Previews/PreviewTransparent.shader") as Shader) { hideFlags = HideFlags.HideAndDontSave };

        internal override void Process()
        {
            if (m_Width != graph.width || m_Height != graph.height)
                ReloadTexture();
            var port = outputContainer.Q<Port>();
            graph.processData[port] = (image.image.value as Texture2D)?.GetPixels().Select(c => (Vector4)c).ToArray() ?? new Vector4[m_Width * m_Height];
        }
    }

    class ExportTextureNode : TextureGraphNode
    {
        static readonly List<int> s_PopupValues = new List<int>() { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

        Color[] m_Colors;
        internal Color[] colors => m_Colors;

        readonly PopupField<int> widthPopupField;
        readonly PopupField<int> heightPopupField;

        [SerializeField]
        int m_WidthValue = s_PopupValues[6];
        [SerializeField]
        int m_HeightValue = s_PopupValues[6];

        internal ExportTextureNode() : base()
        {
            title = "Export Texture";
            capabilities = capabilities & ~Capabilities.Deletable;
            AddInputPort<Vector4>("Color");
            RefreshPorts();
            widthPopupField = new PopupField<int>(s_PopupValues, defaultValue: m_WidthValue) { name = "Width" };
            heightPopupField = new PopupField<int>(s_PopupValues, defaultValue: m_HeightValue) { name = "Height" };
            widthPopupField.OnValueChanged(OnWidthValueChanged);
            heightPopupField.OnValueChanged(OnHeightValueChanged);
            heightPopupField.SetEnabled(false);
            extensionContainer.Add(UIElementsUtility.CreateLabeledElement("Width", widthPopupField));
            extensionContainer.Add(UIElementsUtility.CreateLabeledElement("Height", heightPopupField));
            RefreshExpandedState();
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            widthPopupField.value = m_WidthValue;
            heightPopupField.value = m_HeightValue;
        }

        void OnWidthValueChanged(ChangeEvent<int> e)
        {
            heightPopupField.value = e.newValue;
            m_WidthValue = e.newValue;
            graph.MarkNordIsDirty();
        }

        void OnHeightValueChanged(ChangeEvent<int> e)
        {
            m_HeightValue = e.newValue;
            graph.MarkNordIsDirty();
        }

        internal override void Process()
        {
            var length = graph.width * graph.height;
            m_Colors = new Color[length];
            var vectors = new Vector4[length];
            GetInput<Vector4>(ref vectors);
            m_Colors = Array.ConvertAll(vectors, v => (Color)v);
        }
    }

    class DecomposeChannelsNode : TextureGraphNode
    {
        internal DecomposeChannelsNode() : base()
        {
            title = "Decompose Channels";
            AddInputPort<Vector4>("Color");
            var port = AddOutputPort<float>("R");
            port.name = "Red";
            port = AddOutputPort<float>("G");
            port.name = "Green";
            port = AddOutputPort<float>("B");
            port.name = "Blue";
            port = AddOutputPort<float>("A");
            port.name = "Alpha";
            RefreshPorts();
        }

        internal override void Process()
        {
            var length = graph.width * graph.height;
            var reds = new float[length];
            var greens = new float[length];
            var blues = new float[length];
            var alphas = new float[length];
            var vectors = new Vector4[length];
            GetInput<Vector4>(ref vectors);
            for (var i = 0; i < length; ++i)
            {
                reds[i] = vectors[i].x;
                greens[i] = vectors[i].y;
                blues[i] = vectors[i].z;
                alphas[i] = vectors[i].w;
            }
            var port = outputContainer.Q<Port>("Red");
            graph.processData[port] = reds;
            port = outputContainer.Q<Port>("Green");
            graph.processData[port] = greens;
            port = outputContainer.Q<Port>("Blue");
            graph.processData[port] = blues;
            port = outputContainer.Q<Port>("Alpha");
            graph.processData[port] = alphas;
        }
    }

    class CombineChannelsNode : TextureGraphNode
    {
        internal CombineChannelsNode() : base()
        {
            title = "Combine Channels";
            var port = AddInputPort<float>("R");
            port.name = "Red";
            port = AddInputPort<float>("G");
            port.name = "Green";
            port = AddInputPort<float>("B");
            port.name = "Blue";
            port = AddInputPort<float>("A");
            port.name = "Alpha";
            AddOutputPort<Vector4>("Color");
            RefreshPorts();
        }

        internal override void Process()
        {
            var length = graph.width * graph.height;
            var reds = new float[length];
            var greens = new float[length];
            var blues = new float[length];
            var alphas = new float[length];
            GetInput<float>(ref reds, "Red");
            GetInput<float>(ref greens, "Green");
            GetInput<float>(ref blues, "Blue");
            GetInput<float>(ref alphas, "Alpha");
            var port = outputContainer.Q<Port>();
            graph.processData[port] = reds.Select((r, i) => new Vector4(r, greens[i], blues[i], alphas[i])).ToArray();
        }
    }

    class ConstantColor : TextureGraphNode
    {
        readonly Vector4Field m_VectorField;

        [SerializeField]
        Vector4 m_Value = Vector4.one;

        internal ConstantColor() : base()
        {
            title = "Constant Color";
            AddOutputPort<Vector4>("Value");
            RefreshPorts();
            m_VectorField = new Vector4Field();
            m_VectorField.value = m_Value;
            m_VectorField.OnValueChanged(e => m_Value = e.newValue);
            extensionContainer.Add(m_VectorField);
            RefreshExpandedState();
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            m_VectorField.value = m_Value;
        }
    }

    class BlendNode : TextureGraphNode
    {
        enum BlendMode
        {
            Normal,
            Addition,
            Difference,
            Multiply,
            Screen,
            Overlay,
            HardLight,
            SoftLight,
            Dodge,
            Burn
        }

        readonly EnumField m_EnumField;
        readonly SliderWithFloatField m_Slider;

        [SerializeField]
        int m_BlendMode = (int)BlendMode.Normal;
        [SerializeField]
        float m_BlendValue = 1f;

        BlendNode() : base()
        {
            title = "Blend";
            var port = AddInputPort<Vector4>("A");
            port.name = "A";
            port = AddInputPort<Vector4>("B");
            port.name = "B";
            AddOutputPort<Vector4>("Out");
            RefreshPorts();
            m_EnumField = new EnumField((BlendMode)m_BlendMode);
            m_EnumField.OnValueChanged(e => m_BlendMode = (int)(BlendMode)e.newValue);
            m_EnumField.OnValueChanged(e => graph.MarkNordIsDirty());
            extensionContainer.Add(m_EnumField);
            m_Slider = new SliderWithFloatField(0f, 1f, m_BlendValue, value => m_BlendValue = value, value => graph.MarkNordIsDirty());
            extensionContainer.Add(m_Slider);
            RefreshExpandedState();
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            m_EnumField.value = (BlendMode)m_BlendMode;
            m_Slider.Query<BaseField<float>>().ForEach(f => f.value = m_BlendValue);
        }

        internal override void Process()
        {
            var length = graph.width * graph.height;
            var valueA = new Vector4[length];
            var valueB = new Vector4[length];
            var result = new Vector4[length];
            GetInput<Vector4>(ref valueA, "A");
            GetInput<Vector4>(ref valueB, "B");
            switch (m_EnumField.value)
            {
                case BlendMode.Normal:
                    result = valueA.Select((v, i) => BlendEachChannel(v, valueB[i], m_Slider.value, (a, b) => a)).ToArray();
                    break;
                case BlendMode.Addition:
                    result = valueA.Select((v, i) => BlendEachChannel(v, valueB[i], m_Slider.value, (a, b) => a + b)).ToArray();
                    break;
                case BlendMode.Difference:
                    result = valueA.Select((v, i) => BlendEachChannel(v, valueB[i], m_Slider.value, (a, b) => Math.Abs(a - b))).ToArray();
                    break;
                case BlendMode.Multiply:
                    result = valueA.Select((v, i) => BlendEachChannel(v, valueB[i], m_Slider.value, (a, b) => a * b)).ToArray();
                    break;
                case BlendMode.Screen:
                    result = valueA.Select((v, i) => BlendEachChannel(v, valueB[i], m_Slider.value, (a, b) => 1f - (1f - a) * (1f - b))).ToArray();
                    break;
                case BlendMode.Overlay:
                    result = valueA.Select((v, i) => BlendEachChannel(v, valueB[i], m_Slider.value, (a, b) => Overlay(a, b))).ToArray();
                    break;
                case BlendMode.HardLight:
                    result = valueA.Select((v, i) => BlendEachChannel(v, valueB[i], m_Slider.value, (a, b) => Overlay(b, a))).ToArray();
                    break;
                case BlendMode.SoftLight:
                    result = valueA.Select((v, i) => BlendEachChannel(v, valueB[i], m_Slider.value, (a, b) => SoftLight(a, b))).ToArray();
                    break;
                case BlendMode.Dodge:
                    result = valueA.Select((v, i) => BlendEachChannel(v, valueB[i], m_Slider.value, (a, b) => SafetyDivide(b, (1f - a)))).ToArray();
                    break;
                case BlendMode.Burn:
                    result = valueA.Select((v, i) => BlendEachChannel(v, valueB[i], m_Slider.value, (a, b) => 1f - SafetyDivide(1f - b, a))).ToArray();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("invalid enum value (BlendMode)");
            }
            var port = outputContainer.Q<Port>();
            graph.processData[port] = result;
        }

        static Vector4 BlendEachChannel(Vector4 a, Vector4 b, float blendValue, Func<float, float, float> blend)
        {
            float newAlpha = Mathf.Lerp(b.w, 1f, blendValue * a.w);
            return new Vector4(SafetyDivide(Mathf.Lerp(b.x, blend.Invoke(a.x, b.x * b.w), blendValue * a.w), newAlpha),
                               SafetyDivide(Mathf.Lerp(b.y, blend.Invoke(a.y, b.y * b.w), blendValue * a.w), newAlpha),
                               SafetyDivide(Mathf.Lerp(b.z, blend.Invoke(a.z, b.z * b.w), blendValue * a.w), newAlpha),
                               newAlpha);
        }

        static float SafetyDivide(float a, float b)
        {
            return b == 0 ? 0 : a / b;
        }

        static float Overlay(float a, float b)
        {
            return 0.5 < b ? b * a * 2f : 1f - (1f - b) * (1f - a) * 2f;
        }

        static float SoftLight(float a, float b)
        {
            return (1f - 2f * b) * a * a + 2f * b * a;
        }
    }

    class ConstantFloat : TextureGraphNode
    {
        readonly FloatField m_FloatField;

        [SerializeField]
        float m_Value = 1f;

        internal ConstantFloat() : base()
        {
            title = "Constant Float";
            AddOutputPort<float>("Value");
            RefreshPorts();
            m_FloatField = new FloatField();
            m_FloatField.value = m_Value;
            m_FloatField.OnValueChanged(e => m_Value = e.newValue);
            extensionContainer.Add(m_FloatField);
            RefreshExpandedState();
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            m_FloatField.value = m_Value;
        }
    }

    class MathNode : TextureGraphNode
    {
        enum CalculateMode
        {
            Add,
            Subtract,
            Multiply,
            Divide,
            Surplus,
            Reverse
        }

        readonly EnumField m_EnumField;

        [SerializeField]
        int m_CalculateMode = (int)CalculateMode.Add;

        internal MathNode() : base()
        {
            title = "Math";
            var port = AddInputPort<float>("A");
            port.name = "A";
            port = AddInputPort<float>("B");
            port.name = "B";
            AddOutputPort<float>("Out");
            RefreshPorts();
            m_EnumField = new EnumField((CalculateMode)m_CalculateMode);
            m_EnumField.OnValueChanged(e => m_CalculateMode = (int)(CalculateMode)e.newValue);
            extensionContainer.Add(m_EnumField);
            RefreshExpandedState();
        }

        public override void OnAfterDeserialize()
        {
            base.OnAfterDeserialize();
            m_EnumField.value = (CalculateMode)m_CalculateMode;
        }

        internal override void Process()
        {
            var length = graph.width * graph.height;
            var valueA = new float[length];
            var valueB = new float[length];
            var result = new float[length];
            GetInput<float>(ref valueA, "A");
            GetInput<float>(ref valueB, "B");
            switch (m_EnumField.value)
            {
                case CalculateMode.Add:
                    result = valueA.Select((v, i) => v + valueB[i]).ToArray();
                    break;
                case CalculateMode.Subtract:
                    result = valueA.Select((v, i) => v - valueB[i]).ToArray();
                    break;
                case CalculateMode.Multiply:
                    result = valueA.Select((v, i) => v * valueB[i]).ToArray();
                    break;
                case CalculateMode.Divide:
                    result = valueA.Select((v, i) => v / valueB[i]).ToArray();
                    break;
                case CalculateMode.Surplus:
                    result = valueA.Select((v, i) => v % valueB[i]).ToArray();
                    break;
                case CalculateMode.Reverse:
                    result = valueA.Select(v => 1f - v).ToArray();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("invalid enum value (CalculateMode)");
            }
            var port = outputContainer.Q<Port>();
            graph.processData[port] = result;
        }
    }

}
