using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

namespace MomomaAssets
{

    public class SkinnedMeshEditor : EditorWindow
    {
        const string ModelExporterTypeName = "UnityEditor.Formats.Fbx.Exporter.ModelExporter, Unity.Formats.Fbx.Editor";
        const string ExportModelSettingsSerializeTypeName = "UnityEditor.Formats.Fbx.Exporter.ExportModelSettingsSerialize, Unity.Formats.Fbx.Editor";

        readonly static Color s_RangeColor = new Color(0f, 1f, 1f, 0.2f);
#if !UNITY_2019_4_OR_NEWER
        readonly static MethodInfo s_OverlapBoxMethodInfo = typeof(Physics).GetMethod("OverlapBox_Internal", BindingFlags.Static | BindingFlags.NonPublic);
#endif

        static AddRequest s_Request;
        static Type ModelExporterType = Type.GetType(ModelExporterTypeName);
        static Type ExportModelSettingsSerializeType = Type.GetType(ExportModelSettingsSerializeTypeName);

        PreviewRenderUtility previewRender;
        Vector2 dragBeginPosition;
        Vector2 dragEndPosition;
        HashSet<GameObject> renderGOs;
        Stack<HashSet<GameObject>> inactiveGOs;
        Animator rootAnimator;
        GameObject rootObjCopy;
        Vector3 centerPos;
        Bounds bounds;

        [SerializeField]
        Color backgroundColor = new Color(0.5f, 0.6f, 0.6f, 1f);

        [MenuItem("MomomaTools/SkinnedMeshEditor")]
        static void ShowWindow()
        {
            GetWindow<SkinnedMeshEditor>(nameof(SkinnedMeshEditor));
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
                var currentEvent = Event.current;
                if (rect.Contains(Event.current.mousePosition))
                {
                    if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 1)
                    {
                        ResetRange();
                        var drag = currentEvent.delta;
                        if (drag != Vector2.zero)
                        {
                            previewCam.transform.RotateAround(centerPos, Vector3.up, drag.x);
                            previewCam.transform.RotateAround(centerPos, previewCam.transform.rotation * Vector3.right, drag.y);
                            Repaint();
                        }
                        currentEvent.Use();
                    }
                    else if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 2)
                    {
                        ResetRange();
                        var drag = currentEvent.delta;
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
                        currentEvent.Use();
                    }
                    else if (currentEvent.type == EventType.ScrollWheel)
                    {
                        var scroll = Event.current.delta.y;
                        if (scroll != 0)
                        {
                            var cameraPos = previewCam.transform.position - centerPos;
                            var farLimit = bounds.size.magnitude * 4f;
                            previewCam.transform.position = centerPos + cameraPos.normalized * Mathf.Clamp(cameraPos.magnitude + scroll * 0.1f, 0.01f, farLimit);
                            Repaint();
                        }
                        currentEvent.Use();
                    }
                    else if (currentEvent.type == EventType.MouseDrag && currentEvent.button == 0)
                    {
                        dragEndPosition = currentEvent.mousePosition;
                        if (dragBeginPosition == Vector2.zero)
                        {
                            dragBeginPosition = dragEndPosition;
                        }
                        Repaint();
                        currentEvent.Use();
                    }
                    else if (currentEvent.type == EventType.MouseDown && currentEvent.button == 0)
                    {
                        ResetRange();
                        dragBeginPosition = currentEvent.mousePosition;
                        dragEndPosition = dragBeginPosition;
                        currentEvent.Use();
                    }
                    else if (currentEvent.type == EventType.MouseUp && currentEvent.button == 0)
                    {
                        var endRay = MouseToRay(currentEvent.mousePosition, rect, previewCam);
                        var beginRay = MouseToRay(dragBeginPosition, rect, previewCam);
                        var physicsScene = PhysicsSceneExtensions.GetPhysicsScene(previewCam.scene);
                        try
                        {
                            Physics.queriesHitBackfaces = true;
                            if (dragBeginPosition == dragEndPosition)
                            {
                                physicsScene.Raycast(endRay.origin, endRay.direction, out var hit, 10f);
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
#if UNITY_2019_4_OR_NEWER
                                var cols = new Collider[renderGOs.Count];
                                var count = physicsScene.OverlapBox(position, size, cols, rotation, Physics.AllLayers, QueryTriggerInteraction.UseGlobal);
                                if (count > 0)
                                {
                                    PushInactiveGO(cols.Take(count).Select(c => c.gameObject));
                                }
#else
                                var cols = s_OverlapBoxMethodInfo.Invoke(null, new object[] { physicsScene, position, size, rotation, Physics.AllLayers, QueryTriggerInteraction.UseGlobal }) as Collider[];
                                if (cols.Length > 0)
                                {
                                    PushInactiveGO(cols.Select(c => c.gameObject));
                                }
#endif
                            }
                        }
                        finally
                        {
                            Physics.queriesHitBackfaces = false;
                        }
                        Repaint();
                        ResetRange();
                    }
                }
                else if (currentEvent.type != EventType.Layout)
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
            previewRender.camera.backgroundColor = backgroundColor;
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
            var outMesh = new Mesh() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            try
            {
                EditorUtility.DisplayProgressBar("Export Meshes", "Preparing.", 0);
                float max;
                var skinnedMRs = renderGOs.Select(go => go.GetComponent<SkinnedMeshRenderer>()).ToArray();
                var allBones = new HashSet<Transform>();
                var combinedBones = new List<Transform>();
                var rootBones = new HashSet<Transform>();
                var blendShapeDict = new Dictionary<string, List<CombineInstance>>();
                var materials = new HashSet<Material>();
                foreach (var r in skinnedMRs)
                {
                    for (var i = 0; i < r.sharedMesh.blendShapeCount; ++i)
                    {
                        r.SetBlendShapeWeight(i, 0f);
                        var blendShapeName = r.sharedMesh.GetBlendShapeName(i);
                        if (!blendShapeDict.ContainsKey(blendShapeName))
                            blendShapeDict.Add(blendShapeName, new List<CombineInstance>());
                    }
                }
                var combines = new List<(CombineInstance, Transform[])[]>();
                var materialGropu = skinnedMRs.GroupBy(r => r.sharedMaterial);
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
                        var srcBones = r.bones;
                        allBones.UnionWith(srcBones);
                        combinedBones.AddRange(srcBones);
                        rootBones.Add(srcBones[0]);
                        var srcBakedMesh = new Mesh();
                        r.BakeMesh(srcBakedMesh);
                        srcBakedMesh.boneWeights = srcMesh.boneWeights;
                        subMeshCombines[smCombineIndex].Item1.mesh = srcBakedMesh;
                        subMeshCombines[smCombineIndex].Item2 = srcBones;
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
                try
                {
                    var subMeshIndex = 0;
                    foreach (var combine in combines)
                    {
                        var subMeshCombines = new CombineInstance[combine.Length];
                        for (var i = 0; i < combine.Length; ++i)
                        {
                            combine[i].Item1.mesh.bindposes = Array.ConvertAll(combine[i].Item2, t => t.worldToLocalMatrix);
                            subMeshCombines[i] = combine[i].Item1;
                        }
                        var subMesh = new Mesh() { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                        try
                        {
                            subMesh.CombineMeshes(subMeshCombines, true, false);
                        }
                        finally
                        {
                            foreach (var c in subMeshCombines)
                                DestroyImmediate(c.mesh);
                        }
                        combineInstances[subMeshIndex].mesh = subMesh;
                        ++subMeshIndex;
                    }
                    outMesh.MarkDynamic();
                    outMesh.CombineMeshes(combineInstances, false, false);
                }
                finally
                {
                    foreach (var c in combineInstances)
                        DestroyImmediate(c.mesh);
                }
                var outVertices = outMesh.vertices;
                var outNormals = outMesh.normals;
                for (var i = 0; i < outNormals.Length; ++i)
                    outNormals[i] = outNormals[i].normalized;
                max = blendShapeDict.Count;
                var keyIndex = 0;
                var vertexCount = outMesh.vertexCount;
                var deltaVertices = new Vector3[vertexCount];
                var deltaNormals = new Vector3[vertexCount];
                var deltaTangents = new Vector3[vertexCount];
                foreach (var pair in blendShapeDict)
                {
                    EditorUtility.DisplayProgressBar("Export Meshes", "Caliculating blend shapes.", keyIndex / max);
                    var bsCombines = pair.Value;
                    var bsMesh = new Mesh();
                    try
                    {
                        bsMesh.CombineMeshes(bsCombines.ToArray(), false, false);
                        foreach (var c in bsCombines)
                            DestroyImmediate(c.mesh);
                        var bsVertices = bsMesh.vertices;
                        var bsNormals = bsMesh.normals;
                        for (var i = 0; i < vertexCount; ++i)
                        {
                            deltaVertices[i] = bsVertices[i] - outVertices[i];
                            deltaNormals[i] = bsNormals[i].normalized - outNormals[i];
                        }
                        outMesh.AddBlendShapeFrame(pair.Key, 100, deltaVertices, deltaNormals, deltaTangents);
                    }
                    finally
                    {
                        DestroyImmediate(bsMesh);
                    }
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
                outMesh.boneWeights = Array.ConvertAll(outMesh.boneWeights, weight =>
                 {
                     if (weight.boneIndex0 > -1) weight.boneIndex0 = orderedBonesDict[combinedBones[weight.boneIndex0]];
                     if (weight.boneIndex1 > -1) weight.boneIndex1 = orderedBonesDict[combinedBones[weight.boneIndex1]];
                     if (weight.boneIndex2 > -1) weight.boneIndex2 = orderedBonesDict[combinedBones[weight.boneIndex2]];
                     if (weight.boneIndex3 > -1) weight.boneIndex3 = orderedBonesDict[combinedBones[weight.boneIndex3]];
                     return weight;
                 });
                outMesh.bindposes = Array.ConvertAll(orderedBones, t => t.worldToLocalMatrix);
                outMesh.normals = outNormals;
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
                ExportModelSettingsSerializeType.BaseType.GetField("mayaCompatibleNaming", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(settings, false);
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
                DestroyImmediate(outMesh);
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
                if (bounds.size == Vector3.zero)
                    bounds = skinned.bounds;
                else
                    bounds.Encapsulate(skinned.bounds);
            rootObjCopy.transform.position = -bounds.center;
            bounds.center = Vector3.zero;
            previewRender.AddSingleGO(rootObjCopy);
            var indices = new List<int>();
            var edgeToTriangles =new Dictionary<Edge, AdjacentTriangle>();
            var adjacentTriangles =new HashSet<AdjacentTriangle>();
            foreach (var skinned in skinnedMRs)
            {
                var srcMesh = skinned.sharedMesh;
                var meshName = srcMesh.name;
                var srcVertices = srcMesh.vertices;
                var vertexIDs = new Dictionary<Vector3, int>();
                var convertIndices = new int[srcVertices.Length];
                for (var i = 0; i < srcVertices.Length; ++i)
                    convertIndices[i] = vertexIDs.TryGetValue(srcVertices[i], out var index) ? index : (vertexIDs[srcVertices[i]] = i);
                for (var i = 0; i < srcMesh.subMeshCount; ++i)
                {
                    indices.Clear();
                    edgeToTriangles.Clear();
                    adjacentTriangles.Clear();
                    srcMesh.GetTriangles(indices, i);
                    try
                    {
                        EditorUtility.DisplayProgressBar("Converting...", meshName, 0f);
                        for (var j = 0; j < indices.Count; j += 3)
                        {
                            var triangle = new Triangle(indices[j], indices[j + 1], indices[j + 2]);
                            if (edgeToTriangles.TryGetValue(triangle.Edges[0], out var adjacentTriangle))
                            {
                                adjacentTriangle.AddTriangle(triangle);
                                edgeToTriangles.Remove(triangle.Edges[0]);
                                edgeToTriangles[triangle.Edges[1]] = adjacentTriangle;
                                edgeToTriangles[triangle.Edges[2]] = adjacentTriangle;
                            }
                            else if (edgeToTriangles.TryGetValue(triangle.Edges[1], out adjacentTriangle))
                            {
                                adjacentTriangle.AddTriangle(triangle);
                                edgeToTriangles.Remove(triangle.Edges[1]);
                                edgeToTriangles[triangle.Edges[2]] = adjacentTriangle;
                                edgeToTriangles[triangle.Edges[0]] = adjacentTriangle;
                            }
                            else if (edgeToTriangles.TryGetValue(triangle.Edges[2], out adjacentTriangle))
                            {
                                adjacentTriangle.AddTriangle(triangle);
                                edgeToTriangles.Remove(triangle.Edges[2]);
                                edgeToTriangles[triangle.Edges[0]] = adjacentTriangle;
                                edgeToTriangles[triangle.Edges[1]] = adjacentTriangle;
                            }
                            else
                            {
                                adjacentTriangle = new AdjacentTriangle(triangle);
                                edgeToTriangles[triangle.Edges[0]] = adjacentTriangle;
                                edgeToTriangles[triangle.Edges[1]] = adjacentTriangle;
                                edgeToTriangles[triangle.Edges[2]] = adjacentTriangle;
                            }
                        }
                        adjacentTriangles.UnionWith(edgeToTriangles.Values);
                        foreach (var j in adjacentTriangles)
                            j.ConvertIndices(convertIndices);
                        var convertedAdjacentTriangles = new Queue<AdjacentTriangle>(adjacentTriangles.Count);
                        EditorUtility.DisplayProgressBar("Converting...", meshName, 0.5f);
                        while (adjacentTriangles.Count > 0)
                        {
                            var adjacentTriangle = adjacentTriangles.First();
                            adjacentTriangles.Remove(adjacentTriangle);
                            convertedAdjacentTriangles.Enqueue(adjacentTriangle);
                            while (adjacentTriangles.RemoveWhere(adjacentTriangle.AddAdjacentTriangle) > 0) { }
                        }
                        EditorUtility.DisplayProgressBar("Converting...", meshName, 0.75f);
                        foreach (var j in convertedAdjacentTriangles)
                        {
                            var mesh = Instantiate(srcMesh);
                            mesh.hideFlags = HideFlags.HideAndDontSave;
                            mesh.triangles = j.GetTriangles();
                            var renderGO = new GameObject { hideFlags = HideFlags.DontSave };
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
                    finally
                    {
                        EditorUtility.ClearProgressBar();
                    }
                }
            }
            foreach (var t in rootObjCopy.GetComponentsInChildren<Transform>(true))
                UnityEditorInternal.ComponentUtility.DestroyComponentsMatching(t.gameObject, c => !(c is Transform));
        }

        sealed class Edge : IEquatable<Edge>
        {
            int id1;
            int id2;

            public Edge(int id1, int id2)
            {
                if (id1 < id2)
                {
                    this.id1 = id1;
                    this.id2 = id2;
                }
                else
                {
                    this.id1 = id2;
                    this.id2 = id1;
                }
            }

            public void ConvertIndices(IReadOnlyList<int> indices)
            {
                var new1 = indices[id1];
                var new2 = indices[id2];
                if (new1 < new2)
                {
                    id1 = new1;
                    id2 = new2;
                }
                else
                {
                    id1 = new2;
                    id2 = new1;
                }
            }

            public bool Equals(Edge other) => id1.Equals(other.id1) && id2.Equals(other.id2);
            public override int GetHashCode() => id1 ^ id2;
        }

        sealed class Triangle
        {
            public Edge[] Edges { get; }
            public int[] Vertices { get; }

            public Triangle(int id1, int id2, int id3)
            {
                Vertices = new[] { id1, id2, id3 };
                Edges = new[] { new Edge(id1, id2), new Edge(id2, id3), new Edge(id3, id1) };
            }
        }

        sealed class AdjacentTriangle
        {
            readonly HashSet<Edge> edges;
            readonly List<int> vertices;

            public AdjacentTriangle(Triangle triangle)
            {
                edges = new HashSet<Edge>(triangle.Edges);
                vertices = new List<int>(triangle.Vertices);
            }

            public bool AddTriangle(Triangle triangle)
            {
                var newEdges = triangle.Edges;
                if (edges.Remove(newEdges[0]))
                {
                    edges.Add(newEdges[1]);
                    edges.Add(newEdges[2]);
                }
                else if (edges.Remove(newEdges[1]))
                {
                    edges.Add(newEdges[0]);
                    edges.Add(newEdges[2]);
                }
                else if (edges.Remove(newEdges[2]))
                {
                    edges.Add(newEdges[0]);
                    edges.Add(newEdges[1]);
                }
                else
                {
                    return false;
                }
                vertices.AddRange(triangle.Vertices);
                return true;
            }

            public void ConvertIndices(IReadOnlyList<int> indices)
            {
                foreach (var i in edges)
                    i.ConvertIndices(indices);
            }

            public bool AddAdjacentTriangle(AdjacentTriangle adjacentTriangle)
            {
                if (!edges.Overlaps(adjacentTriangle.edges))
                    return false;
                edges.UnionWith(adjacentTriangle.edges);
                vertices.AddRange(adjacentTriangle.vertices);
                return true;
            }

            public int[] GetTriangles()
            {
                return vertices.ToArray();
            }
        }
    }

}// namespace MomomaAssets

