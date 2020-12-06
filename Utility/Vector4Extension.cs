#if UNITY_EDITOR
using System;
using UnityEngine;

namespace MomomaAssets.Utility
{

    public static class Vector4Extention
    {
        public static Vector4 Abs(this Vector4 v)
        {
            v.x = Math.Abs(v.x);
            v.y = Math.Abs(v.y);
            v.z = Math.Abs(v.z);
            v.w = Math.Abs(v.w);
            return v;
        }

        public static Vector4 Overlay(this Vector4 basis, Vector4 blend)
        {
            basis.x = Overlay(basis.x, blend.x);
            basis.y = Overlay(basis.y, blend.y);
            basis.z = Overlay(basis.z, blend.z);
            basis.w = Overlay(basis.w, blend.w);
            return basis;
        }

        static float Overlay(float basis, float blend)
        {
            return 0.5 < basis ? basis * blend * 2f : 1f - (1f - basis) * (1f - blend) * 2f;
        }
    }

}// namespace MomomaAssets.Utility
#endif
