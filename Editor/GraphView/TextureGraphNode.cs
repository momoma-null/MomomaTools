using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEngine.Experimental.UIElements.StyleEnums;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{
    abstract class TextureGraphNode : Node
    {
        TextureGraph m_Graph;
        protected TextureGraph graph => m_Graph ?? (m_Graph = GetFirstAncestorOfType<TextureGraph>());

        protected TextureGraphNode() : base()
        {
            style.maxWidth = 150f;
            extensionContainer.style.backgroundColor = new Color(0.1803922f, 0.1803922f, 0.1803922f, 0.8039216f);
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
        }

        protected abstract void Process();

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
                    inputValue = TextureGraph.AssignTo<T>(rawData);
                }
            }
        }

        protected Port AddInputPort<T>(string portName = null)
        {
            var port = Port.Create<TextureGraphEdge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(T));
            port.portName = portName;
            inputContainer.Add(port);
            return port;
        }

        protected Port AddOutputPort<T>(string portName = null)
        {
            var port = Port.Create<TextureGraphEdge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(T));
            port.portName = portName;
            outputContainer.Add(port);
            return port;
        }
    }

    public sealed class TokenNode<T> : TokenNode where T : Edge, new()
    {
        TokenNode() : this(
            Port.Create<T>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, null),
            Port.Create<T>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, null))
        { }

        public TokenNode(Port input, Port output) : base(input, output) { }
    }

    [NodeMenu("Input/Texture", typeof(TextureGraph))]
    class ImportTextureNode : TextureGraphNode, IBindableGraphElement<UnityEngine.Object>
    {
        public event Action<GraphElement> onValueChanged;

        readonly ObjectField m_ObjectField;
        readonly Image image;

        int m_Width;
        int m_Height;

        ImportTextureNode() : base()
        {
            title = "Import Texture";
            style.width = 136f;
            AddOutputPort<Vector4>("Color");
            RefreshPorts();
            m_ObjectField = new ObjectField() { objectType = typeof(Texture2D), style = { positionLeft = 0f, positionRight = 0f } };
            m_ObjectField.OnValueChanged(e => ReloadTexture());
            m_ObjectField.OnValueChanged(e => onValueChanged?.Invoke(this));
            m_ObjectField[0].style.flexShrink = 1f;
            m_ObjectField[0][1].style.flexShrink = 1f;
            extensionContainer.Add(m_ObjectField);
            image = new Image { scaleMode = ScaleMode.ScaleToFit, style = { positionLeft = 0f, positionRight = 0f, positionBottom = 0f, marginRight = 3f, marginLeft = 3f } };
            extensionContainer.Add(image);
            RefreshExpandedState();
        }

        ~ImportTextureNode()
        {
            if (image.image.value != null)
                Texture.DestroyImmediate(image.image);
        }

        public void Bind(SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = 2;
            m_ObjectField.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
        }

        public void Reset()
        {
            m_ObjectField.value = null;
        }

        void ReloadTexture()
        {
            if (graph == null)
                return;
            if (image.image.value != null)
                Texture.DestroyImmediate(image.image);
            m_Width = graph.width;
            m_Height = graph.height;
            var srcTexture = m_ObjectField.value as Texture2D;
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
            var oldRT = RenderTexture.active;
            RenderTexture.active = renderTexture;
            Graphics.Blit(srcTexture, renderTexture, material);
            newImage.ReadPixels(new Rect(0, 0, m_Width, m_Height), 0, 0, false);
            newImage.Apply();
            RenderTexture.active = oldRT;
            renderTexture.Release();
            RenderTexture.ReleaseTemporary(renderTexture);
            image.image = newImage;
        }

        readonly static Material s_NormalmapMaterial = new Material(EditorGUIUtility.LoadRequired("Previews/PreviewEncodedNormals.shader") as Shader) { hideFlags = HideFlags.HideAndDontSave };
        readonly static Material s_TransparentMaterial = new Material(EditorGUIUtility.LoadRequired("Previews/PreviewTransparent.shader") as Shader) { hideFlags = HideFlags.HideAndDontSave };

        protected override void Process()
        {
            if (m_Width != graph.width || m_Height != graph.height)
                ReloadTexture();
            var port = outputContainer.Q<Port>();
            graph.processData[port] = (image.image.value as Texture2D)?.GetPixels().Select(c => (Vector4)c).ToArray() ?? new Vector4[m_Width * m_Height];
        }
    }

    class ExportTextureNode : TextureGraphNode, IBindableGraphElement<int>
    {
        public event Action<GraphElement> onValueChanged;

        static readonly List<int> s_PopupValues = new List<int>() { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

        Color[] m_Colors;
        internal Color[] colors => m_Colors.ToArray();

        readonly PopupField<int> m_WidthPopupField;
        readonly PopupField<int> m_HeightPopupField;

        internal ExportTextureNode() : base()
        {
            title = "Export Texture";
            capabilities &= ~Capabilities.Deletable;
            AddInputPort<Vector4>("Color");
            RefreshPorts();
            m_WidthPopupField = new PopupField<int>(s_PopupValues, defaultValue: s_PopupValues[6]) { name = "Width" };
            m_HeightPopupField = new PopupField<int>(s_PopupValues, defaultValue: s_PopupValues[6]) { name = "Height" };
            m_HeightPopupField.SetEnabled(false);
            m_WidthPopupField.OnValueChanged(OnWidthValueChanged);
            m_WidthPopupField.OnValueChanged(e => onValueChanged?.Invoke(this));
            extensionContainer.Add(UIElementsUtility.CreateLabeledElement("Width", m_WidthPopupField));
            extensionContainer.Add(UIElementsUtility.CreateLabeledElement("Height", m_HeightPopupField));
            RefreshExpandedState();
        }

        void OnWidthValueChanged(ChangeEvent<int> e)
        {
            m_HeightPopupField.value = e.newValue;
        }

        public void Bind(SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = 2;
            var widthSP = arrayProperty.GetArrayElementAtIndex(0);
            if (!s_PopupValues.Contains(widthSP.intValue))
                widthSP.intValue = s_PopupValues[6];
            var heightSP = arrayProperty.GetArrayElementAtIndex(1);
            if (!s_PopupValues.Contains(heightSP.intValue))
                heightSP.intValue = s_PopupValues[6];
            m_WidthPopupField.BindProperty(widthSP);
            m_HeightPopupField.BindProperty(heightSP);
        }

        public void Reset()
        {
            m_WidthPopupField.value = s_PopupValues[6];
            m_HeightPopupField.value = s_PopupValues[6];
        }

        public void Process(int width, int height)
        {
            var vectors = new Vector4[width * height];
            GetInput<Vector4>(ref vectors);
            m_Colors = Array.ConvertAll(vectors, v => (Color)v);
        }

        protected override void Process()
        {
            Process(m_WidthPopupField.value, m_HeightPopupField.value);
        }
    }

    [NodeMenu("Color/Decompose", typeof(TextureGraph))]
    class DecomposeChannelsNode : TextureGraphNode
    {
        DecomposeChannelsNode() : base()
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

        protected override void Process()
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

    [NodeMenu("Color/Combine", typeof(TextureGraph))]
    class CombineChannelsNode : TextureGraphNode
    {
        CombineChannelsNode() : base()
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

        protected override void Process()
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

    [NodeMenu("Color/Constant", typeof(TextureGraph))]
    class ConstantColor : TextureGraphNode, IBindableGraphElement<Vector4>
    {
        public event Action<GraphElement> onValueChanged;

        readonly Vector4Field m_VectorField;

        ConstantColor() : base()
        {
            title = "Constant Color";
            AddOutputPort<Vector4>("Value");
            RefreshPorts();
            m_VectorField = new Vector4Field() { style = { flexWrap = Wrap.NoWrap } };
            m_VectorField.OnValueChanged(e => onValueChanged?.Invoke(this));
            extensionContainer.Add(m_VectorField);
            RefreshExpandedState();
        }

        public void Bind(SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = 1;
            m_VectorField.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
        }

        public void Reset()
        {
            m_VectorField.value = Vector4.one;
        }

        protected override void Process()
        {
            var port = outputContainer.Q<Port>();
            graph.processData[port] = Enumerable.Repeat(m_VectorField.value, graph.width * graph.height).ToArray();
        }
    }

    [NodeMenu("Color/Blend", typeof(TextureGraph))]
    class BlendNode : TextureGraphNode, IBindableGraphElement<int>, IBindableGraphElement<float>
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

        public event Action<GraphElement> onValueChanged;

        readonly EnumPopupField<BlendMode> m_EnumField;
        readonly SliderWithFloatField m_Slider;

        BlendNode() : base()
        {
            title = "Blend";
            var port = AddInputPort<Vector4>("A");
            port.name = "A";
            port = AddInputPort<Vector4>("B");
            port.name = "B";
            AddOutputPort<Vector4>("Out");
            RefreshPorts();
            m_EnumField = new EnumPopupField<BlendMode>(BlendMode.Normal);
            m_Slider = new SliderWithFloatField(0f, 1f, 1f);
            m_EnumField.OnValueChanged(e => onValueChanged?.Invoke(this));
            m_Slider.OnValueChanged(e => onValueChanged?.Invoke(this));
            extensionContainer.Add(m_EnumField);
            extensionContainer.Add(m_Slider);
            RefreshExpandedState();
        }

        void IBindableGraphElement<int>.Bind(SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = 1;
            m_EnumField.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
        }

        void IBindableGraphElement<float>.Bind(SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = 1;
            m_Slider.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
        }

        public void Reset()
        {
            m_EnumField.enumValue = BlendMode.Normal;
            m_Slider.value = 1f;
        }

        protected override void Process()
        {
            var length = graph.width * graph.height;
            var valueA = new Vector4[length];
            var valueB = new Vector4[length];
            var result = new Vector4[length];
            GetInput<Vector4>(ref valueA, "A");
            GetInput<Vector4>(ref valueB, "B");
            switch (m_EnumField.enumValue)
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
                    throw new ArgumentOutOfRangeException("BlendMode");
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

    [NodeMenu("Single/Constant", typeof(TextureGraph))]
    class ConstantFloat : TextureGraphNode, IBindableGraphElement<float>
    {
        public event Action<GraphElement> onValueChanged;

        readonly FloatField m_FloatField;

        internal ConstantFloat() : base()
        {
            title = "Constant Float";
            AddOutputPort<float>("Value");
            RefreshPorts();
            m_FloatField = new FloatField();
            m_FloatField.OnValueChanged(e => onValueChanged?.Invoke(this));
            extensionContainer.Add(m_FloatField);
            RefreshExpandedState();
        }

        public void Bind(SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = 1;
            m_FloatField.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
        }

        public void Reset()
        {
            m_FloatField.value = 1f;
        }

        protected override void Process()
        {
            var v = m_FloatField.value;
            var port = outputContainer.Q<Port>();
            graph.processData[port] = Enumerable.Repeat(new Vector4(v, v, v, v), graph.width * graph.height).ToArray();
        }
    }

    [NodeMenu("Single/Math", typeof(TextureGraph))]
    class MathNode : TextureGraphNode, IBindableGraphElement<int>
    {
        enum CalculateMode
        {
            Add,
            Subtract,
            Multiply,
            Divide,
            Surplus
        }

        public event Action<GraphElement> onValueChanged;

        readonly EnumPopupField<CalculateMode> m_EnumField;

        internal MathNode() : base()
        {
            title = "Math";
            var port = AddInputPort<float>("A");
            port.name = "A";
            port = AddInputPort<float>("B");
            port.name = "B";
            AddOutputPort<float>("Out");
            RefreshPorts();
            m_EnumField = new EnumPopupField<CalculateMode>(CalculateMode.Add);
            m_EnumField.OnValueChanged(e => onValueChanged?.Invoke(this));
            extensionContainer.Add(m_EnumField);
            RefreshExpandedState();
        }

        public void Bind(SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = 1;
            m_EnumField.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
        }

        public void Reset()
        {
            m_EnumField.enumValue = CalculateMode.Add;
        }

        protected override void Process()
        {
            var length = graph.width * graph.height;
            var valueA = new float[length];
            var valueB = new float[length];
            var result = new float[length];
            GetInput<float>(ref valueA, "A");
            GetInput<float>(ref valueB, "B");
            switch (m_EnumField.enumValue)
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
                default:
                    throw new ArgumentOutOfRangeException("CalculateMode");
            }
            var port = outputContainer.Q<Port>();
            graph.processData[port] = result;
        }
    }

    [NodeMenu("Color/Bump", typeof(TextureGraph))]
    class BumpMapNode : TextureGraphNode, IBindableGraphElement<int>
    {
        enum BumpMapType
        {
            Normal,
            Height
        }

        public event Action<GraphElement> onValueChanged;

        readonly EnumPopupField<BumpMapType> m_EnumField;

        BumpMapNode() : base()
        {
            title = "Bump Map";
            AddInputPort<Vector4>();
            AddOutputPort<Vector4>();
            RefreshPorts();
            m_EnumField = new EnumPopupField<BumpMapType>(BumpMapType.Normal);
            m_EnumField.OnValueChanged(e => onValueChanged?.Invoke(this));
            extensionContainer.Add(m_EnumField);
            RefreshExpandedState();
        }

        public void Bind(SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = 1;
            m_EnumField.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
        }

        public void Reset()
        {
            m_EnumField.enumValue = BumpMapType.Normal;
        }

        protected override void Process()
        {
            var width = graph.width;
            var height = graph.height;
            var length = width * height;
            var inputs = new Vector4[length];
            var outputs = new Vector4[length];
            GetInput<Vector4>(ref inputs);
            switch (m_EnumField.enumValue)
            {
                case BumpMapType.Normal:
                    var heights = inputs.Select(v => ConvertToHeight(v).x).ToArray();
                    outputs = heights.Select((h, i) => ConvertToNormal(heights[(i - width + length) % length], heights[(i + width) % length], heights[(i + 1) - (i % width == width - 1 ? width : 0)], heights[(i - 1) + (i % width == 0 ? width : 0)], width, height)).ToArray();
                    break;
                case BumpMapType.Height:
                    outputs = inputs.Select(ConvertToHeight).ToArray();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("BumpMapType");
            }
            var port = outputContainer.Q<Port>();
            graph.processData[port] = outputs;
        }

        Vector4 ConvertToHeight(Vector4 v)
        {
            float height = (v.x + v.y + v.z) / 3f;
            return new Vector4(height, height, height, 1f);
        }

        Vector4 ConvertToNormal(float u, float b, float r, float l, float width, float height)
        {
            var vertical = new Vector3(100f / width, 0, (b - u)).normalized;
            var horizontal = new Vector3(0, 100f / height, (r - l)).normalized;
            var normal = Vector3.Cross(vertical, horizontal).normalized;
            return new Vector4(normal.y * 0.5f + 0.5f, normal.x * 0.5f + 0.5f, normal.z * 0.5f + 0.5f, 1f);
        }
    }

    [NodeMenu("Color/Tone Curve", typeof(TextureGraph))]
    class ToneCurveNode : TextureGraphNode, IBindableGraphElement<AnimationCurve>
    {
        public event Action<GraphElement> onValueChanged;

        readonly CurveField m_RCurveField;
        readonly CurveField m_GCurveField;
        readonly CurveField m_BCurveField;
        readonly CurveField m_ACurveField;

        ToneCurveNode() : base()
        {
            title = "Tone Curve";
            AddInputPort<Vector4>("Color");
            AddOutputPort<Vector4>("Color");
            RefreshPorts();
            m_RCurveField = new CurveField() { ranges = new Rect(0f, 0f, 1f, 1f) };
            m_GCurveField = new CurveField() { ranges = new Rect(0f, 0f, 1f, 1f) };
            m_BCurveField = new CurveField() { ranges = new Rect(0f, 0f, 1f, 1f) };
            m_ACurveField = new CurveField() { ranges = new Rect(0f, 0f, 1f, 1f) };
            m_GCurveField.SetEnabled(false);
            m_BCurveField.SetEnabled(false);
            m_RCurveField.OnValueChanged(e => m_GCurveField.value = e.newValue);
            m_RCurveField.OnValueChanged(e => m_BCurveField.value = e.newValue);
            m_RCurveField.OnValueChanged(e => onValueChanged?.Invoke(this));
            m_ACurveField.OnValueChanged(e => onValueChanged?.Invoke(this));
            extensionContainer.Add(m_RCurveField);
            extensionContainer.Add(m_GCurveField);
            extensionContainer.Add(m_BCurveField);
            extensionContainer.Add(m_ACurveField);
            RefreshExpandedState();
        }

        public void Bind(SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = 4;
            m_RCurveField.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
            m_GCurveField.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
            m_BCurveField.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
            m_ACurveField.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
        }

        public void Reset()
        {
            m_RCurveField.value = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            m_GCurveField.value = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            m_BCurveField.value = AnimationCurve.Linear(0f, 0f, 1f, 1f);
            m_ACurveField.value = AnimationCurve.Linear(0f, 0f, 1f, 1f);
        }

        protected override void Process()
        {
            var length = graph.width * graph.height;
            var inputs = new Vector4[length];
            GetInput<Vector4>(ref inputs);
            var outputs = inputs.Select(v => new Vector4(m_RCurveField.value.Evaluate(v.x), m_GCurveField.value.Evaluate(v.y), m_BCurveField.value.Evaluate(v.z), m_ACurveField.value.Evaluate(v.w))).ToArray();
            var port = outputContainer.Q<Port>();
            graph.processData[port] = outputs;
        }
    }

    [NodeMenu("Transform/Rotate", typeof(TextureGraph))]
    class RotateNode : TextureGraphNode, IBindableGraphElement<int>
    {
        enum RotationType
        {
            Right90,
            Left90,
            Rotate180,
            Horizontal,
            Vertical
        }

        public event Action<GraphElement> onValueChanged;

        readonly EnumPopupField<RotationType> m_EnumField;

        RotateNode() : base()
        {
            title = "Rotate";
            AddInputPort<Vector4>();
            AddOutputPort<Vector4>();
            RefreshPorts();
            m_EnumField = new EnumPopupField<RotationType>(RotationType.Right90);
            m_EnumField.OnValueChanged(e => onValueChanged?.Invoke(this));
            extensionContainer.Add(m_EnumField);
            RefreshExpandedState();
        }

        public void Bind(SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = 1;
            m_EnumField.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
        }

        public void Reset()
        {
            m_EnumField.enumValue = RotationType.Right90;
        }

        protected override void Process()
        {
            var width = graph.width;
            var height = graph.height;
            var length = width * height;
            var inputs = new Vector4[length];
            var outputs = new Vector4[length];
            GetInput<Vector4>(ref inputs);
            var index = 0;
            switch (m_EnumField.enumValue)
            {
                case RotationType.Right90:
                    for (var j = 0; j < width; ++j)
                        for (var i = width - 1 - j; i < length; i += width)
                            outputs[index++] = inputs[i];
                    break;
                case RotationType.Left90:
                    for (var j = 0; j < width; ++j)
                        for (var i = width * (height - 1) + j; i >= 0; i -= width)
                            outputs[index++] = inputs[i];
                    break;
                case RotationType.Rotate180:
                    outputs = inputs.Reverse().ToArray();
                    break;
                case RotationType.Horizontal:
                    outputs = inputs.Select((v, i) => inputs[i / width * 2 * width + width - 1 - i]).ToArray();
                    break;
                case RotationType.Vertical:
                    outputs = inputs.Select((v, i) => inputs[i % width * 2 + width * (height - 1) - i]).ToArray();
                    break;
                default:
                    throw new ArgumentOutOfRangeException("RotationType");
            }
            var port = outputContainer.Q<Port>();
            graph.processData[port] = outputs;
        }
    }

    [NodeMenu("Color/HSV Shift", typeof(TextureGraph))]
    class HSVShiftNode : TextureGraphNode, IBindableGraphElement<float>
    {
        public event Action<GraphElement> onValueChanged;

        readonly SliderWithFloatField m_HueSlider;
        readonly SliderWithFloatField m_SaturationSlider;
        readonly SliderWithFloatField m_ValueSlider;

        HSVShiftNode() : base()
        {
            title = "HSV Shift";
            style.width = 150f;
            AddInputPort<Vector4>();
            AddOutputPort<Vector4>();
            RefreshPorts();
            m_HueSlider = new SliderWithFloatField(-0.5f, 0.5f, 0);
            m_SaturationSlider = new SliderWithFloatField(0f, 2f, 1f);
            m_ValueSlider = new SliderWithFloatField(0f, 2f, 1f);
            m_HueSlider.OnValueChanged(e => onValueChanged?.Invoke(this));
            m_SaturationSlider.OnValueChanged(e => onValueChanged?.Invoke(this));
            m_ValueSlider.OnValueChanged(e => onValueChanged?.Invoke(this));
            extensionContainer.Add(UIElementsUtility.CreateLabeledElement("H", m_HueSlider));
            extensionContainer.Add(UIElementsUtility.CreateLabeledElement("S", m_SaturationSlider));
            extensionContainer.Add(UIElementsUtility.CreateLabeledElement("V", m_ValueSlider));
            RefreshExpandedState();
        }

        public void Bind(SerializedProperty arrayProperty)
        {
            arrayProperty.arraySize = 3;
            m_HueSlider.BindProperty(arrayProperty.GetArrayElementAtIndex(0));
            m_SaturationSlider.BindProperty(arrayProperty.GetArrayElementAtIndex(1));
            m_ValueSlider.BindProperty(arrayProperty.GetArrayElementAtIndex(2));
        }

        public void Reset()
        {
            m_HueSlider.value = 0f;
            m_SaturationSlider.value = 1f;
            m_ValueSlider.value = 1f;
        }

        protected override void Process()
        {
            var inputs = new Vector4[graph.width * graph.height];
            GetInput<Vector4>(ref inputs);
            var outputs = inputs.Select(vector =>
            {
                Color.RGBToHSV(vector, out float h, out float s, out float v);
                var c = Color.HSVToRGB((h + m_HueSlider.value + 1f) % 1, s * m_SaturationSlider.value, v * m_ValueSlider.value, true);
                return new Vector4(c.r, c.g, c.b, vector.w);
            }).ToArray();
            var port = outputContainer.Q<Port>();
            graph.processData[port] = outputs;
        }
    }

}
