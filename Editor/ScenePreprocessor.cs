
using System.Collections.Generic;
using UnityEditor.Build;
using UnityEngine;

namespace MomomaAssets
{
    sealed class ScenePreprocessor : IProcessSceneWithReport
    {
        int IOrderedCallback.callbackOrder => 0;

        void IProcessSceneWithReport.OnProcessScene(UnityEngine.SceneManagement.Scene scene, UnityEditor.Build.Reporting.BuildReport report)
        {
            var roots = scene.GetRootGameObjects();
            var preprocessors = new List<IPreprocessBehaviour>();
            foreach (var root in roots)
            {
                root.GetComponentsInChildren(true, preprocessors);
                foreach (var i in preprocessors)
                {
                    i.Process();
                    Debug.Log($"{i.GetType().Name} process done.({i})");
                    Object.DestroyImmediate(i as Component);
                }
            }
        }
    }
}
