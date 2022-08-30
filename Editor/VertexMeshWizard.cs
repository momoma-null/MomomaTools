using UnityEditor;
using UnityEngine;

namespace MomomaAssets
{
    sealed class VertexMeshWizard : ScriptableWizard
    {
        [MenuItem("Assets/Create/Vertex Mesh")]
        static void CreateWizard()
        {
            DisplayWizard<VertexMeshWizard>(ObjectNames.NicifyVariableName(nameof(VertexMeshWizard)));
        }

        [SerializeField]
        ushort vertexCount;

        void OnWizardCreate()
        {
            var indices = new ushort[vertexCount];
            for (ushort i = 0; i < vertexCount; ++i)
            {
                indices[i] = i;
            }
            var mesh = new Mesh();
            mesh.vertices = new Vector3[vertexCount];
            mesh.SetIndices(indices, MeshTopology.Points, 0, false);
            mesh.bounds = new Bounds(Vector3.zero, Vector3.one);
            ProjectWindowUtil.CreateAsset(mesh, "VertexMesh.asset");
        }
    }
}
