using System;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;
using UnityEditor.Experimental.UIElements.GraphView;

namespace MomomaAssets
{
    public interface ISerializedGraphElement
    {
        string Guid { get; set; }
        string TypeName { get; set; }
        Rect Position { get; set; }
        IList<string> ReferenceGuids { get; }

        void SetFieldValues<T>(IGraphViewCallback graphView, params INotifyValueChanged<T>[] fields);
        void GetFieldValues<T>(IGraphViewCallback graphView, params INotifyValueChanged<T>[] fields);
    }

    public static class SerializedGraphElementExtensions
    {
        static readonly Dictionary<string, ConstructorInfo> s_ConstructorInfos = new Dictionary<string, ConstructorInfo>();

        public static void Serialize<T, TGraphView>(this GraphElement graphElement, T serializedGraphElement, TGraphView graphView) where T : ISerializedGraphElement where TGraphView : GraphView, IGraphViewCallback
        {
            if (serializedGraphElement == null)
                throw new ArgumentNullException(nameof(serializedGraphElement));
            if (graphView == null)
                throw new ArgumentNullException(nameof(graphView));
            serializedGraphElement.Guid = graphElement.persistenceKey;
            serializedGraphElement.TypeName = graphElement.GetType().AssemblyQualifiedName;
            serializedGraphElement.Position = graphElement.GetPosition();
            var referenceGuids = serializedGraphElement.ReferenceGuids;
            switch (graphElement)
            {
                case Node node:
                    node.Query<Port>().ForEach(port => referenceGuids.Add(port.persistenceKey));
                    break;
                case Edge edge:
                    referenceGuids.Add(edge.input?.persistenceKey);
                    referenceGuids.Add(edge.output?.persistenceKey);
                    break;
            }
            if (graphElement is IFieldHolder fieldHolder)
            {
                var fieldRegister = new FieldValuesSetter(serializedGraphElement, graphView);
                fieldHolder.RegisterFields(fieldRegister);
            }
        }

        public static GraphElement Deserialize<TGraphView>(this ISerializedGraphElement serializedGraphElement, GraphElement graphElement, TGraphView graphView) where TGraphView : GraphView, IGraphViewCallback
        {
            if (graphView == null)
                throw new ArgumentNullException(nameof(graphView));
            if (graphElement == null)
            {
                var typeName = serializedGraphElement.TypeName;
                if (!s_ConstructorInfos.TryGetValue(typeName, out var info))
                {
                    info = Type.GetType(typeName).GetConstructor(BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance, null, new Type[0], null);
                    s_ConstructorInfos[typeName] = info;
                }
                graphElement = info.Invoke(new object[0]) as GraphElement;
                graphView.AddElement(graphElement);
                if (graphElement is IFieldHolder fieldHolder)
                {
                    var fieldRegister = new FieldValuesGetter(serializedGraphElement, graphView);
                    fieldHolder.RegisterFields(fieldRegister);
                }
            }
            graphElement.persistenceKey = serializedGraphElement.Guid;
            graphElement.SetPosition(serializedGraphElement.Position);
            if (graphElement is Node node)
            {
                var guidsQueue = new Queue<string>(serializedGraphElement.ReferenceGuids);
                node.Query<Port>().ForEach(port => port.persistenceKey = guidsQueue.Dequeue());
            }
            return graphElement;
        }
    }
}
