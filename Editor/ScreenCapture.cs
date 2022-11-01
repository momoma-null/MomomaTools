using System.IO;
using Unity.Collections;
using Unity.Jobs;
using UnityEditor;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Jobs;

namespace MomomaAssets
{
    static class ScreenCapture
    {
        struct DegammaJob : IJobParallelFor
        {
            public NativeArray<Color> Colors;

            public void Execute(int index)
            {
                Colors[index] = Colors[index].gamma;
            }
        }

        [MenuItem("CONTEXT/Camera/Capture")]
        static void Capture(MenuCommand menuCommand)
        {
            var camera = menuCommand.context as Camera;
            var path = EditorUtility.SaveFilePanel("Capture", EditorApplication.applicationPath, camera.name, "png");
            var targetTexture = camera.targetTexture;
            var targetTextureIsNull = targetTexture == null;
            if (targetTextureIsNull)
            {
                targetTexture = RenderTexture.GetTemporary(camera.pixelWidth, camera.pixelHeight, 24, GraphicsFormat.R32G32B32A32_SFloat);
                targetTexture.antiAliasing = 2;
                camera.targetTexture = targetTexture;
            }
            try
            {
                var texture = new Texture2D(targetTexture.width, targetTexture.height, TextureFormat.RGBAFloat, false, false);
                try
                {
                    var oldActive = RenderTexture.active;
                    RenderTexture.active = targetTexture;
                    camera.Render();
                    texture.ReadPixels(new Rect(0, 0, texture.width, texture.height), 0, 0);
                    RenderTexture.active = oldActive;
                    if (targetTextureIsNull)
                    {
                        camera.targetTexture = null;
                        RenderTexture.ReleaseTemporary(targetTexture);
                    }
                    texture.Apply();
                    var degamma = new DegammaJob() { Colors = texture.GetRawTextureData<Color>() };
                    degamma.Run(degamma.Colors.Length);
                    var bytes = ImageConversion.EncodeNativeArrayToPNG(degamma.Colors, GraphicsFormat.R32G32B32A32_SFloat, (uint)texture.width, (uint)texture.height);
                    File.WriteAllBytes(path, bytes.ToArray());
                }
                finally
                {
                    Object.DestroyImmediate(texture);
                }
            }
            finally
            {
                if (targetTextureIsNull)
                {
                    camera.targetTexture = null;
                    RenderTexture.ReleaseTemporary(targetTexture);
                }
            }
            AssetDatabase.Refresh();
        }
    }
}
