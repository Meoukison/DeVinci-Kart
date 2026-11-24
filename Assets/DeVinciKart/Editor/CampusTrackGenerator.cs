using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Unity.Collections;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using KartLab;

namespace DeVinciKart.EditorTools
{
    /// <summary>
    /// Génère la piste « Ancien campus → Campus du Parc » calée sur la carte réelle de La Défense / Nanterre.
    /// Repère : origine = centre de la Grande Arche, X = est, Z = nord, 1 unité = 1 m.
    ///   Départ dans le Pôle Léonard de Vinci (sortie sous la marquise) → Le Pouce (place Carpeaux) → rond-point de la statue
    ///   → rampe sur les marches et passage SOUS la Grande Arche → pont au-dessus du boulevard circulaire en tranchée
    ///   → Terrasses de Nanterre → arrivée au Campus du Parc, au bord du parc André-Malraux.
    /// Menu : DeVinci Kart > Générer la piste Campus → Campus
    /// </summary>
    public static partial class CampusTrackGenerator
    {
        // ============================== RÉGLAGES ==============================
        const string ModelsFolder = "Assets/3DModels";
        const string GenFolder = "Assets/DeVinciKart/Generated";
        const string ScenePath = "Assets/Scenes/Piste_AncienCampus_NouveauCampus.unity";

        const float RoadHalf = 6f, CurbW = 1f, BarrierT = 0.5f, RoadLift = 0.06f, Step = 1f;

        // Axe historique (de la Grande Arche vers l'est / Paris). Vers l'ouest = -AxisEast.
        static readonly Vector3 AxisEast = new Vector3(0.900f, 0f, -0.436f).normalized;
        static Vector3 A(float s, float y = 0f) { var p = AxisEast * s; p.y = y; return p; }

        // Positions réelles (m, depuis la Grande Arche)
        // (circuit raccourci : les distances réelles sont compressées, l'ordre et l'orientation des lieux sont conservés)
        static readonly Vector3 PoleDoor = new Vector3(-60f, 0f, 230f);      // porte sous la marquise du Pôle Léonard de Vinci
        static readonly Vector3 PoucePos = new Vector3(130f, 0f, 90f);       // Le Pouce, place Carpeaux
        const float PouceHeight = 30f;                                        // Pouce géant
        static readonly Vector3 StatueCenter = new Vector3(195f, 0f, 15f);   // statue (rond-point)
        const float StatueR = 32f;
        static readonly Vector3 NewCampusPos = new Vector3(-644f, -2.5f, 356f); // Campus du Parc
        static readonly Vector3 ParkCenter = new Vector3(-700f, 0f, 150f);   // parc André-Malraux
        const float ParkRadius = 140f;

        // Ancien campus (modèle Tripo ×100). Repère local du modèle tel qu'importé par glTFast.
        const float OldCampusScale = 100f, OldCampusDrop = -8.6f;            // socle de 8,6 m enterré
        const float DoorLx = -6.5f, DoorLz = 17.5f;                           // porte sous la marquise (repère local)
        const float HallX0 = -15f, HallX1 = 2f, HallZ0 = -16f, HallZ1 = 17.5f, HallH = 9.5f;

        // Grande Arche (modèle ×110) : plancher de l'ouverture à 15,1 m, marches côté est.
        const float ArchScale = 110f, ArchFloor = 15.3f;

        // Pont : boulevard circulaire en tranchée, traversé par l'axe
        const float BridgeS = -215f, TrenchHalfW = 38f, TrenchHalfLen = 90f, TrenchDepth = 9f;

        const float NewCampusScale = 150f;
        const float JumpScale = 13f, PillarsScale = 16f, ShopHeight = 17f;
        // =====================================================================

        enum Zone { Hall, Bridge, Module, Normal }

        class PathData
        {
            public readonly List<Vector3> p = new List<Vector3>();
            public readonly List<Vector3> fwd = new List<Vector3>();
            public readonly List<Vector3> right = new List<Vector3>();
            public readonly List<float> dist = new List<float>();
            public float length;
            public int Count => p.Count;

            public int Nearest(Vector3 q)
            {
                int best = 0; float bd = float.MaxValue;
                for (int i = 0; i < p.Count; i++)
                {
                    float d = (p[i] - q).sqrMagnitude;
                    if (d < bd) { bd = d; best = i; }
                }
                return best;
            }
        }

        static readonly Dictionary<string, Material> M = new Dictionary<string, Material>();
        static readonly List<string> Warnings = new List<string>();
        static Mesh cubeMesh, sphereMesh, cylMesh;
        static Font font;

        static Matrix4x4 hallL2W, hallW2L, bridgeL2W, bridgeW2L;
        static float hallYaw, bridgeYaw;

        static Vector3 Hall(float lx, float lz, float y = 0f) => hallL2W.MultiplyPoint3x4(new Vector3(lx, y, lz));

        static void ComputeFrames()
        {
            // L'ancien campus est orienté pour que sa porte (sous la marquise) regarde vers Le Pouce.
            Vector3 toPouce = PoucePos - PoleDoor; toPouce.y = 0f; toPouce.Normalize();
            hallYaw = Mathf.Atan2(toPouce.x, toPouce.z) * Mathf.Rad2Deg;
            Quaternion q = Quaternion.Euler(0f, hallYaw, 0f);
            Vector3 origin = PoleDoor - q * new Vector3(DoorLx, 0f, DoorLz);
            hallL2W = Matrix4x4.TRS(origin, q, Vector3.one);
            hallW2L = hallL2W.inverse;

            Vector3 west = -AxisEast;
            bridgeYaw = Mathf.Atan2(west.x, west.z) * Mathf.Rad2Deg;
            bridgeL2W = Matrix4x4.TRS(A(BridgeS), Quaternion.Euler(0f, bridgeYaw, 0f), Vector3.one);
            bridgeW2L = bridgeL2W.inverse;
        }

        static Vector3 N(float s, float n, float y = 0f) => A(s, y) + new Vector3(-AxisEast.z, 0, AxisEast.x) * n; // n > 0 : côté nord de l'axe

        static Vector3[] ControlPoints()
        {
            Vector3 e = (PoucePos - PoleDoor); e.y = 0; e.Normalize();
            var pts = new List<Vector3>
            {
                Hall(DoorLx, HallZ0 + 4f),       // fond du hall : départ
                Hall(DoorLx, 4f),
                Hall(DoorLx, DoorLz),            // porte sous la marquise
                Hall(DoorLx, 30f),
                PoleDoor + e * 45f,
                new Vector3(20, 0, 182),         // camion-poubelle qui manœuvre
                new Vector3(62, 0, 138),
                new Vector3(104, 0, 78),         // Le Pouce géant à gauche
                N(76, 94),                       // virage serré vers l'entrée du quartier City Slicker
            };
            // quartier City Slicker : vallée sous les ponts, épingle, remontée, grand pont, rond-point de l'Ange
            foreach (var c in ModMain) pts.Add(Mod(c));
            pts.AddRange(new[]
            {
                A(200), A(165),                  // ralentisseurs, puis montée vers l'esplanade
                A(55, ArchFloor),                // haut des marches
                A(0, ArchFloor),                 // SOUS la Grande Arche (tremplin)
                A(-62, ArchFloor),
                N(-92, 7, ArchFloor - 4.5f),     // S rapide dans la descente, sous le viaduc
                N(-122, -7, ArchFloor - 11f),
                A(-160, 0.3f),
                A(-215), A(-270),                // pont au-dessus du boulevard circulaire
                // Terrasses : boucle qui repasse au-dessus d'elle-même
                N(-300, 0), N(-330, 2, 0.5f), N(-358, 18, 2.5f), N(-366, 40, 4.5f), N(-352, 64, 6.5f), N(-325, 72, 8f), N(-302, 56, 9f),
                N(-294, 30, 9.3f), N(-294, 0, 9.3f), N(-296, -22, 7.5f), N(-312, -44, 4.5f), N(-338, -56, 1.5f), N(-362, -56),
                // chicane avec barrières coulissantes
                N(-384, -50), N(-406, -61), N(-426, -50),
                // épingle à droite puis bosse
                N(-436, -44), N(-450, -24), N(-448, 2, 1.5f), N(-442, 30, 5f), N(-440, 52, 6f), N(-442, 72, 2f), N(-446, 92),
                // épingle à gauche puis arrivée au Campus du Parc
                N(-458, 118), N(-484, 124), N(-505, 108), N(-512, 84), N(-528, 60), N(-555, 44), N(-600, 40), N(-650, 40),
            });
            return pts.ToArray();
        }

        static Vector3[] ItemBoxSpots() => new[] { PoleDoor + (PoucePos - PoleDoor).normalized * 75f, N(60, 108), Mod(-80f, -7f, -14f), Mod(-139f, -3.5f, -145f),
                                                   A(-170), N(-368, -55), N(-484, 124) };
        static Vector3[] BoostSpots() => new[] { A(150, 1f), A(48, ArchFloor), Mod(-86.3f, -4.2f, 51.3f), Mod(-140.8f, -2.6f, -129.1f), N(-345, 9, 1.5f), N(-512, 84) };
        static Vector3 PillarsSpot => N(-572, 43);

        [MenuItem("DeVinci Kart/Générer la piste Campus → Campus")]
        public static void Generate()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Warnings.Clear();
            M.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("DeVinci Kart", "Préparation…", 0.05f);
                EnsureFolders();
                GrabPrimitiveMeshes();
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                ComputeFrames();
                ComputeModuleFrame();
                moduleActive = true;
                itemSerial = 0;
                minimapAlts.Clear();
                altCPath = null;

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Textures et matériaux…", 0.1f);
                CreateMaterials();
                CreateExtraMaterials();
                EnsureKartLabSetup();
                ResetProps(null);

                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var root = new GameObject("Piste_Campus").transform;

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Quartier City Slicker (rond-point de l'Ange, parc, ponts)…", 0.15f);
                BuildModule(root);

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Tracé de la piste…", 0.2f);
                var path = BuildPath(ControlPoints(), Step);
                int missed = SnapToModule(path);
                if (missed > 30) Warnings.Add($"{missed} points du tracé n'ont pas trouvé la route du quartier City Slicker.");
                altCPath = PrepareDirtShortcut(path);
                var trackGO = new GameObject("TrackPath");
                trackGO.transform.SetParent(root, false);
                var tp = trackGO.AddComponent<TrackPath>();
                tp.points = path.p.ToArray();
                tp.forwards = path.fwd.ToArray();
                tp.length = path.length;

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Route, vibreurs, barrières…", 0.3f);
                BuildRoad(path, root);

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Sol, tranchée, pont, parc…", 0.4f);
                BuildGroundAndTrench(root);
                BuildBridge(root);
                BuildPark(root);

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Hall du Pôle Léonard de Vinci…", 0.5f);
                BuildHall(root);

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Import des modèles 3D…", 0.6f);
                PlaceLandmarks(root);

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Décor de course…", 0.75f);
                BuildItemBoxes(path, root);
                BuildBoostPads(path, root);
                int finishIdx = path.Count - 1 - 15;
                BuildFinish(path, finishIdx, root);
                BuildJumpsAndPillars(path, root);
                BuildKicker(path, root);
                BuildShops(path, finishIdx, root);
                BuildBillboards(path, root);

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Panneaux, arches, tribunes, public, circulation…", 0.8f);
                ResetProps(path);
                BuildTrackProps(path, finishIdx, root);
                BuildLife(path, finishIdx, root);

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Variantes, raccourcis, viaduc, obstacles…", 0.83f);
                BuildModuleAlternatives(path, root);
                BuildDirtShortcut(path, root);
                BuildViaduct(path, root);
                BuildObstaclesAndRoadProps(path, root);
                BuildMinimap(root, path, minimapAlts, AxisEast, "Campus", false, 0);

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Arbres, lampadaires, tours…", 0.85f);
                BuildDecor(path, root);

                SetupCameraAndLighting();

                EditorUtility.DisplayProgressBar("DeVinci Kart", "Kart KartLab, ligne centrale, checkpoints…", 0.93f);
                var route = BuildTrackRoute(path, root);
                BuildKartLabCheckpoints(path, root);
                PlaceKartLabKart(route);

                EditorSceneManager.SaveScene(scene, ScenePath);
                AddSceneToBuild(ScenePath);

                string msg = $"Piste générée : {path.length:0} m (quartier City Slicker, boucle, chicane, 3 variantes / raccourcis).\nScène : {ScenePath}";
                if (Warnings.Count > 0) msg += "\n\nAttention :\n- " + string.Join("\n- ", Warnings);
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("DeVinci Kart", msg, "OK");
            }
            finally
            {
                moduleActive = false;
                altCPath = null;
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ utilitaires
        static void EnsureFolders()
        {
            foreach (var f in new[] { "Assets/DeVinciKart", GenFolder, GenFolder + "/Textures", GenFolder + "/Materials", GenFolder + "/Meshes", GenFolder + "/MeshesCity" })
            {
                if (AssetDatabase.IsValidFolder(f)) continue;
                string parent = Path.GetDirectoryName(f).Replace('\\', '/');
                AssetDatabase.CreateFolder(parent, Path.GetFileName(f));
            }
        }

        static void GrabPrimitiveMeshes()
        {
            System.Func<PrimitiveType, Mesh> Grab = t =>
            {
                var g = GameObject.CreatePrimitive(t);
                var m = g.GetComponent<MeshFilter>().sharedMesh;
                Object.DestroyImmediate(g);
                return m;
            };
            cubeMesh = Grab(PrimitiveType.Cube);
            sphereMesh = Grab(PrimitiveType.Sphere);
            cylMesh = Grab(PrimitiveType.Cylinder);
        }

        class MB
        {
            public readonly List<Vector3> v = new List<Vector3>();
            public readonly List<Vector2> uv = new List<Vector2>();
            public readonly List<int> t = new List<int>();

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector2 ua, Vector2 ub, Vector2 uc, Vector2 ud, Vector3 n)
            {
                int i = v.Count;
                v.Add(a); v.Add(b); v.Add(c); v.Add(d);
                uv.Add(ua); uv.Add(ub); uv.Add(uc); uv.Add(ud);
                Vector3 cr = Vector3.Cross(b - a, c - a);
                if (cr.sqrMagnitude < 1e-10f) cr = Vector3.Cross(c - a, d - a);
                if (Vector3.Dot(cr, n) >= 0f)
                {
                    t.Add(i); t.Add(i + 1); t.Add(i + 2);
                    t.Add(i); t.Add(i + 2); t.Add(i + 3);
                }
                else
                {
                    t.Add(i); t.Add(i + 2); t.Add(i + 1);
                    t.Add(i); t.Add(i + 3); t.Add(i + 2);
                }
            }

            void Face(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 n, int ax, int ay, float tile)
            {
                System.Func<Vector3, Vector2> U = q => new Vector2(q[ax] / tile, q[ay] / tile);
                Quad(a, b, c, d, U(a), U(b), U(c), U(d), n);
            }

            public void Box(Vector3 mn, Vector3 mx, float tile, bool bottom = true)
            {
                var a = new Vector3(mn.x, mn.y, mn.z); var b = new Vector3(mx.x, mn.y, mn.z);
                var c = new Vector3(mx.x, mn.y, mx.z); var d = new Vector3(mn.x, mn.y, mx.z);
                var e = new Vector3(mn.x, mx.y, mn.z); var f = new Vector3(mx.x, mx.y, mn.z);
                var g = new Vector3(mx.x, mx.y, mx.z); var h = new Vector3(mn.x, mx.y, mx.z);
                Face(e, f, g, h, Vector3.up, 0, 2, tile);
                if (bottom) Face(a, b, c, d, Vector3.down, 0, 2, tile);
                Face(a, b, f, e, Vector3.back, 0, 1, tile);
                Face(d, c, g, h, Vector3.forward, 0, 1, tile);
                Face(a, d, h, e, Vector3.left, 2, 1, tile);
                Face(b, c, g, f, Vector3.right, 2, 1, tile);
            }

            public bool Empty => v.Count == 0;

            public Mesh Build(string name)
            {
                var m = new Mesh { name = name, indexFormat = IndexFormat.UInt32 };
                m.SetVertices(v);
                m.SetUVs(0, uv);
                m.SetTriangles(t, 0);
                m.RecalculateNormals();
                m.RecalculateTangents();
                m.RecalculateBounds();
                return SaveMesh(m, name);
            }
        }

        // Dossier des maillages générés : chaque piste a le sien pour ne pas écraser l'autre.
        static string MeshDir = GenFolder + "/Meshes";

        static Mesh SaveMesh(Mesh m, string name)
        {
            string p = $"{MeshDir}/{name}.asset";
            if (AssetDatabase.LoadAssetAtPath<Mesh>(p) != null) AssetDatabase.DeleteAsset(p);
            AssetDatabase.CreateAsset(m, p);
            return m;
        }

        static GameObject MeshGO(string name, Transform parent, MB mb, string mat, bool collider)
        {
            if (mb.Empty) return null;
            var mesh = mb.Build(name);
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = M[mat];
            if (collider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        static GameObject Prim(PrimitiveType type, string name, Transform parent, Vector3 pos, Vector3 scale, string mat, Quaternion? rot = null, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot ?? Quaternion.identity;
            go.transform.localScale = scale;
            go.GetComponent<MeshRenderer>().sharedMaterial = M[mat];
            if (!keepCollider) Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }

        static GameObject Text3D(Transform parent, string text, Vector3 pos, Quaternion rot, float size, Color color)
        {
            var go = new GameObject("Texte_" + text.Replace("\n", " "));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localRotation = rot;
            var tm = go.AddComponent<TextMesh>();
            tm.text = text;
            tm.font = font;
            tm.fontSize = 96;
            tm.characterSize = size / 10f;
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.color = color;
            go.GetComponent<MeshRenderer>().sharedMaterial = font.material;
            return go;
        }

        static void AddCombined(Dictionary<string, List<CombineInstance>> lists, string mat, Mesh mesh, Matrix4x4 m)
        {
            if (!lists.TryGetValue(mat, out var l)) lists[mat] = l = new List<CombineInstance>();
            l.Add(new CombineInstance { mesh = mesh, transform = m });
        }

        static void FlushCombined(Dictionary<string, List<CombineInstance>> lists, string prefix, Transform parent)
        {
            foreach (var kv in lists)
            {
                var mesh = new Mesh { name = prefix + "_" + kv.Key, indexFormat = IndexFormat.UInt32 };
                mesh.CombineMeshes(kv.Value.ToArray(), true, true);
                mesh.RecalculateBounds();
                SaveMesh(mesh, mesh.name);
                var go = new GameObject(mesh.name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = M[kv.Key];
                GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            }
        }

        static Vector3 CentripetalCR(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float u)
        {
            System.Func<Vector3, Vector3, float> K = (a, b) => Mathf.Max(Mathf.Sqrt(Vector3.Distance(a, b)), 1e-3f);
            float t0 = 0f, t1 = t0 + K(p0, p1), t2 = t1 + K(p1, p2), t3 = t2 + K(p2, p3);
            float t = Mathf.Lerp(t1, t2, u);
            Vector3 a1 = (t1 - t) / (t1 - t0) * p0 + (t - t0) / (t1 - t0) * p1;
            Vector3 a2 = (t2 - t) / (t2 - t1) * p1 + (t - t1) / (t2 - t1) * p2;
            Vector3 a3 = (t3 - t) / (t3 - t2) * p2 + (t - t2) / (t3 - t2) * p3;
            Vector3 b1 = (t2 - t) / (t2 - t0) * a1 + (t - t0) / (t2 - t0) * a2;
            Vector3 b2 = (t3 - t) / (t3 - t1) * a2 + (t - t1) / (t3 - t1) * a3;
            return (t2 - t) / (t2 - t1) * b1 + (t - t1) / (t2 - t1) * b2;
        }

        static PathData BuildPath(Vector3[] c, float step)
        {
            int n = c.Length;
            System.Func<Vector3, Vector3> Flat = v => new Vector3(v.x, 0f, v.z);
            var dense = new List<Vector3>();
            for (int i = 0; i < n - 1; i++)
            {
                Vector3 p0 = i == 0 ? 2f * c[0] - c[1] : c[i - 1];
                Vector3 p3 = i + 2 < n ? c[i + 2] : 2f * c[n - 1] - c[n - 2];
                const int sub = 60;
                for (int s = 0; s < sub; s++)
                {
                    float u = s / (float)sub;
                    Vector3 q = CentripetalCR(Flat(p0), Flat(c[i]), Flat(c[i + 1]), Flat(p3), u);
                    float sm = u * u * (3f - 2f * u);
                    q.y = Mathf.Lerp(c[i].y, c[i + 1].y, sm);
                    dense.Add(q);
                }
            }
            dense.Add(c[n - 1]);

            var pd = new PathData();
            pd.p.Add(dense[0]); pd.dist.Add(0f);
            float acc = 0f, target = step;
            for (int i = 1; i < dense.Count; i++)
            {
                Vector3 a = dense[i - 1], b = dense[i];
                float L = Vector3.Distance(a, b);
                if (L < 1e-6f) continue;
                while (acc + L >= target)
                {
                    pd.p.Add(Vector3.Lerp(a, b, (target - acc) / L));
                    pd.dist.Add(target);
                    target += step;
                }
                acc += L;
            }
            if ((pd.p[pd.p.Count - 1] - dense[dense.Count - 1]).magnitude > 0.2f)
            {
                pd.p.Add(dense[dense.Count - 1]);
                pd.dist.Add(acc);
            }
            pd.length = acc;

            for (int i = 0; i < pd.p.Count; i++)
            {
                Vector3 f = (pd.p[Mathf.Min(i + 1, pd.p.Count - 1)] - pd.p[Mathf.Max(i - 1, 0)]).normalized;
                Vector3 fFlat = new Vector3(f.x, 0f, f.z).normalized;
                pd.fwd.Add(f);
                pd.right.Add(Vector3.Cross(Vector3.up, fFlat).normalized);
            }
            return pd;
        }

        static Transform FrameGO(string name, Transform parent, Matrix4x4 l2w)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.SetPositionAndRotation(l2w.GetColumn(3), l2w.rotation);
            return t;
        }

        static Zone ZoneAt(Vector3 p)
        {
            Vector3 h = hallW2L.MultiplyPoint3x4(p);
            if (h.x > HallX0 - 0.5f && h.x < HallX1 + 0.5f && h.z > HallZ0 - 1f && h.z < HallZ1 + 0.5f) return Zone.Hall;
            Vector3 b = bridgeW2L.MultiplyPoint3x4(p);
            if (Mathf.Abs(b.x) < 4f && Mathf.Abs(b.z) < TrenchHalfW + 12f) return Zone.Bridge;
            if (InModuleCrop(p, 0.5f)) return Zone.Module;
            return Zone.Normal;
        }

        // ------------------------------------------------------------------ route

        static void BuildRoad(PathData path, Transform root)
        {
            var parent = new GameObject("Route").transform;
            parent.SetParent(root, false);
            int n = path.Count;
            const int chunk = 200;
            System.Func<int, float, Vector3> L = (i, off) => path.p[i] - path.right[i] * off + Vector3.up * RoadLift;
            System.Func<int, float, Vector3> R = (i, off) => path.p[i] + path.right[i] * off + Vector3.up * RoadLift;

            for (int c0 = 0, k = 0; c0 < n - 1; c0 += chunk, k++)
            {
                var mb = new MB();
                int c1 = Mathf.Min(c0 + chunk, n - 1);
                for (int i = c0; i < c1; i++)
                {
                    if (ZoneAt(path.p[i]) == Zone.Module && ZoneAt(path.p[i + 1]) == Zone.Module) continue;   // City Slicker a déjà sa route
                    float v0 = path.dist[i] / 8f, v1 = path.dist[i + 1] / 8f;
                    mb.Quad(L(i, RoadHalf), L(i + 1, RoadHalf), R(i + 1, RoadHalf), R(i, RoadHalf),
                        new Vector2(0, v0), new Vector2(0, v1), new Vector2(1, v1), new Vector2(1, v0), Vector3.up);
                }
                MeshGO($"Chaussee_{k:00}", parent, mb, "Asphalt", true);
            }

            var curbN = new MB(); var curbB = new MB();
            var barN = new MB(); var barB = new MB(); var skirt = new MB(); var bank = new MB(); var deck = new MB();
            float o1 = RoadHalf + CurbW, o2 = o1 + BarrierT;

            // distance horizontale à une autre portion de route plus basse (passages dessus / dessous)
            var lowerDist = new float[n];
            for (int i = 0; i < n; i++)
            {
                lowerDist[i] = float.MaxValue;
                if (path.p[i].y < 1.5f) continue;
                for (int j = 0; j < n; j += 2)
                {
                    if (Mathf.Abs(i - j) < 60 || path.p[j].y > path.p[i].y - 2.5f) continue;
                    float dx = path.p[j].x - path.p[i].x, dz = path.p[j].z - path.p[i].z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    if (d < lowerDist[i]) lowerDist[i] = d;
                }
            }

            // Terrasses : une portion surélevée est soit entièrement en talus, soit entièrement en viaduc.
            // (avant, le talus s'arrêtait net là où le tablier commençait : trou visible et talus « coupé » en biseau)
            var viaductRun = new bool[n];
            for (int r0 = 0; r0 < n; )
            {
                if (path.p[r0].y <= 0.1f) { r0++; continue; }
                int r1 = r0;
                while (r1 + 1 < n && path.p[r1 + 1].y > 0.1f) r1++;
                bool needDeck = false;
                for (int i = r0; i <= r1 && !needDeck; i++)
                {
                    float along = Vector3.Dot(new Vector3(path.p[i].x, 0f, path.p[i].z), AxisEast);
                    if (along >= -262f || ModuleBlocked(path.p[i], 4f)) continue;
                    needDeck = lowerDist[i] <= o2 + 1.6f * path.p[i].y + 10f;
                }
                for (int i = Mathf.Max(0, r0 - 1); i <= r1; i++) viaductRun[i] = needDeck;
                r0 = r1 + 1;
            }

            var barGapPrev = new bool[2];
            for (int i = 0; i < n - 1; i++)
            {
                Zone z0 = ZoneAt(path.p[i]), z1 = ZoneAt(path.p[i + 1]);
                if (z0 == Zone.Hall || z1 == Zone.Hall || z0 == Zone.Module || z1 == Zone.Module) continue;
                bool bridge = z0 == Zone.Bridge;
                var curb = bridge ? curbB : curbN;
                var bar = bridge ? barB : barN;
                float hgt = bridge ? 1.2f : 1.0f;
                float lip = bridge ? 0.15f : 0.08f;
                float u0 = path.dist[i] / 4f, u1 = path.dist[i + 1] / 4f;

                foreach (int side in new[] { -1, 1 })
                {
                    System.Func<int, float, float, Vector3> P = (j, off, dy) => path.p[j] + path.right[j] * (side * off) + Vector3.up * (RoadLift + dy);
                    Vector3 outward = path.right[i] * side;

                    curb.Quad(P(i, RoadHalf, 0f), P(i + 1, RoadHalf, 0f), P(i + 1, o1, lip), P(i, o1, lip),
                        new Vector2(0, u0), new Vector2(0, u1), new Vector2(1, u1), new Vector2(1, u0), Vector3.up);
                    float yb = -0.35f;
                    int si = side < 0 ? 0 : 1;
                    bool gap = InBarrierGap(P(i, o1, 0f)) || InBarrierGap(P(i + 1, o1, 0f));   // ouverture vers un raccourci
                    if (gap != barGapPrev[si] && i > 0)
                    {
                        // bout de barrière fermé (sinon on voit l'intérieur creux au bord de l'ouverture)
                        Vector3 capN = path.fwd[i] * (gap ? 1f : -1f);
                        Vector3 ci = P(i, o1, yb), co = P(i, o2, yb), cit = P(i, o1, hgt), cot = P(i, o2, hgt);
                        if ((side > 0) == gap) bar.Quad(ci, cit, cot, co, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.1f, 1), new Vector2(0.1f, 0), capN);
                        else bar.Quad(co, cot, cit, ci, new Vector2(0, 0), new Vector2(0, 1), new Vector2(0.1f, 1), new Vector2(0.1f, 0), capN);
                    }
                    barGapPrev[si] = gap;
                    if (!gap)
                    {
                        bar.Quad(P(i, o1, yb), P(i + 1, o1, yb), P(i + 1, o1, hgt), P(i, o1, hgt),
                            new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1), -outward);
                        bar.Quad(P(i, o1, hgt), P(i + 1, o1, hgt), P(i + 1, o2, hgt), P(i, o2, hgt),
                            new Vector2(u0, 0.95f), new Vector2(u1, 0.95f), new Vector2(u1, 1), new Vector2(u0, 1), Vector3.up);
                        bar.Quad(P(i, o2, yb), P(i + 1, o2, yb), P(i + 1, o2, hgt), P(i, o2, hgt),
                            new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1), outward);
                    }
                    // dans une ouverture, le talus / mur part directement du bord du vibreur (pas de fente sous la route)
                    float ob = gap ? o1 : o2, yt = gap ? lip : yb;

                    // sous la route surélevée : tablier de pont au-dessus d'une autre route, talus en herbe dans les Terrasses, sinon mur
                    if (path.p[i].y > 0.1f || path.p[i + 1].y > 0.1f)
                    {
                        float h = Mathf.Max(path.p[i].y, path.p[i + 1].y);
                        float along = Vector3.Dot(new Vector3(path.p[i].x, 0f, path.p[i].z), AxisEast);
                        float ld = Mathf.Min(lowerDist[i], lowerDist[i + 1]);
                        bool terrasses = along < -262f && !ModuleBlocked(path.p[i], 4f);
                        if (ld < 16f || (terrasses && viaductRun[i]))
                        {
                            Vector3 a0 = P(i, o2, -1.25f), a1 = P(i + 1, o2, -1.25f), b0 = P(i, 0f, -1.25f), b1 = P(i + 1, 0f, -1.25f);
                            deck.Quad(a0, a1, P(i + 1, o2, yb), P(i, o2, yb), new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 0.3f), new Vector2(u0, 0.3f), outward);
                            deck.Quad(b0, b1, a1, a0, new Vector2(u0, 0), new Vector2(u1, 0), new Vector2(u1, 1), new Vector2(u0, 1), Vector3.down);
                            // piles sous le tablier, hors de la route du dessous
                            if (i % 9 == 0 && path.p[i].y + RoadLift - 1.25f > 0.4f)
                            {
                                Vector3 pp = path.p[i] + path.right[i] * (side * (o2 - 0.9f)); pp.y = 0f;
                                float free = float.MaxValue;
                                for (int jj = 0; jj < n; jj += 2)
                                {
                                    if (path.p[jj].y > path.p[i].y - 2.5f) continue;
                                    float ddx = path.p[jj].x - pp.x, ddz = path.p[jj].z - pp.z;
                                    free = Mathf.Min(free, Mathf.Sqrt(ddx * ddx + ddz * ddz));
                                }
                                if (free > o2 + 1.2f)
                                    deck.Box(pp + new Vector3(-0.7f, 0f, -0.7f), pp + new Vector3(0.7f, path.p[i].y + RoadLift - 1.25f, 0.7f), 3f, false);
                            }
                        }
                        else if (terrasses)
                        {
                            float w0 = ob + 1.6f * Mathf.Max(0f, path.p[i].y + yt), w1 = ob + 1.6f * Mathf.Max(0f, path.p[i + 1].y + yt);
                            Vector3 g0 = path.p[i] + path.right[i] * (side * w0); g0.y = -0.05f;
                            Vector3 g1 = path.p[i + 1] + path.right[i + 1] * (side * w1); g1.y = -0.05f;
                            Vector3 c0 = P(i, ob, yt), c1 = P(i + 1, ob, yt);
                            bank.Quad(c0, c1, g1, g0, new Vector2(c0.x, c0.z) / 8f, new Vector2(c1.x, c1.z) / 8f, new Vector2(g1.x, g1.z) / 8f, new Vector2(g0.x, g0.z) / 8f, outward + Vector3.up);   // même échelle de texture que le sol
                        }
                        else
                        {
                            Vector3 a = path.p[i] + path.right[i] * (side * ob); a.y = -0.3f;
                            Vector3 b = path.p[i + 1] + path.right[i + 1] * (side * ob); b.y = -0.3f;
                            Vector3 c = P(i + 1, ob, yt), d = P(i, ob, yt);
                            float ua = path.dist[i] / 4f, ub = path.dist[i + 1] / 4f;
                            skirt.Quad(a, b, c, d, new Vector2(ua, a.y / 4f), new Vector2(ub, b.y / 4f),
                                new Vector2(ub, c.y / 4f), new Vector2(ua, d.y / 4f), outward);
                        }
                    }
                }
            }

            MeshGO("Vibreurs", parent, curbN, "Curb", true);
            MeshGO("Trottoirs_Pont", parent, curbB, "Concrete", true);
            MeshGO("Barrieres", parent, barN, "Barrier", true);
            MeshGO("Parapets_Pont", parent, barB, "Concrete", true);
            MeshGO("Murs_Route_Surelevee", parent, skirt, "Stone", true);
            var bankGO = MeshGO("Talus_Herbe", parent, bank, "Grass", true);
            if (bankGO != null) bankGO.AddComponent<GroundSurface>().speedFactor = 0.6f;
            MeshGO("Tabliers_Ponts", parent, deck, "Concrete", true);
        }

        // ------------------------------------------------------------------ sol / tranchée / pont / parc

        static void BuildGroundAndTrench(Transform root)
        {
            // Tout est construit dans le repère du pont : X = le long de la tranchée, Z = le long de l'axe (vers l'ouest).
            var parent = FrameGO("Sol_et_Tranchee", root, bridgeL2W);
            const float big = 4000f;
            float hw = TrenchHalfW, hl = TrenchHalfLen, d = TrenchDepth;

            BuildGround(parent);

            var walls = new MB();
            const float rw = RoadHalf + CurbW + BarrierT + 0.6f;   // sous la route du pont : le mur s'arrête au niveau du sol (pas de marche)
            foreach (float zs in new[] { -1f, 1f })
            {
                float za = zs < 0 ? -hw - 0.6f : hw - 0.2f, zb = zs < 0 ? -hw + 0.2f : hw + 0.6f;
                walls.Box(new Vector3(-hl, -d, za), new Vector3(-rw, 0.4f, zb), 4f, false);
                walls.Box(new Vector3(rw, -d, za), new Vector3(hl, 0.4f, zb), 4f, false);
                walls.Box(new Vector3(-rw, -d, za), new Vector3(rw, -0.02f, zb), 4f, false);
            }
            MeshGO("Murs_Tranchee", parent, walls, "Concrete", true);

            var floor = new MB();
            Vector3 f0 = new Vector3(-hl, -d + 0.02f, -hw), f1 = new Vector3(hl, -d + 0.02f, -hw),
                    f2 = new Vector3(hl, -d + 0.02f, hw), f3 = new Vector3(-hl, -d + 0.02f, hw);
            // la texture d'asphalte est orientée le long de X (sens de circulation du boulevard)
            floor.Quad(f0, f1, f2, f3, new Vector2(0, f0.x / 8f), new Vector2(0, f1.x / 8f), new Vector2(3, f2.x / 8f), new Vector2(3, f3.x / 8f), Vector3.up);
            MeshGO("Boulevard_Circulaire", parent, floor, "Asphalt", false);

            var portals = new MB();
            portals.Box(new Vector3(-hl - 6f, -d, -hw - 0.6f), new Vector3(-hl, 2f, hw + 0.6f), 4f, false);
            portals.Box(new Vector3(hl, -d, -hw - 0.6f), new Vector3(hl + 6f, 2f, hw + 0.6f), 4f, false);
            MeshGO("Tunnels", parent, portals, "DarkMetal", false);
            Text3D(parent, "BOULEVARD CIRCULAIRE", new Vector3(-hl + 0.2f, -3f, 0f), Quaternion.Euler(0, -90, 0), 2.5f, Color.white);
        }

        static void BuildBridge(Transform root)
        {
            var parent = FrameGO("Pont_Boulevard_Circulaire", root, bridgeL2W);
            float hb = RoadHalf + CurbW + BarrierT;
            float hw = TrenchHalfW + 2f;
            var deck = new MB();
            deck.Box(new Vector3(-hb, -1.6f, -hw), new Vector3(hb, 0.02f, hw), 4f);            // tablier
            deck.Box(new Vector3(-hb + 1f, -3f, -hw), new Vector3(-hb + 2.2f, -1.6f, hw), 4f);  // poutres
            deck.Box(new Vector3(hb - 2.2f, -3f, -hw), new Vector3(hb - 1f, -1.6f, hw), 4f);
            deck.Box(new Vector3(-hb + 0.5f, -TrenchDepth, -1.5f), new Vector3(hb - 0.5f, -3f, 1.5f), 4f); // pile centrale (terre-plein)
            MeshGO("Pont_Beton", parent, deck, "Concrete", false);

            var lists = new Dictionary<string, List<CombineInstance>>();
            foreach (float z in new[] { -32f, -16f, 0f, 16f, 32f })
            foreach (float x in new[] { -(RoadHalf + CurbW + BarrierT * 0.5f), RoadHalf + CurbW + BarrierT * 0.5f })
            {
                Vector3 p = bridgeL2W.MultiplyPoint3x4(new Vector3(x, 1.2f + RoadLift, z));
                AddCombined(lists, "DarkMetal", cylMesh, Matrix4x4.TRS(p + Vector3.up * 2.2f, Quaternion.identity, new Vector3(0.18f, 2.2f, 0.18f)));
                AddCombined(lists, "LampHead", sphereMesh, Matrix4x4.TRS(p + Vector3.up * 4.6f, Quaternion.identity, Vector3.one * 0.7f));
            }
            FlushCombined(lists, "Pont_Lampadaires", root);
            var sign = Text3D(parent, "JARDINS DE L'ARCHE", Vector3.zero, Quaternion.identity, 1.4f, Color.white);
            sign.transform.localPosition = new Vector3(0, 7f, -hw - 4f);
            var gant = new MB();
            gant.Box(new Vector3(-hb - 1f, 0, -hw - 4.4f), new Vector3(-hb, 8f, -hw - 3.6f), 2f);
            gant.Box(new Vector3(hb, 0, -hw - 4.4f), new Vector3(hb + 1f, 8f, -hw - 3.6f), 2f);
            gant.Box(new Vector3(-hb - 1f, 6f, -hw - 4.4f), new Vector3(hb + 1f, 8f, -hw - 3.6f), 2f);
            MeshGO("Portique_Pont", parent, gant, "Brand", true);
        }

        static void BuildPark(Transform root)
        {
            var parent = new GameObject("Parc_Andre_Malraux").transform;
            parent.SetParent(root, false);
            // lac (ellipse)
            var lake = new MB();
            Vector3 c = ParkCenter + new Vector3(-5f, 0.04f, 15f);
            const int seg = 48; float rx = 70f, rz = 35f; Quaternion rot = Quaternion.Euler(0, 20f, 0);
            for (int i = 0; i < seg; i++)
            {
                float a0 = i * Mathf.PI * 2f / seg, a1 = (i + 1) * Mathf.PI * 2f / seg;
                Vector3 p0 = c + rot * new Vector3(Mathf.Cos(a0) * rx, 0, Mathf.Sin(a0) * rz);
                Vector3 p1 = c + rot * new Vector3(Mathf.Cos(a1) * rx, 0, Mathf.Sin(a1) * rz);
                lake.Quad(c, p0, p1, c, new Vector2(c.x, c.z) / 20f, new Vector2(p0.x, p0.z) / 20f, new Vector2(p1.x, p1.z) / 20f, new Vector2(c.x, c.z) / 20f, Vector3.up);
            }
            var lakeGO = MeshGO("Lac", parent, lake, "Water", false);
            GameObjectUtility.SetStaticEditorFlags(lakeGO, 0);
            lakeGO.AddComponent<UVScroller>().speed = new Vector2(0.01f, 0.006f);
            // chemins
            var paths = new MB();
            for (int i = 0; i < 64; i++)
            {
                float a0 = i * Mathf.PI * 2f / 64, a1 = (i + 1) * Mathf.PI * 2f / 64;
                foreach (float rr in new[] { 100f })
                {
                    Vector3 i0 = ParkCenter + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * (rr - 2f) + Vector3.up * 0.03f;
                    Vector3 i1 = ParkCenter + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * (rr - 2f) + Vector3.up * 0.03f;
                    Vector3 o0 = ParkCenter + new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * (rr + 2f) + Vector3.up * 0.03f;
                    Vector3 o1 = ParkCenter + new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * (rr + 2f) + Vector3.up * 0.03f;
                    paths.Quad(i0, o0, o1, i1, new Vector2(i0.x, i0.z) / 6f, new Vector2(o0.x, o0.z) / 6f, new Vector2(o1.x, o1.z) / 6f, new Vector2(i1.x, i1.z) / 6f, Vector3.up);
                }
            }
            MeshGO("Allees", parent, paths, "Concrete", false);
            var t = Text3D(parent, "PARC ANDRÉ-MALRAUX", Vector3.zero, Quaternion.identity, 4f, Color.white);
            t.transform.position = ParkCenter + new Vector3(70f, 8f, 115f);
            t.transform.rotation = Quaternion.Euler(0, -60f, 0);
        }

        // ------------------------------------------------------------------ hall de départ (dans le Pôle)

        static void BuildHall(Transform root)
        {
            var parent = FrameGO("Hall_PoleLeonardDeVinci", root, hallL2W);
            float cx = DoorLx;

            var floor = new MB(); floor.Box(new Vector3(HallX0 - 0.5f, -0.5f, HallZ0 - 0.5f), new Vector3(HallX1 + 0.5f, 0.02f, HallZ1 + 0.3f), 4f, false);
            MeshGO("Hall_Sol", parent, floor, "Tiles", true);

            var walls = new MB();
            walls.Box(new Vector3(HallX0 - 0.5f, 0, HallZ0 - 0.5f), new Vector3(HallX0, HallH, HallZ1), 10f);
            walls.Box(new Vector3(HallX1, 0, HallZ0 - 0.5f), new Vector3(HallX1 + 0.5f, HallH, HallZ1), 10f);
            walls.Box(new Vector3(HallX0 - 0.5f, 0, HallZ0 - 0.5f), new Vector3(HallX1 + 0.5f, HallH, HallZ0), 10f);
            MeshGO("Hall_Murs", parent, walls, "HallWall", true);

            var ceil = new MB(); ceil.Box(new Vector3(HallX0 - 0.5f, HallH, HallZ0 - 0.5f), new Vector3(HallX1 + 0.5f, HallH + 0.5f, HallZ1), 6f);
            MeshGO("Hall_Plafond", parent, ceil, "Concrete", false);

            var pillars = new MB();
            for (float z = HallZ0 + 4f; z < HallZ1 - 1f; z += 6f)
            {
                pillars.Box(new Vector3(HallX0, 0, z - 0.4f), new Vector3(HallX0 + 0.6f, HallH, z + 0.4f), 4f, false);
                pillars.Box(new Vector3(HallX1 - 0.6f, 0, z - 0.4f), new Vector3(HallX1, HallH, z + 0.4f), 4f, false);
            }
            MeshGO("Hall_Piliers", parent, pillars, "DarkMetal", false);

            var panels = new MB();
            for (float z = HallZ0 + 3f; z < HallZ1 - 1f; z += 5f)
                panels.Box(new Vector3(cx - 3.5f, HallH - 0.15f, z - 0.4f), new Vector3(cx + 3.5f, HallH, z + 0.4f), 4f);
            MeshGO("Hall_Lumieres", parent, panels, "LightPanel", false);
            foreach (float z in new[] { -8f, 3f, 13f })
            {
                var lg = new GameObject("Lumiere_Hall");
                lg.transform.SetParent(parent, false);
                lg.transform.localPosition = new Vector3(cx, HallH - 1.5f, z);
                var l = lg.AddComponent<Light>();
                l.type = LightType.Point;
                l.range = 22f;
                l.intensity = 3f;
                l.color = new Color(1f, 0.95f, 0.88f);
            }

            Text3D(parent, "DEVINCI KART", new Vector3(HallX0 + 0.05f, 6f, -4f), Quaternion.Euler(0, -90, 0), 1.2f, new Color(0.3f, 0.85f, 1f));
            Text3D(parent, "PÔLE LÉONARD DE VINCI", new Vector3(HallX1 - 0.05f, 6f, -4f), Quaternion.Euler(0, 90, 0), 0.9f, new Color(1f, 0.85f, 0.3f));
            Text3D(parent, "ESILV", new Vector3(HallX0 + 0.05f, 6f, 9f), Quaternion.Euler(0, -90, 0), 1.4f, Color.white);
            Text3D(parent, "ESILV", new Vector3(HallX1 - 0.05f, 6f, 9f), Quaternion.Euler(0, 90, 0), 1.4f, Color.white);

            // grille de départ + ligne
            var grid = new MB();
            for (int s = 0; s < 8; s++)
            {
                float x = s % 2 == 0 ? cx - 2.5f : cx + 2.5f;
                float z = 8f - (s / 2) * 4f - (s % 2) * 2f;
                grid.Box(new Vector3(x - 1.3f, 0.07f, z + 1.2f), new Vector3(x + 1.3f, 0.08f, z + 1.45f), 4f, false);
            }
            MeshGO("Grille_Depart", parent, grid, "White", false);
            var line = new MB();
            Vector3 l0 = new Vector3(cx - RoadHalf, 0.075f, 10.5f), l1 = new Vector3(cx + RoadHalf, 0.075f, 10.5f),
                    l2 = new Vector3(cx + RoadHalf, 0.075f, 12f), l3 = new Vector3(cx - RoadHalf, 0.075f, 12f);
            line.Quad(l0, l1, l2, l3, new Vector2(0, 0), new Vector2(8, 0), new Vector2(8, 1), new Vector2(0, 1), Vector3.up);
            MeshGO("Ligne_Depart", parent, line, "Checker", false);

            Text3D(parent, "DÉPART", new Vector3(cx, 7.2f, 12.3f), Quaternion.identity, 1.1f, Color.white);
        }

        // ------------------------------------------------------------------ modèles 3D
        static string Simplify(string s)
        {
            var sb = new StringBuilder();
            foreach (char ch in s.Normalize(NormalizationForm.FormD))
                if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(ch))
                    sb.Append(char.ToLowerInvariant(ch));
            return sb.ToString();
        }

        static GameObject FindModel(string key)
        {
            if (!Directory.Exists(ModelsFolder)) return null;
            string want = Simplify(key);
            foreach (var file in Directory.GetFiles(ModelsFolder, "*.glb"))
            {
                if (Simplify(Path.GetFileNameWithoutExtension(file)) != want) continue;
                string ap = file.Replace('\\', '/');
                var go = AssetDatabase.LoadAssetAtPath<GameObject>(ap);
                if (go == null)
                {
                    AssetDatabase.ImportAsset(ap, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
                    go = AssetDatabase.LoadAssetAtPath<GameObject>(ap);
                }
                return go;
            }
            return null;
        }

        static GameObject InstantiateModel(string key, Transform parent, float approxSize)
        {
            var prefab = FindModel(key);
            if (prefab != null)
                return (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);

            Warnings.Add($"Modèle « {key} » introuvable ou non importé (installez glTFast) → cube de remplacement.");
            var ph = GameObject.CreatePrimitive(PrimitiveType.Cube);
            ph.name = key + " (MANQUANT)";
            ph.transform.SetParent(parent, false);
            Object.DestroyImmediate(ph.GetComponent<Collider>());
            ph.GetComponent<MeshRenderer>().sharedMaterial = M["Missing"];
            var holder = new GameObject(key + "_placeholder");
            holder.transform.SetParent(parent, false);
            ph.transform.SetParent(holder.transform, false);
            ph.transform.localPosition = new Vector3(0, 0.5f, 0);
            ph.transform.localScale = new Vector3(0.9f, 1f, 0.9f);
            return holder;
        }

        static GameObject PlaceScaled(string key, Transform parent, Vector3 pos, float yaw, float scale)
        {
            var go = InstantiateModel(key, parent, 1f);
            // on conserve la rotation / l'échelle d'origine de la racine importée (certains .glb Sketchfab en ont une)
            go.transform.SetPositionAndRotation(pos, Quaternion.Euler(0, yaw, 0) * go.transform.localRotation);
            go.transform.localScale = go.transform.localScale * scale;
            return go;
        }

        static Bounds WorldBounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }

        /// <summary>Place un modèle en le mettant à la hauteur voulue, posé (centre bas) sur groundPos.</summary>
        static GameObject PlaceFit(string key, Transform parent, Vector3 groundPos, float yaw, float height)
        {
            var go = InstantiateModel(key, parent, height);
            // on conserve la rotation / l'échelle d'origine de la racine importée (sinon Lakitu se retrouve la tête en bas)
            Vector3 nativeScale = go.transform.localScale;
            go.transform.SetPositionAndRotation(Vector3.zero, Quaternion.Euler(0, yaw, 0) * go.transform.localRotation);
            var b = WorldBounds(go);
            float s = height / Mathf.Max(b.size.y, 1e-4f);
            go.transform.localScale = nativeScale * s;
            b = WorldBounds(go);
            go.transform.position += groundPos - new Vector3(b.center.x, b.min.y, b.center.z);
            return go;
        }

        /// <summary>Copie les meshes du modèle en retirant les triangles qui touchent la boîte (exprimée dans le repère boxW2L).</summary>
        static void CutHole(GameObject model, Matrix4x4 boxW2L, Bounds box, string name)
        {
            int k = 0;
            foreach (var mf in model.GetComponentsInChildren<MeshFilter>())
            {
                var src = mf.sharedMesh;
                if (src == null) continue;
                Matrix4x4 m2b = boxW2L * mf.transform.localToWorldMatrix;
                using (var arr = Mesh.AcquireReadOnlyMeshData(src))
                {
                    var d = arr[0];
                    int vc = d.vertexCount;
                    var pos = new NativeArray<Vector3>(vc, Allocator.Temp);
                    d.GetVertices(pos);
                    var inside = new bool[vc];
                    int nIn = 0;
                    for (int i = 0; i < vc; i++)
                        if (box.Contains(m2b.MultiplyPoint3x4(pos[i]))) { inside[i] = true; nIn++; }
                    if (nIn == 0) { pos.Dispose(); continue; }

                    var mesh = new Mesh { name = $"{name}_{k}", indexFormat = IndexFormat.UInt32 };
                    mesh.SetVertices(pos);
                    if (d.HasVertexAttribute(VertexAttribute.Normal))
                    {
                        var nor = new NativeArray<Vector3>(vc, Allocator.Temp); d.GetNormals(nor); mesh.SetNormals(nor); nor.Dispose();
                    }
                    if (d.HasVertexAttribute(VertexAttribute.Tangent))
                    {
                        var tan = new NativeArray<Vector4>(vc, Allocator.Temp); d.GetTangents(tan); mesh.SetTangents(tan); tan.Dispose();
                    }
                    if (d.HasVertexAttribute(VertexAttribute.TexCoord0))
                    {
                        var uv = new NativeArray<Vector2>(vc, Allocator.Temp); d.GetUVs(0, uv); mesh.SetUVs(0, uv); uv.Dispose();
                    }
                    if (d.HasVertexAttribute(VertexAttribute.Color))
                    {
                        var col = new NativeArray<Color>(vc, Allocator.Temp); d.GetColors(col); mesh.SetColors(col); col.Dispose();
                    }
                    mesh.subMeshCount = d.subMeshCount;
                    for (int s = 0; s < d.subMeshCount; s++)
                    {
                        var sm = d.GetSubMesh(s);
                        var idx = new NativeArray<int>(sm.indexCount, Allocator.Temp);
                        d.GetIndices(idx, s);
                        var keep = new List<int>(sm.indexCount);
                        for (int t = 0; t + 2 < idx.Length; t += 3)
                        {
                            int a = idx[t], b = idx[t + 1], c = idx[t + 2];
                            if (inside[a] || inside[b] || inside[c]) continue;
                            keep.Add(a); keep.Add(b); keep.Add(c);
                        }
                        mesh.SetTriangles(keep, s, false);
                        idx.Dispose();
                    }
                    pos.Dispose();
                    mesh.RecalculateBounds();
                    mf.sharedMesh = SaveMesh(mesh, mesh.name);
                    k++;
                }
            }
            if (k == 0) Warnings.Add("Aucun triangle trouvé dans le volume du hall : vérifiez la position de l'ancien campus.");
        }

        static void PlaceLandmarks(Transform root)
        {
            var parent = new GameObject("Monuments").transform;
            parent.SetParent(root, false);

            // Ancien campus (Pôle Léonard de Vinci) : la porte sous la marquise donne sur la piste
            Vector3 campusPos = (Vector3)hallL2W.GetColumn(3) + Vector3.up * OldCampusDrop;
            var oldCampus = PlaceScaled("AncienCampus", parent, campusPos, hallYaw, OldCampusScale);
            oldCampus.name = "Ancien Campus (Pôle Léonard de Vinci)";
            var hallBox = new Bounds();
            hallBox.SetMinMax(new Vector3(HallX0 - 0.4f, -1f, HallZ0 - 0.6f), new Vector3(HallX1 + 0.4f, HallH + 0.4f, HallZ1 + 0.6f));
            EditorUtility.DisplayProgressBar("DeVinci Kart", "Ouverture de la porte sous la marquise…", 0.65f);
            CutHole(oldCampus, hallW2L, hallBox, "AncienCampus_Evide");

            // Grande Arche : ouverture dans l'axe, marches côté est (parvis)
            float archYaw = Mathf.Atan2(AxisEast.x, AxisEast.z) * Mathf.Rad2Deg;
            PlaceScaled("ArcheLaDefenseModel", parent, Vector3.zero, archYaw, ArchScale).name = "Grande Arche";

            // Campus du Parc (nouveau campus)
            PlaceScaled("ESILV_NouveauCampus3D", parent, NewCampusPos, -15f, NewCampusScale).name = "Campus du Parc (nouveau campus)";

            // Le Pouce, place Carpeaux
            Prim(PrimitiveType.Cube, "Pouce_Socle", parent, PoucePos + Vector3.up * 0.75f, new Vector3(18f, 1.5f, 18f), "Concrete");
            PlaceFit("LePouceModele3d", parent, PoucePos + Vector3.up * 1.5f, 200f, PouceHeight).name = "Le Pouce (géant)";

            // Lakitu au-dessus de la grille de départ
            var bobHolder = new GameObject("Lakitu_Flottement").transform;
            bobHolder.SetParent(parent, false);
            var lakitu = PlaceFit("Lakitu", bobHolder, Hall(DoorLx - 1.5f, 14.5f, 2.3f), hallYaw + 180f, 2.4f);
            lakitu.name = "Lakitu";
            // feu de départ : les 3 lampes ajoutées dans Blender doivent regarder la grille de départ
            var lamps = new Renderer[3];
            foreach (var r in lakitu.GetComponentsInChildren<Renderer>(true))
                for (int k = 0; k < 3; k++) if (r.name.StartsWith("Lampe_" + (k + 1))) lamps[k] = r;
            if (lamps[1] != null)
            {
                Vector3 grid = Hall(DoorLx - 2.5f, 8f, 1.2f);   // pilote en pole position
                Vector3 face = Flat(lamps[1].transform.up), want = Flat(grid - lamps[1].transform.position);
                bobHolder.RotateAround(lakitu.transform.position, Vector3.up, Vector3.SignedAngle(face, want, Vector3.up));
                lakitu.AddComponent<LakituStartLights>().lamps = lamps;
            }
            else Warnings.Add("Lakitu : lampes du feu introuvables (Lampe_1..3) — relancer Backup_Modeles/lakitu_feu.py dans Blender.");
            var sp = bobHolder.gameObject.AddComponent<Spinner>();
            sp.degreesPerSecond = Vector3.zero;
            sp.bobAmplitude = 0.25f;
            sp.bobFrequency = 0.4f;
        }

        // ------------------------------------------------------------------ décor de course (purement visuel)

        const string KartLabPrefabs = "Assets/KartLab/Prefabs/";

        static GameObject KLPrefab(string name)
        {
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(KartLabPrefabs + name + ".prefab");
            if (go == null) Warnings.Add($"Prefab KartLab « {name} » introuvable (dossier Assets/KartLab manquant ?).");
            return go;
        }

        /// <summary>Rangées de boîtes à objets KartLab (aléatoires) en travers de la route.</summary>
        static void BuildItemBoxes(PathData path, Transform root)
        {
            var parent = new GameObject("Boites_Objets").transform;
            parent.SetParent(root, false);
            string[] kinds = { "MushroomBox", "StarBox", "GreenShellBox", "BananaBox", "IceFlowerBox" };
            int k = 0;
            foreach (var spot in ItemBoxSpots())
            {
                int i = path.Nearest(spot);
                Vector3 f = path.fwd[i]; f.y = 0; f.Normalize();
                Vector3 c = CenterOnModuleRoad(path.p[i], path.right[i]);
                foreach (float off in new[] { -4.5f, -1.5f, 1.5f, 4.5f })
                {
                    var prefab = KLPrefab(kinds[k % kinds.Length]);
                    if (prefab == null) return;
                    var box = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                    box.name = $"BoiteObjet_{k:00}";
                    box.transform.SetPositionAndRotation(c + path.right[i] * off + Vector3.up * (RoadLift + 0.1f), Quaternion.LookRotation(f));
                    var zone = box.GetComponent<TrackZone>();
                    if (zone != null) zone.fixedItem = ItemKind.None;   // None = objet aléatoire
                    k++;
                }
            }
        }

        /// <summary>Bandes d'accélération KartLab (BoostStrip) alignées sur la route.</summary>
        static void BuildBoostPads(PathData path, Transform root)
        {
            var parent = new GameObject("Boosts").transform;
            parent.SetParent(root, false);
            int k = 0;
            foreach (var spot in BoostSpots())
            {
                int i = path.Nearest(spot);
                if (InModuleCrop(spot, 0f))
                {
                    // pile sur la plaque de boost d'origine de City Slicker (on garde son visuel)
                    Vector3 q = spot;
                    if (ModuleSurface(spot, spot.y, 2f, out float y)) q.y = y;
                    BoostAt(q, path.fwd[i], parent, $"Boost_{k++}", true);
                }
                else BoostAt(path.p[i], path.fwd[i], parent, $"Boost_{k++}", true);
            }
        }

        /// <summary>Ligne centrale utilisée par KartRecovery (retour automatique sur la piste).</summary>
        static TrackRoute BuildTrackRoute(PathData path, Transform root)
        {
            var go = new GameObject("TrackRoute_KartLab");
            go.transform.SetParent(root, false);
            var route = go.AddComponent<TrackRoute>();
            var pts = new List<Vector3>();
            for (int i = 0; i < path.Count; i += 2) pts.Add(path.p[i] + Vector3.up * (RoadLift + 0.03f));
            if ((path.Count - 1) % 2 != 0) pts.Add(path.p[path.Count - 1] + Vector3.up * (RoadLift + 0.03f));
            route.points = pts.ToArray();
            route.closed = false;
            route.roadHalfWidth = RoadHalf + CurbW;
            return route;
        }

        /// <summary>Checkpoints KartLab (points de réapparition) tous les ~120 m.</summary>
        static void BuildKartLabCheckpoints(PathData path, Transform root)
        {
            var parent = new GameObject("Checkpoints_KartLab").transform;
            parent.SetParent(root, false);
            var prefab = KLPrefab("Checkpoint");
            if (prefab == null) return;
            int k = 0;
            for (int i = 80; i < path.Count - 20; i += 120)
            {
                for (int t = 0; t < 30 && NearOtherPart(path, i); t++) i += 4;
                var cp = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                cp.name = $"Checkpoint_{k++:00}";
                Vector3 f = path.fwd[i]; f.y = 0; f.Normalize();
                cp.transform.SetPositionAndRotation(path.p[i] + Vector3.up * (RoadLift + 0.12f), Quaternion.LookRotation(f));
                cp.transform.localScale = new Vector3(1.75f, 1f, 1f);   // déclencheur sur toute la largeur (12 m + vibreurs)
            }
        }

        /// <summary>Kart joueur KartLab (cockpit VR, objets, klaxon…) sur la pole position, dans le hall.</summary>
        static void PlaceKartLabKart(TrackRoute route)
        {
            var prefab = BuildKartVariant() ?? KLPrefab("PlayerKart");
            if (prefab == null) return;
            // la caméra du kart remplace la caméra de la scène
            var oldCam = GameObject.Find("Main Camera");
            if (oldCam != null) Object.DestroyImmediate(oldCam);
            var kart = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            kart.name = "Player Kart";
            Vector3 pos = Hall(DoorLx - 2.5f, 8f, RoadLift + 0.02f);
            kart.transform.SetPositionAndRotation(pos, Quaternion.Euler(0f, hallYaw, 0f));
            foreach (var cam in kart.GetComponentsInChildren<Camera>(true))
            {
                cam.farClipPlane = 3000f;
                cam.gameObject.tag = "MainCamera";
            }
            var rec = kart.GetComponent<KartRecovery>();
            if (rec != null)
            {
                rec.route = route;
                rec.offTrackDelay = 2.5f;   // le saut sous la Grande Arche dure plus d'1 s en l'air
            }
            var motor = kart.GetComponent<KartMotor>();
            if (motor != null) { motor.groundMask = 1; motor.fallResetY = -30f; }   // couche Default ; le parc du quartier est à -8 m
            Selection.activeGameObject = kart;
        }

        /// <summary>Couche 8 « Kart » et conversion des matériaux KartLab (Built-in Standard) vers URP Lit.</summary>
        public static void EnsureKartLabSetup()
        {
            var tagManager = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset");
            if (tagManager != null && tagManager.Length > 0)
            {
                var so = new SerializedObject(tagManager[0]);
                var layers = so.FindProperty("layers");
                if (layers != null && layers.arraySize > 8 && string.IsNullOrEmpty(layers.GetArrayElementAtIndex(8).stringValue))
                {
                    layers.GetArrayElementAtIndex(8).stringValue = "Kart";
                    so.ApplyModifiedProperties();
                }
            }

            var urpLit = Shader.Find("Universal Render Pipeline/Lit");
            if (urpLit == null) return;
            int converted = 0;
            foreach (string guid in AssetDatabase.FindAssets("t:Material", new[] { "Assets/KartLab", "Assets/ThirdParty", "Assets/BEDRILL", "Assets/FastMesh" }))
            {
                var mat = AssetDatabase.LoadAssetAtPath<Material>(AssetDatabase.GUIDToAssetPath(guid));
                if (mat == null || mat.shader == null) continue;
                string sn = mat.shader.name;
                if (sn != "Standard" && sn != "Standard (Specular setup)" && !sn.StartsWith("Legacy Shaders/") && !sn.StartsWith("Unlit/") && !sn.StartsWith("Hidden/InternalErrorShader")) continue;
                Color col = mat.HasProperty("_Color") ? mat.GetColor("_Color") : Color.white;
                Texture tex = mat.HasProperty("_MainTex") ? mat.GetTexture("_MainTex") : null;
                Vector2 ts = mat.HasProperty("_MainTex") ? mat.GetTextureScale("_MainTex") : Vector2.one;
                Vector2 to = mat.HasProperty("_MainTex") ? mat.GetTextureOffset("_MainTex") : Vector2.zero;
                float gloss = mat.HasProperty("_Glossiness") ? mat.GetFloat("_Glossiness") : 0.3f;
                float metal = mat.HasProperty("_Metallic") ? mat.GetFloat("_Metallic") : 0f;
                Texture bump = mat.HasProperty("_BumpMap") ? mat.GetTexture("_BumpMap") : null;
                bool emissive = mat.IsKeywordEnabled("_EMISSION");
                Color emis = mat.HasProperty("_EmissionColor") ? mat.GetColor("_EmissionColor") : Color.black;
                Texture emisTex = mat.HasProperty("_EmissionMap") ? mat.GetTexture("_EmissionMap") : null;
                float mode = mat.HasProperty("_Mode") ? mat.GetFloat("_Mode") : 0f;
                float cutoff = mat.HasProperty("_Cutoff") ? mat.GetFloat("_Cutoff") : 0.5f;

                mat.shader = urpLit;
                mat.SetColor("_BaseColor", col);
                mat.SetTexture("_BaseMap", tex);
                mat.SetTextureScale("_BaseMap", ts);
                mat.SetTextureOffset("_BaseMap", to);
                mat.SetFloat("_Smoothness", gloss);
                mat.SetFloat("_Metallic", metal);
                if (bump != null) { mat.SetTexture("_BumpMap", bump); mat.EnableKeyword("_NORMALMAP"); }
                if (emissive) { mat.EnableKeyword("_EMISSION"); mat.SetColor("_EmissionColor", emis); if (emisTex) mat.SetTexture("_EmissionMap", emisTex); }
                if (mode >= 2f)
                {
                    mat.SetFloat("_Surface", 1f);
                    mat.SetFloat("_Blend", 0f);
                    mat.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
                    mat.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
                    mat.SetFloat("_ZWrite", 0f);
                    mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                    mat.renderQueue = (int)RenderQueue.Transparent;
                }
                else if (mode >= 1f)
                {
                    mat.SetFloat("_AlphaClip", 1f);
                    mat.SetFloat("_Cutoff", cutoff);
                    mat.EnableKeyword("_ALPHATEST_ON");
                    mat.renderQueue = (int)RenderQueue.AlphaTest;
                }
                EditorUtility.SetDirty(mat);
                converted++;
            }
            if (converted > 0) { AssetDatabase.SaveAssets(); Debug.Log($"[DeVinci Kart] {converted} matériaux KartLab convertis en URP Lit."); }
        }

        static void BuildFinish(PathData path, int fi, Transform root)
        {
            var parent = new GameObject("Arrivee_CampusDuParc").transform;
            parent.SetParent(root, false);
            Vector3 p = path.p[fi], r = path.right[fi];
            Vector3 f = path.fwd[fi]; f.y = 0; f.Normalize();

            var line = new MB();
            Vector3 up = Vector3.up * (RoadLift + 0.015f);
            line.Quad(p - r * RoadHalf + up, p - r * RoadHalf + f * 1.5f + up, p + r * RoadHalf + f * 1.5f + up, p + r * RoadHalf + up,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(8, 1), new Vector2(8, 0), Vector3.up);
            MeshGO("Ligne_Arrivee", parent, line, "Checker", false);

            var gantry = new GameObject("Portique_Arrivee").transform;
            gantry.SetParent(parent, false);
            gantry.SetPositionAndRotation(p, Quaternion.LookRotation(f));
            float w = RoadHalf + CurbW + BarrierT + 1f;
            Text3D(gantry, "ARRIVÉE · CAMPUS DU PARC", new Vector3(0, 8.3f, -0.1f), Quaternion.identity, 1.2f, Color.white);

            var podium = new GameObject("Podium").transform;
            podium.SetParent(parent, false);
            podium.SetPositionAndRotation(p + r * (w + 9f) + f * 30f, Quaternion.LookRotation(r));
            var steps = new[] { (0f, 1.5f, "1", "TrophePremier"), (-2.4f, 1.0f, "2", "Trophe2eme"), (2.4f, 0.7f, "3", "Trophe3eme") };
            foreach (var st in steps)
            {
                float x = st.Item1, h = st.Item2;
                string num = st.Item3, model = st.Item4;
                Prim(PrimitiveType.Cube, "Marche_" + num, podium, new Vector3(x, h / 2f, 0), new Vector3(2.3f, h, 2f), "White", null, true);
                Text3D(podium, num, new Vector3(x, h / 2f, -1.02f), Quaternion.identity, 0.8f, new Color(0.1f, 0.2f, 0.5f));
                var tr = PlaceFit(model, podium, podium.TransformPoint(new Vector3(x, h, 0)), podium.eulerAngles.y + 180f, 1.3f);
                tr.AddComponent<Spinner>().degreesPerSecond = new Vector3(0, 30, 0);
            }
        }

        static void AddMeshColliders(GameObject go)
        {
            foreach (var mf in go.GetComponentsInChildren<MeshFilter>())
                if (mf.sharedMesh != null && mf.GetComponent<Collider>() == null)
                    mf.gameObject.AddComponent<MeshCollider>().sharedMesh = mf.sharedMesh;
        }

        /// <summary>Tremplin(s) « Saut » posés sur la route et colonnade « PiliersParc » en travers de la route.</summary>
        static void BuildJumpsAndPillars(PathData path, Transform root)
        {
            var parent = new GameObject("Piliers_Parc").transform;
            parent.SetParent(root, false);
            {
                int i = path.Nearest(PillarsSpot);
                Vector3 ff = path.fwd[i]; ff.y = 0; ff.Normalize();
                var pil = PlaceScaled("PiliersParc", parent, path.p[i] + Vector3.up * RoadLift, Mathf.Atan2(ff.x, ff.z) * Mathf.Rad2Deg, PillarsScale);
                pil.name = "Piliers_Parc (au milieu de la route)";
                AddMeshColliders(pil);
            }
        }

        /// <summary>Pizzeria à droite et kebab à gauche de la ligne d'arrivée, vitrine (+Z du modèle) tournée vers la route.</summary>
        static void BuildShops(PathData path, int fi, Transform root)
        {
            var parent = new GameObject("Commerces_Arrivee").transform;
            parent.SetParent(root, false);
            Vector3 p = path.p[fi], r = path.right[fi];
            float d = RoadHalf + CurbW + BarrierT + 1f + 13f;
            Vector3 toRoadFromRight = -r, toRoadFromLeft = r;
            PlaceFit("pizzeria", parent, p + r * d, Mathf.Atan2(toRoadFromRight.x, toRoadFromRight.z) * Mathf.Rad2Deg, ShopHeight).name = "Pizzeria (droite)";
            PlaceFit("kebabEsilv", parent, p - r * d, Mathf.Atan2(toRoadFromLeft.x, toRoadFromLeft.z) * Mathf.Rad2Deg, ShopHeight).name = "Kebab ESILV (gauche)";
            var walk = new MB();
            foreach (float side in new[] { -1f, 1f })
            {
                Vector3 c = p + r * side * d;
                walk.Box(c - new Vector3(13f, 0.1f, 13f), c + new Vector3(13f, 0.035f, 13f), 6f, false);
            }
            MeshGO("Parvis_Commerces", parent, walk, "Concrete", false);
        }

        /// <summary>Panneaux publicitaires le long de la piste.</summary>
        static void BuildBillboards(PathData path, Transform root)
        {
            var parent = new GameObject("Panneaux_Pub").transform;
            parent.SetParent(root, false);
            string[] texts = { "ESILV", "PÔLE LÉONARD\nDE VINCI", "DEVINCI KART", "IIM", "EMLV", "LA DÉFENSE", "NANTERRE", "ESILV" };
            Color[] cols = { new Color(0.05f, 0.3f, 0.6f), new Color(0.75f, 0.1f, 0.15f), new Color(0.1f, 0.1f, 0.12f), new Color(0.95f, 0.55f, 0.05f) };
            int k = 0;
            for (int i = 110; i < path.Count - 60; i += 125)
            {
                if (ZoneAt(path.p[i]) != Zone.Normal || path.p[i].y > 0.2f) { i -= 105; continue; }
                float side = k % 2 == 0 ? 1f : -1f;
                Vector3 outDir = path.right[i] * side; outDir.y = 0; outDir.Normalize();
                var bb = new GameObject("Panneau_" + k).transform;
                bb.SetParent(parent, false);
                Vector3 pos = path.p[i] + outDir * (RoadHalf + CurbW + BarrierT + 4f); pos.y = 0f;
                bb.SetPositionAndRotation(pos, Quaternion.LookRotation(outDir));
                Prim(PrimitiveType.Cube, "Poteau_G", bb, new Vector3(-3.2f, 2.5f, 0.2f), new Vector3(0.3f, 5f, 0.3f), "DarkMetal");
                Prim(PrimitiveType.Cube, "Poteau_D", bb, new Vector3(3.2f, 2.5f, 0.2f), new Vector3(0.3f, 5f, 0.3f), "DarkMetal");
                var board = Prim(PrimitiveType.Cube, "Panneau", bb, new Vector3(0, 4.2f, 0), new Vector3(8f, 3f, 0.25f), "White");
                var mpb = new MaterialPropertyBlock();
                mpb.SetColor("_BaseColor", cols[k % cols.Length]);
                board.GetComponent<Renderer>().SetPropertyBlock(mpb);
                Text3D(bb, texts[k % texts.Length].Replace("\\n", "\n"), new Vector3(0, 4.2f, -0.15f), Quaternion.identity, 1.1f, Color.white);
                k++;
            }
        }

        // ------------------------------------------------------------------ décor

        static void BuildDecor(PathData path, Transform root)
        {
            var parent = new GameObject("Decor").transform;
            parent.SetParent(root, false);
            var rng = new System.Random(42);
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);

            Vector3 finishP = path.p[Mathf.Max(0, path.Count - 16)];
            var coarse = new List<Vector2>();
            for (int i = 0; i < path.Count; i += 4) coarse.Add(new Vector2(path.p[i].x, path.p[i].z));
            System.Func<float, float, float, bool> NearRoad = (x, z, d) =>
            {
                float d2 = d * d;
                foreach (var c in coarse) if ((c.x - x) * (c.x - x) + (c.y - z) * (c.y - z) < d2) return true;
                return false;
            };
            System.Func<float, float, bool> Blocked = (x, z) =>
            {
                var p = new Vector3(x, 0, z);
                Vector3 h = hallW2L.MultiplyPoint3x4(p);
                if (Mathf.Abs(h.x) < 70f && Mathf.Abs(h.z) < 60f) return true;                  // ancien campus
                if (new Vector2(x - NewCampusPos.x, z - NewCampusPos.z).magnitude < 110f) return true; // nouveau campus
                if (new Vector2(x, z).magnitude < 95f) return true;                             // Grande Arche
                Vector3 b = bridgeW2L.MultiplyPoint3x4(p);
                if (Mathf.Abs(b.x) < TrenchHalfLen + 10f && Mathf.Abs(b.z) < TrenchHalfW + 6f) return true; // tranchée
                if (ModuleBlocked(p, 10f) || ViaductBlocked(p)) return true;                    // quartier City Slicker, viaduc
                if (new Vector2(x - finishP.x, z - finishP.z).magnitude < 50f) return true;    // pizzeria / kebab / podium
                if (new Vector2(x - PoucePos.x, z - PoucePos.z).magnitude < 20f) return true;
                Vector3 lk = Quaternion.Euler(0, -20f, 0) * (p - (ParkCenter + new Vector3(-5f, 0, 15f)));
                if ((lk.x * lk.x) / (78f * 78f) + (lk.z * lk.z) / (43f * 43f) < 1f) return true; // lac
                if (IsReserved(x, z, 4f)) return true;                                          // tribunes, paddock, etc.
                return NearRoad(x, z, 15f);
            };

            // Arbres : beaucoup dans le parc André-Malraux, quelques-uns ailleurs
            var trees = new Dictionary<string, List<CombineInstance>>();
            string[] leaves = { "Leaves1", "Leaves2", "Leaves3" };
            System.Action<Vector3, float, float> Tree = (bp, h, s) =>
            {
                AddCombined(trees, "Bark", cylMesh, Matrix4x4.TRS(bp + Vector3.up * h * 0.5f, Quaternion.identity, new Vector3(0.45f, h * 0.5f, 0.45f)));
                string lm = leaves[rng.Next(leaves.Length)];
                AddCombined(trees, lm, sphereMesh, Matrix4x4.TRS(bp + Vector3.up * (h + s * 0.25f), Quaternion.Euler(0, R(0, 360), 0), new Vector3(s, s * 0.85f, s)));
                AddCombined(trees, lm, sphereMesh, Matrix4x4.TRS(bp + new Vector3(R(-1, 1), h + s * 0.6f, R(-1, 1)), Quaternion.identity, Vector3.one * s * 0.65f));
            };
            int placed = 0;
            for (int tries = 0; tries < 8000 && placed < 520; tries++)
            {
                float ang = R(0, Mathf.PI * 2f), rad = ParkRadius * Mathf.Sqrt(R(0, 1));
                float x = ParkCenter.x + Mathf.Cos(ang) * rad, z = ParkCenter.z + Mathf.Sin(ang) * rad;
                if (Blocked(x, z)) continue;
                Tree(new Vector3(x, 0, z), R(3f, 6f), R(3.5f, 7f));
                placed++;
            }
            placed = 0;
            for (int tries = 0; tries < 8000 && placed < 260; tries++)
            {
                // alignements d'arbres le long de l'axe (Terrasses de Nanterre)
                float s = R(-700f, -170f), side = rng.Next(2) == 0 ? -1f : 1f;
                Vector3 p = A(s) + new Vector3(-AxisEast.z, 0, AxisEast.x) * side * R(18f, 260f);
                if (Blocked(p.x, p.z)) continue;
                Tree(p, R(3f, 5f), R(3f, 5f));
                placed++;
            }
            FlushCombined(trees, "Arbres", parent);

            BuildVegetation(path, parent, rng, Blocked);
            BuildSkyline(parent, rng, NearRoad, Blocked);
        }

        static void SetupCameraAndLighting()
        {

            var sunGO = GameObject.Find("Directional Light");
            var sun = sunGO ? sunGO.GetComponent<Light>() : null;
            if (sun != null)
            {
                sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);   // soleil un peu plus bas : ombres plus longues et plus lisibles
                sun.intensity = 1.3f;
                sun.shadows = LightShadows.Soft;
                sun.shadowStrength = 0.85f;
                sun.color = new Color(1f, 0.95f, 0.87f);
            }
            // lumière ambiante en dégradé : dessous des ponts et faces à l'ombre moins « plats », sans les déboucher
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = new Color(0.62f, 0.72f, 0.86f);
            RenderSettings.ambientEquatorColor = new Color(0.5f, 0.54f, 0.52f);
            RenderSettings.ambientGroundColor = new Color(0.26f, 0.27f, 0.22f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 600f;
            RenderSettings.fogEndDistance = 3200f;
            RenderSettings.fogColor = new Color(0.72f, 0.8f, 0.9f);
        }

        static void AddSceneToBuild(string path)
        {
            var scenes = EditorBuildSettings.scenes.ToList();
            if (scenes.All(s => s.path != path)) scenes.Insert(0, new EditorBuildSettingsScene(path, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ textures & matériaux

        static int Mod(int a, int m) { int r = a % m; return r < 0 ? r + m : r; }

        static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 1442695041);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777215f;
            }
        }

        static float VNoise(float x, float y, int period, int seed)
        {
            int xi = Mathf.FloorToInt(x), yi = Mathf.FloorToInt(y);
            float fx = x - xi, fy = y - yi;
            fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
            int x0 = Mod(xi, period), x1 = Mod(xi + 1, period), y0 = Mod(yi, period), y1 = Mod(yi + 1, period);
            return Mathf.Lerp(Mathf.Lerp(Hash(x0, y0, seed), Hash(x1, y0, seed), fx),
                              Mathf.Lerp(Hash(x0, y1, seed), Hash(x1, y1, seed), fx), fy);
        }

        static float Fbm(float u, float v, int basePeriod, int oct, int seed)
        {
            float sum = 0, amp = 1, norm = 0; int p = basePeriod;
            for (int o = 0; o < oct; o++) { sum += VNoise(u * p, v * p, p, seed + o * 17) * amp; norm += amp; amp *= 0.5f; p *= 2; }
            return sum / norm;
        }

        static Texture2D Tex(string name, int w, int h, System.Func<float, float, int, int, Color> f)
        {
            string path = $"{GenFolder}/Textures/{name}.png";
            var t = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                px[y * w + x] = f((x + 0.5f) / w, (y + 0.5f) / h, x, y);
            t.SetPixels(px);
            t.Apply();
            File.WriteAllBytes(path, t.EncodeToPNG());
            Object.DestroyImmediate(t);
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var imp = (TextureImporter)AssetImporter.GetAtPath(path);
            imp.wrapMode = TextureWrapMode.Repeat;
            imp.mipmapEnabled = true;
            imp.anisoLevel = 8;
            imp.filterMode = FilterMode.Trilinear;
            imp.SaveAndReimport();
            return AssetDatabase.LoadAssetAtPath<Texture2D>(path);
        }

        static Material Mat(string name, Color c, Texture2D tex = null, float smooth = 0.3f, float metal = 0f, Color? emission = null, Texture2D emisTex = null)
        {
            string path = $"{GenFolder}/Materials/{name}.mat";
            var sh = Shader.Find("Universal Render Pipeline/Lit");
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(sh); AssetDatabase.CreateAsset(m, path); }
            else m.shader = sh;
            m.SetColor("_BaseColor", c);
            m.SetTexture("_BaseMap", tex);
            m.SetFloat("_Smoothness", smooth);
            m.SetFloat("_Metallic", metal);
            if (emission.HasValue)
            {
                m.EnableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", emission.Value);
                if (emisTex) m.SetTexture("_EmissionMap", emisTex);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.DisableKeyword("_EMISSION");
                m.SetColor("_EmissionColor", Color.black);
            }
            m.enableInstancing = true;
            EditorUtility.SetDirty(m);
            M[name] = m;
            return m;
        }

        static void CreateMaterials()
        {
            var asphalt = Tex("asphalt", 512, 512, (u, v, x, y) =>
            {
                float n = Fbm(u, v, 8, 5, 1);
                float speck = Hash(x, y, 7) > 0.985f ? 0.12f : 0f;
                float g = 0.2f + n * 0.13f + speck;
                Color c = new Color(g, g, g * 1.04f);
                bool edge = (u > 0.035f && u < 0.06f) || (u > 0.94f && u < 0.965f);
                bool dash = Mathf.Abs(u - 0.5f) < 0.009f && v < 0.5f;
                if (edge || dash) c = Color.Lerp(c, new Color(0.92f, 0.92f, 0.9f), 0.9f);
                return c;
            });
            var curb = Tex("curb", 64, 64, (u, v, x, y) => v < 0.5f ? new Color(0.85f, 0.08f, 0.08f) : new Color(0.95f, 0.95f, 0.95f));
            var barrier = Tex("barrier", 128, 64, (u, v, x, y) =>
            {
                if (v > 0.85f) return new Color(1f, 0.82f, 0.1f);
                return u < 0.5f ? new Color(0.1f, 0.35f, 0.95f) : new Color(0.96f, 0.96f, 0.96f);
            });
            var grass = Tex("grass", 512, 512, (u, v, x, y) =>
            {
                float n = Fbm(u, v, 4, 5, 3), b = Hash(x, y, 11);
                return new Color(0.2f + n * 0.12f, 0.45f + n * 0.2f + b * 0.06f, 0.12f + n * 0.06f);
            });
            var stone = Tex("stone", 512, 512, (u, v, x, y) =>
            {
                int row = y / 64;
                int bx = (x + (row % 2) * 64) / 128;
                bool mortar = y % 64 < 4 || (x + (row % 2) * 64) % 128 < 4;
                float tint = Hash(bx, row, 5) * 0.12f;
                float n = Fbm(u, v, 8, 4, 9) * 0.15f;
                if (mortar) return new Color(0.62f, 0.6f, 0.56f);
                return new Color(0.72f + tint + n, 0.66f + tint + n, 0.55f + tint * 0.8f + n);
            });
            var tiles = Tex("tiles", 256, 256, (u, v, x, y) =>
            {
                bool line = x % 64 < 2 || y % 64 < 2;
                float n = Fbm(u, v, 4, 3, 13) * 0.06f;
                return line ? new Color(0.55f, 0.56f, 0.6f) : new Color(0.84f + n, 0.85f + n, 0.88f + n);
            });
            var concrete = Tex("concrete", 512, 512, (u, v, x, y) =>
            {
                bool joint = x % 256 < 3 || y % 256 < 3;
                float n = Fbm(u, v, 8, 5, 21);
                float g = 0.58f + n * 0.12f;
                return joint ? new Color(0.45f, 0.45f, 0.45f) : new Color(g, g, g * 1.02f);
            });
            var checker = Tex("checker", 64, 64, (u, v, x, y) => ((x / 32) + (y / 32)) % 2 == 0 ? Color.white : new Color(0.05f, 0.05f, 0.05f));
            var water = Tex("water", 256, 256, (u, v, x, y) =>
            {
                float n = Fbm(u, v, 4, 5, 31);
                float rip = Mathf.Pow(Mathf.Abs(Mathf.Sin((v * 8f + n * 3f) * Mathf.PI)), 12f) * 0.25f;
                return new Color(0.1f + rip, 0.32f + n * 0.12f + rip, 0.48f + n * 0.15f + rip);
            });
            var rainbow = Tex("rainbow", 256, 256, (u, v, x, y) =>
            {
                Color c = Color.HSVToRGB(Mathf.Repeat(u + v * 0.5f, 1f), 0.7f, 1f);
                bool border = u < 0.06f || u > 0.94f || v < 0.06f || v > 0.94f;
                return border ? Color.white : c;
            });
            var boost = Tex("boost", 128, 256, (u, v, x, y) =>
            {
                float s = Mathf.Repeat(v * 3f - Mathf.Abs(u - 0.5f) * 1.6f, 1f);
                bool border = u < 0.05f || u > 0.95f;
                return border ? new Color(0.2f, 0.2f, 0.2f) : (s < 0.4f ? new Color(1f, 0.92f, 0.2f) : new Color(1f, 0.45f, 0.05f));
            });
            var chevron = Tex("chevron", 256, 128, (u, v, x, y) =>
            {
                float s = Mathf.Repeat(u * 3f - Mathf.Abs(v - 0.5f) * 1.8f, 1f);
                bool border = v < 0.06f || v > 0.94f;
                return border ? Color.white : (s > 0.55f && s < 0.85f ? Color.white : new Color(0.85f, 0.08f, 0.08f));
            });
            var hallWall = Tex("hallwall", 256, 256, (u, v, x, y) =>
            {
                float n = Fbm(u, v, 4, 3, 41) * 0.05f;
                if (v > 0.30f && v < 0.36f) return new Color(0.05f, 0.42f, 0.75f);
                if (v > 0.37f && v < 0.39f) return new Color(0.98f, 0.73f, 0.1f);
                return new Color(0.9f + n, 0.9f + n, 0.92f + n);
            });
            var glass = Tex("glass", 256, 256, (u, v, x, y) =>
            {
                bool frame = x % 32 < 3 || y % 42 < 4;
                float lit = Hash(x / 32, y / 42, 51) > 0.8f ? 0.15f : 0f;
                return frame ? new Color(0.55f, 0.58f, 0.62f) : new Color(0.25f + lit, 0.38f + lit, 0.5f + lit);
            });

            Mat("Asphalt", Color.white, asphalt, 0.25f);
            Mat("Curb", Color.white, curb, 0.35f);
            Mat("Barrier", Color.white, barrier, 0.45f);
            Mat("Grass", Color.white, grass, 0.05f);
            Mat("Stone", Color.white, stone, 0.1f);
            Mat("Tiles", Color.white, tiles, 0.7f);
            Mat("Concrete", Color.white, concrete, 0.15f);
            Mat("Checker", Color.white, checker, 0.2f);
            Mat("Water", new Color(0.8f, 0.9f, 1f), water, 0.93f, 0.1f);
            Mat("Rainbow", Color.white, rainbow, 0.8f, 0f, new Color(0.6f, 0.6f, 0.6f), rainbow);
            Mat("Boost", Color.white, boost, 0.5f, 0f, new Color(0.8f, 0.5f, 0.1f), boost);
            Mat("Chevron", Color.white, chevron, 0.3f);
            Mat("HallWall", Color.white, hallWall, 0.3f);
            Mat("Glass", Color.white, glass, 0.88f, 0.5f);
            Mat("Building", new Color(0.93f, 0.9f, 0.85f), glass, 0.3f);
            Mat("Brand", new Color(0.05f, 0.3f, 0.6f), null, 0.5f, 0.3f);
            Mat("DarkMetal", new Color(0.18f, 0.19f, 0.21f), null, 0.6f, 0.8f);
            Mat("White", new Color(0.95f, 0.95f, 0.95f), null, 0.4f);
            Mat("LampHead", new Color(1f, 0.95f, 0.8f), null, 0.5f, 0f, new Color(2f, 1.8f, 1.3f));
            Mat("LightPanel", Color.white, null, 0.5f, 0f, new Color(3f, 3f, 2.8f));
            Mat("StartLight", new Color(0.1f, 0.1f, 0.1f), null, 0.8f, 0f, Color.black);
            Mat("Bark", new Color(0.36f, 0.25f, 0.16f), null, 0.1f);
            Mat("Leaves1", new Color(0.2f, 0.5f, 0.18f), null, 0.1f);
            Mat("Leaves2", new Color(0.28f, 0.58f, 0.2f), null, 0.1f);
            Mat("Leaves3", new Color(0.16f, 0.42f, 0.2f), null, 0.1f);
            Mat("KartBody", new Color(0.85f, 0.1f, 0.12f), null, 0.75f, 0.3f);
            Mat("Tire", new Color(0.06f, 0.06f, 0.06f), null, 0.2f);
            Mat("Driver", new Color(0.1f, 0.35f, 0.85f), null, 0.3f);
            Mat("Missing", new Color(1f, 0f, 1f), null, 0.2f);
            AssetDatabase.SaveAssets();
        }
    }
}
