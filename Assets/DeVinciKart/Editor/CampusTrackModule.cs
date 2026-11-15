using System.Collections.Generic;
using System.Linq;
using KartLab;
using UnityEditor;
using UnityEngine;

namespace DeVinciKart.EditorTools
{
    /// <summary>
    /// Piste 1 (Campus → Campus), version « chaotique » :
    /// - le quartier City Slicker (rond-point de l'Ange + vallée du parc sous les ponts), découpé dans
    ///   Assets/CitySlicker/module_ange_parc.gltf et posé EN MIROIR à la place de l'ancien rond-point de la statue ;
    /// - deux variantes dans ce quartier : le pont en arc avec son saut, et le tunnel creusé sous la colline ;
    /// - une piste en terre avec tremplin dans les Terrasses ;
    /// - un viaduc routier (voitures qui roulent au-dessus de la piste), des obstacles mobiles et des accessoires
    ///   du Low Poly Road Pack (FastMesh).
    /// Modèle « city slicker » par amogusstrikesback2 (Sketchfab), licence CC-BY-4.0.
    /// </summary>
    public static partial class CampusTrackGenerator
    {
        const string ModuleAsset = "Assets/CitySlicker/module_ange_parc.gltf";
        const string FM = "Assets/FastMesh/Prefabs/Road/";
        const float ModuleS = 210f;                                   // origine du module : sur l'axe, 210 m à l'est de la Grande Arche
        const float ViaductS = -140f, ViaductY = 10.5f, ViaductHalfLen = 260f;
        static readonly Vector3 NNorth = new Vector3(-AxisEast.z, 0f, AxisEast.x);
        static Matrix4x4 modL2W = Matrix4x4.identity, modW2L = Matrix4x4.identity;
        static Quaternion modRot = Quaternion.identity;
        static Transform moduleRoot;
        static bool moduleActive;
        static PathData altCPath;                                     // piste en terre (pour ouvrir les barrières)

        // Emprise du module, repère City Slicker (x, z) : union de rectangles et contour (sens trigonométrique).
        static readonly Rect[] ModRects = { Rect.MinMaxRect(-160f, -240f, -13f, 110f), Rect.MinMaxRect(-13f, -110f, 28f, -8f), Rect.MinMaxRect(-96f, 110f, -68f, 117f) };
        static readonly Vector2[] ModOutline =
        {
            new Vector2(-160, -240), new Vector2(-13, -240), new Vector2(-13, -110), new Vector2(28, -110), new Vector2(28, -8), new Vector2(-13, -8),
            new Vector2(-13, 110), new Vector2(-68, 110), new Vector2(-68, 117), new Vector2(-96, 117), new Vector2(-96, 110), new Vector2(-160, 110),
        };

        // Tracé principal dans le module (x, y, z City Slicker) : descente dans la vallée sous les deux ponts, épingle du bas,
        // remontée, grand pont, tour du rond-point de l'Ange, sortie vers la Grande Arche.
        static readonly Vector3[] ModMain =
        {
            new Vector3(-82, 0f, 117), new Vector3(-82, -0.3f, 108), new Vector3(-84, -2.15f, 80), new Vector3(-86, -4.07f, 56), new Vector3(-87, -5.33f, 40),
            new Vector3(-87, -6.37f, 26), new Vector3(-83, -6.93f, 10), new Vector3(-79, -6.94f, -5), new Vector3(-80, -6.92f, -16), new Vector3(-83, -6.94f, -28),
            new Vector3(-89, -7.15f, -43), new Vector3(-87, -6.99f, -58), new Vector3(-75, -7.10f, -71), new Vector3(-65, -7.27f, -83), new Vector3(-63, -7.30f, -93),
            new Vector3(-67, -6.76f, -105), new Vector3(-73, -6.94f, -118), new Vector3(-76, -7.19f, -135), new Vector3(-80, -7.68f, -155), new Vector3(-84, -7.29f, -190),
            new Vector3(-98, -6.94f, -216), new Vector3(-120, -6.95f, -226), new Vector3(-136, -7f, -216), new Vector3(-139, -7f, -195), new Vector3(-139, -3.81f, -160),
            new Vector3(-139, -2.91f, -125), new Vector3(-139, -2.26f, -100), new Vector3(-139, -1.34f, -88), new Vector3(-137, 1.34f, -77), new Vector3(-129, 2.76f, -71),
            new Vector3(-110, 5.28f, -70), new Vector3(-90, 6.06f, -70), new Vector3(-70, 5.12f, -70), new Vector3(-50, 2.11f, -69), new Vector3(-34, -0.08f, -69),
            new Vector3(-24, 0f, -74), new Vector3(-11, 0f, -77), new Vector3(1, 0f, -71), new Vector3(6, 0f, -59), new Vector3(4, 0f, -46),
            new Vector3(2, 0f, -34), new Vector3(0, 0f, -20), new Vector3(0, 0f, -8),
        };
        // Variante A : en haut de la remontée, on prend la rampe vers le pont en arc, saut au-dessus du trou, puis le pont redescend sur le rond-point.
        static readonly Vector3[] ModAltA =
        {
            new Vector3(-139, -2.26f, -100), new Vector3(-139, -1.3f, -88), new Vector3(-142, 1.4f, -75), new Vector3(-148, 2.4f, -61), new Vector3(-147.5f, 3.3f, -46),
            new Vector3(-143.5f, 4f, -30), new Vector3(-136f, 4f, -16.5f), new Vector3(-128.5f, 4.2f, -6.5f), new Vector3(-114.2f, 6f, 6.2f), new Vector3(-103.4f, 6f, 18.2f),
            new Vector3(-93.9f, 6f, 27.9f), new Vector3(-85.8f, 6f, 33.6f), new Vector3(-76.3f, 6f, 34.5f), new Vector3(-62.8f, 6f, 29.1f), new Vector3(-49.6f, 5.2f, 15.3f),
            new Vector3(-37.6f, 3.9f, -0.7f), new Vector3(-28.4f, 1.5f, -16.3f), new Vector3(-24.5f, 0.1f, -30.5f), new Vector3(-24f, 0f, -45f), new Vector3(-26f, 0f, -58f),
            new Vector3(-22f, 0f, -70f), new Vector3(-11f, 0f, -77f),
        };
        // Variante B : le tunnel sous la colline du parc (raccourci de l'épingle du bas).
        static readonly Vector3[] ModAltB =
        {
            new Vector3(-80, -7.68f, -155), new Vector3(-82, -7.6f, -168), new Vector3(-87, -7.7f, -180), new Vector3(-94, -7.72f, -186.5f), new Vector3(-106, -7.4f, -186.5f),
            new Vector3(-118, -7.05f, -186.5f), new Vector3(-126, -7f, -188f), new Vector3(-133.5f, -7f, -184.5f), new Vector3(-138.5f, -6f, -174f), new Vector3(-139, -3.81f, -160),
        };

        // ------------------------------------------------------------------ repère et emprise du module

        static void ComputeModuleFrame()
        {
            Vector3 west = -AxisEast;
            modRot = Quaternion.Euler(0f, Mathf.Atan2(west.x, west.z) * Mathf.Rad2Deg, 0f);
            modL2W = Matrix4x4.TRS(A(ModuleS), modRot, new Vector3(-1f, 1f, 1f));   // miroir : le quartier tourne à droite
            modW2L = modL2W.inverse;
        }

        static Vector3 Mod(Vector3 c) => modL2W.MultiplyPoint3x4(c);
        static Vector3 Mod(float x, float y, float z) => modL2W.MultiplyPoint3x4(new Vector3(x, y, z));

        static bool InModRect(float x, float z)
        {
            foreach (var r in ModRects) if (x >= r.xMin && x <= r.xMax && z >= r.yMin && z <= r.yMax) return true;
            return false;
        }

        /// <summary>Le point est-il dans la partie découpée du module (là où City Slicker fournit la route) ?</summary>
        static bool InModuleCrop(Vector3 p, float margin)
        {
            if (!moduleActive) return false;
            Vector3 l = modW2L.MultiplyPoint3x4(p);
            if (!InModRect(l.x, l.z)) return false;
            if (margin <= 0f) return true;
            return InModRect(l.x + margin, l.z) && InModRect(l.x - margin, l.z) && InModRect(l.x, l.z + margin) && InModRect(l.x, l.z - margin);
        }

        /// <summary>Emprise totale du module (aucun décor ajouté dedans).</summary>
        static bool ModuleBlocked(Vector3 p, float margin = 6f)
        {
            if (!moduleActive) return false;
            Vector3 l = modW2L.MultiplyPoint3x4(p);
            return l.x > -167f - margin && l.x < 57f + margin && l.z > -245f - margin && l.z < 122f + margin;
        }

        static bool ViaductBlocked(Vector3 p)
        {
            if (!moduleActive) return false;
            Vector3 q = new Vector3(p.x, 0f, p.z);
            return Mathf.Abs(Vector3.Dot(q, AxisEast) - ViaductS) < 16f && Mathf.Abs(Vector3.Dot(q, NNorth)) < ViaductHalfLen + 40f;
        }

        static bool IsModuleCollider(Collider c) => moduleRoot != null && c != null && c.transform.IsChildOf(moduleRoot);

        /// <summary>Surface du module la plus proche de la hauteur attendue (ponts / dessous de ponts / tunnel).</summary>
        static bool ModuleSurface(Vector3 p, float expected, float maxGap, out float y)
        {
            y = expected;
            float best = float.MaxValue;
            foreach (var h in Physics.RaycastAll(new Vector3(p.x, expected + 3.5f, p.z), Vector3.down, 9f, 1, QueryTriggerInteraction.Ignore))
            {
                if (!IsModuleCollider(h.collider) || h.normal.y < 0.5f) continue;
                float d = Mathf.Abs(h.point.y - expected);
                if (d < best) { best = d; y = h.point.y; }
            }
            return best <= maxGap;
        }

        static void RecomputeFrames(PathData pd)
        {
            int n = pd.Count;
            for (int i = 0; i < n; i++)
            {
                Vector3 f = (pd.p[Mathf.Min(i + 1, n - 1)] - pd.p[Mathf.Max(i - 1, 0)]).normalized;
                pd.fwd[i] = f;
                pd.right[i] = Vector3.Cross(Vector3.up, Flat(f)).normalized;
            }
        }

        /// <summary>Recale les points du tracé situés dans le module sur sa route réelle, avec un léger lissage.</summary>
        static int SnapToModule(PathData path)
        {
            int n = path.Count, missed = 0;
            var ys = new float[n]; var inM = new bool[n];
            for (int i = 0; i < n; i++)
            {
                ys[i] = path.p[i].y;
                inM[i] = InModuleCrop(path.p[i], 0f);
                if (!inM[i]) continue;
                if (ModuleSurface(path.p[i], path.p[i].y, 1.6f, out float y)) ys[i] = y; else missed++;
            }
            for (int i = 0; i < n; i++)
            {
                if (!inM[i]) continue;
                float a = 0f; int c = 0;
                for (int k = -2; k <= 2; k++) { int j = Mathf.Clamp(i + k, 0, n - 1); a += ys[j]; c++; }
                path.p[i] = new Vector3(path.p[i].x, a / c, path.p[i].z);
            }
            RecomputeFrames(path);
            return missed;
        }

        // ------------------------------------------------------------------ construction du module

        static void BuildModule(Transform root)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModuleAsset);
            if (asset == null)
            {
                AssetDatabase.ImportAsset(ModuleAsset, ImportAssetOptions.ForceUpdate);
                asset = AssetDatabase.LoadAssetAtPath<GameObject>(ModuleAsset);
            }
            if (asset == null) { Warnings.Add("Quartier City Slicker introuvable : " + ModuleAsset); moduleActive = false; return; }

            moduleRoot = new GameObject("Quartier_CitySlicker (rond-point de l'Ange + parc · CC-BY amogusstrikesback2)").transform;
            moduleRoot.SetParent(root, false);
            moduleRoot.SetPositionAndRotation(A(ModuleS), modRot);
            moduleRoot.localScale = new Vector3(-1f, 1f, 1f);

            var level = (GameObject)PrefabUtility.InstantiatePrefab(asset);
            if (PrefabUtility.IsPartOfPrefabInstance(level))
                PrefabUtility.UnpackPrefabInstance(level, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            level.transform.SetParent(moduleRoot, false);
            foreach (var t in level.GetComponentsInChildren<Transform>(true)) { t.gameObject.layer = 0; t.gameObject.isStatic = true; }
            foreach (var l in level.GetComponentsInChildren<Light>(true)) Object.DestroyImmediate(l.gameObject);
            foreach (var c in level.GetComponentsInChildren<Camera>(true)) Object.DestroyImmediate(c.gameObject);

            if (!AssetDatabase.IsValidFolder(GenFolder + "/MeshesModule")) AssetDatabase.CreateFolder(GenFolder, "MeshesModule");
            csMeshFolder = GenFolder + "/MeshesModule";
            try { PrepareCitySlicker(level); }
            finally { csMeshFolder = GenFolder + "/MeshesCitySlicker"; }

            BuildTunnel();
            Physics.SyncTransforms();
            BuildModuleWalls();
            Physics.SyncTransforms();

            // l'Ange au centre du rond-point (comme sur la piste 2)
            var mon = Group("Monuments_Quartier", root);
            Vector3 S = Mod(-11f, 0f, -60f); S.y = CSGround(S.x, S.z, 0.5f);
            Prim(PrimitiveType.Cylinder, "Piedestal_Ange", mon, S + Vector3.up * 1.2f, new Vector3(6f, 1.2f, 6f), "Stone");
            PlaceFit("StatueAnge", mon, S + Vector3.up * 2.4f, YawTowards(S, Mod(-40f, 0f, -69f)), 11f).name = "Statue (Ange)";
            var credit = Text3D(mon, "Quartier : \"city slicker\" par amogusstrikesback2 - CC-BY-4.0", Mod(-96f, 3.5f, 118f) + Vector3.up * 2f,
                Quaternion.LookRotation(AxisEast), 0.3f, Color.white);
            credit.name = "Credits_CC-BY";
        }

        /// <summary>Tunnel creusé sous la colline du parc (repère City Slicker, sous la racine en miroir).</summary>
        static void BuildTunnel()
        {
            const float x0 = -92.5f, x1 = -121.5f, zS = -191.5f, zN = -181.5f, yE = -7.72f, yW = -7.02f, ceil = -1.6f;
            var floor = new MB(); var walls = new MB(); var rock = new MB();
            floor.Quad(new Vector3(x0, yE, zS), new Vector3(x1, yW, zS), new Vector3(x1, yW, zN), new Vector3(x0, yE, zN),
                new Vector2(0, 0), new Vector2(0, 3.6f), new Vector2(1.2f, 3.6f), new Vector2(1.2f, 0), Vector3.up);
            walls.Quad(new Vector3(x0, yE - 0.4f, zS), new Vector3(x1, yW - 0.4f, zS), new Vector3(x1, ceil, zS), new Vector3(x0, ceil, zS),
                new Vector2(0, 0), new Vector2(7, 0), new Vector2(7, 1.5f), new Vector2(0, 1.5f), Vector3.forward);
            walls.Quad(new Vector3(x0, yE - 0.4f, zN), new Vector3(x1, yW - 0.4f, zN), new Vector3(x1, ceil, zN), new Vector3(x0, ceil, zN),
                new Vector2(0, 0), new Vector2(7, 0), new Vector2(7, 1.5f), new Vector2(0, 1.5f), Vector3.back);
            walls.Quad(new Vector3(x0, ceil, zS), new Vector3(x1, ceil, zS), new Vector3(x1, ceil, zN), new Vector3(x0, ceil, zN),
                new Vector2(0, 0), new Vector2(7, 0), new Vector2(7, 2.5f), new Vector2(0, 2.5f), Vector3.down);
            // blocs de roche qui referment la colline autour des deux portails
            rock.Box(new Vector3(-104f, -8.2f, -199f), new Vector3(x0, 2.6f, zS), 4f);
            rock.Box(new Vector3(-104f, -8.2f, zN), new Vector3(x0, 2.6f, -176.5f), 4f);
            rock.Box(new Vector3(-104f, ceil, zS), new Vector3(x0, 2.6f, zN), 4f);
            rock.Box(new Vector3(x1, -7.6f, -198f), new Vector3(-116.2f, 1.2f, zS), 4f);
            rock.Box(new Vector3(x1, -7.6f, zN), new Vector3(-116.2f, 1.2f, -175f), 4f);
            rock.Box(new Vector3(x1, ceil, zS), new Vector3(-116.2f, 1.2f, zN), 4f);
            var parent = Group("Tunnel_Raccourci", moduleRoot);
            MeshGO("Tunnel_Sol", parent, floor, "Asphalt", true);
            MeshGO("Tunnel_Parois", parent, walls, "Concrete", true);
            MeshGO("Tunnel_Roche", parent, rock, "Stone", true);
            for (float x = x0 - 2f; x > x1 + 1f; x -= 4.5f)
                Prim(PrimitiveType.Cube, "Neon", parent, new Vector3(x, ceil - 0.06f, (zS + zN) * 0.5f), new Vector3(2.4f, 0.08f, 0.35f), "LightPanel");
            foreach (float z in new[] { zS + 0.6f, zN - 0.6f })
                for (float x = x0 - 1f; x > x1 + 1f; x -= 3f)
                    Prim(PrimitiveType.Cube, "Balise", parent, new Vector3(x, Mathf.Lerp(yE, yW, (x0 - x) / (x0 - x1)) + 0.5f, z), new Vector3(0.25f, 0.8f, 0.25f), "Boost");
        }

        /// <summary>Murs de soutènement le long du bord du module, là où son terrain descend sous le sol de la piste.</summary>
        static void BuildModuleWalls()
        {
            var w = new MB();
            int n = ModOutline.Length, count = 0;
            for (int e = 0; e < n; e++)
            {
                Vector2 a = ModOutline[e], b = ModOutline[(e + 1) % n];
                Vector2 d = b - a; float L = d.magnitude; d /= L;
                Vector2 inward = new Vector2(-d.y, d.x);
                for (float t = 0f; t < L - 0.01f; t += 2f)
                {
                    float t1 = Mathf.Min(L, t + 2f);
                    Vector2 m = a + d * ((t + t1) * 0.5f) + inward * 0.7f;
                    float low = float.MaxValue;
                    foreach (var h in Physics.RaycastAll(Mod(m.x, 60f, m.y), Vector3.down, 130f, 1, QueryTriggerInteraction.Ignore))
                        if (IsModuleCollider(h.collider)) low = Mathf.Min(low, h.point.y);
                    if (low == float.MaxValue || low > -0.4f) continue;
                    Vector2 p0 = a + d * t - inward * 0.4f, p1 = a + d * t1 - inward * 0.4f;   // juste derrière le terrain du module
                    float yb = low - 0.8f;
                    w.Quad(new Vector3(p0.x, yb, p0.y), new Vector3(p1.x, yb, p1.y), new Vector3(p1.x, 0f, p1.y), new Vector3(p0.x, 0f, p0.y),
                        new Vector2(t / 4f, yb / 4f), new Vector2(t1 / 4f, yb / 4f), new Vector2(t1 / 4f, 0f), new Vector2(t / 4f, 0f), new Vector3(inward.x, 0f, inward.y));
                    count++;
                }
            }
            MeshGO("Murs_Bordure_Quartier", moduleRoot, w, "Concrete", true);
        }

        /// <summary>
        /// Sol de la piste : grands pavés partout, sauf la tranchée du boulevard et l'emprise du module ;
        /// dans l'emprise du module, sol en dalles de 4 m (1 m sur les bords) seulement là où City Slicker n'a pas de terrain.
        /// </summary>
        static void BuildGround(Transform parent)
        {
            const float big = 4000f;
            float hw = TrenchHalfW, hl = TrenchHalfLen, d = TrenchDepth;
            // emprise du module dans le repère du pont (mêmes axes à un signe près)
            float bx0 = float.MaxValue, bx1 = float.MinValue, bz0 = float.MaxValue, bz1 = float.MinValue;
            if (moduleActive)
                foreach (var c in new[] { new Vector2(-160f, -240f), new Vector2(28f, -240f), new Vector2(28f, 117f), new Vector2(-160f, 117f) })
                {
                    Vector3 q = bridgeW2L.MultiplyPoint3x4(Mod(c.x, 0f, c.y));
                    bx0 = Mathf.Min(bx0, q.x); bx1 = Mathf.Max(bx1, q.x); bz0 = Mathf.Min(bz0, q.z); bz1 = Mathf.Max(bz1, q.z);
                }
            var xs = new List<float> { -big, -hl, hl, big }; var zs = new List<float> { -big, -hw, hw, big };
            if (moduleActive) { xs.Add(bx0); xs.Add(bx1); zs.Add(bz0); zs.Add(bz1); }
            xs = xs.Distinct().OrderBy(v => v).ToList(); zs = zs.Distinct().OrderBy(v => v).ToList();
            var ground = new MB(); var fine = new MB();
            for (int i = 0; i < xs.Count - 1; i++)
            for (int j = 0; j < zs.Count - 1; j++)
            {
                float cx = (xs[i] + xs[i + 1]) * 0.5f, cz = (zs[j] + zs[j + 1]) * 0.5f;
                if (Mathf.Abs(cx) < hl && Mathf.Abs(cz) < hw) continue;                                   // tranchée
                if (moduleActive && cx > bx0 && cx < bx1 && cz > bz0 && cz < bz1) continue;              // module : dalles fines
                ground.Box(new Vector3(xs[i], -d - 2f, zs[j]), new Vector3(xs[i + 1], 0f, zs[j + 1]), 8f, false);
            }
            if (moduleActive)
            {
                const float cell = 4f, y = -0.25f;   // sous le terrain du module : jamais de chevauchement visible
                System.Func<float, float, bool> Covered = (x, z) =>
                {
                    Vector3 w = bridgeL2W.MultiplyPoint3x4(new Vector3(x, 0f, z));
                    foreach (var h in Physics.RaycastAll(new Vector3(w.x, 80f, w.z), Vector3.down, 200f, 1, QueryTriggerInteraction.Ignore))
                        if (IsModuleCollider(h.collider)) return true;
                    return false;
                };
                System.Action<float, float, float> Tile = (x, z, s) => fine.Quad(new Vector3(x, y, z), new Vector3(x + s, y, z), new Vector3(x + s, y, z + s), new Vector3(x, y, z + s),
                    new Vector2(x, z) / 8f, new Vector2(x + s, z) / 8f, new Vector2(x + s, z + s) / 8f, new Vector2(x, z + s) / 8f, Vector3.up);
                for (float x = bx0; x < bx1 - 0.01f; x += cell)
                for (float z = bz0; z < bz1 - 0.01f; z += cell)
                {
                    float s = Mathf.Min(cell, bx1 - x), sz = Mathf.Min(cell, bz1 - z);
                    if (s < cell || sz < cell) { Tile(x, z, Mathf.Min(s, sz)); continue; }
                    int hits = 0;
                    foreach (var o in new[] { new Vector2(0.3f, 0.3f), new Vector2(3.7f, 0.3f), new Vector2(0.3f, 3.7f), new Vector2(3.7f, 3.7f), new Vector2(2f, 2f) })
                        if (Covered(x + o.x, z + o.y)) hits++;
                    if (hits == 5) continue;
                    if (hits == 0) { Tile(x, z, cell); continue; }
                    for (float u = 0f; u < cell; u += 1f)
                    for (float v = 0f; v < cell; v += 1f)
                        if (!Covered(x + u + 0.5f, z + v + 0.5f)) Tile(x + u, z + v, 1f);
                }
            }
            var g = MeshGO("Sol", parent, ground, "Grass", true);
            g.AddComponent<GroundSurface>().speedFactor = 0.6f;   // l'herbe ralentit le kart (KartLab)
            var gf = MeshGO("Sol_Autour_Quartier", parent, fine, "Grass", true);
            if (gf != null) gf.AddComponent<GroundSurface>().speedFactor = 0.6f;
        }

        // ------------------------------------------------------------------ itinéraires secondaires

        static TrackRoute AltRoute(PathData p, string name, float half, Transform root, bool active = true)
        {
            var go = new GameObject(name);
            go.transform.SetParent(root, false);
            if (!active) go.SetActive(false);
            var r = go.AddComponent<TrackRoute>();
            var pts = new List<Vector3>();
            for (int i = 0; i < p.Count; i += 2) pts.Add(p.p[i] + Vector3.up * (RoadLift + 0.03f));
            if ((p.Count - 1) % 2 != 0) pts.Add(p.p[p.Count - 1] + Vector3.up * (RoadLift + 0.03f));
            r.points = pts.ToArray();
            r.closed = false;
            r.roadHalfWidth = half;
            r.alternative = true;
            return r;
        }

        /// <summary>Route complète « départ → variante → arrivée » (inactive) pour tester une variante au pilote automatique.</summary>
        static void TestRoute(PathData main, PathData alt, string name, Transform root)
        {
            int a = main.Nearest(alt.p[0]), b = main.Nearest(alt.p[alt.Count - 1]);
            var pd = new PathData();
            for (int i = 0; i < a; i++) pd.p.Add(main.p[i]);
            for (int i = 0; i < alt.Count; i++) pd.p.Add(alt.p[i]);
            for (int i = b + 1; i < main.Count; i++) pd.p.Add(main.p[i]);
            AltRoute(pd, name, 7f, root, false);
        }

        static PathData BuildModuleAlt(Vector3[] cityPts)
        {
            var alt = BuildPath(cityPts.Select(Mod).ToArray(), Step);
            for (int i = 0; i < alt.Count; i++)
                if (InModuleCrop(alt.p[i], 0f) && ModuleSurface(alt.p[i], alt.p[i].y, 1.6f, out float y))
                    alt.p[i] = new Vector3(alt.p[i].x, y, alt.p[i].z);
            RecomputeFrames(alt);
            return alt;
        }

        static void BuildModuleAlternatives(PathData main, Transform root)
        {
            if (!moduleActive || moduleRoot == null) return;
            var parent = Group("Variantes_Quartier", root);

            // --- A : pont en arc avec saut
            var a = BuildPath(ModAltA.Select(Mod).ToArray(), Step);
            int n = a.Count;
            var has = new bool[n]; var ys = new float[n];
            for (int i = 0; i < n; i++)
            {
                ys[i] = a.p[i].y;
                has[i] = ModuleSurface(a.p[i], a.p[i].y, 1.3f, out float y);
                if (has[i]) ys[i] = y;
            }
            int edge = -1;
            for (int i = 30; i < n - 10 && edge < 0; i++)
                if (has[i] && !has[i + 1] && !has[i + 2] && !has[i + 3] && !has[i + 4]) edge = i;
            for (int i = 0; i < n; i++)
            {
                if (has[i]) continue;
                int i0 = i - 1, i1 = i + 1;
                while (i0 >= 0 && !has[i0]) i0--;
                while (i1 < n && !has[i1]) i1++;
                if (i0 >= 0 && i1 < n) ys[i] = Mathf.Lerp(ys[i0], ys[i1], (i - i0) / (float)(i1 - i0));
            }
            for (int i = 0; i < n; i++) a.p[i] = new Vector3(a.p[i].x, ys[i], a.p[i].z);
            RecomputeFrames(a);
            if (edge > 20)
            {
                BuildKickerAt(a, parent, edge - 8, "Tremplin_Pont_Arc", 8, 2.0f, 5.5f);
                PlaceBoost(a, edge - 34, parent, "Boost_Pont_Arc_1");
                PlaceBoost(a, edge - 22, parent, "Boost_Pont_Arc_2");
            }
            else Warnings.Add("Variante du pont : bord du saut introuvable (le tremplin n'a pas été posé).");
            PlaceItemRow(a, a.Nearest(Mod(-76.3f, 6f, 34.5f)), parent, new[] { -3f, 0f, 3f });
            AltRoute(a, "TrackRoute_Variante_PontArc", 7f, root);
            TestRoute(main, a, "TestRoute_Variante_PontArc", root);

            // --- B : tunnel sous la colline
            var b = BuildModuleAlt(ModAltB);
            PlaceItemRow(b, b.Nearest(Mod(-108f, -7.3f, -186.5f)), parent, new[] { -2.5f, 0f, 2.5f });
            AltRoute(b, "TrackRoute_Raccourci_Tunnel", 5f, root);
            TestRoute(main, b, "TestRoute_Raccourci_Tunnel", root);
            Debug.Log($"[DeVinci Kart] Variantes du quartier : pont en arc {a.length:0} m (saut au point {edge}), tunnel {b.length:0} m.");
        }

        /// <summary>Piste en terre des Terrasses (calculée avant la route principale pour y ouvrir les barrières).</summary>
        static PathData PrepareDirtShortcut(PathData main)
        {
            Vector3 merge = N(-451f, -12f);
            float my = main.p[main.Nearest(merge)].y;
            var ctrl = new[] { N(-358f, -56f, 0.03f), N(-368f, -44f, 0.03f), N(-384f, -30f, 0.03f), N(-404f, -20f, 0.03f), N(-424f, -14f, 0.03f), N(-442f, -12f, my * 0.5f), new Vector3(merge.x, my, merge.z) };
            var c = BuildPath(ctrl, Step);
            // les deux bouts suivent exactement la route principale
            for (int i = 0; i < c.Count; i++)
            {
                int j = main.Nearest(c.p[i]);
                Vector3 m = main.p[j]; m.y = 0f; Vector3 q = c.p[i]; q.y = 0f;
                if ((m - q).magnitude < RoadHalf) c.p[i] = new Vector3(c.p[i].x, main.p[j].y, c.p[i].z);
            }
            RecomputeFrames(c);
            return c;
        }

        static bool InBarrierGap(Vector3 barrierPoint)
        {
            if (altCPath == null) return false;
            int n = altCPath.Count;
            for (int i = 0; i < n; i += 2)
            {
                if (i > 40 && i < n - 40) { i = n - 41; continue; }
                Vector3 d = altCPath.p[i] - barrierPoint; d.y = 0f;
                if (d.magnitude < 6.5f) return true;
            }
            return false;
        }

        static void BuildDirtShortcut(PathData main, Transform root)
        {
            if (altCPath == null) return;
            var parent = Group("Raccourci_Terre (Terrasses)", root);
            var c = altCPath;
            var mb = new MB();
            const float hw = 4.2f;
            // la terre commence au bord de la chaussée principale (jamais par-dessus l'asphalte)
            System.Func<int, float> DistMain = k => { Vector3 m = main.p[main.Nearest(c.p[k])]; m.y = 0f; Vector3 q = c.p[k]; q.y = 0f; return (m - q).magnitude; };
            int first = 0, last = c.Count - 1;
            while (first < c.Count - 2 && DistMain(first) < RoadHalf + CurbW) first++;
            while (last > first + 1 && DistMain(last) < RoadHalf + CurbW) last--;
            for (int i = first; i < last; i++)
            {
                Vector3 l0 = c.p[i] - c.right[i] * hw + Vector3.up * 0.035f, r0 = c.p[i] + c.right[i] * hw + Vector3.up * 0.035f;
                Vector3 l1 = c.p[i + 1] - c.right[i + 1] * hw + Vector3.up * 0.035f, r1 = c.p[i + 1] + c.right[i + 1] * hw + Vector3.up * 0.035f;
                mb.Quad(l0, l1, r1, r0, new Vector2(0, c.dist[i] / 6f), new Vector2(0, c.dist[i + 1] / 6f), new Vector2(1.4f, c.dist[i + 1] / 6f), new Vector2(1.4f, c.dist[i] / 6f), Vector3.up);
            }
            var go = MeshGO("Piste_Terre", parent, mb, "Dirt", true);
            if (go != null) go.AddComponent<GroundSurface>().speedFactor = 0.85f;
            BuildKickerAt(c, parent, Mathf.RoundToInt(c.Count * 0.45f), "Tremplin_Terre", 7, 1.3f, 3.8f);
            // panneau « RACCOURCI » à l'entrée
            {
                int i = Mathf.Min(first + 6, c.Count - 1);
                Vector3 side = Flat(c.right[i]);
                Vector3 q = c.p[i] - side * (hw + 1.5f); q.y = Mathf.Max(0f, c.p[i].y);
                var post = Group("Panneau_Raccourci", parent);
                post.SetPositionAndRotation(q, Quaternion.LookRotation(-Flat(c.fwd[i])));
                Prim(PrimitiveType.Cube, "Poteau", post, new Vector3(0f, 1.5f, 0f), new Vector3(0.18f, 3f, 0.18f), "DarkMetal");
                Prim(PrimitiveType.Cube, "Planche", post, new Vector3(0f, 2.9f, 0f), new Vector3(3.2f, 0.9f, 0.12f), "Bark");
                Text3D(post, "RACCOURCI →", new Vector3(0f, 2.9f, -0.08f), Quaternion.Euler(0f, 180f, 0f), 0.5f, Color.white);
            }
            // balises bois le long de la piste en terre
            for (int i = first + 8; i < last - 8; i += 6)
                foreach (float side in new[] { -1f, 1f })
                    Prim(PrimitiveType.Cylinder, "Balise", parent, c.p[i] + c.right[i] * side * (hw + 0.6f) + Vector3.up * 0.5f, new Vector3(0.25f, 0.5f, 0.25f), (i / 6) % 2 == 0 ? "Barrier" : "Bark");
            AltRoute(c, "TrackRoute_Raccourci_Terre", 5f, root);
            TestRoute(main, c, "TestRoute_Raccourci_Terre", root);
        }

        // ------------------------------------------------------------------ objets / boosts sur une route quelconque

        static void PlaceBoost(PathData p, int i, Transform parent, string name)
        {
            if (i < 0 || i >= p.Count) return;
            BoostAt(p.p[i], p.fwd[i], parent, name, true);
        }

        /// <summary>
        /// Plaque d'accélération : déclencheur KartLab (BoostStrip) dont on masque les panneaux verts,
        /// avec un visuel unique (chevrons orange) — ou rien si on la pose sur une plaque d'origine de City Slicker.
        /// </summary>
        static void BoostAt(Vector3 pos, Vector3 fwd, Transform parent, string name, bool visual)
        {
            var strip = KLPrefab("BoostStrip");
            if (strip == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(strip, parent);
            go.name = name;
            Vector3 f = Flat(fwd);
            go.transform.SetPositionAndRotation(pos + Vector3.up * (RoadLift + 0.02f), Quaternion.LookRotation(f, Vector3.up));
            foreach (var rd in go.GetComponentsInChildren<Renderer>(true)) rd.enabled = false;
            if (!visual) return;
            var mb = new MB();
            Vector3 r = Vector3.Cross(Vector3.up, f).normalized, c = pos + Vector3.up * (RoadLift + 0.025f);
            const float hw = 2.6f, hl = 3.4f;
            mb.Quad(c - r * hw - f * hl, c - r * hw + f * hl, c + r * hw + f * hl, c + r * hw - f * hl,
                new Vector2(0, 2), new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 2), Vector3.up);   // chevrons dans le sens de la course
            MeshGO(name + "_Visuel", parent, mb, "Boost", false);
        }

        /// <summary>Dans le quartier City Slicker, recentre un point sur la largeur réelle de la chaussée (asphalte du modèle).</summary>
        static Vector3 CenterOnModuleRoad(Vector3 p, Vector3 right)
        {
            if (!InModuleCrop(p, 0f)) return p;
            right = Flat(right);
            float lo = float.MaxValue, hi = float.MinValue;
            bool prev = false; bool seenZeroRun = false;
            for (float o = -16f; o <= 16.01f; o += 0.5f)
            {
                bool on = false;
                foreach (var h in Physics.RaycastAll(p + right * o + Vector3.up * 2.5f, Vector3.down, 5f, 1, QueryTriggerInteraction.Ignore))
                    if (IsModuleCollider(h.collider) && h.collider.name.Contains("asphalt")) { on = true; break; }
                if (on && !prev && o <= 0f) { lo = o; seenZeroRun = false; }
                if (on) { if (o >= 0f) seenZeroRun = true; hi = o; }
                if (!on && prev && seenZeroRun) break;
                prev = on;
            }
            if (lo == float.MaxValue || hi < 0f || hi - lo < 4f) return p;
            return p + right * ((lo + hi) * 0.5f);
        }

        static int itemSerial;
        static void PlaceItemRow(PathData p, int i, Transform parent, float[] offsets)
        {
            string[] kinds = { "MushroomBox", "StarBox", "GreenShellBox", "BananaBox", "IceFlowerBox" };
            Vector3 f = Flat(p.fwd[i]);
            Vector3 c = CenterOnModuleRoad(p.p[i], p.right[i]);
            foreach (float off in offsets)
            {
                var prefab = KLPrefab(kinds[itemSerial % kinds.Length]);
                if (prefab == null) return;
                var box = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
                box.name = $"BoiteObjet_Variante_{itemSerial++:00}";
                box.transform.SetPositionAndRotation(c + p.right[i] * off + Vector3.up * (RoadLift + 0.1f), Quaternion.LookRotation(f));
                var zone = box.GetComponent<TrackZone>();
                if (zone != null) zone.fixedItem = ItemKind.None;
            }
        }

        // ------------------------------------------------------------------ viaduc routier au-dessus de la descente de la Grande Arche

        static void BuildViaduct(PathData path, Transform root)
        {
            var parent = Group("Viaduc_Circulation (voitures au-dessus de la piste)", root);
            var frame = new GameObject("Viaduc").transform;
            frame.SetParent(parent, false);
            frame.SetPositionAndRotation(A(ViaductS), Quaternion.LookRotation(NNorth));   // z local = le long du viaduc, x local = vers l'est
            float L = ViaductHalfLen, Y = ViaductY;
            var deck = new MB();
            deck.Box(new Vector3(-6.5f, Y - 1.3f, -L), new Vector3(6.5f, Y, L), 4f);
            deck.Box(new Vector3(-6.5f, Y, -L), new Vector3(-6f, Y + 1.1f, L), 4f);
            deck.Box(new Vector3(6f, Y, -L), new Vector3(6.5f, Y + 1.1f, L), 4f);
            var deckGO = MeshGO("Viaduc_Tablier", frame, deck, "Concrete", true);
            var lane = new MB();
            lane.Quad(new Vector3(-6f, Y + 0.02f, -L), new Vector3(-6f, Y + 0.02f, L), new Vector3(6f, Y + 0.02f, L), new Vector3(6f, Y + 0.02f, -L),
                new Vector2(0, -L / 8f), new Vector2(0, L / 8f), new Vector2(2, L / 8f), new Vector2(2, -L / 8f), Vector3.up);
            MeshGO("Viaduc_Chaussee", frame, lane, "Asphalt", false);
            // portails aux deux bouts : les voitures entrent / sortent d'immeubles-tunnels
            var ends = new MB();
            foreach (float s in new[] { -1f, 1f })
            {
                ends.Box(new Vector3(-16f, 0f, s > 0 ? L : -L - 26f), new Vector3(-6.5f, Y + 14f, s > 0 ? L + 26f : -L), 6f);
                ends.Box(new Vector3(6.5f, 0f, s > 0 ? L : -L - 26f), new Vector3(16f, Y + 14f, s > 0 ? L + 26f : -L), 6f);
                ends.Box(new Vector3(-6.5f, Y + 5f, s > 0 ? L : -L - 26f), new Vector3(6.5f, Y + 14f, s > 0 ? L + 26f : -L), 6f);
                ends.Box(new Vector3(-6.5f, 0f, s > 0 ? L + 2f : -L - 26f), new Vector3(6.5f, Y - 1.3f, s > 0 ? L + 26f : -L - 2f), 6f);
            }
            MeshGO("Viaduc_Portails", frame, ends, "TowerBrick", true);
            // piles, sauf sur la piste
            float trackZ = frame.InverseTransformPoint(path.p[path.Nearest(A(ViaductS, 3f))]).z;
            for (float z = -L + 12f; z < L - 6f; z += 26f)
            {
                Vector3 w = frame.TransformPoint(new Vector3(0f, 0f, z));
                if (Mathf.Abs(z - trackZ) < 15f || RoadDistance(w) < Edge + 4f) continue;
                Prim(PrimitiveType.Cylinder, "Pile", frame, new Vector3(0f, (Y - 1.3f) * 0.5f, z), new Vector3(3f, (Y - 1.3f) * 0.5f, 3f), "Concrete", null, true);
            }
            // lampadaires sur le parapet
            for (float z = -L + 20f; z < L; z += 40f)
                foreach (float x in new[] { -6.25f, 6.25f })
                {
                    Prim(PrimitiveType.Cylinder, "Mat", frame, new Vector3(x, Y + 3f, z), new Vector3(0.15f, 2f, 0.15f), "DarkMetal");
                    Prim(PrimitiveType.Sphere, "Lampe", frame, new Vector3(x, Y + 5.1f, z), Vector3.one * 0.6f, "LampHead");
                }
            Text3D(frame, "A14 · LA DÉFENSE", new Vector3(6.6f, Y - 0.65f, trackZ), Quaternion.LookRotation(Vector3.left), 0.9f, Color.white);
            Text3D(frame, "A14 · NANTERRE", new Vector3(-6.6f, Y - 0.65f, trackZ), Quaternion.LookRotation(Vector3.right), 0.9f, Color.white);
            // circulation : 2 voies dans chaque sens
            string[] models = { "sedan", "taxi", "suv", "van", "hatchback-sports", "police", "sedan-sports", "suv-luxury", "delivery", "truck" };
            float[] lanes = { -4.2f, -1.5f, 1.5f, 4.2f };
            for (int k = 0; k < 14; k++)
            {
                float x = lanes[k % lanes.Length];
                bool north = x > 0f;
                Vector3 a = new Vector3(x, Y + 0.05f, north ? -L - 4f : L + 4f), b = new Vector3(x, Y + 0.05f, north ? L + 4f : -L - 4f);
                var car = Prop(CK + models[k % models.Length] + ".obj", frame, frame.TransformPoint(a), frame.rotation * Quaternion.Euler(0f, north ? 0f : 180f, 0f), 1.8f, false, false);
                if (car == null) continue;
                var mover = car.gameObject.AddComponent<TrafficMover>();
                mover.localA = a; mover.localB = b;
                mover.speed = Rnd(14f, 22f);
                mover.phase = (k / (float)lanes.Length * 0.27f + Rnd(0f, 0.25f)) % 1f;
                car.localPosition = Vector3.Lerp(a, b, mover.phase);
            }
        }

        // ------------------------------------------------------------------ obstacles mobiles et accessoires FastMesh

        static Transform MovingObstacle(string prefabPath, Transform parent, PathData path, int i, float height, float yawInFrame, Vector3 offset, float period, float phase, string name)
        {
            var holder = new GameObject(name).transform;
            holder.SetParent(parent, false);
            holder.SetPositionAndRotation(path.p[i] + Vector3.up * RoadLift, Quaternion.LookRotation(Flat(path.fwd[i])));
            var mobile = new GameObject("Mobile").transform;
            mobile.SetParent(holder, false);
            var model = PropH(prefabPath, mobile, holder.position, holder.rotation * Quaternion.Euler(0f, yawInFrame, 0f), height, false);
            if (model == null) { Object.DestroyImmediate(holder.gameObject); return null; }
            Bounds nb = NativeBounds(prefabPath);
            var col = model.gameObject.AddComponent<BoxCollider>();
            col.center = nb.center; col.size = nb.size;
            var rb = mobile.gameObject.AddComponent<Rigidbody>();
            rb.isKinematic = true; rb.useGravity = false;
            var mv = mobile.gameObject.AddComponent<PingPongMover>();
            mv.localOffset = offset; mv.period = period; mv.phase = phase;
            return holder;
        }

        /// <summary>Accessoire dégommable : déclencheur + composant Knockable (s'envole, ralentit le kart, revient).</summary>
        static Transform KnockableProp(string prefabPath, Transform parent, Vector3 groundPos, Quaternion rot, float height, string name)
        {
            if (LoadProp(prefabPath) == null) return null;
            var box = new GameObject(name).transform;
            box.SetParent(parent, false);
            box.SetPositionAndRotation(groundPos + Vector3.up * RoadLift, rot);
            var model = PropH(prefabPath, box, box.position, rot, height, false);
            if (model == null) { Object.DestroyImmediate(box.gameObject); return null; }
            Bounds nb = NativeBounds(prefabPath);
            float s = height / Mathf.Max(nb.size.y, 1e-4f);
            var col = box.gameObject.AddComponent<BoxCollider>();
            col.center = new Vector3(0f, nb.size.y * s * 0.5f, 0f);
            col.size = new Vector3(Mathf.Max(0.5f, nb.size.x * s), nb.size.y * s, Mathf.Max(0.5f, nb.size.z * s));
            box.gameObject.AddComponent<Knockable>();
            return box;
        }

        /// <summary>Ralentisseur : visuel FastMesh aplati + bosse arrondie invisible pour la physique (0,2 m de haut, 3 m de long).</summary>
        static void SpeedBump(PathData path, int i, Transform parent)
        {
            const float H = 0.2f, Lb = 3f;
            Vector3 c = path.p[i] + Vector3.up * RoadLift, f = Flat(path.fwd[i]), r = Vector3.Cross(Vector3.up, f).normalized;
            float hw = RoadHalf + CurbW;
            var mb = new MB();
            const int seg = 8;
            for (int k = 0; k < seg; k++)
            {
                float t0 = k / (float)seg, t1 = (k + 1) / (float)seg;
                float y0 = H * Mathf.Sin(Mathf.PI * t0), y1 = H * Mathf.Sin(Mathf.PI * t1);
                Vector3 a0 = c + f * (Lb * (t0 - 0.5f)) + Vector3.up * y0, a1 = c + f * (Lb * (t1 - 0.5f)) + Vector3.up * y1;
                mb.Quad(a0 - r * hw, a1 - r * hw, a1 + r * hw, a0 + r * hw, new Vector2(0, t0), new Vector2(0, t1), new Vector2(6, t1), new Vector2(6, t0), Vector3.up);
            }
            var bump = MeshGO("Ralentisseur", parent, mb, "Asphalt", true);
            string pf = FM + "SpeedBump.prefab";
            if (LoadProp(pf) == null || bump == null) return;
            Bounds nb = NativeBounds(pf);
            float yaw = AcrossYaw(pf);
            float across = yaw == 0f ? nb.size.x : nb.size.z, along = yaw == 0f ? nb.size.z : nb.size.x;
            var vis = Prop(pf, bump.transform, c, Quaternion.LookRotation(f) * Quaternion.Euler(0f, yaw, 0f), 1f);
            if (vis == null) return;
            float sx = 2f * RoadHalf / Mathf.Max(0.1f, across), sz = Lb * 0.9f / Mathf.Max(0.1f, along), sy = (H + 0.02f) / Mathf.Max(0.01f, nb.size.y);
            vis.localScale = yaw == 0f ? new Vector3(sx, sy, sz) : new Vector3(sz, sy, sx);
            vis.position = c + Vector3.up * (-nb.min.y * sy + 0.005f) - (vis.rotation * Vector3.Scale(new Vector3(nb.center.x, 0f, nb.center.z), vis.localScale));
            bump.GetComponent<MeshRenderer>().enabled = false;   // on voit le ralentisseur FastMesh, la bosse ne sert qu'à la physique
        }

        /// <summary>Le point au sol est-il sous une portion de route surélevée (pont de la boucle, rampe…) ?</summary>
        static bool UnderBridge(PathData path, Vector3 q)
        {
            for (int j = 0; j < path.Count; j += 3)
            {
                if (path.p[j].y < q.y + 3.5f) continue;
                float dx = path.p[j].x - q.x, dz = path.p[j].z - q.z;
                if (dx * dx + dz * dz < 16f * 16f) return true;
            }
            return false;
        }

        /// <summary>Lacet de 0 ou 90° pour que la plus grande dimension (x ou z) du prefab soit en travers de la route.</summary>
        static float AcrossYaw(string prefabPath)
        {
            var b = NativeBounds(prefabPath);
            return b.size.x >= b.size.z ? 0f : 90f;
        }

        static void BuildObstaclesAndRoadProps(PathData path, Transform root)
        {
            var parent = Group("Obstacles_et_Accessoires_Route (FastMesh)", root);
            int n = path.Count;

            // 1. chicane en slalom : barrières de chantier fixes, qu'on peut dégommer (ça coûte un peu de vitesse)
            {
                int i0 = path.Nearest(N(-376f, -54f)), i1 = path.Nearest(N(-432f, -48f));
                int k = 0;
                for (int i = i0; i < i1; i += 11, k++)
                    KnockableProp(FM + "RoadBarrier-1.prefab", parent, path.p[i] + path.right[i] * ((k % 2 == 0 ? 1f : -1f) * 3.4f), Quaternion.LookRotation(Flat(path.fwd[i])) * Quaternion.Euler(0f, AcrossYaw(FM + "RoadBarrier-1.prefab"), 0f), 1.15f, $"Barriere_Chantier_{k}");
            }

            // 2. camion-poubelle qui change de voie entre le campus et le Pouce (il reste toujours un passage)
            {
                int i = path.Nearest(new Vector3(41f, 0f, 160f));
                MovingObstacle(CK + "garbage-truck.obj", parent, path, i, 3.4f, 0f, new Vector3(2.5f, 0f, 0f), 6f, 0f, "Camion_Poubelle");
            }

            // 3. plots dégommables en quinconce dans la descente de la boucle
            {
                int k = 0;
                foreach (var sp in new[] { N(-298f, -16f), N(-303f, -32f), N(-314f, -45f), N(-330f, -54f) })
                {
                    int i = path.Nearest(sp);
                    float side = k++ % 2 == 0 ? 1f : -1f;
                    for (int c = 0; c < 3; c++)
                        KnockableProp(CK + "cone.obj", parent, path.p[i] + path.right[i] * side * (1.8f + c * 1.6f) + Flat(path.fwd[i]) * (c * 0.8f), Quaternion.identity, 0.85f, $"Plot_{k}_{c}");
                }
            }

            // 4. ralentisseurs en sortie du quartier : bosse arrondie basse, on roule dessus (petit saut)
            foreach (float s in new[] { 204f, 186f })
                SpeedBump(path, path.Nearest(A(s)), parent);

            // 5. décor routier (lampadaires, panneaux, bornes, feux) le long des routes au sol
            string[] lamps = { FM + "LampPost-1.prefab", FM + "LampPost-2.prefab" };
            string[] signs = { FM + "RoadSigns-1.prefab", FM + "RoadSigns-2.prefab", FM + "RoadSigns-3.prefab", FM + "StreetSign-1.prefab", FM + "DirectionalSign.prefab" };
            int placed = 0;
            for (int i = 60, j = 0; i < n - 30; i += 38, j++)
            {
                if (!OnGround(path, i)) continue;
                float side = j % 2 == 0 ? 1f : -1f;
                Vector3 outDir = Flat(path.right[i]) * side;
                Vector3 q = path.p[i] + outDir * (Edge + 1.8f); q.y = 0f;
                if (NearMonument(q) || ModuleBlocked(q, 2f) || InBarrierGap(q) || UnderBridge(path, q)) continue;
                string pf = j % 3 == 0 ? signs[(j / 3) % signs.Length] : lamps[j % 2];
                PropH(pf, parent, q, Quaternion.LookRotation(-outDir), pf.Contains("Lamp") ? 6f : 3f);
                placed++;
            }
            // bornes et panneaux triangulaires aux entrées des raccourcis
            if (altCPath != null)
                foreach (int i in new[] { 6, altCPath.Count - 8 })
                    foreach (float side in new[] { -1f, 1f })
                        PropH(FM + "RoadBollard-1.prefab", parent, altCPath.p[i] + altCPath.right[i] * side * 5f, Quaternion.identity, 1f);
            Debug.Log($"[DeVinci Kart] Accessoires FastMesh : {placed} lampadaires / panneaux.");
        }
    }
}
