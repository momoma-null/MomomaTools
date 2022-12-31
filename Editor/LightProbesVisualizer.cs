using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MomomaAssets
{
    [InitializeOnLoad]
    static class LightProbesVisualizer
    {
        const string menuPath = "MomomaTools/Light Probes Visualizer";
        static readonly List<Matrix4x4> matrices = new List<Matrix4x4>();
        static Mesh sphereMesh;
        static Material diffuseMaterial;
        static MaterialPropertyBlock materialPropertyBlock;

        static LightProbesVisualizer()
        {
            EditorApplication.delayCall += () => Initialize();
        }

        [MenuItem(menuPath)]
        static void ToggleLock()
        {
            var enabled = !Menu.GetChecked(menuPath);
            Menu.SetChecked(menuPath, enabled);
            SceneView.duringSceneGui -= OnSceneGUI;
            if (enabled)
                SceneView.duringSceneGui += OnSceneGUI;
        }

        static void Initialize()
        {
            var temp = ObjectFactory.CreatePrimitive(PrimitiveType.Sphere);
            sphereMesh = temp.GetComponent<MeshFilter>().sharedMesh;
            UnityEngine.Object.DestroyImmediate(temp);
            diffuseMaterial = new Material(Shader.Find("Hidden/MS_LightProbes")) { hideFlags = HideFlags.HideAndDontSave, enableInstancing = true };
            materialPropertyBlock = new MaterialPropertyBlock();
            Lightmapping.lightingDataUpdated += () => RecalculateMatrices();
            EditorSceneManager.activeSceneChangedInEditMode += (x, y) => RecalculateMatrices();
            RecalculateMatrices();
            SceneView.duringSceneGui -= OnSceneGUI;
            if (Menu.GetChecked(menuPath))
                SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView view)
        {
            Graphics.DrawMeshInstanced(sphereMesh, 0, diffuseMaterial, matrices, materialPropertyBlock, ShadowCastingMode.Off, false, 0, view.camera, LightProbeUsage.CustomProvided);
        }

        static void RecalculateMatrices()
        {
            matrices.Clear();
            materialPropertyBlock.Clear();
            var lightProbes = LightmapSettings.lightProbes;
            if (lightProbes != null)
            {
                matrices.AddRange(lightProbes.positions.Select(pos => Matrix4x4.TRS(pos, Quaternion.identity, 0.1f * Vector3.one)));
                materialPropertyBlock.CopySHCoefficientArraysFrom(lightProbes.bakedProbes);
            }
        }
    }
}
