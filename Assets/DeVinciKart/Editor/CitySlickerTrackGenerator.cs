using System.Collections.Generic;
using System.Linq;
using KartLab;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;

namespace DeVinciKart.EditorTools
{
    /// <summary>
    /// Piste 2 « City Slicker DeVinci » : le niveau 3D Assets/CitySlicker (scene.gltf) transformé en circuit fermé de 3 tours (~980 m / tour).
    /// Départ sur la grande avenue Est (portique d'origine), avenue Nord, descente dans le parc (sous les ponts), épingle du bas,
    /// remontée, grand pont Ouest, tour du rond-point de l'Ange (la statue Garfield est remplacée) puis retour sur la ligne.
    /// Grande Arche, Pouce géant, ancien et nouveau campus sont posés autour du niveau.
    /// Menu : DeVinci Kart > Générer la piste 2 : City Slicker (3 tours). La piste 1 n'est pas modifiée.
    /// Modèle « city slicker » par amogusstrikesback2 (Sketchfab), licence CC-BY-4.0.
    /// </summary>
    public static partial class CampusTrackGenerator
    {
        const string CSModelPath = "Assets/CitySlicker/scene.gltf";
        static string csMeshFolder = GenFolder + "/MeshesCitySlicker";   // la piste 1 range ses copies ailleurs
        const string CSCredit = "\"city slicker\" par amogusstrikesback2 (sketchfab.com) - CC-BY-4.0";

        // ce que l'on retire / ne rend pas solide dans le niveau d'origine (noms de matériaux glTF)
        static readonly string[] CSHidden = { "statue_garfiel", "m_puzzle", "m_halopuzzle" };
        static readonly string[] CSNoCollide = { "glow", "shadow", "noisepattern", "boost" };   // flèches 3D des plaques de boost : décor
        static readonly string[] CSCutMats = { "concret_block", "beton1" };
        // blocs de béton qui fermaient le grand pont Ouest (x, z, largeur, profondeur)
        static readonly Rect[] CSCut = { new Rect(-137f, -80f, 5f, 24f), new Rect(-44f, -80f, 10f, 24f), new Rect(-137f, -81f, 7f, 4f) };

        // tracé vérifié hors Unity sur la géométrie du niveau (hauteurs par niveau, sans saut de niveau, marges aux bords)
        static readonly Vector3[] CSControl =
        {
            new Vector3(0f, 0f, -20f), new Vector3(0f, 0f, 0f), new Vector3(3f, 0f, 40f), new Vector3(9f, 0f, 90f),
            new Vector3(6f, 0.03f, 125f), new Vector3(-10f, 0.03f, 137f), new Vector3(-40f, 0.03f, 138f), new Vector3(-70f, 0.03f, 134f),
            new Vector3(-82f, -0.07f, 112f), new Vector3(-84f, -2.15f, 80f), new Vector3(-86f, -4.07f, 56f), new Vector3(-87f, -5.33f, 40f),
            new Vector3(-87f, -6.37f, 26f), new Vector3(-83f, -6.93f, 10f), new Vector3(-79f, -6.94f, -5f), new Vector3(-80f, -6.92f, -16f),
            new Vector3(-83f, -6.94f, -28f), new Vector3(-89f, -7.15f, -43f), new Vector3(-87f, -6.99f, -58f), new Vector3(-75f, -7.10f, -71f),
            new Vector3(-65f, -7.27f, -83f), new Vector3(-63f, -7.30f, -93f), new Vector3(-67f, -6.76f, -105f), new Vector3(-73f, -6.94f, -118f),
            new Vector3(-76f, -7.19f, -135f), new Vector3(-80f, -7.68f, -155f), new Vector3(-84f, -7.29f, -190f), new Vector3(-98f, -6.94f, -216f),
            new Vector3(-120f, -6.95f, -226f), new Vector3(-136f, -7f, -216f), new Vector3(-139f, -7f, -195f), new Vector3(-139f, -3.81f, -160f),
            new Vector3(-139f, -2.91f, -125f), new Vector3(-139f, -2.26f, -100f), new Vector3(-139f, -1.34f, -88f), new Vector3(-137f, 1.34f, -77f),
            new Vector3(-129f, 2.76f, -71f), new Vector3(-110f, 5.28f, -70f), new Vector3(-90f, 6.06f, -70f), new Vector3(-70f, 5.12f, -70f),
            new Vector3(-50f, 2.11f, -69f), new Vector3(-34f, -0.08f, -69f), new Vector3(-24f, 0f, -74f), new Vector3(-11f, 0f, -77f),
            new Vector3(1f, 0f, -71f), new Vector3(6f, 0f, -59f), new Vector3(4f, 0f, -46f), new Vector3(2f, 0f, -34f),
        };

        static readonly Vector3 CSAngel = new Vector3(-11f, 0f, -60f);        // centre du rond-point (à la place de Garfield)
        // emplacements choisis par calcul de visibilité depuis le tracé (zones libres, sans route ni immeuble)
        static readonly Vector3 CSPouce = new Vector3(-60f, 0f, -24f);         // entre le parc et le rond-point, visible ~45 % du tour
        static readonly Vector3 CSArch = new Vector3(-20f, 0f, 330f);          // dans l'axe de l'avenue de départ
        static readonly Vector3 CSOldCampus = new Vector3(-165f, 0f, 22f);     // grande place libre sous la diagonale, face à la descente
        static readonly Vector3 CSNewCampus = new Vector3(110f, 0f, 320f);     // au nord, à côté de la Grande Arche : en face sur la ligne droite de départ

        [MenuItem("DeVinci Kart/Générer la piste 2 : City Slicker (3 tours)")]
        public static void GenerateCitySlicker()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Warnings.Clear();
            M.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("City Slicker", "Import du modèle…", 0.05f);
                AssetDatabase.Refresh();
                var levelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CSModelPath);
                if (levelAsset == null)
                {
                    AssetDatabase.ImportAsset(CSModelPath, ImportAssetOptions.ForceUpdate);
                    levelAsset = AssetDatabase.LoadAssetAtPath<GameObject>(CSModelPath);
                }
                if (levelAsset == null)
                {
                    EditorUtility.ClearProgressBar();
                    EditorUtility.DisplayDialog("DeVinci Kart", "Impossible de charger " + CSModelPath + " (glTFast doit être installé et le fichier importé).", "OK");
                    return;
                }

                EnsureFolders();
                if (!AssetDatabase.IsValidFolder(GenFolder + "/MeshesCitySlicker")) AssetDatabase.CreateFolder(GenFolder, "MeshesCitySlicker");
                GrabPrimitiveMeshes();
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                hallL2W = Matrix4x4.TRS(new Vector3(1e6f, 0f, 1e6f), Quaternion.identity, Vector3.one); hallW2L = hallL2W.inverse;
                bridgeL2W = Matrix4x4.TRS(new Vector3(-1e6f, 0f, 1e6f), Quaternion.identity, Vector3.one); bridgeW2L = bridgeL2W.inverse;
                CreateMaterials();
                CreateExtraMaterials();
                EnsureKartLabSetup();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var root = new GameObject("Piste_CitySlicker").transform;

                EditorUtility.DisplayProgressBar("City Slicker", "Niveau, collisions…", 0.15f);
                var level = (GameObject)PrefabUtility.InstantiatePrefab(levelAsset);
                if (PrefabUtility.IsPartOfPrefabInstance(level))
                    PrefabUtility.UnpackPrefabInstance(level, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                level.name = "CitySlicker (CC-BY amogusstrikesback2)";
                level.transform.SetParent(root, false);
                foreach (var t in level.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = 0; t.gameObject.isStatic = true; }
                foreach (var l in level.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
                foreach (var c in level.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);
                int colliders = PrepareCitySlicker(level);
                Physics.SyncTransforms();

                EditorUtility.DisplayProgressBar("City Slicker", "Tracé du circuit…", 0.35f);
                var path = BuildClosedPath(CSControl, Step);
                int missed = SnapPathToLevel(path);
                if (missed > path.Count / 20) Warnings.Add($"{missed} points du tracé n'ont pas trouvé la route (orientation du modèle ?)");
                ResetProps(path);
                var trackGO = new GameObject("TrackPath");
                trackGO.transform.SetParent(root, false);
                var tp = trackGO.AddComponent<TrackPath>();
                tp.points = path.p.ToArray(); tp.forwards = path.fwd.ToArray(); tp.length = path.length;
                int finishIdx = path.Nearest(new Vector3(0f, 0f, 1.75f));   // portique de départ d'origine

                EditorUtility.DisplayProgressBar("City Slicker", "Monuments…", 0.5f);
                PlaceCSLandmarks(root);
                BuildCSOuterGround(root);

                EditorUtility.DisplayProgressBar("City Slicker", "Objets, boosts…", 0.65f);
                BuildCSItems(path, root);
                SetupCameraAndLighting();

                EditorUtility.DisplayProgressBar("City Slicker", "Kart, tours, checkpoints…", 0.85f);
                var route = BuildTrackRoute(path, root);
                route.closed = true;
                route.roadHalfWidth = 11f;   // les rues du niveau font 15 à 25 m de large
                BuildKartLabCheckpoints(path, root);
                var kart = PlaceCityKart(path, finishIdx, route);
                if (kart != null)
                {
                    var hud = kart.GetComponentInChildren<KartHUD>(true);
                    if (hud != null) hud.CourseName = "CITY SLICKER / " + CityLaps + " TOURS";
                    var km = kart.GetComponent<KartMotor>();
                    if (km != null) km.fallResetY = -25f;   // le parc est à -7 m sous la ville
                }
                BuildCSLapSystem(path, finishIdx, root, kart);

                // crédits CC-BY (obligatoires)
                var credit = Text3D(root, "Circuit : " + CSCredit, new Vector3(14f, 6f, 40f), Quaternion.identity, 0.35f, Color.white);
                credit.name = "Credits_CC-BY";

                EditorSceneManager.SaveScene(scene, CityScenePath);
                BuildMenuSceneInternal();
                ConfigureBuildScenes();
                EditorSceneManager.OpenScene(CityScenePath);

                string msg = $"Piste 2 « City Slicker » générée : {path.length:0} m par tour, {CityLaps} tours, {colliders} surfaces solides.\n" +
                             $"Scène : {CityScenePath}\nLe menu « Menu_Choix_Circuit » permet de choisir entre les 2 circuits (touches 1 / 2).";
                if (Warnings.Count > 0) msg += "\n\nAttention :\n- " + string.Join("\n- ", Warnings.Distinct());
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("DeVinci Kart", msg, "OK");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        static bool NameHas(string name, string[] keys)
        {
            name = name.ToLowerInvariant();
            foreach (var k in keys) if (name.Contains(k)) return true;
            return false;
        }

        /// <summary>Retire la statue Garfield et les pièces de puzzle, ouvre le pont Ouest et ajoute les MeshColliders.</summary>
        static int PrepareCitySlicker(GameObject level)
        {
            int n = 0, saved = 0;
            foreach (var mr in level.GetComponentsInChildren<MeshRenderer>(true))
            {
                var mf = mr.GetComponent<MeshFilter>();
                if (mf == null || mf.sharedMesh == null) continue;
                var src = mf.sharedMesh;
                var mats = mr.sharedMaterials;
                var tr = mf.transform;
                Vector3[] verts = null;
                bool changed = false;
                var keepMats = new List<Material>();
                var subs = new List<int[]>();
                var colTris = new List<int>();
                int visCount = 0;
                for (int s = 0; s < src.subMeshCount; s++)
                {
                    string mn = s < mats.Length && mats[s] != null ? mats[s].name : "";
                    if (NameHas(mn, CSHidden)) { changed = true; continue; }
                    int[] tris = src.GetTriangles(s);
                    if (NameHas(mn, CSCutMats))
                    {
                        if (verts == null) verts = src.vertices;
                        var kept = new List<int>(tris.Length);
                        for (int t = 0; t + 2 < tris.Length; t += 3)
                        {
                            Vector3 c = tr.TransformPoint((verts[tris[t]] + verts[tris[t + 1]] + verts[tris[t + 2]]) / 3f);
                            var q = new Vector2(c.x, c.z);
                            bool cut = false;
                            foreach (var r in CSCut) if (r.Contains(q)) { cut = true; break; }
                            if (cut) continue;
                            kept.Add(tris[t]); kept.Add(tris[t + 1]); kept.Add(tris[t + 2]);
                        }
                        if (kept.Count != tris.Length) { changed = true; tris = kept.ToArray(); }
                    }
                    if (tris.Length == 0) { changed = true; continue; }
                    keepMats.Add(mats.Length > s ? mats[s] : null);
                    subs.Add(tris);
                    visCount += tris.Length;
                    if (!NameHas(mn, CSNoCollide)) colTris.AddRange(tris);
                }

                Mesh vis = src;
                if (changed)
                {
                    if (subs.Count == 0) { mr.enabled = false; continue; }
                    vis = Object.Instantiate(src);
                    vis.name = src.name + "_DK";
                    vis.subMeshCount = subs.Count;
                    for (int s = 0; s < subs.Count; s++) vis.SetTriangles(subs[s], s, false);
                    vis.RecalculateBounds();
                    AssetDatabase.CreateAsset(vis, $"{csMeshFolder}/{SafeName(tr.name)}_{saved++}.asset");
                    mf.sharedMesh = vis;
                    mr.sharedMaterials = keepMats.ToArray();
                }
                if (colTris.Count == 0) continue;
                Mesh col = vis;
                if (colTris.Count != visCount)
                {
                    if (verts == null) verts = src.vertices;
                    col = new Mesh { name = src.name + "_Col", indexFormat = verts.Length > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
                    col.vertices = verts;
                    col.triangles = colTris.ToArray();
                    col.RecalculateBounds();
                    AssetDatabase.CreateAsset(col, $"{csMeshFolder}/{SafeName(tr.name)}_{saved++}_col.asset");
                }
                var mc = mf.gameObject.GetComponent<MeshCollider>();
                if (mc == null) mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = col;
                n++;
            }
            return n;
        }

        static string SafeName(string s)
        {
            foreach (char c in System.IO.Path.GetInvalidFileNameChars()) s = s.Replace(c, '_');
            return s.Length > 40 ? s.Substring(0, 40) : s;
        }

        /// <summary>Recale chaque point du tracé sur la surface du niveau la plus proche de la hauteur prévue (gère ponts et dessous de ponts).</summary>
        static int SnapPathToLevel(PathData path)
        {
            int missed = 0, m = path.Count - 1;
            var ys = new float[m];
            for (int i = 0; i < m; i++)
            {
                Vector3 p = path.p[i];
                var hits = Physics.RaycastAll(p + Vector3.up * 3f, Vector3.down, 7f, 1, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue, y = p.y;
                foreach (var h in hits)
                {
                    float d = Mathf.Abs(h.point.y - p.y);
                    if (d < best) { best = d; y = h.point.y; }
                }
                if (best == float.MaxValue) missed++;
                ys[i] = y;
            }
            for (int i = 0; i < m; i++)   // lissage léger (vibreurs, joints)
            {
                float a = 0f;
                for (int k = -2; k <= 2; k++) a += ys[(i + k + m) % m];
                path.p[i] = new Vector3(path.p[i].x, a / 5f, path.p[i].z);
            }
            path.p[m] = path.p[0];
            for (int i = 0; i <= m; i++)
            {
                int k = i % m;
                Vector3 f = (path.p[(k + 1) % m] - path.p[(k - 1 + m) % m]).normalized;
                path.fwd[i] = f;
                path.right[i] = Vector3.Cross(Vector3.up, Flat(f)).normalized;
            }
            return missed;
        }

        static float CSGround(float x, float z, float fallback)
        {
            return Physics.Raycast(new Vector3(x, 150f, z), Vector3.down, out var h, 300f, 1, QueryTriggerInteraction.Ignore) ? h.point.y : fallback;
        }

        static float YawTowards(Vector3 from, Vector3 to) { Vector3 d = to - from; return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg; }

        static void PlaceCSLandmarks(Transform root)
        {
            var parent = Group("Monuments", root);

            // l'Ange remplace Garfield au centre du rond-point
            Vector3 S = CSAngel; S.y = CSGround(S.x, S.z, 0.5f);
            Prim(PrimitiveType.Cylinder, "Piedestal_Ange", parent, S + Vector3.up * 1.2f, new Vector3(6f, 1.2f, 6f), "Stone");
            PlaceFit("StatueAnge", parent, S + Vector3.up * 2.4f, YawTowards(S, new Vector3(-40f, 0f, -69f)), 11f).name = "Statue (Ange)";

            // Pouce géant
            Vector3 P = CSPouce; P.y = CSGround(P.x, P.z, 0f);
            Prim(PrimitiveType.Cube, "Pouce_Socle", parent, P + Vector3.up * 0.75f, new Vector3(14f, 1.5f, 14f), "Concrete");
            PlaceFit("LePouceModele3d", parent, P + Vector3.up * 1.5f, YawTowards(P, new Vector3(-79f, 0f, -5f)), 42f).name = "Le Pouce (géant)";

            // Grande Arche dans l'axe de l'avenue de départ
            PlaceFit("ArcheLaDefenseModel", parent, CSArch, 0f, 105f).name = "Grande Arche";

            // ancien campus (façade marquise = +Z local) et nouveau campus (façade = -Z local), tournés vers le circuit
            Vector3 O = CSOldCampus; O.y = CSGround(O.x, O.z, 0f);
            var old = PlaceScaled("AncienCampus", parent, O, YawTowards(O, new Vector3(-86f, 0f, 40f)), OldCampusScale);
            float so = FitOnSpot(old, O, 36f);
            old.transform.position += Vector3.up * OldCampusDrop * so;   // socle enterré
            old.name = "Ancien Campus (Pôle Léonard de Vinci)";
            Vector3 N = CSNewCampus; N.y = CSGround(N.x, N.z, 0f);
            var nw = PlaceFit("ESILV_NouveauCampus3D", parent, N, YawTowards(N, new Vector3(0f, 0f, 0f)) + 180f, 55f);
            nw.name = "Nouveau campus (Campus du Parc)";
        }

        /// <summary>Réduit le modèle s'il dépasse le rayon libre et le pose (centre bas) sur le point ; renvoie le facteur appliqué.</summary>
        static float FitOnSpot(GameObject go, Vector3 spot, float radius)
        {
            var b = WorldBounds(go);
            float s = Mathf.Min(1f, 2f * radius / Mathf.Max(1e-3f, Mathf.Max(b.size.x, b.size.z)));
            go.transform.localScale *= s;
            b = WorldBounds(go);
            go.transform.position += spot - new Vector3(b.center.x, b.min.y, b.center.z);
            return s;
        }

        /// <summary>Sol autour du niveau (le modèle s'arrête net) pour que les monuments ne flottent pas dans le vide.</summary>
        static void BuildCSOuterGround(Transform root)
        {
            var parent = Group("Sol_Exterieur", root);
            const float x0 = -310f, x1 = 160f, z0 = -340f, z1 = 250f, F = 1500f, y = -0.8f;
            Prim(PrimitiveType.Cube, "Sol_Nord", parent, new Vector3(0f, y, (z1 + F) / 2f), new Vector3(2f * F, 1f, F - z1), "Grass");
            Prim(PrimitiveType.Cube, "Sol_Sud", parent, new Vector3(0f, y, (z0 - F) / 2f), new Vector3(2f * F, 1f, F + z0), "Grass");
            Prim(PrimitiveType.Cube, "Sol_Ouest", parent, new Vector3((x0 - F) / 2f, y, (z0 + z1) / 2f), new Vector3(F + x0, 1f, z1 - z0), "Grass");
            Prim(PrimitiveType.Cube, "Sol_Est", parent, new Vector3((x1 + F) / 2f, y, (z0 + z1) / 2f), new Vector3(F - x1, 1f, z1 - z0), "Grass");
        }

        static void BuildCSItems(PathData path, Transform root)
        {
            var boxes = Group("Boites_Objets", root);
            string[] kinds = { "MushroomBox", "StarBox", "GreenShellBox", "BananaBox", "IceFlowerBox" };
            int k = 0;
            foreach (var spot in new[] { new Vector3(-40, 0, 138), new Vector3(-80, -7, -14), new Vector3(-139, -4, -165), new Vector3(-95, 6, -70) })
            {
                int i = path.Nearest(spot);
                Vector3 f = Flat(path.fwd[i]);
                foreach (float off in new[] { -6f, -2f, 2f, 6f })
                {
                    var prefab = KLPrefab(kinds[k % kinds.Length]);
                    if (prefab == null) return;
                    var box = (GameObject)PrefabUtility.InstantiatePrefab(prefab, boxes);
                    box.name = $"BoiteObjet_{k:00}";
                    box.transform.SetPositionAndRotation(path.p[i] + path.right[i] * off + Vector3.up * (RoadLift + 0.1f), Quaternion.LookRotation(f));
                    var zone = box.GetComponent<TrackZone>();
                    if (zone != null) zone.fixedItem = ItemKind.None;
                    k++;
                }
            }
            var boosts = Group("Boosts", root);
            var strip = KLPrefab("BoostStrip");
            if (strip == null) return;
            int b = 0;
            // sur les plaques de boost d'origine du niveau + la grande avenue
            foreach (var spot in new[] { new Vector3(-86.3f, -4.2f, 51.3f), new Vector3(-140.8f, -2.6f, -129.1f), new Vector3(5f, 0f, 75f), new Vector3(-80f, -7.5f, -160f) })
            {
                int i = path.Nearest(spot);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(strip, boosts);
                go.name = $"Boost_{b++}";
                go.transform.SetPositionAndRotation(path.p[i] + Vector3.up * (RoadLift + 0.02f), Quaternion.LookRotation(path.fwd[i], Vector3.up));
            }
        }

        /// <summary>Portes de tour larges (rues de 15-25 m), placées loin des croisements pont / dessous de pont.</summary>
        static void BuildCSLapSystem(PathData path, int fi, Transform root, GameObject kart)
        {
            var parent = Group("Tours", root);
            var mgr = new GameObject("LapRaceManager").AddComponent<LapRaceManager>();
            mgr.transform.SetParent(parent, false);
            mgr.totalLaps = CityLaps;
            mgr.kart = kart != null ? kart.GetComponent<KartMotor>() : null;
            int m = path.Count - 1;
            const int gateCount = 6;
            var gates = new List<LapGate>();
            for (int g = 0; g < gateCount; g++)
            {
                int i = (fi + g * m / gateCount) % m;
                for (int tries = 0; g > 0 && tries < 80 && NearOtherPart(path, i); tries++) i = (i + 3) % m;
                var go = new GameObject(g == 0 ? "Porte_00_Ligne" : $"Porte_{g:00}");
                go.transform.SetParent(parent, false);
                go.transform.SetPositionAndRotation(path.p[i] + Vector3.up * RoadLift, Quaternion.LookRotation(Flat(path.fwd[i])));
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0, 2.5f, 0);
                col.size = new Vector3(30f, 6f, 3f);
                var gate = go.AddComponent<LapGate>();
                gate.index = g;
                gate.manager = mgr;
                gates.Add(gate);
            }
            mgr.gates = gates.ToArray();
        }

        static bool NearOtherPart(PathData path, int i)
        {
            int m = path.Count - 1;
            for (int j = 0; j < m; j++)
            {
                int di = Mathf.Abs(j - i); di = Mathf.Min(di, m - di);
                if (di < 40) continue;
                Vector3 d = path.p[j] - path.p[i]; d.y = 0f;
                if (d.magnitude < 22f) return true;
            }
            return false;
        }
    }
}
