using System;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.IO;

namespace MomomaAssets
{

    public class SkinnedMeshEditor : EditorWindow
    {
        GameObject rootObj;
        PreviewRenderUtility previewRender;
        List<GameObject> renderGOs;
        Vector3 centerPos;
        Stack<GameObject> inactiveGOs;

        [MenuItem("MomomaTools/SkinnedMeshEditor")]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<SkinnedMeshEditor>("SkinnedMeshEditor");
        }

        void OnEnable()
        {
            previewRender = new PreviewRenderUtility();
            previewRender.camera.fieldOfView = 30f;
            previewRender.camera.farClipPlane = 100f;
            previewRender.camera.nearClipPlane = 0.1f;
            previewRender.camera.clearFlags = CameraClearFlags.SolidColor;
            ResetCamera();
        }

        void OnDisable()
        {
            previewRender?.Cleanup();
            previewRender = null;
            ResetRenderGameObjects();
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
                ResetRenderGameObjects();
                SplitSkinnedMeshRenderers(skinnedMRs);
                ResetCamera();
                inactiveGOs = new Stack<GameObject>();
            }
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                var rect = GUILayoutUtility.GetRect(512f, 512f);
                previewRender.BeginPreview(rect, GUIStyle.none);
                if (rect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.type == EventType.MouseDrag && Event.current.button == 1)
                    {
                        var drag = Event.current.delta;
                        if (drag != Vector2.zero)
                        {
                            previewRender.camera.transform.RotateAround(centerPos, previewRender.camera.transform.rotation * Vector3.up, drag.x);
                            previewRender.camera.transform.RotateAround(centerPos, previewRender.camera.transform.rotation * Vector3.right, drag.y);
                            Repaint();
                        }
                    }
                    else if (Event.current.type == EventType.MouseDrag && Event.current.button == 2)
                    {
                        var drag = Event.current.delta;
                        if (drag != Vector2.zero)
                        {
                            drag.x *= -1f;
                            var deltaPos = previewRender.camera.transform.rotation * drag * 0.002f;
                            deltaPos.x = Mathf.Clamp(deltaPos.x + centerPos.x, -1f, 1f) - centerPos.x;
                            deltaPos.y = Mathf.Clamp(deltaPos.y + centerPos.y, -1f, 1f) - centerPos.y;
                            deltaPos.z = Mathf.Clamp(deltaPos.z + centerPos.z, -1f, 1f) - centerPos.z;
                            centerPos += deltaPos;
                            previewRender.camera.transform.position += deltaPos;
                            Repaint();
                        }
                    }
                    else if (Event.current.type == EventType.ScrollWheel)
                    {
                        var scroll = Event.current.delta.y;
                        if (scroll != 0)
                        {
                            var cameraPos = previewRender.camera.transform.position - centerPos;
                            previewRender.camera.transform.position = centerPos + cameraPos.normalized * Mathf.Clamp(cameraPos.magnitude + scroll * 0.1f, 0.1f, 5.0f);
                            Repaint();
                        }
                    }
                    else if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        var screenPoint = Event.current.mousePosition - rect.min;
                        screenPoint *= new Vector2(previewRender.camera.pixelWidth, previewRender.camera.pixelHeight) / rect.size;
                        screenPoint.y = previewRender.camera.pixelHeight - screenPoint.y;
                        var ray = previewRender.camera.ScreenPointToRay(screenPoint);
                        var physScene = PhysicsSceneExtensions.GetPhysicsScene(renderGOs[0].scene);
                        RaycastHit hitInfo;
                        physScene.Raycast(ray.origin, ray.direction, out hitInfo, 10f);
                        if (hitInfo.collider)
                        {
                            hitInfo.collider.gameObject.SetActive(false);
                            inactiveGOs.Push(hitInfo.collider.gameObject);
                            Repaint();
                        }
                    }
                }
                previewRender.camera.Render();
                previewRender.EndAndDrawPreview(rect);

                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledGroupScope(inactiveGOs == null || inactiveGOs.Count == 0))
                {
                    if (GUILayout.Button("Undo"))
                    {
                        var undoGO = inactiveGOs.Pop();
                        undoGO.SetActive(true);
                    }
                }

                if (GUILayout.Button("Reset"))
                {
                    foreach (var go in renderGOs)
                        foreach (Transform tr in go.transform)
                            tr.gameObject.SetActive(true);
                }

                GUILayout.FlexibleSpace();
            }
        }

        void ResetCamera()
        {
            previewRender.camera.transform.position = new Vector3(0, 0, 5f);
            previewRender.camera.transform.rotation = Quaternion.Euler(0, 180f, 0);
            centerPos = Vector3.zero;
        }

        void ResetRenderGameObjects()
        {
            if (renderGOs != null)
            {
                foreach (var go in renderGOs)
                    DestroyImmediate(go);
            }
            renderGOs = new List<GameObject>();
        }

        void SplitSkinnedMeshRenderers(SkinnedMeshRenderer[] skinnedMRs)
        {
            var bounds = new Bounds();
            foreach (var skinned in skinnedMRs)
                bounds.Encapsulate(skinned.bounds);
            foreach (var skinned in skinnedMRs)
            {
                var adjacentTriangles = new List<AdjacentTriangle>();
                var srcMesh = skinned.sharedMesh;

                var srcVertices = srcMesh.vertices;
                var length = srcVertices.Length;
                var convertIndices = Enumerable.Repeat(-1, length).ToArray();
                for (var i = 0; i < length; ++i)
                {
                    if (convertIndices[i] > -1)
                        continue;
                    convertIndices[i] = i;
                    var vertex = srcVertices[i];
                    var index = i + 1;
                    while (index < length && (index = Array.IndexOf(srcVertices, vertex, index)) > -1)
                    {
                        convertIndices[index] = i;
                        ++index;
                    }
                }
                for (var i = 0; i < srcMesh.subMeshCount; ++i)
                {
                    var indices = new List<int>();
                    srcMesh.GetTriangles(indices, i);
                    var maxCount = indices.Count;
                    try
                    {
                        while (indices.Count > 0)
                        {
                            EditorUtility.DisplayProgressBar("Converting...", srcMesh.name, 1f - 1f * indices.Count / maxCount);
                            adjacentTriangles.Insert(0, new AdjacentTriangle(indices[0], indices[1], indices[2], convertIndices));
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
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
                var meshes = new Mesh[adjacentTriangles.Count];
                for (var i = 0; i < meshes.Length; ++i)
                {
                    var tempMesh = Instantiate(srcMesh);
                    tempMesh.triangles = adjacentTriangles[i].triangles.ToArray();
                    meshes[i] = tempMesh;
                }
                var renderGO = new GameObject();
                renderGOs.Add(renderGO);
                renderGO.hideFlags = HideFlags.HideAndDontSave;
                previewRender.AddSingleGO(renderGO);
                var material = skinned.sharedMaterial;
                foreach (var mesh in meshes)
                {
                    var meshGO = GameObject.CreatePrimitive(PrimitiveType.Quad);
                    meshGO.transform.parent = renderGO.transform;
                    meshGO.GetComponent<MeshFilter>().sharedMesh = mesh;
                    meshGO.GetComponent<MeshRenderer>().sharedMaterial = material;
                    meshGO.GetComponent<MeshCollider>().sharedMesh = mesh;
                }
                renderGO.transform.localPosition = skinned.transform.localPosition - bounds.center;
                renderGO.transform.localRotation = skinned.transform.localRotation;
                renderGO.transform.localScale = skinned.transform.localScale;
            }
        }

        class AdjacentTriangle
        {
            readonly HashSet<(int, int)> lines = new HashSet<(int, int)>();
            internal readonly Queue<int> triangles = new Queue<int>();
            readonly IList<int> convertIndices;

            internal AdjacentTriangle(int index0, int index1, int index2, IList<int> convertIndices)
            {
                this.convertIndices = convertIndices;
                lines.Add(GetLine(index0, index1));
                lines.Add(GetLine(index1, index2));
                lines.Add(GetLine(index2, index0));
                triangles.Enqueue(index0);
                triangles.Enqueue(index1);
                triangles.Enqueue(index2);
            }

            (int, int) GetLine(int i, int j)
            {
                i = convertIndices[i];
                j = convertIndices[j];
                return i < j ? (i, j) : (j, i);
            }

            internal bool AddTriangle(int index0, int index1, int index2)
            {
                var newLines = new (int, int)[] { GetLine(index0, index1), GetLine(index1, index2), GetLine(index2, index0) };
                if (lines.Remove(newLines[0]))
                {
                    lines.Add(newLines[1]);
                    lines.Add(newLines[2]);
                    triangles.Enqueue(index0);
                    triangles.Enqueue(index1);
                    triangles.Enqueue(index2);
                    return true;
                }
                else if (lines.Remove(newLines[1]))
                {
                    lines.Add(newLines[0]);
                    lines.Add(newLines[2]);
                    triangles.Enqueue(index0);
                    triangles.Enqueue(index1);
                    triangles.Enqueue(index2);
                    return true;
                }
                else if (lines.Remove(newLines[2]))
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
