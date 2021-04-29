using System;
using UnityEditor.ProjectWindowCallback;

namespace MomomaAssets
{
    sealed class CreateGraphObjectEndAction : EndNameEditAction
    {
        public event Action<string> OnEndNameEdit;

        public override void Action(int instanceId, string pathName, string resourceFile)
        {
            OnEndNameEdit?.Invoke(pathName);
        }
    }
}
