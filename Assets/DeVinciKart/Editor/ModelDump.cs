using System.Collections.Generic;
using System.IO;
using System.Text;
using KartLab;
using UnityEditor;
using UnityEngine;

namespace DeVinciKart.EditorTools
{
    /// <summary>
    /// Outil de diagnostic : mesure chaque modèle (taille, centre) et enregistre des vues orthographiques
    /// (arrière : droite=+X haut=+Y / dessus : droite=+X haut=+Z / côté : droite=+Z haut=+Y) dans Temp/claude_preview.
    /// </summary>
    public static class ModelDump
    {
        const int S = 192, RowsPerSheet = 8;

        /// <summary>Rend des vues de la scène ouverte. Temp/claude_preview/shots.txt : nom;px,py,pz;tx,ty,tz;fov</summary>
        [MenuItem("DeVinci Kart/Captures (debug)")]
        public static void Shots()
        {
            string outDir = "Temp/claude_preview";
            string file = Path.Combine(outDir, "shots.txt");
            if (!File.Exists(file)) { Debug.LogWarning("shots.txt absent"); return; }
            const int W = 800, H = 450;
            var camGO = new GameObject("shotcam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGO.AddComponent<Camera>();
            cam.enabled = false; cam.nearClipPlane = 0.05f; cam.farClipPlane = 4000f;
            cam.clearFlags = CameraClearFlags.Skybox;
            var rt = new RenderTexture(W, H, 24);
            cam.targetTexture = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            var inv = System.Globalization.CultureInfo.InvariantCulture;
            int n = 0;
            foreach (var line in File.ReadAllLines(file))
            {
                var parts = line.Split(';');
                if (parts.Length < 4) continue;
                var p = parts[1].Split(','); var t = parts[2].Split(',');
                Vector3 pos = new Vector3(float.Parse(p[0], inv), float.Parse(p[1], inv), float.Parse(p[2], inv));
                Vector3 tgt = new Vector3(float.Parse(t[0], inv), float.Parse(t[1], inv), float.Parse(t[2], inv));
                cam.fieldOfView = float.Parse(parts[3], inv);
                cam.transform.position = pos;
                cam.transform.rotation = Quaternion.LookRotation(tgt - pos, Vector3.up);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
                tex.Apply();
                RenderTexture.active = null;
                File.WriteAllBytes(Path.Combine(outDir, "shot_" + parts[0].Trim() + ".png"), tex.EncodeToPNG());
                n++;
            }
            cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex); Object.DestroyImmediate(camGO);
            File.WriteAllText(Path.Combine(outDir, "shots_done.txt"), n + " captures " + System.DateTime.Now);
            Debug.Log("[DeVinci Kart] " + n + " captures");
        }

        [MenuItem("DeVinci Kart/Dump modeles (debug)")]
        public static void Dump()
        {
            CampusTrackGenerator.EnsureKartLabSetup();
            string outDir = "Temp/claude_preview";
            Directory.CreateDirectory(outDir);
            var paths = new List<string>();
            foreach (var g in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/BEDRILL/Track_Environment_Free/Prefabs" }))
                paths.Add(AssetDatabase.GUIDToAssetPath(g));
            paths.Sort();
            string listFile = Path.Combine(outDir, "dump_list.txt");
            if (File.Exists(listFile))
                foreach (var l in File.ReadAllLines(listFile))
                    if (!string.IsNullOrWhiteSpace(l)) paths.Add(l.Trim());

            var sb = new StringBuilder();
            var camGO = new GameObject("dumpcam") { hideFlags = HideFlags.HideAndDontSave };
            var cam = camGO.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.35f, 0.35f, 0.42f);
            cam.enabled = false;
            cam.nearClipPlane = 0.01f;
            cam.farClipPlane = 1500f;
            var rt = new RenderTexture(S, S, 24);
            cam.targetTexture = rt;
            Vector3 origin = new Vector3(0, 3000, 0);

            var objs = new List<GameObject>();
            var names = new List<string>();
            foreach (var p in paths)
            {
                string ap = p; float rotY = 0f;
                int at = p.LastIndexOf('@');
                if (at > 0) { ap = p.Substring(0, at); rotY = float.Parse(p.Substring(at + 1), System.Globalization.CultureInfo.InvariantCulture); }
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(ap);
                if (prefab == null) { sb.AppendLine($"MISSING {p}"); continue; }
                var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                go.transform.rotation = Quaternion.Euler(0, rotY, 0);
                objs.Add(go); names.Add(p);
            }

            // volants du kart KartLab (repère local du pivot « wheel »)
            var kartPrefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/KartLab/Prefabs/PlayerKart.prefab");
            if (kartPrefab != null)
            {
                var kart = (GameObject)PrefabUtility.InstantiatePrefab(kartPrefab);
                PrefabUtility.UnpackPrefabInstance(kart, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                var cockpit = kart.GetComponentInChildren<KartCockpit>(true);
                if (cockpit != null)
                {
                    foreach (var w in new[] { cockpit.wheel, cockpit.exteriorWheel })
                    {
                        if (w == null) continue;
                        sb.AppendLine($"WHEEL path={TPath(w)} localPos={w.localPosition} localRot={w.localEulerAngles} lossyScale={w.lossyScale} parent={w.parent?.name}");
                        foreach (var t in w.GetComponentsInChildren<Transform>(true))
                            if (t != w) sb.AppendLine($"   child {TPath(t)} active={t.gameObject.activeSelf} pos={t.localPosition} rot={t.localEulerAngles} scale={t.localScale} mesh={(t.GetComponent<MeshFilter>() ? t.GetComponent<MeshFilter>().sharedMesh.name : "-")}");
                        // bornes dans le repère local du pivot
                        var lb = new Bounds(); bool first = true;
                        foreach (var mf in w.GetComponentsInChildren<MeshFilter>(true))
                        {
                            if (mf.sharedMesh == null) continue;
                            var mb = mf.sharedMesh.bounds;
                            for (int c = 0; c < 8; c++)
                            {
                                Vector3 corner = mb.center + Vector3.Scale(mb.extents, new Vector3((c & 1) == 0 ? -1 : 1, (c & 2) == 0 ? -1 : 1, (c & 4) == 0 ? -1 : 1));
                                Vector3 q = w.InverseTransformPoint(mf.transform.TransformPoint(corner));
                                if (first) { lb = new Bounds(q, Vector3.zero); first = false; } else lb.Encapsulate(q);
                            }
                        }
                        sb.AppendLine($"   LOCAL BOUNDS center={lb.center.ToString("F4")} size={lb.size.ToString("F4")}");
                    }
                    sb.AppendLine($"COCKPIT wheelPosition={cockpit.wheelPosition} column={(cockpit.column ? TPath(cockpit.column) : "-")} columnLength={cockpit.columnLength}");
                    if (cockpit.wheel != null)
                    {
                        var wgo = cockpit.wheel.gameObject;
                        wgo.transform.SetParent(null, true);
                        wgo.transform.rotation = Quaternion.identity;
                        objs.Add(wgo); names.Add("KartLab cockpit.wheel (rotation identité)");
                    }
                }
                Object.DestroyImmediate(kart);
            }

            Texture2D sheet = null;
            int sheetIndex = 0;
            for (int i = 0; i < objs.Count; i++)
            {
                int row = i % RowsPerSheet;
                if (row == 0) sheet = new Texture2D(S * 3, S * RowsPerSheet, TextureFormat.RGB24, false);
                var go = objs[i];
                go.transform.position = origin;
                var rs = go.GetComponentsInChildren<Renderer>();
                if (rs.Length == 0) { sb.AppendLine($"{i:00} {names[i]} : aucun renderer"); }
                else
                {
                    var b = rs[0].bounds;
                    foreach (var r in rs) b.Encapsulate(r.bounds);
                    Vector3 c = b.center - origin;
                    sb.AppendLine($"{i:00} sheet{i / RowsPerSheet} row{row} {names[i]}\n     size={b.size.ToString("F3")} center={c.ToString("F3")} min={(b.min - origin).ToString("F3")} rootScale={go.transform.localScale} renderers={rs.Length} colliders={go.GetComponentsInChildren<Collider>().Length} mat={(rs[0].sharedMaterial ? rs[0].sharedMaterial.name + " / " + rs[0].sharedMaterial.shader.name : "null")}");
                    float ext = Mathf.Max(b.extents.x, Mathf.Max(b.extents.y, b.extents.z)) * 1.12f + 0.001f;
                    cam.orthographicSize = ext;
                    var dirs = new[] { Vector3.back, Vector3.up, Vector3.right };
                    var ups = new[] { Vector3.up, Vector3.forward, Vector3.up };
                    for (int v = 0; v < 3; v++)
                    {
                        cam.transform.position = b.center + dirs[v] * (ext * 3f + 2f);
                        cam.transform.rotation = Quaternion.LookRotation(-dirs[v], ups[v]);
                        cam.Render();
                        RenderTexture.active = rt;
                        sheet.ReadPixels(new Rect(0, 0, S, S), v * S, S * (RowsPerSheet - 1 - row));
                        RenderTexture.active = null;
                    }
                }
                go.transform.position = origin + Vector3.right * 5000f;
                if (row == RowsPerSheet - 1 || i == objs.Count - 1)
                {
                    sheet.Apply();
                    File.WriteAllBytes(Path.Combine(outDir, $"dump_{sheetIndex++}.png"), sheet.EncodeToPNG());
                    Object.DestroyImmediate(sheet);
                }
            }
            foreach (var go in objs) Object.DestroyImmediate(go);
            cam.targetTexture = null;
            Object.DestroyImmediate(rt);
            Object.DestroyImmediate(camGO);
            File.WriteAllText(Path.Combine(outDir, "dump.txt"), sb.ToString());
            Debug.Log("[DeVinci Kart] Dump terminé : " + objs.Count + " modèles");
        }

        static string TPath(Transform t)
        {
            string s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }
    }
}
