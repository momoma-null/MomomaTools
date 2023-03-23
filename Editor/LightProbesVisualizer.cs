using System;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace MomomaAssets
{
    [InitializeOnLoad]
    static class LightProbesVisualizer
    {
        sealed class Group
        {
            public readonly List<Matrix4x4> matrices = new List<Matrix4x4>();
            public readonly MaterialPropertyBlock propertyBlock = new MaterialPropertyBlock();
        }

        const string menuPath = "MomomaTools/Light Probes Visualizer";

        static readonly List<Group> groups = new List<Group>();
        static Mesh sphereMesh;
        static Material diffuseMaterial;

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
            Lightmapping.lightingDataUpdated += () => RecalculateMatrices();
            EditorSceneManager.activeSceneChangedInEditMode += (x, y) => RecalculateMatrices();
            RecalculateMatrices();
            SceneView.duringSceneGui -= OnSceneGUI;
            if (Menu.GetChecked(menuPath))
                SceneView.duringSceneGui += OnSceneGUI;
        }

        static void OnSceneGUI(SceneView view)
        {
            foreach (var group in groups)
                Graphics.DrawMeshInstanced(sphereMesh, 0, diffuseMaterial, group.matrices, group.propertyBlock, ShadowCastingMode.Off, false, 0, view.camera, LightProbeUsage.CustomProvided);
        }

        static void RecalculateMatrices()
        {
            groups.Clear();
            var lightProbes = LightmapSettings.lightProbes;
            if (lightProbes != null)
            {
                var positions = lightProbes.positions;
                var bakedProbes = lightProbes.bakedProbes;
                var remainCount = positions.Length;
                var index = 0;
                while (true)
                {
                    var group = new Group();
                    var max = Mathf.Min(index + 1023, positions.Length);
                    for (var i = index; i < max; ++i)
                    {
                        group.matrices.Add(Matrix4x4.TRS(positions[i], Quaternion.identity, 0.1f * Vector3.one));
                    }
                    var probesParts = new SphericalHarmonicsL2[max - index];
                    Array.Copy(bakedProbes, index, probesParts, 0, probesParts.Length);
                    group.propertyBlock.CopySHCoefficientArraysFrom(probesParts);
                    groups.Add(group);
                    index += 1023;
                    if (index >= positions.Length)
                        break;
                }
            }
        }
    }
}
