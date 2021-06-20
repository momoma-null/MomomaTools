#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{
    sealed class CatenaryGenerator : MonoBehaviour, ISerializationCallbackReceiver
    {
        enum Axis { X, Y, Z }

        [SerializeField]
        MeshRenderer m_SourceMeshRenderer = null;
        [SerializeField]
        Axis m_Axis = Axis.Z;
        [SerializeField]
        Transform[] m_Anchors = new Transform[0];

        [SerializeField, HideInInspector]
        GameObject[] m_MeshObjects = new GameObject[0];
        [SerializeField]
        float m_Catenary = 10f;

        void ISerializationCallbackReceiver.OnAfterDeserialize()
        {
            EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            EditorApplication.hierarchyChanged += OnHierarchyChanged;
        }

        void ISerializationCallbackReceiver.OnBeforeSerialize() { }

        void OnHierarchyChanged()
        {
            if (this == null)
            {
                DestroyAllCurves();
                EditorApplication.hierarchyChanged -= OnHierarchyChanged;
            }
        }

        void OnReset()
        {
            hideFlags |= HideFlags.DontSaveInBuild;
        }

        void DestroyAllCurves()
        {
            foreach (var go in m_MeshObjects)
            {
                if (go == null || !go.scene.isLoaded)
                    continue;
                var oldMesh = go.GetComponent<MeshFilter>()?.sharedMesh;
                Undo.DestroyObjectImmediate(go);
                if (oldMesh != null)
                    Undo.DestroyObjectImmediate(oldMesh);
            }
            if (this != null && this.gameObject.scene.isLoaded)
            {
                using (var so = new SerializedObject(this))
                using (var sp = so.FindProperty(nameof(m_MeshObjects)))
                {
                    sp.ClearArray();
                    so.ApplyModifiedProperties();
                }
            }
        }

        void RecalculateMesh()
        {
            if (m_Anchors.Length < 2 || m_SourceMeshRenderer == null)
                return;
            var sourceMesh = m_SourceMeshRenderer.GetComponent<MeshFilter>().sharedMesh;
            if (sourceMesh == null)
                return;
            using (var so = new SerializedObject(this))
            using (var sp = so.FindProperty(nameof(m_MeshObjects)))
            {
                DestroyAllCurves();
                var bounds = sourceMesh.bounds;
                var unitLength = 0f;
                switch (m_Axis)
                {
                    case Axis.X: unitLength = bounds.size.x; break;
                    case Axis.Y: unitLength = bounds.size.y; break;
                    case Axis.Z: unitLength = bounds.size.z; break;
                    default: throw new System.ArgumentOutOfRangeException(nameof(m_Axis));
                }
                sp.ClearArray();
                for (var i = 0; i < m_Anchors.Length - 1; ++i)
                {
                    if (m_Anchors[i] == null || m_Anchors[i + 1] == null)
                        continue;
                    var startPos = m_Anchors[i].position;
                    var endPos = m_Anchors[i + 1].position;
                    var curve = new CatenaryCurve(startPos, endPos, m_Catenary, unitLength);
                    var combines = new List<CombineInstance>();
                    foreach (var line in curve)
                    {
                        var scale = (line.to - line.from).magnitude / unitLength;
                        var matrix = Matrix4x4.LookAt((line.from + line.to) * 0.5f, line.to, Vector3.up) * Matrix4x4.Scale(Vector3.one + Vector3.forward * (scale - 1f));
                        switch (m_Axis)
                        {
                            case Axis.X: matrix *= Matrix4x4.Rotate(Quaternion.Euler(0, 90f, 0)); break;
                            case Axis.Y: matrix *= Matrix4x4.Rotate(Quaternion.Euler(90f, 0, 0)); break;
                        }
                        combines.Add(new CombineInstance() { mesh = sourceMesh, transform = matrix });
                    }
                    var mesh = new Mesh();
                    mesh.CombineMeshes(combines.ToArray(), true, true, true);
                    MeshUtility.Optimize(mesh);
                    mesh.UploadMeshData(true);
                    var go = new GameObject($"ProcedualMesh{i}") { hideFlags = HideFlags.HideInHierarchy | HideFlags.NotEditable };
                    ++sp.arraySize;
                    using (var element = sp.GetArrayElementAtIndex(sp.arraySize - 1))
                        element.objectReferenceValue = go;
                    var meshFilter = go.AddComponent<MeshFilter>();
                    var renderer = go.AddComponent<MeshRenderer>();
                    meshFilter.sharedMesh = mesh;
                    renderer.sharedMaterials = m_SourceMeshRenderer.sharedMaterials;
                    Undo.RegisterCreatedObjectUndo(mesh, "Create Mesh");
                    Undo.RegisterCreatedObjectUndo(go, "Create GameObject");
                }
                so.ApplyModifiedProperties();
            }
        }

        void OnDrawGizmos()
        {
            if (m_Anchors.Length < 2 || m_SourceMeshRenderer == null)
                return;
            var sourceMesh = m_SourceMeshRenderer.GetComponent<MeshFilter>().sharedMesh;
            if (sourceMesh == null)
                return;
            var bounds = sourceMesh.bounds;
            var unitLength = 0f;
            switch (m_Axis)
            {
                case Axis.X: unitLength = bounds.size.x; break;
                case Axis.Y: unitLength = bounds.size.y; break;
                case Axis.Z: unitLength = bounds.size.z; break;
                default: throw new System.ArgumentOutOfRangeException(nameof(m_Axis));
            }
            Gizmos.color = Color.blue;
            for (var i = 0; i < m_Anchors.Length - 1; ++i)
            {
                if (m_Anchors[i] == null || m_Anchors[i + 1] == null)
                    continue;
                var startPos = m_Anchors[i].position;
                var endPos = m_Anchors[i + 1].position;
                var curve = new CatenaryCurve(startPos, endPos, m_Catenary, unitLength);
                foreach (var line in curve)
                {
                    Gizmos.DrawLine(line.from, line.to);
                }
            }
        }

        sealed class CatenaryCurve : IEnumerable<(Vector3 from, Vector3 to)>
        {
            static float Asinh(float x) => Mathf.Log(x + Mathf.Sqrt(x * x + 1f));
            static float Acosh(float x) => Mathf.Log(x + Mathf.Sqrt(x * x - 1f));
            static float Sinh(float x) => (Mathf.Exp(x) - Mathf.Exp(-x)) * 0.5f;
            static float Cosh(float x) => (Mathf.Exp(x) + Mathf.Exp(-x)) * 0.5f;

            readonly Vector3 m_FromPos;
            readonly Vector3 m_ToPos;
            readonly float m_Catenary;
            readonly float m_UnitLength;

            public CatenaryCurve(Vector3 from, Vector3 to, float catenary, float unitLength)
            {
                m_FromPos = from;
                m_ToPos = to;
                m_Catenary = catenary;
                m_UnitLength = unitLength;
            }


            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

            public IEnumerator<(Vector3 from, Vector3 to)> GetEnumerator()
            {
                var hDirection = new Vector3(m_ToPos.x - m_FromPos.x, 0, m_ToPos.z - m_FromPos.z);
                var hDistance = hDirection.magnitude;
                hDirection /= hDistance;
                var vDistance = m_ToPos.y - m_FromPos.y;
                var a = m_Catenary * Asinh(vDistance / m_Catenary * 0.5f / Sinh(hDistance / m_Catenary * 0.5f)) - hDistance * 0.5f;
                var b = hDistance + a;
                var totalLength = m_Catenary * (Sinh(b / m_Catenary) - Sinh(a / m_Catenary));
                var count = Mathf.RoundToInt(totalLength / m_UnitLength);
                var length = totalLength / count;
                var currentPos = m_FromPos;
                var currentA = a;
                var origin = m_FromPos - (a * hDirection + m_Catenary * Cosh(a / m_Catenary) * Vector3.up);
                for (var i = 0; i < count; ++i)
                {
                    var nextA = Asinh(length / m_Catenary + Sinh(currentA / m_Catenary)) * m_Catenary;
                    var nextPos = origin + nextA * hDirection + m_Catenary * Cosh(nextA / m_Catenary) * Vector3.up;
                    yield return (currentPos, nextPos);
                    currentPos = nextPos;
                    currentA = nextA;
                }
            }
        }

        [CustomEditor(typeof(CatenaryGenerator))]
        [CanEditMultipleObjects]
        sealed class CatenaryGeneratorInspector : Editor
        {
            public override void OnInspectorGUI()
            {
                base.OnInspectorGUI();
                var catenaryGenerator = (target as CatenaryGenerator);
                using (new EditorGUI.DisabledScope(catenaryGenerator.m_Anchors.Length < 2 || catenaryGenerator.m_SourceMeshRenderer == null))
                {
                    if (GUILayout.Button("Generate"))
                        (target as CatenaryGenerator).RecalculateMesh();
                }
            }
        }
    }
}
#endif
