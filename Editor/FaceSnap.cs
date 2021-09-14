using UnityEngine;
using UnityEditor;
using UnityEditor.EditorTools;

namespace MomomaAssets
{
    [EditorTool("MomomaTools/FaceSnap")]
    sealed class FaceSnap : EditorTool
    {
        RaycastHit[] m_Results = new RaycastHit[1];

        public override void OnToolGUI(EditorWindow window)
        {
            var currentPosition = Tools.handlePosition;
            var size = 0.15f * HandleUtility.GetHandleSize(currentPosition);
            EditorGUI.BeginChangeCheck();
            var position = Handles.FreeMoveHandle(currentPosition, Tools.handleRotation, size, Vector3.zero, Handles.RectangleHandleCap);
            if (EditorGUI.EndChangeCheck())
            {
                var delta = position - currentPosition;
                Undo.RecordObjects(Selection.transforms, "Move Objects");
                var ray = HandleUtility.GUIPointToWorldRay(HandleUtility.WorldToGUIPoint(position));
                foreach (var t in Selection.transforms)
                {
                    switch (Tools.pivotMode)
                    {
                        case PivotMode.Center:
                            var bounds = new Bounds();
                            var rot = t.rotation;
                            try
                            {
                                t.rotation = Quaternion.identity;
                                t.position += delta;
                                var renderers = t.GetComponentsInChildren<Renderer>(false);
                                if (renderers.Length > 0)
                                {
                                    bounds = new Bounds();
                                    foreach (var r in renderers)
                                    {
                                        if (bounds.extents == Vector3.zero)
                                        {
                                            bounds = r.bounds;
                                        }
                                        else
                                        {
                                            bounds.Encapsulate(r.bounds);
                                        }
                                    }
                                }
                                bounds.center = t.position + rot * (bounds.center - t.position);
                            }
                            finally
                            {
                                t.rotation = rot;
                            }
                            ray.origin = bounds.center - Vector3.Project(bounds.center - ray.origin, ray.direction);
                            if (Physics.BoxCastNonAlloc(ray.origin, bounds.extents, ray.direction, m_Results, rot, Mathf.Infinity, Physics.AllLayers) > 0)
                            {
                                var invR = Quaternion.Inverse(rot);
                                var ro = invR * (m_Results[0].point - bounds.center);
                                var rd = invR * -ray.direction;
                                var tx = -ro.x / rd.x - bounds.extents.x / Mathf.Abs(rd.x);
                                var ty = -ro.y / rd.y - bounds.extents.y / Mathf.Abs(rd.y);
                                var tz = -ro.z / rd.z - bounds.extents.z / Mathf.Abs(rd.z);
                                var tN = Mathf.Max(tx, ty, tz);
                                t.position += ray.direction * tN;
                                t.rotation = AlignToNormal(rot, m_Results[0].normal);
                            }
                            break;
                        case PivotMode.Pivot:
                            if (Physics.RaycastNonAlloc(ray, m_Results, Mathf.Infinity, Physics.AllLayers) > 0)
                            {
                                t.position = m_Results[0].point;
                                t.rotation = AlignToNormal(t.rotation, m_Results[0].normal);
                            }
                            break;
                    }
                }
            }
        }

        static Quaternion AlignToNormal(Quaternion rotation, Vector3 normal)
        {
            var localDir = Quaternion.Inverse(rotation) * normal;
            var absX = Mathf.Abs(localDir.x);
            var absY = Mathf.Abs(localDir.y);
            var absZ = Mathf.Abs(localDir.z);
            var tNorm = Vector3.zero;
            if (absX > absY && absX > absZ)
                tNorm.x = Mathf.Sign(localDir.x);
            else if (absY > absZ)
                tNorm.y = Mathf.Sign(localDir.y);
            else
                tNorm.z = Mathf.Sign(localDir.z);
            tNorm = rotation * tNorm;
            return Quaternion.FromToRotation(tNorm, normal) * rotation;
        }
    }
}
