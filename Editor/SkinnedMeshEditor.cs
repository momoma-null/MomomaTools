using System;
using System.IO;
using System.Reflection;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;

namespace MomomaAssets
{

    public class SkinnedMeshEditor : EditorWindow
    {
        const string ModelExporterTypeName = "UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor";
        const string ExportModelSettingsSerializeTypeName = "UnityEditor.Formats.Fbx.Exporter.ExportModelSettingsSerialize, Unity.Formats.Fbx.Editor";

        static AddRequest s_Request;
        static Type ModelExporterType = Type.GetType(ModelExporterTypeName);
        static Type ExportModelSettingsSerializeType = Type.GetType(ExportModelSettingsSerializeTypeName);

        GameObject rootObj;
        PreviewRenderUtility previewRender;
        List<GameObject> renderGOs;
        Vector3 centerPos;
        Stack<GameObject> inactiveGOs;
        Bounds bounds;

        [MenuItem("MomomaTools/SkinnedMeshEditor")]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<SkinnedMeshEditor>("SkinnedMeshEditor");
        }

        void OnEnable()
        {
            previewRender = new PreviewRenderUtility();
            ResetCamera();
        }

        void OnDisable()
        {
            ResetRenderGameObjects();
            previewRender?.Cleanup();
            previewRender = null;
        }

        void Update()
        {
            if (s_Request != null && s_Request.IsCompleted && s_Request.Status == StatusCode.Success)
            {
                ModelExporterType = Type.GetType(ModelExporterTypeName);
                ExportModelSettingsSerializeType = Type.GetType(ExportModelSettingsSerializeTypeName);
                Repaint();
            }
        }

        void OnGUI()
        {
            if (ModelExporterType == null || ExportModelSettingsSerializeType == null)
            {
                if (s_Request == null)
                {
                    s_Request = Client.Add("com.unity.formats.fbx");
                }
                else
                {
                    if (s_Request.IsCompleted)
                    {
                        if (s_Request.Status == StatusCode.Success)
                            EditorGUILayout.HelpBox("failed to get type of Fbx Exporter.", MessageType.Error);
                        else if (s_Request.Status == StatusCode.Failure)
                            EditorGUILayout.HelpBox("Import of Fbx Exporter failed.", MessageType.Error);
                    }
                    else
                    {
                        EditorGUILayout.HelpBox("Request Fbx Exporter...", MessageType.Info);
                    }
                }
                return;
            }
            rootObj = EditorGUILayout.ObjectField(rootObj, typeof(GameObject), true) as GameObject;
            if (!rootObj)
                return;
            var skinnedMRs = rootObj.GetComponentsInChildren<SkinnedMeshRenderer>();
            if (skinnedMRs.Length == 0)
            {
                EditorGUILayout.HelpBox("Select GameObject including SkinnedMeshRenderer.", MessageType.Info);
                return;
            }
            if (GUILayout.Button("Split"))
            {
                ResetRenderGameObjects();
                SplitSkinnedMeshRenderers();
                ResetCamera();
            }
            if (renderGOs == null || renderGOs.Count == 0)
                return;
            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                RendererPreview();
                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                using (new EditorGUI.DisabledGroupScope(inactiveGOs == null || inactiveGOs.Count == 0))
                {
                    if (GUILayout.Button("Undo"))
                    {
                        UndoInactiveGO();
                    }
                }

                if (GUILayout.Button("Reset"))
                {
                    while (inactiveGOs.Count > 0)
                    {
                        UndoInactiveGO();
                    }
                }

                GUILayout.FlexibleSpace();
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();

                if (GUILayout.Button("Export"))
                {
                    MergeAndExportSkinnedMeshes();
                }

                GUILayout.FlexibleSpace();
            }
        }

        void UndoInactiveGO()
        {
            var undoGO = inactiveGOs.Pop();
            undoGO.GetComponent<Renderer>().enabled = true;
            undoGO.GetComponent<Collider>().enabled = true;
        }

        void ResetCamera()
        {
            previewRender.camera.clearFlags = CameraClearFlags.SolidColor;
            previewRender.camera.transform.position = new Vector3(0, 0, Mathf.Max(bounds.size.x, bounds.size.y) * 4f);
            previewRender.camera.transform.rotation = Quaternion.Euler(0, 180f, 0);
            centerPos = Vector3.zero;
        }

        void ResetRenderGameObjects()
        {
            if (renderGOs != null)
            {
                foreach (var go in renderGOs)
                {
                    DestroyImmediate(go.GetComponent<MeshCollider>().sharedMesh);
                    DestroyImmediate(go);
                }
            }
            renderGOs = new List<GameObject>();
            inactiveGOs = new Stack<GameObject>();
        }

        void RendererPreview()
        {
            var rect = GUILayoutUtility.GetRect(512f, 512f);
            previewRender.BeginPreview(rect, GUIStyle.none);
            var previewCam = previewRender.camera;
            if (rect.Contains(Event.current.mousePosition))
            {
                if (Event.current.type == EventType.MouseDrag && Event.current.button == 1)
                {
                    var drag = Event.current.delta;
                    if (drag != Vector2.zero)
                    {
                        previewCam.transform.RotateAround(centerPos, previewCam.transform.rotation * Vector3.up, drag.x);
                        previewCam.transform.RotateAround(centerPos, previewCam.transform.rotation * Vector3.right, drag.y);
                        Repaint();
                    }
                }
                else if (Event.current.type == EventType.MouseDrag && Event.current.button == 2)
                {
                    var drag = Event.current.delta;
                    if (drag != Vector2.zero)
                    {
                        drag.x *= -1f;
                        var limit = Mathf.Max(new float[] { bounds.extents.x, bounds.extents.y, bounds.extents.z });
                        var deltaPos = previewCam.transform.rotation * drag * 0.002f;
                        deltaPos.x = Mathf.Clamp(deltaPos.x + centerPos.x, -limit, limit) - centerPos.x;
                        deltaPos.y = Mathf.Clamp(deltaPos.y + centerPos.y, -limit, limit) - centerPos.y;
                        deltaPos.z = Mathf.Clamp(deltaPos.z + centerPos.z, -limit, limit) - centerPos.z;
                        centerPos += deltaPos;
                        previewCam.transform.position += deltaPos;
                        Repaint();
                    }
                }
                else if (Event.current.type == EventType.ScrollWheel)
                {
                    var scroll = Event.current.delta.y;
                    if (scroll != 0)
                    {
                        var cameraPos = previewCam.transform.position - centerPos;
                        var farLimit = Mathf.Max(new float[] { bounds.size.x, bounds.size.y, bounds.size.z }) * 4f;
                        previewCam.transform.position = centerPos + cameraPos.normalized * Mathf.Clamp(cameraPos.magnitude + scroll * 0.1f, 0.001f, farLimit);
                        Repaint();
                    }
                }
                else if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                {
                    var screenPoint = Event.current.mousePosition - rect.min;
                    screenPoint *= new Vector2(previewCam.pixelWidth, previewCam.pixelHeight) / rect.size;
                    screenPoint.y = previewCam.pixelHeight - screenPoint.y;
                    var ray = previewCam.ScreenPointToRay(screenPoint);
                    var physScene = PhysicsSceneExtensions.GetPhysicsScene(previewCam.scene);
                    RaycastHit hitInfo;
                    physScene.Raycast(ray.origin, ray.direction, out hitInfo, 10f);
                    if (hitInfo.collider)
                    {
                        hitInfo.collider.GetComponent<Renderer>().enabled = false;
                        hitInfo.collider.enabled = false;
                        inactiveGOs.Push(hitInfo.collider.gameObject);
                        Repaint();
                    }
                };
            }
            if (bounds != null)
            {
                previewCam.nearClipPlane = Mathf.Sqrt(bounds.SqrDistance(previewCam.transform.position));
                previewCam.farClipPlane = bounds.size.magnitude + previewCam.nearClipPlane;
            }
            previewCam.Render();
            previewRender.EndAndDrawPreview(rect);
        }

        void MergeAndExportSkinnedMeshes()
        {
            var path = AssetDatabase.GetAssetPath(rootObj.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh);
            path = Path.ChangeExtension(path, ".fbx");
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var exportGO = new GameObject(rootObj.name);
            try
            {
                EditorUtility.DisplayProgressBar("Export Meshes", "Preparing.", 0);
                float max;
                var allMaterials = new HashSet<Material>();
                var skinnedMRs = renderGOs.Except(inactiveGOs).Select(go => go.GetComponent<SkinnedMeshRenderer>());
                var bonesGroup = skinnedMRs.GroupBy(r => r.bones, new BonesEqualityComparer());
                var outMeshIndex = 0;
                foreach (var bMeshes in bonesGroup)
                {
                    var blendShapeDict = new Dictionary<string, List<CombineInstance>>();
                    var materials = new Queue<Material>();
                    Matrix4x4[] bindposes = null;
                    var materialGropu = bMeshes.GroupBy(r => r.sharedMaterial);
                    foreach (var mMeshes in materialGropu)
                    {
                        foreach (var r in mMeshes)
                        {
                            for (var i = 0; i < r.sharedMesh.blendShapeCount; ++i)
                            {
                                var blendShapeName = r.sharedMesh.GetBlendShapeName(i);
                                if (!blendShapeDict.ContainsKey(blendShapeName))
                                    blendShapeDict.Add(blendShapeName, new List<CombineInstance>());
                            }
                        }
                    }
                    var offset = bMeshes.Key[0].root.position;
                    bMeshes.Key[0].root.position = Vector3.zero;
                    var combineInstances = new CombineInstance[materialGropu.Count()];
                    var subMeshIndex = 0;
                    foreach (var mMeshes in materialGropu)
                    {
                        var subMeshCombine = new CombineInstance[mMeshes.Count()];
                        var smCombineIndex = 0;
                        max = subMeshCombine.Length;
                        materials.Enqueue(mMeshes.Key);
                        allMaterials.Add(mMeshes.Key);
                        foreach (var r in mMeshes)
                        {
                            EditorUtility.DisplayProgressBar("Export Meshes", "Making combine instances.", smCombineIndex / max);
                            if (bindposes == null)
                                bindposes = r.sharedMesh.bindposes;
                            r.transform.position -= offset;
                            var srcMesh = r.sharedMesh;
                            subMeshCombine[smCombineIndex].mesh = srcMesh;
                            subMeshCombine[smCombineIndex].transform = r.transform.localToWorldMatrix;
                            r.transform.Reset();
                            foreach (var key in blendShapeDict.Keys)
                            {
                                var bsMesh = Instantiate(srcMesh);
                                var blendShapeIndex = srcMesh.GetBlendShapeIndex(key);
                                if (blendShapeIndex > -1)
                                {
                                    r.SetBlendShapeWeight(blendShapeIndex, 100f);
                                    r.BakeMesh(bsMesh);
                                    r.SetBlendShapeWeight(blendShapeIndex, 0f);
                                }
                                else
                                {
                                    r.BakeMesh(bsMesh);
                                }
                                var bsCombine = new CombineInstance();
                                bsCombine.mesh = bsMesh;
                                bsCombine.transform = Matrix4x4.identity;
                                blendShapeDict[key].Add(bsCombine);
                            }
                            ++smCombineIndex;
                        }
                        var subMesh = new Mesh();
                        subMesh.CombineMeshes(subMeshCombine);
                        subMesh.bindposes = bindposes;
                        combineInstances[subMeshIndex].mesh = subMesh;
                        ++subMeshIndex;
                    }
                    var outMesh = new Mesh();
                    outMesh.CombineMeshes(combineInstances, false, false);
                    Array.ForEach(combineInstances, c => DestroyImmediate(c.mesh));
                    var outVertices = outMesh.vertices;
                    max = blendShapeDict.Keys.Count();
                    var keyIndex = 0;
                    foreach (var key in blendShapeDict.Keys)
                    {
                        EditorUtility.DisplayProgressBar("Export Meshes", "Caliculating blend shapes.", keyIndex / max);
                        var bsCombines = blendShapeDict[key];
                        var bsMesh = new Mesh();
                        bsMesh.CombineMeshes(bsCombines.ToArray(), false);
                        var deltaVertices = new Vector3[outMesh.vertexCount];
                        var deltaNormals = deltaVertices.ToArray();
                        var deltaTangents = deltaVertices.ToArray();
                        var bsVertices = bsMesh.vertices;
                        deltaVertices = deltaVertices.Select((v, i) => bsVertices[i] - outVertices[i]).ToArray();
                        outMesh.AddBlendShapeFrame(key, 100, deltaVertices, deltaNormals, deltaTangents);
                        DestroyImmediate(bsMesh);
                        bsCombines.ForEach(c => DestroyImmediate(c.mesh));
                        ++keyIndex;
                    }
                    bMeshes.Key[0].parent = exportGO.transform;
                    var transformQueue = new Queue<(Vector3, Quaternion)>();
                    Array.ForEach(bMeshes.Key, t => transformQueue.Enqueue((t.position, t.rotation)));
                    Array.ForEach(bMeshes.Key, t => t.localScale = Vector3.one);
                    Array.ForEach(bMeshes.Key, t => (t.position, t.rotation) = transformQueue.Dequeue());
                    outMesh.bindposes = bMeshes.Key.Select(t => t.worldToLocalMatrix).ToArray();
                    var bindPosesCount = bindposes.Length;
                    var newWeights = new List<BoneWeight>();
                    max = outMesh.boneWeights.Length;
                    var weightIndex = 0;
                    foreach (var weight in outMesh.boneWeights)
                    {
                        EditorUtility.DisplayProgressBar("Export Meshes", "Reordering bone index.", weightIndex / max);
                        var newWeight = weight;
                        if (weight.boneIndex0 > -1)
                            newWeight.boneIndex0 %= bindPosesCount;
                        if (weight.boneIndex1 > -1)
                            newWeight.boneIndex1 %= bindPosesCount;
                        if (weight.boneIndex2 > -1)
                            newWeight.boneIndex2 %= bindPosesCount;
                        if (weight.boneIndex3 > -1)
                            newWeight.boneIndex3 %= bindPosesCount;
                        newWeights.Add(newWeight);
                        ++weightIndex;
                    }
                    EditorUtility.DisplayProgressBar("Export Meshes", "Export assets.", 1f);
                    outMesh.boneWeights = newWeights.ToArray();
                    var newMeshGO = new GameObject("Mesh" + outMeshIndex);
                    var newSkinned = newMeshGO.AddComponent<SkinnedMeshRenderer>();
                    newSkinned.sharedMaterials = materials.ToArray();
                    newSkinned.sharedMesh = outMesh;
                    newSkinned.bones = bMeshes.Key;
                    newMeshGO.transform.parent = exportGO.transform;
                    ++outMeshIndex;
                }
                var settings = ExportModelSettingsSerializeType.GetConstructor(new Type[] { }).Invoke(new object[] { });
                var fieldInfo = ExportModelSettingsSerializeType.BaseType.GetField("exportFormat", BindingFlags.Instance | BindingFlags.NonPublic);
                fieldInfo.SetValue(settings, Enum.ToObject(fieldInfo.FieldType, 1));
                ModelExporterType.GetMethod("ExportObject", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(UnityEngine.Object), ExportModelSettingsSerializeType.GetInterface("IExportOptions") }, null).Invoke(null, new object[] { path, exportGO, settings });
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                foreach (var m in allMaterials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(m), m);
                importer.importBlendShapeNormals = ModelImporterNormals.Import;
                importer.SaveAndReimport();
            }
            catch
            {
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                ResetRenderGameObjects();
                Array.ForEach(exportGO.GetComponentsInChildren<SkinnedMeshRenderer>(), r => DestroyImmediate(r.sharedMesh));
                DestroyImmediate(exportGO);
            }
        }

        class BonesEqualityComparer : IEqualityComparer<Transform[]>
        {

            public bool Equals(Transform[] x, Transform[] y)
            {
                return x.SequenceEqual(y);
            }

            public int GetHashCode(Transform[] transforms)
            {
                var hash = 0;
                Array.ForEach(transforms, t => hash ^= t.GetHashCode());
                return hash;
            }

        }

        void SplitSkinnedMeshRenderers()
        {
            var rootObjCopy = Instantiate(rootObj);
            var skinnedMRs = rootObjCopy.GetComponentsInChildren<SkinnedMeshRenderer>();
            bounds = new Bounds();
            foreach (var skinned in skinnedMRs)
                bounds.Encapsulate(skinned.bounds);
            rootObjCopy.hideFlags = HideFlags.HideAndDontSave;
            rootObjCopy.transform.position = -bounds.center;
            rootObjCopy.transform.rotation = Quaternion.identity;
            bounds.center = Vector3.zero;
            previewRender.AddSingleGO(rootObjCopy);
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
                            var mesh = Instantiate(srcMesh);
                            mesh.hideFlags = HideFlags.HideAndDontSave;
                            mesh.triangles = adjacentTriangles[0].triangles.ToArray();
                            var renderGO = new GameObject();
                            var newSkinned = renderGO.AddComponent<SkinnedMeshRenderer>();
                            newSkinned.sharedMaterial = skinned.sharedMaterials[i];
                            newSkinned.bones = skinned.bones;
                            newSkinned.sharedMesh = mesh;
                            renderGO.AddComponent<MeshCollider>().sharedMesh = mesh;
                            renderGO.hideFlags = HideFlags.DontSave;
                            renderGO.transform.position = skinned.transform.position;
                            renderGO.transform.rotation = skinned.transform.rotation;
                            renderGO.transform.localScale = skinned.transform.localScale;
                            renderGOs.Add(renderGO);
                            previewRender.AddSingleGO(renderGO);
                        }
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
            Array.ForEach(Array.FindAll(rootObjCopy.GetComponentsInChildren<Component>(true), c => !(c is Transform)), c => DestroyImmediate(c));
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

