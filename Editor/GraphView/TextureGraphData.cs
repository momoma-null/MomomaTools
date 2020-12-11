using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEditor;
using UnityEditor.Experimental.UIElements;

namespace MomomaAssets
{

    class TextureGraphData : ScriptableObject
    {
        [Serializable]
        public class NodeObjectData
        {
            [SerializeField]
            string m_TypeName = "";
            public string typeName => m_TypeName;

            [SerializeField]
            string m_Guid ="";
            public string guid => m_Guid;

            [SerializeField]
            string m_SerializedNodeObject = "";
            public string serializedNodeObject => m_SerializedNodeObject;
        }

        [Serializable]
        public class EdgeObjectData
        {
            [SerializeField]
            string m_TypeName = "";
            public string typeName => m_TypeName;

            [SerializeField]
            string m_Guid ="";
            public string guid => m_Guid;

            [SerializeField]
            string m_SerializedEdgeObject = "";
            public string serializedEdgeObject => m_SerializedEdgeObject;
        }

        [SerializeField, HideInInspector]
        List<NodeObjectData> m_Nodes = new List<NodeObjectData>();
        public NodeObjectData[] nodes => m_Nodes.ToArray();

        [SerializeField, HideInInspector]
        List<EdgeObjectData> m_Edges = new List<EdgeObjectData>();
        public EdgeObjectData[] edges => m_Edges.ToArray();
    }

    [CustomEditor(typeof(TextureGraphData))]
    class TextureGraphDataInspector : Editor
    {
        public override void OnInspectorGUI()
        {
            if (GUILayout.Button("Open"))
            {
                var window = TextureGraphWindow.ShowWindow();
                var graph = window.GetRootVisualContainer().Q<TextureGraph>();
                graph.graphData = target as TextureGraphData;
            }
        }
    }

}
