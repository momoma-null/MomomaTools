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
        class EdgeData
        {
            [SerializeField]
            internal int m_OutputNode;
            [SerializeField]
            internal int m_InputNode;
        }

        [Serializable]
        class NodeData
        {
            [SerializeField]
            internal int m_HashCode;
            [SerializeField]
            internal string m_Type;
            [SerializeField]
            internal Vector2 m_Position;
            [SerializeField]
            internal bool m_Expanded;
        }

        [SerializeField]
        List<EdgeData> m_Edges = new List<EdgeData>();
        [SerializeField]
        List<NodeData> m_Nodes = new List<NodeData>();

        internal static void SaveGraph(string path, TextureGraph graphView)
        {
            var edges = graphView.edges.ToList();
            var graphData = ScriptableObject.CreateInstance<TextureGraphData>();
            foreach (var edge in edges)
            {
                graphData.m_Edges.Add(new EdgeData()
                {
                    m_OutputNode = edge.output.node.GetHashCode(),
                    m_InputNode = edge.input.node.GetHashCode()
                });
            }
            var nodes = graphView.nodes.ToList();
            foreach (var node in nodes)
            {
                graphData.m_Nodes.Add(new NodeData()
                {
                    m_HashCode = node.GetHashCode(),
                    m_Type = node.GetType().AssemblyQualifiedName,
                    m_Position = node.GetPosition().position,
                    m_Expanded = node.expanded,
                });
            }
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
                var window = EditorWindow.GetWindow<TextureGraphWindow>();
                foreach (var element in window.GetRootVisualContainer())
                {
                    if (element is TextureGraph)
                    {
                        graphView = element as TextureGraph;
                        break;
                    }
                }
                if (graphView == null)
                {
                    Debug.LogError("Not found Graph View.");
                    return;
                }
            }
            graphView.graphData = graphData;
            graphView.edges.ForEach(graphView.RemoveElement);
            graphView.nodes.ForEach(graphView.RemoveElement);
            var nodesDict = new Dictionary<int, Node>();
            foreach (var nodeData in graphData.m_Nodes)
            {
                var node = Activator.CreateInstance(Type.GetType(nodeData.m_Type), true) as Node;
                var rect = node.GetPosition();
                rect.position = nodeData.m_Position;
                node.SetPosition(rect);
                graphView.AddElement(node);
                nodesDict[nodeData.m_HashCode] = node;
            }
            foreach (var edgeData in graphData.m_Edges)
            {
                var outputNode = nodesDict[edgeData.m_OutputNode];
                var inputNode = nodesDict[edgeData.m_InputNode];
                var edge = new Edge();
                edge.output = outputNode.outputContainer.Q<Port>();
                edge.input = inputNode.inputContainer.Q<Port>();
                edge.output.Connect(edge);
                edge.input.Connect(edge);
                graphView.Add(edge);
            }
            foreach (var nodeData in graphData.m_Nodes)
            {
                nodesDict[nodeData.m_HashCode].expanded = nodeData.m_Expanded;
            }
        }
    }

    [CustomEditor(typeof(TextureGraphData))]
    class TextureGraphDataInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            using (new EditorGUI.DisabledScope())
            {
                base.OnInspectorGUI();
            }
            if (GUILayout.Button("Open"))
            {
                TextureGraphData.LoadGraph(AssetDatabase.GetAssetPath(target), null);
            }
        }
    }

}
