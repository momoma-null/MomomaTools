using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace MomomaAssets
{

    public class SkinnedMeshEditor : EditorWindow
    {
        GameObject rootObj;
        List<AdjacentTriangle> adjacentTriangles;
        Vector2 scrollPos = Vector2.zero;
        PreviewRenderUtility previewRender;
        Mesh mesh, srcMesh;
        Material material;
        GameObject renderGO;
        int meshIndex;

        [MenuItem("MomomaTools/SkinnedMeshEditor")]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<SkinnedMeshEditor>("SkinnedMeshEditor");
        }

        void OnGUI()
        {
            rootObj = EditorGUILayout.ObjectField(rootObj, typeof(GameObject), true) as GameObject;
            if (!rootObj)
                return;
            var skinnedMRs = rootObj.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if (skinnedMRs.Length == 0)
            {
                EditorGUILayout.HelpBox("Select GameObject including SkinnedMeshRenderer.", MessageType.Info);
                return;
            }
            if (GUILayout.Button("Split"))
            {
                SplitSkinnedMeshRenderers(skinnedMRs);
            }
            if (adjacentTriangles == null || adjacentTriangles.Count < 1)
                return;
            EditorGUI.BeginChangeCheck();
            meshIndex = EditorGUILayout.IntSlider(meshIndex, 0, adjacentTriangles.Count - 1);
            if (EditorGUI.EndChangeCheck())
            {
                mesh = new Mesh();
                var newVertices = new List<Vector3>();
                System.Array.ForEach(srcMesh.vertices, v => newVertices.Add(v * 100));
                mesh.vertices = newVertices.ToArray();
                mesh.uv = srcMesh.uv;
                mesh.triangles = adjacentTriangles[meshIndex].triangles.ToArray();
            }
            if (mesh)
            {
                if (previewRender == null)
                    previewRender = new PreviewRenderUtility();
                var rect = new Rect(50, 50, 512, 512);
                var centerPos = mesh.bounds.center;
                previewRender.BeginPreview(rect, GUIStyle.none);
                previewRender.camera.farClipPlane = 10;
                previewRender.camera.transform.position = new Vector3(0, 0, -5);
                previewRender.camera.clearFlags = CameraClearFlags.SolidColor;
                var drag = Vector2.zero;
                if (Event.current.type == EventType.MouseDrag)
                {
                    drag = Event.current.delta;
                }
                if (renderGO == null)
                {
                    renderGO = new GameObject();
                    renderGO.hideFlags = HideFlags.HideAndDontSave;
                }
                renderGO.transform.RotateAround(centerPos, Vector3.up, -drag.x);
                renderGO.transform.RotateAround(centerPos, Vector3.right, -drag.y);
                previewRender.DrawMesh(mesh, renderGO.transform.position, renderGO.transform.rotation, material, 0);
                previewRender.camera.Render();
                previewRender.EndAndDrawPreview(rect);
                if (drag != Vector2.zero)
                    Repaint();
            }
        }

        void SplitSkinnedMeshRenderers(SkinnedMeshRenderer[] skinnedMRs)
        {
            foreach (var skinned in skinnedMRs)
            {
                adjacentTriangles = new List<AdjacentTriangle>();
                //var bones = skinned.bones;
                srcMesh = skinned.sharedMesh;
                material = skinned.sharedMaterial;
                var srcVertices = srcMesh.vertices;
                for (var i = 0; i < srcMesh.subMeshCount; ++i)
                {
                    var indices = new List<int>();
                    srcMesh.GetTriangles(indices, i);
                    while (indices.Count > 0)
                    {
                        adjacentTriangles.Insert(0, new AdjacentTriangle(indices[0], indices[1], indices[2]));
                        indices.RemoveRange(0, 3);
                        bool again;
                        do
                        {
                            again = false;
                            for (var j = 0; j < indices.Count; j += 3)
                            {
                                if (adjacentTriangles[0].AddTriangle(indices[j], indices[j + 1], indices[j + 2]))
                                {
                                    indices.RemoveRange(j, 3);
                                    again = true;
                                }
                            }
                        }
                        while (again);
                    }
                }
            }
        }

        class AdjacentTriangle
        {
            readonly HashSet<(int, int)> lines = new HashSet<(int, int)>();
            internal readonly Queue<int> triangles = new Queue<int>();

            internal AdjacentTriangle(int index0, int index1, int index2)
            {
                lines.Add(SortIndex(index0, index1));
                lines.Add(SortIndex(index1, index2));
                lines.Add(SortIndex(index2, index0));
                triangles.Enqueue(index0);
                triangles.Enqueue(index1);
                triangles.Enqueue(index2);
            }

            (int, int) SortIndex(int i, int j)
            {
                return i < j ? (i, j) : (j, i);
            }

            internal bool AddTriangle(int index0, int index1, int index2)
            {
                var newLines = new (int, int)[] { SortIndex(index0, index1), SortIndex(index1, index2), SortIndex(index2, index0) };
                if (lines.Contains(newLines[0]))
                {
                    lines.Add(newLines[1]);
                    lines.Add(newLines[2]);
                    triangles.Enqueue(index0);
                    triangles.Enqueue(index1);
                    triangles.Enqueue(index2);
                    return true;
                }
                else if (lines.Contains(newLines[1]))
                {
                    lines.Add(newLines[0]);
                    lines.Add(newLines[2]);
                    triangles.Enqueue(index0);
                    triangles.Enqueue(index1);
                    triangles.Enqueue(index2);
                    return true;
                }
                else if (lines.Contains(newLines[2]))
                {
                    lines.Add(newLines[0]);
                    lines.Add(newLines[1]);
                    triangles.Enqueue(index0);
                    triangles.Enqueue(index1);
                    triangles.Enqueue(index2);
                    return true;
                }
                return false;
            }
        }
    }

}// namespace MomomaAssets
