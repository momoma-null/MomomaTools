using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Experimental.UIElements;

namespace MomomaAssets
{
    public interface ISerializedGraphElement
    {
        string Guid { get; set; }
        string TypeName { get; set; }
        Rect Position { get; set; }
        IList<string> ReferenceGuids { get; }
        IReadOnlyList<IFieldValue> FieldValues { get; }

        void AddFieldValue<T>(INotifyValueChanged<T> field);
    }
}
