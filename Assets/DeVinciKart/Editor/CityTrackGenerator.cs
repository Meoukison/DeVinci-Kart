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
    /// Piste 2 « DeVinci City » : circuit de ville fermé, 3 tours, sens horaire (~1,1 km par tour).
    /// On longe l'ancien campus (ligne de départ / arrivée), on passe devant le Pouce, on voit la Grande Arche au bout de la rue Est,
    /// on longe le nouveau campus et on fait le tour de l'Ange (rond-point).
    /// Menu : DeVinci Kart > Générer la piste 2 : DeVinci City (3 tours). La piste 1 n'est pas modifiée.
    /// </summary>
    public static partial class CampusTrackGenerator
    {
        const string CityScenePath = "Assets/Scenes/Piste_CitySlicker.unity";
        const string MenuScenePath = "Assets/Scenes/Menu_Choix_Circuit.unity";
        const int CityLaps = 3;

        // Monuments (x, z) ; compressés pour tenir dans un tour de ~1,1 km
        static readonly Vector3 CityOldCampus = new Vector3(0f, 0f, 52f);        // façade marquise tournée vers la ligne droite
        static readonly Vector3 CityNewCampus = new Vector3(30f, 0f, -325f);
        static readonly Vector3 CityArch = new Vector3(185f, 0f, -335f);         // au bout de la rue Est
        static readonly Vector3 CityPouce = new Vector3(140f, 0f, -42f);
        static readonly Vector3 CityAngel = new Vector3(-120f, 0f, -215f);       // rond-point
        const float CityArchScale = 75f, CityNewCampusScale = 100f;

        static Vector3[] CityControlPoints()
        {
            Vector3 S = CityAngel; const float r = 32f;
            var xz = new List<Vector2>
            {
                new Vector2(-60, 0), new Vector2(40, 0), new Vector2(130, 0),                         // ligne droite des stands (ancien campus)
                new Vector2(166, -12), new Vector2(180, -45), new Vector2(180, -100),                 // virage 1 (Pouce à l'intérieur), rue Est + tremplin
                new Vector2(168, -130), new Vector2(148, -150), new Vector2(146, -175),               // décroché
                new Vector2(160, -198), new Vector2(174, -218), new Vector2(172, -240), new Vector2(152, -256), // face à la Grande Arche
                new Vector2(125, -258), new Vector2(95, -250), new Vector2(70, -232), new Vector2(40, -232),   // chicane devant le nouveau campus
                new Vector2(15, -250), new Vector2(-15, -258), new Vector2(-55, -256), new Vector2(-95, -252),
                new Vector2(S.x, S.z - r), new Vector2(S.x - r * .707f, S.z - r * .707f), new Vector2(S.x - r, S.z),   // tour de l'Ange
                new Vector2(S.x - r * .707f, S.z + r * .707f),
                new Vector2(-118, -160), new Vector2(-110, -128), new Vector2(-122, -96), new Vector2(-106, -62),      // S de la rue Ouest
                new Vector2(-100, -34), new Vector2(-84, -10),
            };
            return xz.Select(v => new Vector3(v.x, 0f, v.y)).ToArray();
        }

        /// <summary>Spline fermée (centripète), rééchantillonnée tous les « step » mètres ; le premier point est répété à la fin.</summary>
        static PathData BuildClosedPath(Vector3[] c, float step)
        {
            int n = c.Length;
            var dense = new List<Vector3>();
            for (int i = 0; i < n; i++)
                for (int s = 0; s < 60; s++)
                    dense.Add(CentripetalCR(c[(i - 1 + n) % n], c[i], c[(i + 1) % n], c[(i + 2) % n], s / 60f));
            dense.Add(dense[0]);

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
            // dernier point = premier point (boucle fermée)
            if ((pd.p[pd.p.Count - 1] - pd.p[0]).magnitude < step * 0.5f) { pd.p.RemoveAt(pd.p.Count - 1); pd.dist.RemoveAt(pd.dist.Count - 1); }
            pd.p.Add(pd.p[0]); pd.dist.Add(acc);
            pd.length = acc;
            int m = pd.p.Count - 1;   // nombre de points distincts
            for (int i = 0; i <= m; i++)
            {
                int k = i % m;
                Vector3 f = (pd.p[(k + 1) % m] - pd.p[(k - 1 + m) % m]).normalized;
                Vector3 ff = new Vector3(f.x, 0f, f.z).normalized;
                pd.fwd.Add(f);
                pd.right.Add(Vector3.Cross(Vector3.up, ff).normalized);
            }
            return pd;
        }

        public static void GenerateCity()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Warnings.Clear();
            M.Clear();
            try
            {
                EditorUtility.DisplayProgressBar("DeVinci City", "Préparation…", 0.05f);
                EnsureFolders();
                GrabPrimitiveMeshes();
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                // pas de hall ni de tranchée sur cette piste : on les « éloigne » pour que ZoneAt renvoie toujours Normal
                hallL2W = Matrix4x4.TRS(new Vector3(1e6f, 0f, 1e6f), Quaternion.identity, Vector3.one); hallW2L = hallL2W.inverse;
                bridgeL2W = Matrix4x4.TRS(new Vector3(-1e6f, 0f, 1e6f), Quaternion.identity, Vector3.one); bridgeW2L = bridgeL2W.inverse;
                MeshDir = GenFolder + "/MeshesCity";
                cityMode = true;
                cityBlockers.Clear();

                CreateMaterials();
                CreateExtraMaterials();
                EnsureKartLabSetup();
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(MenuScenePath) == null) BuildMenuSceneInternal();

                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                var root = new GameObject("Piste_DeVinciCity").transform;

                EditorUtility.DisplayProgressBar("DeVinci City", "Tracé du circuit…", 0.15f);
                var path = BuildClosedPath(CityControlPoints(), Step);
                ResetProps(path);
                var trackGO = new GameObject("TrackPath");
                trackGO.transform.SetParent(root, false);
                var tp = trackGO.AddComponent<TrackPath>();
                tp.points = path.p.ToArray(); tp.forwards = path.fwd.ToArray(); tp.length = path.length;
                int finishIdx = path.Nearest(new Vector3(-20f, 0f, 0f));

                // zones interdites au décor : monuments (cercles)
                cityBlockers.Add(new Vector3(CityOldCampus.x, 62f, CityOldCampus.z));
                cityBlockers.Add(new Vector3(CityNewCampus.x, 64f, CityNewCampus.z));
                cityBlockers.Add(new Vector3(CityArch.x, 52f, CityArch.z));
                cityBlockers.Add(new Vector3(CityPouce.x, 13f, CityPouce.z));
                cityBlockers.Add(new Vector3(CityAngel.x, 18f, CityAngel.z));
                // couloirs de vue : rien ne doit cacher la Grande Arche depuis la rue Est, ni le Pouce depuis la ligne droite
                for (float t = 0f; t <= 1f; t += 0.1f)
                {
                    Vector3 q = Vector3.Lerp(new Vector3(180f, 0f, -120f), CityArch, t);
                    cityBlockers.Add(new Vector3(q.x, 34f, q.z));
                    Vector3 q2 = Vector3.Lerp(new Vector3(40f, 0f, -5f), CityPouce, t);
                    cityBlockers.Add(new Vector3(q2.x, 14f, q2.z));
                }
                cityBlockers.Add(new Vector3(CityArch.x, 75f, CityArch.z));

                EditorUtility.DisplayProgressBar("DeVinci City", "Route, vibreurs, barrières…", 0.25f);
                BuildRoad(path, root);
                BuildCityGround(path, root);
                BuildCrosswalks(path, root);

                EditorUtility.DisplayProgressBar("DeVinci City", "Monuments…", 0.35f);
                PlaceCityLandmarks(root);

                EditorUtility.DisplayProgressBar("DeVinci City", "Objets, boosts, tremplin, piliers…", 0.45f);
                BuildCityItems(path, root);
                BuildKickerAt(path, root, path.Nearest(new Vector3(180f, 0f, -58f)), "Tremplin_RueEst");
                PlaceCityPillars(path, path.Nearest(new Vector3(115f, 0f, 0f)), root);
                int shopIdx = path.Nearest(new Vector3(72f, 0f, 0f));
                BuildShops(path, shopIdx, root);
                Vector3 sp = path.p[shopIdx];
                cityBlockers.Add(new Vector3(sp.x + path.right[shopIdx].x * 27f, 16f, sp.z + path.right[shopIdx].z * 27f));
                cityBlockers.Add(new Vector3(sp.x - path.right[shopIdx].x * 27f, 16f, sp.z - path.right[shopIdx].z * 27f));
                BuildCityFinish(path, finishIdx, root);
                BuildBillboards(path, root);

                EditorUtility.DisplayProgressBar("DeVinci City", "Panneaux, arches, tribunes, public…", 0.55f);
                BuildTrackProps(path, finishIdx, root);
                BuildLife(path, finishIdx, root);

                EditorUtility.DisplayProgressBar("DeVinci City", "Immeubles…", 0.7f);
                var decor = Group("Decor_Ville", root);
                var rng = new System.Random(2026);
                BuildCityBlocks(path, decor, rng);
                BuildVegetation(path, decor, rng, (x, z) => LandmarkBlocked(new Vector3(x, 0, z)) || IsReserved(x, z, 3f));
                BuildSkyline(decor, rng,
                    (x, z, d) => RoadDistance(new Vector3(x, 0, z)) < Mathf.Max(d, 130f),   // grandes tours seulement au loin
                    (x, z) => LandmarkBlocked(new Vector3(x, 0, z)) || IsReserved(x, z, 6f));

                SetupCameraAndLighting();

                EditorUtility.DisplayProgressBar("DeVinci City", "Kart, tours, checkpoints…", 0.9f);
                var route = BuildTrackRoute(path, root);
                route.closed = true;
                BuildKartLabCheckpoints(path, root);
                var kart = PlaceCityKart(path, finishIdx, route);
                BuildLapSystem(path, finishIdx, root, kart);

                EditorSceneManager.SaveScene(scene, CityScenePath);
                ConfigureBuildScenes();

                string msg = $"Piste 2 « DeVinci City » générée : {path.length:0} m par tour, {CityLaps} tours.\nScène : {CityScenePath}\n" +
                             "Le menu « Menu_Choix_Circuit » permet de choisir entre les 2 circuits (1 / 2).";
                if (Warnings.Count > 0) msg += "\n\nAttention :\n- " + string.Join("\n- ", Warnings.Distinct());
                EditorUtility.ClearProgressBar();
                EditorUtility.DisplayDialog("DeVinci Kart", msg, "OK");
            }
            finally
            {
                cityMode = false;
                MeshDir = GenFolder + "/Meshes";
                EditorUtility.ClearProgressBar();
            }
        }

        // ------------------------------------------------------------------ sol, passages piétons

        static void BuildCityGround(PathData path, Transform root)
        {
            var parent = Group("Sol_Ville", root);
            var ground = new MB();
            ground.Box(new Vector3(-3000, -4, -3000), new Vector3(3000, 0, 3000), 8f, false);
            var g = MeshGO("Sol", parent, ground, "Grass", false);
            g.AddComponent<BoxCollider>();
            g.AddComponent<GroundSurface>().speedFactor = 0.6f;
            // grande dalle de ville (trottoirs / places) autour du circuit
            var slab = new MB();
            slab.Box(new Vector3(-260, -0.1f, -420), new Vector3(330, 0.03f, 150), 6f, false);
            MeshGO("Dalle_Ville", parent, slab, "Concrete", false);
            // pelouse au centre du circuit
            var lawn = new MB();
            lawn.Box(new Vector3(-50, -0.1f, -205), new Vector3(110, 0.045f, -40), 8f, false);
            MeshGO("Square_Central", parent, lawn, "Grass", false);
        }

        static void BuildCrosswalks(PathData path, Transform root)
        {
            var parent = Group("Passages_Pietons", root);
            var mb = new MB();
            float[] radius = TurnRadius(path, out int[] _);
            int last = -200;
            for (int i = 10; i < path.Count - 10; i++)
            {
                if (i - last < 110 || radius[i] < 120f || radius[i - 6] < 120f || radius[i + 6] < 120f) continue;
                Vector3 f = Flat(path.fwd[i]), r = Flat(path.right[i]);
                Vector3 c = path.p[i] + Vector3.up * (RoadLift + 0.012f);
                for (float x = -RoadHalf + 0.6f; x < RoadHalf - 0.3f; x += 1.2f)
                {
                    Vector3 a = c + r * x - f * 1.6f, b = c + r * (x + 0.6f) - f * 1.6f;
                    mb.Quad(a, a + f * 3.2f, b + f * 3.2f, b, Vector2.zero, Vector2.up, Vector2.one, Vector2.right, Vector3.up);
                }
                last = i;
            }
            MeshGO("Passages_Pietons", parent, mb, "White", false);
        }

        // ------------------------------------------------------------------ monuments

        static void PlaceCityLandmarks(Transform root)
        {
            var parent = Group("Monuments", root);
            // ancien campus : sa façade « marquise » (repère local +Z) regarde la ligne droite des stands (-Z monde)
            PlaceScaled("AncienCampus", parent, CityOldCampus + Vector3.up * OldCampusDrop, 180f, OldCampusScale).name = "Ancien Campus (Pôle Léonard de Vinci)";
            PlaceScaled("ESILV_NouveauCampus3D", parent, CityNewCampus + Vector3.up * -2.5f, 180f, CityNewCampusScale).name = "Nouveau campus (Campus du Parc)";
            PlaceScaled("ArcheLaDefenseModel", parent, CityArch, 0f, CityArchScale).name = "Grande Arche";

            // Pouce géant sur sa place
            Prim(PrimitiveType.Cube, "Pouce_Socle", parent, CityPouce + Vector3.up * 0.75f, new Vector3(16f, 1.5f, 16f), "Concrete");
            PlaceFit("LePouceModele3d", parent, CityPouce + Vector3.up * 1.5f, 225f, PouceHeight).name = "Le Pouce (géant)";

            // l'Ange au centre du rond-point
            Vector3 S = CityAngel;
            Prim(PrimitiveType.Cylinder, "RondPoint_Bordure", parent, S + Vector3.up * 0.1f, new Vector3(34f, 0.2f, 34f), "Stone");
            Prim(PrimitiveType.Cylinder, "RondPoint_Pelouse", parent, S + Vector3.up * 0.25f, new Vector3(32f, 0.1f, 32f), "Grass");
            Prim(PrimitiveType.Cylinder, "Piedestal", parent, S + Vector3.up * 2.3f, new Vector3(7f, 2f, 7f), "Stone");
            Prim(PrimitiveType.Cylinder, "Piedestal_Haut", parent, S + Vector3.up * 4.45f, new Vector3(8f, 0.15f, 8f), "Concrete");
            Vector3 toTrack = new Vector3(-60f, 0f, 0f) - S;
            PlaceFit("StatueAnge", parent, S + Vector3.up * 4.6f, Mathf.Atan2(toTrack.x, toTrack.z) * Mathf.Rad2Deg, 11f).name = "Statue (Ange)";
            for (int k = 0; k < 6; k++)
            {
                float ang = k * Mathf.PI / 3f;
                Vector3 bp = S + new Vector3(Mathf.Cos(ang), 0, Mathf.Sin(ang)) * 11f;
                PropH(PK + "tree.obj", parent, bp, Quaternion.Euler(0, k * 60f, 0), 7f);
            }
        }

        // ------------------------------------------------------------------ objets, piliers, arrivée

        static void BuildCityItems(PathData path, Transform root)
        {
            var boxes = Group("Boites_Objets", root);
            string[] kinds = { "MushroomBox", "StarBox", "GreenShellBox", "BananaBox", "IceFlowerBox" };
            int k = 0;
            foreach (var spot in new[] { new Vector3(25, 0, 0), new Vector3(-40, 0, -257), new Vector3(-114, 0, -145), new Vector3(150, 0, -165) })
            {
                int i = path.Nearest(spot);
                Vector3 f = Flat(path.fwd[i]);
                foreach (float off in new[] { -4.5f, -1.5f, 1.5f, 4.5f })
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
            foreach (var spot in new[] { new Vector3(-118, 0, -170), new Vector3(5, 0, -257), new Vector3(-100, 0, -40), new Vector3(130, 0, -1) })
            {
                int i = path.Nearest(spot);
                var go = (GameObject)PrefabUtility.InstantiatePrefab(strip, boosts);
                go.name = $"Boost_{b++}";
                go.transform.SetPositionAndRotation(path.p[i] + Vector3.up * (RoadLift + 0.02f), Quaternion.LookRotation(path.fwd[i], Vector3.up));
            }
        }

        static void PlaceCityPillars(PathData path, int i, Transform root)
        {
            var parent = Group("Piliers_Parc", root);
            Vector3 ff = Flat(path.fwd[i]);
            var pil = PlaceScaled("PiliersParc", parent, path.p[i] + Vector3.up * RoadLift, Mathf.Atan2(ff.x, ff.z) * Mathf.Rad2Deg, PillarsScale);
            pil.name = "Piliers_Parc (au milieu de la route)";
            AddMeshColliders(pil);
        }

        static void BuildCityFinish(PathData path, int fi, Transform root)
        {
            var parent = Group("Depart_Arrivee", root);
            Vector3 p = path.p[fi], r = path.right[fi], f = Flat(path.fwd[fi]);
            var line = new MB();
            Vector3 up = Vector3.up * (RoadLift + 0.015f);
            line.Quad(p - r * RoadHalf + up, p - r * RoadHalf + f * 1.5f + up, p + r * RoadHalf + f * 1.5f + up, p + r * RoadHalf + up,
                new Vector2(0, 0), new Vector2(0, 1), new Vector2(8, 1), new Vector2(8, 0), Vector3.up);
            MeshGO("Ligne_Depart_Arrivee", parent, line, "Checker", false);
            // grille de départ (8 places) derrière la ligne
            var grid = new MB();
            for (int s = 0; s < 8; s++)
            {
                int gi = Mathf.Max(0, fi - 8 - (s / 2) * 6 - (s % 2) * 3);
                Vector3 c = path.p[gi] + path.right[gi] * (s % 2 == 0 ? -2.5f : 2.5f) + Vector3.up * (RoadLift + 0.012f);
                Vector3 rr = path.right[gi], ff = Flat(path.fwd[gi]);
                grid.Quad(c - rr * 1.3f + ff * 1.2f, c + rr * 1.3f + ff * 1.2f, c + rr * 1.3f + ff * 1.45f, c - rr * 1.3f + ff * 1.45f,
                    Vector2.zero, Vector2.right, Vector2.one, Vector2.up, Vector3.up);
            }
            MeshGO("Grille_Depart", parent, grid, "White", false);
            var gantry = Group("Texte", parent);
            gantry.SetPositionAndRotation(p, Quaternion.LookRotation(f));
            Text3D(gantry, "DEVINCI CITY · " + CityLaps + " TOURS", new Vector3(0, 8.3f, -0.1f), Quaternion.identity, 1.2f, Color.white);

            // podium + trophées, côté square central
            var podium = Group("Podium", parent);
            Vector3 pos = p + r * (Edge + 9f) - f * 12f; pos.y = 0f;
            podium.SetPositionAndRotation(pos, Quaternion.LookRotation(-r));
            var steps = new[] { (0f, 1.5f, "1", "TrophePremier"), (-2.4f, 1.0f, "2", "Trophe2eme"), (2.4f, 0.7f, "3", "Trophe3eme") };
            foreach (var st in steps)
            {
                Prim(PrimitiveType.Cube, "Marche_" + st.Item3, podium, new Vector3(st.Item1, st.Item2 / 2f, 0), new Vector3(2.3f, st.Item2, 2f), "White", null, true);
                Text3D(podium, st.Item3, new Vector3(st.Item1, st.Item2 / 2f, -1.02f), Quaternion.identity, 0.8f, new Color(0.1f, 0.2f, 0.5f));
                var tr = PlaceFit(st.Item4, podium, podium.TransformPoint(new Vector3(st.Item1, st.Item2, 0)), podium.eulerAngles.y + 180f, 1.3f);
                tr.AddComponent<Spinner>().degreesPerSecond = new Vector3(0, 30, 0);
            }
            cityBlockers.Add(new Vector3(pos.x, 6f, pos.z));
        }

        // ------------------------------------------------------------------ immeubles en bord de rue

        static void BuildCityBlocks(PathData path, Transform parent, System.Random rng)
        {
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
            var mbs = new Dictionary<string, MB>();
            var cyl = new Dictionary<string, List<CombineInstance>>();
            string[] lowMats = { "Building", "TowerBrick", "TowerBands", "TowerBrick", "Building" };
            int count = 0;
            for (int pass = 0; pass < 2; pass++)          // 1re passe : façade sur rue ; 2e passe : 2e rang, plus haut
                for (int i = 0; i < path.Count - 1; i += 6)
                    foreach (float side in new[] { -1f, 1f })
                    {
                        Vector3 f = Flat(path.fwd[i]), o = Flat(path.right[i]) * side;
                        float w = R(16f, 28f), d = R(14f, 24f);
                        float off = Edge + 6f + d / 2f + (pass == 1 ? R(24f, 40f) : 0f);
                        Vector3 c = path.p[i] + o * off; c.y = 0f;
                        float half = Mathf.Max(w, d) * 0.55f + 1f;
                        if (!RectFree(c, f, o, half, half) || RoadDistance(c) < Edge + 5f + half * 0.7f) continue;
                        // plus haut côté Grande Arche (sud-est), plus bas côté ancien campus
                        float tall = Mathf.InverseLerp(60f, -300f, c.z) * 0.6f + Mathf.InverseLerp(-150f, 200f, c.x) * 0.4f;
                        float h = pass == 0 ? R(12f, 28f) + tall * 25f : R(22f, 45f) + tall * 50f;
                        if (rng.Next(4) == 0 || pass == 1)
                            AddTower(mbs, cyl, rng.Next(6), c.x, c.z, w * 0.8f, d * 0.8f, h, R);
                        else
                        {
                            string mat = lowMats[rng.Next(lowMats.Length)];
                            var mb = TowerMB(mbs, mat);
                            mb.Box(new Vector3(c.x - w / 2, 0, c.z - d / 2), new Vector3(c.x + w / 2, h, c.z + d / 2), 10f, false);
                            if (rng.Next(3) == 0) TowerMB(mbs, "Leaves2").Box(new Vector3(c.x - w / 2 + 1, h, c.z - d / 2 + 1), new Vector3(c.x + w / 2 - 1, h + 0.5f, c.z + d / 2 - 1), 6f, false);
                            // auvent de boutique au rez-de-chaussée, côté rue
                            Vector3 fa = c - o * (d / 2f + 1.2f);
                            string awning = rng.Next(3) == 0 ? "BalloonRed" : rng.Next(2) == 0 ? "BalloonBlue" : "BalloonYellow";
                            AddCombined(cyl, awning, cubeMesh, Matrix4x4.TRS(fa + Vector3.up * 3.6f, Quaternion.LookRotation(f), new Vector3(w * 0.8f, 0.25f, 2.4f)));
                        }
                        Reserve(c, half);
                        count++;
                    }
            foreach (var kv in mbs) MeshGO("Immeubles_" + kv.Key, parent, kv.Value, kv.Key, false);
            FlushCombined(cyl, "Immeubles_Details", parent);
            Debug.Log($"[DeVinci City] {count} immeubles en bord de rue.");
        }

        // ------------------------------------------------------------------ kart, tours

        static GameObject PlaceCityKart(PathData path, int fi, TrackRoute route)
        {
            var prefab = BuildKartVariant() ?? KLPrefab("PlayerKart");
            if (prefab == null) return null;
            var oldCam = GameObject.Find("Main Camera");
            if (oldCam != null) Object.DestroyImmediate(oldCam);
            var kart = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            kart.name = "Player Kart";
            int gi = Mathf.Max(0, fi - 8);   // pole position, juste derrière la ligne
            kart.transform.SetPositionAndRotation(path.p[gi] + path.right[gi] * -2.5f + Vector3.up * (RoadLift + 0.02f), Quaternion.LookRotation(Flat(path.fwd[gi])));
            foreach (var cam in kart.GetComponentsInChildren<Camera>(true)) { cam.farClipPlane = 3000f; cam.gameObject.tag = "MainCamera"; }
            var rec = kart.GetComponent<KartRecovery>();
            if (rec != null) { rec.route = route; rec.offTrackDelay = 2.5f; }
            var motor = kart.GetComponent<KartMotor>();
            if (motor != null) motor.groundMask = 1;
            var hud = kart.GetComponentInChildren<KartHUD>(true);
            if (hud != null) hud.CourseName = "DEVINCI CITY / " + CityLaps + " TOURS";
            Selection.activeGameObject = kart;
            return kart;
        }

        /// <summary>Portes de tour : la n°0 est la ligne d'arrivée, les autres doivent être franchies dans l'ordre (anti-raccourci).</summary>
        static void BuildLapSystem(PathData path, int fi, Transform root, GameObject kart)
        {
            var parent = Group("Tours", root);
            var mgrGO = new GameObject("LapRaceManager");
            mgrGO.transform.SetParent(parent, false);
            var mgr = mgrGO.AddComponent<LapRaceManager>();
            mgr.totalLaps = CityLaps;
            mgr.kart = kart != null ? kart.GetComponent<KartMotor>() : null;
            int m = path.Count - 1;
            const int gateCount = 6;
            var gates = new List<LapGate>();
            for (int g = 0; g < gateCount; g++)
            {
                int i = (fi + g * m / gateCount) % m;
                var go = new GameObject(g == 0 ? "Porte_00_Ligne" : $"Porte_{g:00}");
                go.transform.SetParent(parent, false);
                Vector3 f = Flat(path.fwd[i]);
                go.transform.SetPositionAndRotation(path.p[i] + Vector3.up * RoadLift, Quaternion.LookRotation(f));
                var col = go.AddComponent<BoxCollider>();
                col.isTrigger = true;
                col.center = new Vector3(0, 2.5f, 0);
                col.size = new Vector3(2f * (Edge + 1f), 6f, 3f);
                var gate = go.AddComponent<LapGate>();
                gate.index = g;
                gate.manager = mgr;
                gates.Add(gate);
            }
            mgr.gates = gates.ToArray();
        }

        // ------------------------------------------------------------------ menu de choix des circuits

        [MenuItem("DeVinci Kart/Créer le menu de choix des circuits")]
        public static void BuildMenuScene()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            BuildMenuSceneInternal();
            ConfigureBuildScenes();
            EditorUtility.DisplayDialog("DeVinci Kart", "Menu créé : " + MenuScenePath + "\nIl est placé en premier dans la liste des scènes du build.", "OK");
        }

        static void BuildMenuSceneInternal()
        {
            var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
            var cam = Camera.main;
            if (cam != null)
            {
                cam.clearFlags = CameraClearFlags.SolidColor;
                cam.backgroundColor = new Color(0.05f, 0.12f, 0.25f);
            }
            var menu = new GameObject("TrackMenu").AddComponent<TrackMenu>();
            menu.scenes = new[] { System.IO.Path.GetFileNameWithoutExtension(ScenePath), System.IO.Path.GetFileNameWithoutExtension(CityScenePath) };
            menu.titles = new[] { "1 · Campus → Campus", "2 · City Slicker DeVinci" };
            menu.descriptions = new[]
            {
                "Sprint : du Pôle Léonard de Vinci au Campus du Parc, en passant sous la Grande Arche.",
                "Circuit City Slicker en " + CityLaps + " tours : ponts, parc, rond-point de l'Ange, avec la Grande Arche, le Pouce et les deux campus.",
            };
            EditorSceneManager.SaveScene(scene, MenuScenePath);
        }

        /// <summary>Ordre du build : menu, piste 1, piste 2 (seules les scènes existantes sont gardées).</summary>
        static void ConfigureBuildScenes()
        {
            var wanted = new[] { MenuScenePath, ScenePath, CityScenePath };
            var list = new List<EditorBuildSettingsScene>();
            foreach (var p in wanted)
                if (AssetDatabase.LoadAssetAtPath<SceneAsset>(p) != null) list.Add(new EditorBuildSettingsScene(p, true));
            foreach (var s in EditorBuildSettings.scenes)
                if (!wanted.Contains(s.path)) list.Add(s);
            EditorBuildSettings.scenes = list.ToArray();
        }
    }
}
