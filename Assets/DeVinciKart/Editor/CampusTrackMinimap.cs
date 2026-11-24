using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace DeVinciKart.EditorTools
{
    /// <summary>Génère la texture de la mini-carte (tracé blanc + ombre + damiers) et pose le composant TrackMinimap dans la scène.</summary>
    public static partial class CampusTrackGenerator
    {
        static readonly List<PathData> minimapAlts = new List<PathData>();

        static void BuildMinimap(Transform root, PathData main, IList<PathData> alts, Vector3 axisU, string name, bool closed, int finishIdx)
        {
            axisU.y = 0f; axisU.Normalize();
            Vector3 axisV = new Vector3(-axisU.z, 0f, axisU.x);
            var all = new List<PathData> { main };
            if (alts != null) all.AddRange(alts);
            float u0 = float.MaxValue, u1 = float.MinValue, v0 = float.MaxValue, v1 = float.MinValue;
            foreach (var pd in all)
                foreach (var p in pd.p)
                {
                    float u = Vector3.Dot(p, axisU), v = Vector3.Dot(p, axisV);
                    u0 = Mathf.Min(u0, u); u1 = Mathf.Max(u1, u); v0 = Mathf.Min(v0, v); v1 = Mathf.Max(v1, v);
                }
            float pad = 0.06f * Mathf.Max(u1 - u0, v1 - v0);
            u0 -= pad; u1 += pad; v0 -= pad; v1 += pad;
            int W, H;
            if (u1 - u0 >= v1 - v0) { W = 1024; H = Mathf.Max(128, Mathf.RoundToInt(W * (v1 - v0) / (u1 - u0))); }
            else { H = 1024; W = Mathf.Max(128, Mathf.RoundToInt(H * (u1 - u0) / (v1 - v0))); }
            float ppm = (W - 1) / (u1 - u0);
            var white = new float[W * H]; var shadow = new float[W * H]; var dark = new float[W * H];
            float rMain = Mathf.Max(5f, W * 0.0105f);

            System.Func<Vector3, Vector2> Px = p => new Vector2((Vector3.Dot(p, axisU) - u0) * ppm, (Vector3.Dot(p, axisV) - v0) * ppm);
            System.Action<float[], Vector2, float, float> Disk = (buf, c, r, val) =>
            {
                int xa = Mathf.Max(0, (int)(c.x - r - 1)), xb = Mathf.Min(W - 1, (int)(c.x + r + 1));
                int ya = Mathf.Max(0, (int)(c.y - r - 1)), yb = Mathf.Min(H - 1, (int)(c.y + r + 1));
                for (int y = ya; y <= yb; y++)
                for (int x = xa; x <= xb; x++)
                {
                    float d = Mathf.Sqrt((x - c.x) * (x - c.x) + (y - c.y) * (y - c.y));
                    float a = Mathf.Clamp01(r + 0.5f - d) * val;
                    int k = y * W + x;
                    if (a > buf[k]) buf[k] = a;
                }
            };
            System.Action<PathData, float> Stroke = (pd, r) =>
            {
                for (int i = 0; i < pd.Count - 1; i++)
                {
                    Vector2 a = Px(pd.p[i]), b = Px(pd.p[i + 1]);
                    int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude));
                    for (int s = 0; s <= steps; s++)
                    {
                        Vector2 c = Vector2.Lerp(a, b, s / (float)steps);
                        Disk(white, c, r, 1f);
                        Disk(shadow, c + new Vector2(3f, -4f), r + 2.5f, 0.55f);
                    }
                }
            };
            for (int k = 1; k < all.Count; k++) Stroke(all[k], rMain * 0.62f);
            Stroke(main, rMain);

            // damiers de départ et d'arrivée
            System.Action<int> Checker = idx =>
            {
                idx = Mathf.Clamp(idx, 2, main.Count - 3);
                Vector2 c = Px(main.p[idx]), t = (Px(main.p[idx + 2]) - Px(main.p[idx - 2])).normalized, n = new Vector2(-t.y, t.x);
                float cs = rMain * 0.5f;
                for (int a = -1; a <= 0; a++)
                for (int b = -2; b <= 1; b++)
                {
                    bool black = ((a + b) & 1) != 0;
                    for (float su = 0.15f; su < 1f; su += 0.2f)
                    for (float sv = 0.15f; sv < 1f; sv += 0.2f)
                    {
                        Vector2 q = c + t * ((a + su) * cs) + n * ((b + sv) * cs);
                        int x = Mathf.RoundToInt(q.x), y = Mathf.RoundToInt(q.y);
                        for (int dy = -1; dy <= 1; dy++) for (int dx = -1; dx <= 1; dx++)
                        {
                            int xx = x + dx, yy = y + dy;
                            if (xx < 0 || yy < 0 || xx >= W || yy >= H) continue;
                            dark[yy * W + xx] = black ? 1f : 0f;
                            white[yy * W + xx] = 1f;
                        }
                    }
                }
            };
            if (closed) Checker(finishIdx);
            else { Checker(12); Checker(main.Count - 14); }

            var px = new Color32[W * H];
            for (int k = 0; k < px.Length; k++)
            {
                float a = white[k], s = shadow[k] * (1f - a);
                float g = dark[k] > 0.5f ? 0.08f : 1f;
                float alpha = a + s;
                byte c = (byte)(255 * (alpha > 0f ? (a * g) / alpha : 0f));
                px[k] = new Color32(c, c, c, (byte)(255 * Mathf.Clamp01(alpha)));
            }
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            tex.SetPixels32(px); tex.Apply();
            string file = $"{GenFolder}/Textures/MiniCarte_{name}.png";
            File.WriteAllBytes(file, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            AssetDatabase.ImportAsset(file, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(file);
            if (imp != null)
            {
                imp.textureType = TextureImporterType.Default;
                imp.alphaIsTransparency = true;
                imp.mipmapEnabled = false;
                imp.wrapMode = TextureWrapMode.Clamp;
                imp.textureCompression = TextureImporterCompression.Uncompressed;
                imp.SaveAndReimport();
            }
            var go = new GameObject("MiniCarte (temps réel)");
            go.transform.SetParent(root, false);
            var mm = go.AddComponent<TrackMinimap>();
            mm.map = AssetDatabase.LoadAssetAtPath<Texture2D>(file);
            mm.axisU = axisU; mm.axisV = axisV;
            mm.min = new Vector2(u0, v0); mm.max = new Vector2(u1, v1);
        }
    }
}
