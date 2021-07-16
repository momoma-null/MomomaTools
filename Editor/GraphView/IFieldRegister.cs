using UnityEngine.Experimental.UIElements;

namespace MomomaAssets
{
    public interface IFieldRegister
    {
        void RegisterFields<T>(params INotifyValueChanged<T>[] fields);
    }

    class FieldValuesSetter : IFieldRegister
    {
        readonly ISerializedGraphElement m_SerializedGraphElement;
        readonly IGraphViewCallback m_GraphView;

        public FieldValuesSetter(ISerializedGraphElement serializedGraphElement, IGraphViewCallback graphView)
        {
            m_SerializedGraphElement = serializedGraphElement;
            m_GraphView = graphView;
        }

        public void RegisterFields<T>(params INotifyValueChanged<T>[] fields)
        {
            m_SerializedGraphElement.SetFieldValues(m_GraphView, fields);
        }
    }

    class FieldValuesGetter : IFieldRegister
    {
        readonly ISerializedGraphElement m_SerializedGraphElement;
        readonly IGraphViewCallback m_GraphView;

        public FieldValuesGetter(ISerializedGraphElement serializedGraphElement, IGraphViewCallback graphView)
        {
            m_SerializedGraphElement = serializedGraphElement;
            m_GraphView = graphView;
        }

        public void RegisterFields<T>(params INotifyValueChanged<T>[] fields)
        {
            m_SerializedGraphElement.GetFieldValues(m_GraphView, fields);
        }
    }
}
