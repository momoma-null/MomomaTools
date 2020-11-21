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

        readonly static Color s_RangeColor = new Color(0f, 1f, 1f, 0.2f);
        readonly static MethodInfo s_OverlapBoxMethodInfo = typeof(Physics).GetMethod("OverlapBox_Internal", BindingFlags.Static | BindingFlags.NonPublic);

        static AddRequest s_Request;
        static Type ModelExporterType = Type.GetType(ModelExporterTypeName);
        static Type ExportModelSettingsSerializeType = Type.GetType(ExportModelSettingsSerializeTypeName);

        PreviewRenderUtility previewRender;
        Vector2 dragBeginPosition;
        Vector2 dragEndPosition;
        Color backgroundColor;
        HashSet<GameObject> renderGOs;
        Stack<HashSet<GameObject>> inactiveGOs;
        Animator rootAnimator;
        GameObject rootObjCopy;
        Vector3 centerPos;
        Bounds bounds;

        [MenuItem("MomomaTools/SkinnedMeshEditor")]
        static void ShowWindow()
        {
            EditorWindow.GetWindow<SkinnedMeshEditor>("SkinnedMeshEditor");
        }

        void OnEnable()
        {
            this.minSize = new Vector2(512f, 592f);
            InitializeObjects();
            ResetCamera();
        }

        void OnDisable()
        {
            ClearObjects();
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

            using (var changeCheck = new EditorGUI.ChangeCheckScope())
            {
                rootAnimator = EditorGUILayout.ObjectField(rootAnimator, typeof(Animator), true) as Animator;
                if (!rootAnimator)
                {
                    EditorGUILayout.HelpBox("Select Avatar Object.", MessageType.Info);
                    return;
                }
                if (rootAnimator.GetComponentsInChildren<SkinnedMeshRenderer>().Length == 0)
                {
                    EditorGUILayout.HelpBox("Select Object including SkinnedMeshRenderer.", MessageType.Info);
                    return;
                }
                if (changeCheck.changed)
                {
                    ClearObjects();
                }
            }

            using (new EditorGUILayout.HorizontalScope())
            {
                GUILayout.FlexibleSpace();
                if (GUILayout.Button("Split"))
                {
                    ClearObjects();
                    InitializeObjects();
                    SplitSkinnedMeshRenderers();
                    ResetCamera();
                }
                GUILayout.FlexibleSpace();
            }

            if (renderGOs.Count == 0)
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
                if (GUILayout.Button("Export"))
                {
                    MergeAndExportSkinnedMeshes();
                }
                GUILayout.FlexibleSpace();
            }
        }

        void InitializeObjects()
        {
            renderGOs = new HashSet<GameObject>();
            inactiveGOs = new Stack<HashSet<GameObject>>();
            previewRender = new PreviewRenderUtility();
            previewRender.lights[0].transform.rotation = Quaternion.Euler(50f, 150f, 0f);
            ResetRange();
        }

        void ClearObjects()
        {
            if (renderGOs != null)
            {
                foreach (var go in renderGOs)
                {
                    DestroyImmediate(go.GetComponent<SkinnedMeshRenderer>().sharedMesh);
                    DestroyImmediate(go.GetComponent<MeshCollider>().sharedMesh);
                    DestroyImmediate(go);
                }
            }
            if (inactiveGOs != null)
            {
                foreach (var gos in inactiveGOs)
                {
                    foreach (var go in gos)
                    {
                        DestroyImmediate(go.GetComponent<SkinnedMeshRenderer>().sharedMesh);
                        DestroyImmediate(go.GetComponent<MeshCollider>().sharedMesh);
                        DestroyImmediate(go);
                    }
                }
            }
            renderGOs.Clear();
            inactiveGOs.Clear();
            previewRender?.Cleanup();
            previewRender = null;
        }

        void RendererPreview()
        {
            using (new EditorGUILayout.VerticalScope())
            {
                var rect = GUILayoutUtility.GetRect(512f, 512f);
                previewRender.BeginPreview(rect, GUIStyle.none);
                var previewCam = previewRender.camera;
                if (rect.Contains(Event.current.mousePosition))
                {
                    if (Event.current.type == EventType.MouseDrag && Event.current.button == 1)
                    {
                        ResetRange();
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
                        ResetRange();
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
                            var farLimit = bounds.size.magnitude * 4f;
                            previewCam.transform.position = centerPos + cameraPos.normalized * Mathf.Clamp(cameraPos.magnitude + scroll * 0.1f, 0.01f, farLimit);
                            Repaint();
                        }
                    }
                    else if (Event.current.type == EventType.MouseDrag && Event.current.button == 0)
                    {
                        dragEndPosition = Event.current.mousePosition;
                        if (dragBeginPosition == Vector2.zero)
                        {
                            dragBeginPosition = dragEndPosition;
                        }
                        Repaint();
                    }
                    else if (Event.current.type == EventType.MouseDown && Event.current.button == 0)
                    {
                        ResetRange();
                        dragBeginPosition = Event.current.mousePosition;
                        dragEndPosition = dragBeginPosition;
                    }
                    else if (Event.current.type == EventType.MouseUp && Event.current.button == 0)
                    {
                        var endRay = MouseToRay(Event.current.mousePosition, rect, previewCam);
                        var beginRay = MouseToRay(dragBeginPosition, rect, previewCam);
                        var physicsScene = PhysicsSceneExtensions.GetPhysicsScene(previewCam.scene);
                        try
                        {
                            Physics.queriesHitBackfaces = true;
                            if (dragBeginPosition == dragEndPosition)
                            {
                                RaycastHit hit;
                                physicsScene.Raycast(endRay.origin, endRay.direction, out hit, 10f);
                                if (hit.collider)
                                {
                                    PushInactiveGO(new HashSet<GameObject>() { hit.collider.gameObject });
                                }
                            }
                            else
                            {
                                var position = (endRay.origin + beginRay.origin) * 0.5f + 5f * endRay.direction;
                                var rotation = Quaternion.LookRotation(endRay.direction, previewCam.transform.up);
                                var size = previewCam.transform.InverseTransformPoint(endRay.origin) - previewCam.transform.InverseTransformPoint(beginRay.origin);
                                size.x = Math.Abs(size.x * 0.5f);
                                size.y = Math.Abs(size.y * 0.5f);
                                size.z = 10f;
                                var cols = s_OverlapBoxMethodInfo.Invoke(null, new object[] { physicsScene, position, size, rotation, Physics.AllLayers, QueryTriggerInteraction.UseGlobal }) as Collider[];
                                if (cols.Length > 0)
                                {
                                    PushInactiveGO(cols.Select(c => c.gameObject));
                                }
                            }
                        }
                        catch
                        {
                            throw;
                        }
                        finally
                        {
                            Physics.queriesHitBackfaces = false;
                        }
                        Repaint();
                        ResetRange();
                    }
                }
                else if (Event.current.type != EventType.Layout)
                {
                    ResetRange();
                }
                previewCam.nearClipPlane = Mathf.Sqrt(bounds.SqrDistance(previewCam.transform.position)) + 0.001f;
                previewCam.farClipPlane = bounds.size.magnitude + previewCam.nearClipPlane;
                previewCam.Render();
                previewRender.EndAndDrawPreview(rect);

                EditorGUI.DrawRect(new Rect(dragBeginPosition, dragEndPosition - dragBeginPosition), s_RangeColor);

                using (new EditorGUILayout.HorizontalScope())
                {
                    EditorGUI.BeginChangeCheck();
                    backgroundColor = EditorGUILayout.ColorField("Background Color", backgroundColor);
                    if (EditorGUI.EndChangeCheck())
                    {
                        previewCam.backgroundColor = backgroundColor;
                        Repaint();
                    }

                    GUILayout.FlexibleSpace();

                    using (new EditorGUI.DisabledGroupScope(inactiveGOs.Count == 0))
                    {
                        if (GUILayout.Button("Undo"))
                        {
                            UndoInactiveGO();
                        }
                        if (GUILayout.Button("Reset"))
                        {
                            while (inactiveGOs.Count > 0)
                            {
                                UndoInactiveGO();
                            }
                        }
                    }
                    GUILayout.FlexibleSpace();
                }
            }
        }

        static Ray MouseToRay(Vector2 mousePosition, Rect rect, Camera cam)
        {
            var screenPoint = mousePosition - rect.min;
            screenPoint *= new Vector2(cam.pixelWidth, cam.pixelHeight) / rect.size;
            screenPoint.y = cam.pixelHeight - screenPoint.y;
            return cam.ScreenPointToRay(screenPoint);
        }

        void PushInactiveGO(IEnumerable<GameObject> gos)
        {
            foreach (var go in gos)
            {
                go.SetActive(false);
                renderGOs.Remove(go);
            }
            inactiveGOs.Push(new HashSet<GameObject>(gos));
        }

        void UndoInactiveGO()
        {
            foreach (var go in inactiveGOs.Pop())
            {
                go.SetActive(true);
                renderGOs.Add(go);
            }
        }

        void ResetCamera()
        {
            previewRender.camera.clearFlags = CameraClearFlags.SolidColor;
            previewRender.camera.transform.position = new Vector3(0, 0, Mathf.Max(bounds.size.x, bounds.size.y) * 4f);
            previewRender.camera.transform.rotation = Quaternion.Euler(0, 180f, 0);
            centerPos = Vector3.zero;
        }

        void ResetRange()
        {
            dragBeginPosition = Vector2.zero;
            dragEndPosition = Vector2.zero;
        }

        void MergeAndExportSkinnedMeshes()
        {
            var path = AssetDatabase.GetAssetPath(rootAnimator.GetComponentInChildren<SkinnedMeshRenderer>().sharedMesh);
            path = Path.ChangeExtension(path, ".fbx");
            path = AssetDatabase.GenerateUniqueAssetPath(path);
            var exportGO = rootObjCopy;
            exportGO.transform.position = Vector3.zero;
            exportGO.transform.rotation = Quaternion.identity;
            try
            {
                EditorUtility.DisplayProgressBar("Export Meshes", "Preparing.", 0);
                float max;
                var skinnedMRs = renderGOs.Select(go => go.GetComponent<SkinnedMeshRenderer>());
                var allBones = new HashSet<Transform>();
                var combinedBones = new List<Transform>();
                var rootBones = new HashSet<Transform>();
                var blendShapeDict = new Dictionary<string, List<CombineInstance>>();
                var materials = new HashSet<Material>();
                var materialGropu = skinnedMRs.GroupBy(r => r.sharedMaterial);
                foreach (var mMeshes in materialGropu)
                {
                    foreach (var r in mMeshes)
                    {
                        for (var i = 0; i < r.sharedMesh.blendShapeCount; ++i)
                        {
                            r.SetBlendShapeWeight(i, 0f);
                            var blendShapeName = r.sharedMesh.GetBlendShapeName(i);
                            if (!blendShapeDict.ContainsKey(blendShapeName))
                                blendShapeDict.Add(blendShapeName, new List<CombineInstance>());
                        }
                    }
                }
                var combines = new List<(CombineInstance, Transform[])[]>();
                foreach (var mMeshes in materialGropu)
                {
                    var meshCount = mMeshes.Count();
                    if (meshCount == 0)
                        continue;
                    var subMeshCombines = new (CombineInstance, Transform[])[meshCount];
                    var smCombineIndex = 0;
                    max = meshCount;
                    materials.Add(mMeshes.Key);
                    foreach (var r in mMeshes)
                    {
                        EditorUtility.DisplayProgressBar("Export Meshes", "Making combine instances.", smCombineIndex / max);
                        var srcMesh = r.sharedMesh;
                        allBones.UnionWith(r.bones);
                        combinedBones.AddRange(r.bones);
                        rootBones.Add(r.bones[0]);
                        var srcBakedMesh = new Mesh();
                        r.BakeMesh(srcBakedMesh);
                        srcBakedMesh.boneWeights = srcMesh.boneWeights;
                        subMeshCombines[smCombineIndex].Item1.mesh = srcBakedMesh;
                        subMeshCombines[smCombineIndex].Item2 = r.bones;
                        foreach (var key in blendShapeDict.Keys)
                        {
                            Mesh bsMesh;
                            var blendShapeIndex = srcMesh.GetBlendShapeIndex(key);
                            if (blendShapeIndex > -1)
                            {
                                bsMesh = new Mesh();
                                r.SetBlendShapeWeight(blendShapeIndex, 100f);
                                r.BakeMesh(bsMesh);
                                r.SetBlendShapeWeight(blendShapeIndex, 0f);
                            }
                            else
                            {
                                bsMesh = Instantiate(srcBakedMesh);
                            }
                            var bsCombine = new CombineInstance();
                            bsCombine.mesh = bsMesh;
                            blendShapeDict[key].Add(bsCombine);
                        }
                        ++smCombineIndex;
                    }
                    combines.Add(subMeshCombines);
                }
                EditorUtility.DisplayProgressBar("Export Meshes", "Normalizing bone transforms.", 0f);
                var transforms = exportGO.GetComponentsInChildren<Transform>(true);
                var unusedTfs = transforms.Except(allBones).Where(t => rootBones.All(r => !r.IsChildOf(t))).Reverse().ToArray();
                foreach (var t in unusedTfs)
                    DestroyImmediate(t.gameObject);
                var realRootBones = rootBones.Where(root => !rootBones.Any(t => t != root && root.IsChildOf(t)));
                {
                    var parentQueue = new Queue<Transform>();
                    foreach (var root in realRootBones)
                    {
                        parentQueue.Enqueue(root.parent);
                        root.parent = null;
                    }
                    var tfs = exportGO.GetComponentsInChildren<Transform>();
                    var positionQueue = new Queue<Vector3>();
                    foreach (var t in tfs)
                        positionQueue.Enqueue(t.position);
                    foreach (var t in tfs)
                    {
                        t.localPosition = Vector3.zero;
                        t.localRotation = Quaternion.identity;
                        t.localScale = Vector3.one;
                    }
                    foreach (var t in tfs)
                        t.position = positionQueue.Dequeue();
                    foreach (var root in realRootBones)
                        root.parent = parentQueue.Dequeue();
                }
                foreach (var root in realRootBones)
                {
                    var tfs = root.GetComponentsInChildren<Transform>();
                    var parentQueue = new Queue<Transform>();
                    foreach (var t in tfs)
                    {
                        parentQueue.Enqueue(t.parent);
                        t.parent = null;
                    }
                    foreach (var t in tfs)
                        t.localScale = Vector3.one;
                    foreach (var t in tfs)
                        t.parent = parentQueue.Dequeue();
                }
                var combineInstances = new CombineInstance[combines.Count];
                var subMeshIndex = 0;
                foreach (var combine in combines)
                {
                    var subMeshCombines = new Queue<CombineInstance>();
                    foreach (var subCombine in combine)
                    {
                        subCombine.Item1.mesh.bindposes = subCombine.Item2.Select(t => t.worldToLocalMatrix).ToArray();
                        subMeshCombines.Enqueue(subCombine.Item1);
                    }
                    var subMesh = new Mesh();
                    subMesh.CombineMeshes(subMeshCombines.ToArray(), true, false);
                    foreach (var c in subMeshCombines)
                        DestroyImmediate(c.mesh);
                    combineInstances[subMeshIndex].mesh = subMesh;
                    ++subMeshIndex;
                }
                var outMesh = new Mesh();
                outMesh.MarkDynamic();
                outMesh.CombineMeshes(combineInstances, false, false);
                foreach (var c in combineInstances)
                    DestroyImmediate(c.mesh);
                var outVertices = outMesh.vertices;
                max = blendShapeDict.Keys.Count();
                var keyIndex = 0;
                var vertexCount = outMesh.vertexCount;
                foreach (var key in blendShapeDict.Keys)
                {
                    EditorUtility.DisplayProgressBar("Export Meshes", "Caliculating blend shapes.", keyIndex / max);
                    var bsCombines = blendShapeDict[key];
                    var bsMesh = new Mesh();
                    bsMesh.CombineMeshes(bsCombines.ToArray(), false, false);
                    foreach (var c in bsCombines)
                        DestroyImmediate(c.mesh);
                    var deltaVertices = new Vector3[vertexCount];
                    var deltaNormals = new Vector3[vertexCount];
                    var deltaTangents = new Vector3[vertexCount];
                    var bsVertices = bsMesh.vertices;
                    deltaVertices = deltaVertices.Select((v, i) => bsVertices[i] - outVertices[i]).ToArray();
                    outMesh.AddBlendShapeFrame(key, 100, deltaVertices, deltaNormals, deltaTangents);
                    DestroyImmediate(bsMesh);
                    ++keyIndex;
                }
                EditorUtility.DisplayProgressBar("Export Meshes", "Normalizing the Mesh data.", 0f);
                var orderedBones = allBones.OrderBy(t => Array.IndexOf(transforms, t)).ToArray();
                var orderedBonesDict = new Dictionary<Transform, int>();
                var boneIndex = 0;
                foreach (var t in orderedBones)
                {
                    orderedBonesDict[t] = boneIndex;
                    ++boneIndex;
                }
                outMesh.boneWeights = outMesh.boneWeights.Select(weight =>
                {
                    if (weight.boneIndex0 > -1) weight.boneIndex0 = orderedBonesDict[combinedBones[weight.boneIndex0]];
                    if (weight.boneIndex1 > -1) weight.boneIndex1 = orderedBonesDict[combinedBones[weight.boneIndex1]];
                    if (weight.boneIndex2 > -1) weight.boneIndex2 = orderedBonesDict[combinedBones[weight.boneIndex2]];
                    if (weight.boneIndex3 > -1) weight.boneIndex3 = orderedBonesDict[combinedBones[weight.boneIndex3]];
                    return weight;
                }).ToArray();
                outMesh.bindposes = orderedBones.Select(t => t.worldToLocalMatrix).ToArray();
                outMesh.normals = outMesh.normals.Select(n => n.normalized).ToArray();
                MeshUtility.Optimize(outMesh);
                var newMeshGO = new GameObject("body");
                var newSkinned = newMeshGO.AddComponent<SkinnedMeshRenderer>();
                newSkinned.sharedMaterials = materials.ToArray();
                newSkinned.sharedMesh = outMesh;
                newSkinned.bones = orderedBones;
                newMeshGO.transform.parent = exportGO.transform;
                EditorUtility.DisplayProgressBar("Export Meshes", "Export assets.", 1f);
                var settings = ExportModelSettingsSerializeType.GetConstructor(new Type[] { }).Invoke(new object[] { });
                var fieldInfo = ExportModelSettingsSerializeType.BaseType.GetField("exportFormat", BindingFlags.Instance | BindingFlags.NonPublic);
                fieldInfo.SetValue(settings, Enum.ToObject(fieldInfo.FieldType, 1));
                ModelExporterType.GetMethod("ExportObject", BindingFlags.Static | BindingFlags.NonPublic, null, new Type[] { typeof(string), typeof(UnityEngine.Object), ExportModelSettingsSerializeType.GetInterface("IExportOptions") }, null).Invoke(null, new object[] { path, exportGO, settings });
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                foreach (var m in materials)
                    importer.AddRemap(new AssetImporter.SourceAssetIdentifier(m), m);
                importer.importBlendShapeNormals = ModelImporterNormals.None;
                importer.SaveAndReimport();
            }
            catch
            {
                throw;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
                DestroyImmediate(exportGO);
                ClearObjects();
                InitializeObjects();
            }
        }

        void SplitSkinnedMeshRenderers()
        {
            rootObjCopy = Instantiate(rootAnimator.gameObject);
            var skinnedMRs = rootObjCopy.GetComponentsInChildren<SkinnedMeshRenderer>();
            rootObjCopy.transform.position = Vector3.zero;
            rootObjCopy.transform.rotation = Quaternion.identity;
            bounds = new Bounds();
            foreach (var skinned in skinnedMRs)
                bounds.Encapsulate(skinned.bounds);
            rootObjCopy.transform.position = -bounds.center;
            bounds.center = Vector3.zero;
            previewRender.AddSingleGO(rootObjCopy);
            foreach (var skinned in skinnedMRs)
            {
                var adjacentTriangles = new List<AdjacentTriangle>();
                var srcMesh = skinned.sharedMesh;
                var srcVertices = srcMesh.vertices;
                var length = srcVertices.Length;
                var vertexIDs = new Dictionary<Vector3, int>();
                var convertIndices = srcVertices.Select((v, i) => vertexIDs.ContainsKey(v) ? vertexIDs[v] : vertexIDs[v] = i).ToArray();
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
                                        j -= 3;
                                        again = true;
                                    }
                                }
                            }
                            while (again);
                            var mesh = Instantiate(srcMesh);
                            mesh.hideFlags = HideFlags.HideAndDontSave;
                            mesh.triangles = adjacentTriangles[0].GetTriangles();
                            var renderGO = new GameObject();
                            renderGO.hideFlags = HideFlags.DontSave;
                            var newSkinned = renderGO.AddComponent<SkinnedMeshRenderer>();
                            newSkinned.sharedMaterial = skinned.sharedMaterials[i];
                            newSkinned.bones = skinned.bones;
                            newSkinned.sharedMesh = mesh;
                            var bakedMesh = new Mesh();
                            newSkinned.BakeMesh(bakedMesh);
                            bakedMesh.hideFlags = HideFlags.HideAndDontSave;
                            renderGO.AddComponent<MeshCollider>().sharedMesh = bakedMesh;
                            renderGOs.Add(renderGO);
                            previewRender.AddSingleGO(renderGO);
                        }
                    }
                    catch
                    {
                        throw;
                    }
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
            foreach (var t in rootObjCopy.GetComponentsInChildren<Transform>(true))
                UnityEditorInternal.ComponentUtility.DestroyComponentsMatching(t.gameObject, c => !(c is Transform));
        }

        class AdjacentTriangle
        {
            readonly HashSet<(int, int)> lines = new HashSet<(int, int)>();
            readonly Queue<int> triangles = new Queue<int>();
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
                }
                else if (lines.Remove(newLines[1]))
                {
                    lines.Add(newLines[0]);
                    lines.Add(newLines[2]);
                }
                else if (lines.Remove(newLines[2]))
                {
                    lines.Add(newLines[0]);
                    lines.Add(newLines[1]);
                }
                else
                {
                    return false;
                }
                triangles.Enqueue(index0);
                triangles.Enqueue(index1);
                triangles.Enqueue(index2);
                return true;
            }

            internal int[] GetTriangles()
            {
                return triangles.ToArray();
            }
        }
    }

}// namespace MomomaAssets

