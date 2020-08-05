using UnityEngine;
using UnityEditor;
using System.IO;

namespace MomomaAssets
{

public class SkinnedMeshConverter : EditorWindow
{
    GameObject rootObj;

    [MenuItem("MomomaTools/SkinnedMeshConverter")]
    static void ShowWindow()
    {
        EditorWindow.GetWindow<SkinnedMeshConverter>("SkinnedMeshConverter");
    }

    void OnGUI()
    {
        rootObj = (GameObject)EditorGUILayout.ObjectField(rootObj, typeof(GameObject), true);
        
        if (GUILayout.Button("Convert"))
        {
            var copiedObj = Instantiate(rootObj, Vector3.zero, Quaternion.identity);
            var posTex = new Texture2D(8, 16, TextureFormat.RGBAFloat, false, true);
            var posPixels = new Color[128];
            var rotTex = new Texture2D(8, 16, TextureFormat.RGBAFloat, false, true);
            var rotPixels = new Color[128];
            var skin = copiedObj.GetComponentInChildren<SkinnedMeshRenderer>();
            var mesh = skin.sharedMesh;
            var bones = skin.bones;
            var i = 0;
            foreach (var bone in bones)
            {
                if (i < 128)
                {
                    //bone.localRotation = Quaternion.identity;
                    if (i == 0)
                    {
                        bone.localPosition = Vector3.zero;
                    }
                    posPixels[i] = new Color(bone.position.x * 0.5f + 0.5f, bone.position.y * 0.5f + 0.5f, bone.position.z * 0.5f + 0.5f, 1f);
                    float invQ = bone.rotation.w < 0 ? -1 : 1;
                    var theta = Mathf.Acos(bone.rotation.w * invQ) * 2f;
                    var vec = theta * new Vector3(bone.rotation.x, bone.rotation.y, bone.rotation.z) * invQ / Mathf.Sin(theta * 0.5f) / Mathf.PI;
                    rotPixels[i] = new Color(vec.x * 0.5f + 0.5f, vec.y * 0.5f + 0.5f, vec.z * 0.5f + 0.5f, 1f);
                }
                i++;
            }
            posTex.SetPixels(posPixels, 0);
            posTex.Apply();
            rotTex.SetPixels(rotPixels, 0);
            rotTex.Apply();
            var outMesh = new Mesh();
            skin.BakeMesh(outMesh);
            DestroyImmediate(copiedObj);

            var vertCount = mesh.vertexCount;
            var uv2s = new Vector2[vertCount];
            var uv3s = new Vector2[vertCount];
            var weights = mesh.boneWeights;
            for (int k = 0; k < vertCount; k++)
            {
                uv2s[k] = new Vector2(weights[k].boneIndex0 + weights[k].weight0 * 0.9f, weights[k].boneIndex1 + weights[k].weight1 * 0.9f);
                uv3s[k] = new Vector2(weights[k].boneIndex2 + weights[k].weight2 * 0.9f, weights[k].boneIndex3 + weights[k].weight3 * 0.9f);
            }
            outMesh.uv2 = uv2s;
            outMesh.uv3 = uv3s;
            
            AssetDatabase.CreateAsset(outMesh, Path.ChangeExtension(AssetDatabase.GetAssetPath(mesh), ".asset"));
            AssetDatabase.CreateAsset(posTex, Path.ChangeExtension(AssetDatabase.GetAssetPath(mesh), "pos.asset"));
            AssetDatabase.CreateAsset(rotTex, Path.ChangeExtension(AssetDatabase.GetAssetPath(mesh), "rot.asset"));
            AssetDatabase.Refresh();
        }
    }
}

}// namespace MomomaAssets
