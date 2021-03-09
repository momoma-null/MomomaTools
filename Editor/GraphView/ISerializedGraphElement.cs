using System.Collections.Generic;
using UnityEngine;

namespace MomomaAssets
{
    public interface ISerializedGraphElement
    {
        string Guid { get; set; }
        string TypeName { get; set; }
        Rect Position { get; set; }
        IList<string> ReferenceGuids { get; }
        SerializableFieldValue FieldValue { get; }
    }
}
