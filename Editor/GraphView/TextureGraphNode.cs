using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Profiling;
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

        public override void SetPosition(Rect newPos)
        {
            newPos.x = Mathf.Round(newPos.x * 0.1f) * 10f;
            newPos.y = Mathf.Round(newPos.y * 0.1f) * 10f;
            base.SetPosition(newPos);
        }

        internal abstract void Process();

        protected bool IsProcessed()
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
                var outNode = edge.output.node as TextureGraphNode;
                outNode.Process();
                inputValue = graph.processData[edge.output] as T[];
            }
        }

        protected Port AddInputPort<T>(string portName = null)
        {
            var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Input, Port.Capacity.Single, typeof(T));
            port.portName = portName;
            inputContainer.Add(port);
            return port;
        }
        
        protected Port AddOutputPort<T>(string portName = null)
        {
            var port = Port.Create<Edge>(Orientation.Horizontal, Direction.Output, Port.Capacity.Multi, typeof(T));
            port.portName = portName;
            outputContainer.Add(port);
            return port;
        }
    }

    class ImportTextureNode : TextureGraphNode
    {
        readonly ObjectField objectField;
        readonly Image image;

        internal ImportTextureNode()
        {
            title = "Import Texture";
            AddOutputPort<Vector4>("Color");
            RefreshPorts();
            objectField = new ObjectField() { objectType = typeof(Texture2D) };
            objectField.OnValueChanged(e => OnValueChanged(e.newValue as Texture2D));
            extensionContainer.Add(objectField);
            image = new Image() { image = Texture.Instantiate(Texture2D.blackTexture), style = { maxWidth = 128f, maxHeight = 128f } };
            extensionContainer.Add(image);
            RefreshExpandedState();
        }

        void OnValueChanged(Texture2D newTexture)
        {
            if (newTexture == null)
                newTexture = Texture.Instantiate(Texture2D.blackTexture);
            var width = graph.width;
            var height = graph.height;
            var newImage = new Texture2D(width, height, TextureFormat.RGBAFloat, false, false);
            var renderTexture = RenderTexture.GetTemporary(width, height, 0, RenderTextureFormat.ARGBFloat);
            Graphics.Blit(newTexture, renderTexture);
            newImage.ReadPixels(new Rect(0, 0, width, height), 0, 0, false);
            newImage.Apply();
            RenderTexture.ReleaseTemporary(renderTexture);
            if (image.image.value != null)
                Texture.DestroyImmediate(image.image);
            image.image = newImage;
            graph.graphViewChanged.Invoke(new GraphViewChange());
        }

        internal override void Process()
        {
            if (IsProcessed())
                return;
            var port = outputContainer.Q<Port>();
            graph.processData[port] = (image.image.value as Texture2D).GetRawTextureData<Vector4>().ToArray();
        }
    }

    class ExportTextureNode : TextureGraphNode
    {
        static readonly List<int> s_PopupValues = new List<int>() { 32, 64, 128, 256, 512, 1024, 2048, 4096, 8192 };

        Color[] m_Colors;
        internal Color[] colors => m_Colors;

        readonly PopupField<int> widthPopupField;
        readonly PopupField<int> heightPopupField;

        internal ExportTextureNode()
        {
            title = "Export Texture";
            capabilities = capabilities & ~Capabilities.Deletable;
            AddInputPort<Vector4>("Color");
            RefreshPorts();
            widthPopupField = new PopupField<int>(s_PopupValues, 6) { name = "Width" };
            heightPopupField = new PopupField<int>(s_PopupValues, 6) { name = "Height" };
            extensionContainer.Add(widthPopupField);
            extensionContainer.Add(heightPopupField);
            RefreshExpandedState();
        }

        internal override void Process()
        {
            var width = widthPopupField.value;
            var height = heightPopupField.value;
            m_Colors = new Color[width * height];
            var vectors = new Vector4[width * height];
            GetInput<Vector4>(ref vectors);
            m_Colors = Array.ConvertAll(vectors, v => (Color)v);
        }
    }

    class DecomposeChannelsNode : TextureGraphNode
    {
        internal DecomposeChannelsNode()
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
            if (IsProcessed())
                return;
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
        internal CombineChannelsNode()
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
            if (IsProcessed())
                return;
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

    class MathNode : TextureGraphNode
    {
        enum CalculateMode
        {
            Add,
            Subtract,
            Reverse
        }

        readonly EnumField m_EnumField;

        internal MathNode()
        {
            title = "Math";
            var port = AddInputPort<float>("A");
            port.name = "A";
            port = AddInputPort<float>("B");
            port.name = "B";
            AddOutputPort<float>("Out");
            RefreshPorts();
            m_EnumField = new EnumField(CalculateMode.Add);
            extensionContainer.Add(m_EnumField);
            RefreshExpandedState();
        }

        internal override void Process()
        {
            if (IsProcessed())
                return;
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
