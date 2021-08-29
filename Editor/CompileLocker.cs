using UnityEditor;

namespace MomomaAssets
{
    static class CompileLocker
    {
        const string menuPath = "MomomaTools/CompileLocked";
        static bool isLocked = false;

        [MenuItem(menuPath)]
        static void ToggleLock()
        {
            if (isLocked)
                EditorApplication.UnlockReloadAssemblies();
            else
                EditorApplication.LockReloadAssemblies();
            isLocked = !isLocked;
            Menu.SetChecked(menuPath, isLocked);
        }
    }
}
