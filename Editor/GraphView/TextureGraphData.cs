using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEditor;
using UnityEditor.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{

    class TextureGraphData : ScriptableObject
    {
        [Serializable]
        class SerializedGraphElement
        {
            [SerializeField]
            internal string m_TypeName;
            [SerializeField]
            internal string m_Data;
        }

        [SerializeField, HideInInspector]
        List<SerializedGraphElement> m_Nodes = new List<SerializedGraphElement>();
        [SerializeField, HideInInspector]
        List<SerializedGraphElement> m_Edges = new List<SerializedGraphElement>();
        [SerializeField, HideInInspector]
        Vector3 m_ViewPosition;
        [SerializeField, HideInInspector]
        Vector3 m_ViewScale;

        internal static void SaveGraph(string path, TextureGraph graphView)
        {
            var graphData = ScriptableObject.CreateInstance<TextureGraphData>();
            var edges = graphView.edges.ToList();
            foreach (var edge in edges)
            {
                graphData.m_Edges.Add(new SerializedGraphElement()
                {
                    m_TypeName = edge.GetType().AssemblyQualifiedName,
                    m_Data = EditorJsonUtility.ToJson(edge)
                });
            }
            var nodes = graphView.nodes.ToList();
            foreach (var node in nodes)
            {
                graphData.m_Nodes.Add(new SerializedGraphElement()
                {
                    m_TypeName = node.GetType().AssemblyQualifiedName,
                    m_Data = EditorJsonUtility.ToJson(node)
                });
            }
            graphData.m_ViewPosition = graphView.viewTransform.position;
            graphData.m_ViewScale = graphView.viewTransform.scale;
            if (string.IsNullOrEmpty(path))
            {
                path = @"Assets/NewTextureGraph.asset";
                AssetDatabase.GenerateUniqueAssetPath(path);
            }
            AssetDatabase.CreateAsset(graphData, path);
            AssetDatabase.ImportAsset(path);
            Selection.activeObject = graphData;
            graphView.graphData = graphData;
        }

        internal static void LoadGraph(string path, TextureGraph graphView)
        {
            var graphData = AssetDatabase.LoadAssetAtPath<TextureGraphData>(path);
            if (graphView == null)
            {
                var window = TextureGraphWindow.ShowWindow();
                foreach (var element in window.GetRootVisualContainer())
                {
                    if (element is TextureGraph graph)
                    {
                        graphView = graph;
                        break;
                    }
                }
                if (graphView == null)
                {
                    Debug.LogError("Not found Graph View.");
                    return;
                }
            }
            graphView.DeleteElements(graphView.graphElements.ToList());
            graphView.graphData = graphData;
            var ports = new Dictionary<string, Port>();
            foreach (var nodeData in graphData.m_Nodes)
            {
                var node = Activator.CreateInstance(Type.GetType(nodeData.m_TypeName), true) as Node;
                EditorJsonUtility.FromJsonOverwrite(nodeData.m_Data, node);
                graphView.AddElement(node);
                var serializableNode = node as ISerializableNode;
                if (node is TokenNode token)
                {
                    ports[serializableNode.inputPortGuids[0]] = token.input;
                    ports[serializableNode.outputPortGuids[0]] = token.output;
                }
                else
                {
                    var iPorts = new Queue<Port>(node.inputContainer.Query<Port>().ToList());
                    foreach (var guid in serializableNode.inputPortGuids)
                    {
                        ports[guid] = iPorts.Dequeue();
                    }
                    var oPorts = new Queue<Port>(node.outputContainer.Query<Port>().ToList());
                    foreach (var guid in serializableNode.outputPortGuids)
                    {
                        ports[guid] = oPorts.Dequeue();
                    }
                }
                serializableNode.ReloadGuids();
            }
            foreach (var edgeData in graphData.m_Edges)
            {
                var edge = Activator.CreateInstance(Type.GetType(edgeData.m_TypeName), true) as TextureGraphEdge;
                EditorJsonUtility.FromJsonOverwrite(edgeData.m_Data, edge);
                if (string.IsNullOrEmpty(edge.inputGuid) || string.IsNullOrEmpty(edge.outputGuid) || !ports.ContainsKey(edge.inputGuid) || !ports.ContainsKey(edge.outputGuid))
                    continue;
                var inputPort = ports[edge.inputGuid];
                var outputPort = ports[edge.outputGuid];
                edge.input = inputPort;
                edge.output = outputPort;
                inputPort.Connect(edge);
                outputPort.Connect(edge);
                graphView.AddElement(edge);
            }
            graphView.MarkNordIsDirty();
            graphView.UpdateViewTransform(graphData.m_ViewPosition, graphData.m_ViewScale);
        }
    }

    [CustomEditor(typeof(TextureGraphData))]
    class TextureGraphDataInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open"))
            {
                TextureGraphData.LoadGraph(AssetDatabase.GetAssetPath(target), null);
            }
        }
    }

}
