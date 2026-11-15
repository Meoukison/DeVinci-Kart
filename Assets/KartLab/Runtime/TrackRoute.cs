using UnityEngine;

namespace KartLab
{
    /// <summary>Ordered centre-line samples, in this object's local space.</summary>
    public class TrackRoute : MonoBehaviour
    {
        public Vector3[] points;
        public bool closed=true;
        public float roadHalfWidth=4.5f;
        [Tooltip("Itinéraire secondaire (raccourci, variante) : accepté par KartRecovery, ignoré par le pilote automatique.")]
        public bool alternative;
        public static readonly System.Collections.Generic.List<TrackRoute> All=new System.Collections.Generic.List<TrackRoute>();
        void OnEnable(){if(!All.Contains(this))All.Add(this);}
        void OnDisable(){All.Remove(this);}
        public bool ClosestPose(Vector3 position,out Pose pose,out float distance)
        {
            pose=default;distance=float.PositiveInfinity;
            if(points==null||points.Length<2)return false;
            int segments=closed?points.Length:points.Length-1;
            for(int n=0;n<segments;n++)
            {
                Vector3 a=transform.TransformPoint(points[n]), b=transform.TransformPoint(points[(n+1)%points.Length]);
                Vector3 flat=Vector3.ProjectOnPlane(b-a,Vector3.up);
                if(flat.sqrMagnitude<.0001f)continue;
                float t=Mathf.Clamp01(Vector3.Dot(Vector3.ProjectOnPlane(position-a,Vector3.up),flat)/flat.sqrMagnitude);
                Vector3 closest=Vector3.Lerp(a,b,t);
                float d=Vector3.ProjectOnPlane(position-closest,Vector3.up).magnitude;
                if(d>=distance)continue;
                distance=d;pose=new Pose(closest,Quaternion.LookRotation(flat,Vector3.up));
            }
            return !float.IsPositiveInfinity(distance);
        }
        void OnDrawGizmosSelected()
        {
            if(points==null||points.Length<2)return;
            Gizmos.color=Color.cyan;
            for(int n=0;n<(closed?points.Length:points.Length-1);n++)
                Gizmos.DrawLine(transform.TransformPoint(points[n]),transform.TransformPoint(points[(n+1)%points.Length]));
        }
    }
}
