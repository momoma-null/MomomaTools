using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace MomomaAssets
{
    [InitializeOnLoad]
    sealed class SceneVertexColorMode
    {
        static class Cache
        {
            public static readonly Shader vertexColorShader = Shader.Find("Hidden/MS_VertexColor");
            public static readonly int vertexColorId = Shader.PropertyToID("_MS_VertexColor");
        }

        static readonly SceneView.CameraMode vertexRMode;
        static readonly SceneView.CameraMode vertexGMode;
        static readonly SceneView.CameraMode vertexBMode;
        static readonly SceneView.CameraMode vertexAMode;
        static readonly HashSet<SceneView> setupSceneViews = new HashSet<SceneView>();

        static SceneVertexColorMode()
        {
            vertexRMode = SceneView.AddCameraMode("Vertex R", "Momoma Tools");
            vertexGMode = SceneView.AddCameraMode("Vertex G", "Momoma Tools");
            vertexBMode = SceneView.AddCameraMode("Vertex B", "Momoma Tools");
            vertexAMode = SceneView.AddCameraMode("Vertex A", "Momoma Tools");
            SceneView.beforeSceneGui += view =>
            {
                if (setupSceneViews.Add(view))
                {
                    view.onCameraModeChanged += cameraMode =>
                    {
                        if (cameraMode == vertexRMode)
                        {
                            view.SetSceneViewShaderReplace(Cache.vertexColorShader, string.Empty);
                            Shader.SetGlobalColor(Cache.vertexColorId, Color.red);
                        }
                        else if (cameraMode == vertexGMode)
                        {
                            view.SetSceneViewShaderReplace(Cache.vertexColorShader, string.Empty);
                            Shader.SetGlobalColor(Cache.vertexColorId, Color.green);
                        }
                        else if (cameraMode == vertexBMode)
                        {
                            view.SetSceneViewShaderReplace(Cache.vertexColorShader, string.Empty);
                            Shader.SetGlobalColor(Cache.vertexColorId, Color.blue);
                        }
                        else if (cameraMode == vertexAMode)
                        {
                            view.SetSceneViewShaderReplace(Cache.vertexColorShader, string.Empty);
                            Shader.SetGlobalColor(Cache.vertexColorId, Color.black);
                        }
                        else if (cameraMode.drawMode == DrawCameraMode.Textured)
                        {
                            view.SetSceneViewShaderReplace(null, string.Empty);
                        }
                    };
                }
            };
        }
    }

}// namespace MomomaAssets
