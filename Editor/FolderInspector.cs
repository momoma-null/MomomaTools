using System.IO;
using System.Text;
using UnityEngine;
using UnityEditor;

namespace MomomaAssets
{

    [CustomEditor(typeof(DefaultAsset))]
    class FolderInspector : Editor
    {
        string m_Text = null;

        public override void OnInspectorGUI()
        {
            base.OnInspectorGUI();
            var path = AssetDatabase.GetAssetPath(target);
            if (string.IsNullOrEmpty(path))
                return;
            if (!AssetDatabase.IsValidFolder(path))
                return;
            path = Path.GetDirectoryName(Application.dataPath) + '/' + path + "/.memo";
            if (m_Text == null)
            {
                if (File.Exists(path))
                    m_Text = File.ReadAllText(path, Encoding.UTF8);
                else
                    m_Text = "";
            }
            var oldEnabled = GUI.enabled;
            try
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    GUI.enabled = true;
                    m_Text = EditorGUILayout.TextArea(m_Text);
                    if (check.changed)
                    {
                        if (string.IsNullOrEmpty(m_Text))
                            File.Delete(path);
                        else
                            File.WriteAllText(path, m_Text, Encoding.UTF8);
                    }
                }
            }
            finally
            {
                GUI.enabled = oldEnabled;
            }
        }
    }

}// namespace MomomaAssets
