using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace DeVinciKart.EditorTools
{
    /// <summary>Debug : liste les collisions autour de points lus dans Temp/claude_preview/probe.txt (x,y,z,rayon par ligne).</summary>
    public static class DebugProbe
    {
        [MenuItem("DeVinci Kart/Sonde collisions (debug)")]
        public static void Probe()
        {
            string dir = "Temp/claude_preview", inF = Path.Combine(dir, "probe.txt");
            if (!File.Exists(inF)) { Debug.LogWarning("probe.txt absent"); return; }
            Physics.SyncTransforms();
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            var sb = new StringBuilder();
            foreach (var line in File.ReadAllLines(inF))
            {
                var p = line.Split(',');
                if (p.Length < 4) continue;
                var c = new Vector3(float.Parse(p[0], inv), float.Parse(p[1], inv), float.Parse(p[2], inv));
                float r = float.Parse(p[3], inv);
                sb.AppendLine($"== {c} r={r}");
                foreach (var col in Physics.OverlapSphere(c, r, ~0, QueryTriggerInteraction.Collide))
                {
                    Vector3 cp = col is MeshCollider mc && !mc.convex ? col.bounds.ClosestPoint(c) : col.ClosestPoint(c);
                    sb.AppendLine($"  {GetPath(col.transform)} [{col.GetType().Name}{(col.isTrigger ? ",trigger" : "")}] layer={col.gameObject.layer} bounds={col.bounds.min}..{col.bounds.max}");
                }
                for (int a = 0; a < 360; a += 30)
                {
                    Vector3 d = Quaternion.Euler(0, a, 0) * Vector3.forward;
                    if (Physics.Raycast(c, d, out var h, r * 2f, ~0, QueryTriggerInteraction.Ignore))
                        sb.AppendLine($"  ray {a}° -> {h.collider.name} dist {h.distance:0.00} pt {h.point}");
                }
            }
            File.WriteAllText(Path.Combine(dir, "probe_out.txt"), sb.ToString());
            Debug.Log("[DeVinci Kart] sonde écrite");
        }

        static string GetPath(Transform t)
        {
            string s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
