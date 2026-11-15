using System.Collections.Generic;
using KartLab;
using UnityEditor;
using UnityEngine;

namespace DeVinciKart.EditorTools
{
    /// <summary>
    /// Habillage de la piste : accessoires « Track Environment » (BEDRILL), kits Kenney (tribunes, paddock, voitures, arbres),
    /// public animé, circulation sur le boulevard, montgolfières, tours de plusieurs styles, tremplin, volant DeVinci.
    /// </summary>
    public static partial class CampusTrackGenerator
    {
        const string Bed = "Assets/BEDRILL/Track_Environment_Free/Prefabs/";
        const string RK = "Assets/ThirdParty/racing-kit/Models/OBJ format/";
        const string CK = "Assets/ThirdParty/car-kit/Models/OBJ format/";
        const string PK = "Assets/ThirdParty/platformer-kit/Models/OBJ format/";
        const float Edge = RoadHalf + CurbW + BarrierT;   // bord extérieur des barrières (7,5 m de l'axe)

        static readonly Dictionary<string, GameObject> propCache = new Dictionary<string, GameObject>();
        static readonly Dictionary<string, Bounds> propBounds = new Dictionary<string, Bounds>();
        static readonly List<Vector3> reserved = new List<Vector3>();     // x, rayon, z
        static readonly List<Vector2> coarsePath = new List<Vector2>();
        static System.Random prng = new System.Random(7);
        static Vector3 finishPoint;
        static int crowdCount, meshSerial;

        static void ResetProps(PathData path)
        {
            propCache.Clear(); propBounds.Clear(); reserved.Clear(); coarsePath.Clear();
            prng = new System.Random(7);
            crowdCount = 0; meshSerial = 0;
            if (path == null) return;
            for (int i = 0; i < path.Count; i += 2) coarsePath.Add(new Vector2(path.p[i].x, path.p[i].z));
            finishPoint = path.p[Mathf.Max(0, path.Count - 16)];
        }

        static float Rnd(float a, float b) => a + (float)prng.NextDouble() * (b - a);
        static Vector3 Flat(Vector3 v) { v.y = 0f; return v.sqrMagnitude > 1e-8f ? v.normalized : Vector3.forward; }

        static bool IsReserved(float x, float z, float margin)
        {
            foreach (var r in reserved)
            {
                float dx = r.x - x, dz = r.z - z, rr = r.y + margin;
                if (dx * dx + dz * dz < rr * rr) return true;
            }
            return false;
        }

        static void Reserve(Vector3 p, float radius) => reserved.Add(new Vector3(p.x, radius, p.z));

        static float RoadDistance(Vector3 p)
        {
            float best = float.MaxValue;
            foreach (var c in coarsePath)
            {
                float d = (c.x - p.x) * (c.x - p.x) + (c.y - p.z) * (c.y - p.z);
                if (d < best) best = d;
            }
            return Mathf.Sqrt(best);
        }

        /// <summary>Zones occupées par les monuments (indépendamment de la route).</summary>
        // ---- mode « piste 2 » (DeVinci City) : les monuments sont décrits par des cercles (x, z, rayon)
        static bool cityMode;
        static readonly List<Vector3> cityBlockers = new List<Vector3>();

        static bool CityBlocked(Vector3 p, float margin = 0f)
        {
            foreach (var c in cityBlockers)
            {
                float dx = c.x - p.x, dz = c.z - p.z, r = c.y + margin;
                if (dx * dx + dz * dz < r * r) return true;
            }
            return false;
        }

        static bool LandmarkBlocked(Vector3 p)
        {
            if (cityMode) return CityBlocked(p);
            Vector3 h = hallW2L.MultiplyPoint3x4(p);
            if (Mathf.Abs(h.x) < 62f && Mathf.Abs(h.z) < 50f) return true;                                   // ancien campus
            if (new Vector2(p.x - NewCampusPos.x, p.z - NewCampusPos.z).magnitude < 105f) return true;       // nouveau campus
            if (new Vector2(p.x, p.z).magnitude < 92f) return true;                                          // Grande Arche
            Vector3 b = bridgeW2L.MultiplyPoint3x4(p);
            if (Mathf.Abs(b.x) < TrenchHalfLen + 9f && Mathf.Abs(b.z) < TrenchHalfW + 5f) return true;       // tranchée
            if (ModuleBlocked(p) || ViaductBlocked(p)) return true;                                          // quartier City Slicker, viaduc
            if (new Vector2(p.x - finishPoint.x, p.z - finishPoint.z).magnitude < 46f) return true;          // pizzeria / kebab / podium
            if (new Vector2(p.x - PoucePos.x, p.z - PoucePos.z).magnitude < 14f) return true;                // Le Pouce
            Vector3 lk = Quaternion.Euler(0, -20f, 0) * (p - (ParkCenter + new Vector3(-5f, 0, 15f)));
            if ((lk.x * lk.x) / (78f * 78f) + (lk.z * lk.z) / (43f * 43f) < 1f) return true;                 // lac
            return false;
        }

        /// <summary>Le rectangle (centre c, axes f / o) est-il libre : ni route, ni monument, ni autre décor ?</summary>
        static bool RectFree(Vector3 c, Vector3 f, Vector3 o, float halfAlong, float halfLat)
        {
            for (int a = -2; a <= 2; a++)
            for (int l = -2; l <= 2; l++)
            {
                Vector3 q = c + f * (halfAlong * a / 2f) + o * (halfLat * l / 2f);
                if (RoadDistance(q) < Edge + 1f || LandmarkBlocked(q) || IsReserved(q.x, q.z, 1f)) return false;
            }
            return true;
        }

        // ------------------------------------------------------------------ accessoires (prefabs)

        static GameObject LoadProp(string path)
        {
            if (propCache.TryGetValue(path, out var cached)) return cached;
            var go = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (go == null) Warnings.Add($"Accessoire introuvable : {path}");
            propCache[path] = go;
            return go;
        }

        static Bounds NativeBounds(string path)
        {
            if (propBounds.TryGetValue(path, out var b)) return b;
            var prefab = LoadProp(path);
            b = new Bounds(Vector3.up * 0.5f, Vector3.one);
            if (prefab != null)
            {
                var tmp = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
                b = WorldBounds(tmp);
                Object.DestroyImmediate(tmp);
            }
            propBounds[path] = b;
            return b;
        }

        /// <summary>
        /// Instancie un prefab sous un conteneur : le conteneur porte position / rotation / échelle,
        /// le modèle garde son repère d'origine (on ne touche jamais à la racine importée).
        /// </summary>
        static Transform Prop(string path, Transform parent, Vector3 pos, Quaternion rot, float scale = 1f, bool mirror = false, bool isStatic = true)
        {
            var prefab = LoadProp(path);
            if (prefab == null) return null;
            var holder = new GameObject(prefab.name).transform;
            holder.SetParent(parent, false);
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, holder);
            holder.SetPositionAndRotation(pos, rot);
            holder.localScale = new Vector3(mirror ? -scale : scale, scale, scale);
            foreach (var c in go.GetComponentsInChildren<Collider>(true)) c.enabled = false;   // décor : pas de collision
            if (isStatic)
                foreach (var r in holder.GetComponentsInChildren<Renderer>(true))
                    GameObjectUtility.SetStaticEditorFlags(r.gameObject, StaticEditorFlags.BatchingStatic);
            return holder;
        }

        /// <summary>Comme Prop, mais mis à la hauteur voulue et posé (centre bas) sur groundPos, quel que soit le pivot du modèle.</summary>
        static Transform PropH(string path, Transform parent, Vector3 groundPos, Quaternion rot, float height, bool isStatic = true)
        {
            if (LoadProp(path) == null) return null;
            Bounds b = NativeBounds(path);
            float s = height / Mathf.Max(b.size.y, 1e-4f);
            Vector3 pivot = new Vector3(b.center.x, b.min.y, b.center.z) * s;
            return Prop(path, parent, groundPos - rot * pivot, rot, s, false, isStatic);
        }

        static Transform Group(string name, Transform parent)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            return t;
        }

        // ------------------------------------------------------------------ virages

        /// <summary>Rayon de courbure (m) et sens (+1 = virage à droite) en chaque point ; float.MaxValue hors zone utile.</summary>
        static float[] TurnRadius(PathData path, out int[] dir)
        {
            int n = path.Count;
            var radius = new float[n];
            dir = new int[n];
            for (int i = 0; i < n; i++)
            {
                radius[i] = float.MaxValue;
                if (i < 6 || i >= n - 6) continue;
                float ang = Vector3.SignedAngle(Flat(path.fwd[i - 5]), Flat(path.fwd[i + 5]), Vector3.up);
                dir[i] = ang >= 0f ? 1 : -1;
                float rad = Mathf.Abs(ang) * Mathf.Deg2Rad;
                if (rad > 1e-3f) radius[i] = 10f / rad;
            }
            return radius;
        }

        static bool OnGround(PathData path, int i) =>
            i >= 0 && i < path.Count && ZoneAt(path.p[i]) == Zone.Normal && path.p[i].y < 0.2f && Mathf.Abs(path.fwd[i].y) < 0.02f;

        static bool NearMonument(Vector3 p) => cityMode ? CityBlocked(p) :
            new Vector2(p.x - PoucePos.x, p.z - PoucePos.z).magnitude < 11.5f ||
            ModuleBlocked(p, 3f) || ViaductBlocked(p) ||
            OldCampusFootprint(p);

        static bool OldCampusFootprint(Vector3 p)
        {
            Vector3 h = hallW2L.MultiplyPoint3x4(p);
            return Mathf.Abs(h.x) < 52f && Mathf.Abs(h.z) < 40f;
        }

        // ------------------------------------------------------------------ accessoires de piste (BEDRILL Track Environment)

        static void BuildTrackProps(PathData path, int finishIdx, Transform root)
        {
            var parent = Group("Decor_Piste (Track Environment)", root);
            var signs = Group("Fleches_Virage", parent);
            var tyres = Group("Piles_Pneus", parent);
            var arrows = Group("Fleches_Sol", parent);
            var arches = Group("Arches", parent);
            var lights = Group("Lampadaires", parent);
            var flags = Group("Drapeaux", parent);
            int n = path.Count;
            float[] radius = TurnRadius(path, out int[] dir);
            var tight = new bool[n];
            for (int i = 0; i < n; i++) tight[i] = radius[i] < 45f && OnGround(path, i);

            // 1. Flèches de virage (Sign) + piles de pneus (Tire) à l'extérieur des virages serrés.
            //    Le panneau montre sa face avant (+Z) à la route ; sa flèche pointe vers -X : on le met en miroir dans les virages à gauche.
            int lastSign = -100, lastTyre = -100;
            for (int i = 0; i < n; i++)
            {
                if (!tight[i]) continue;
                Vector3 outDir = Flat(path.right[i]) * (dir[i] > 0 ? -1f : 1f);
                Vector3 p = path.p[i]; p.y = 0f;
                if (i - lastSign >= 9)
                {
                    Vector3 q = p + outDir * (Edge + 2.4f);
                    if (!NearMonument(q))
                    {
                        Prop(Bed + "Sign_free_obs.prefab", signs, q, Quaternion.LookRotation(-outDir), 1.7f, dir[i] < 0);
                        lastSign = i;
                    }
                }
                if (i - lastTyre >= 3)
                {
                    Vector3 q = p + outDir * (Edge + 0.8f);
                    if (!NearMonument(q))
                    {
                        Prop(Bed + "Tire_free_obs.prefab", tyres, q + Vector3.up * 0.37f * 1.25f, Quaternion.Euler(0, Rnd(0, 360), 0), 1.25f);
                        lastTyre = i;
                    }
                }
            }

            // 2. Flèches au sol : flèche coudée avant chaque virage serré, flèche droite dans les lignes droites.
            //    Les flèches du pack pointent vers -Z ; la flèche coudée part vers +X (donc vers la gauche du pilote) : miroir à droite.
            for (int i = 20; i < n; i++)
            {
                if (!tight[i]) continue;
                bool start = true;
                for (int k = 1; k <= 14 && start; k++) if (tight[i - k]) start = false;
                if (!start) continue;
                int j = i - 18;
                if (!OnGround(path, j) || !OnGround(path, j - 4) || !OnGround(path, j + 4)) continue;
                foreach (float lat in new[] { -3f, 3f })
                    Prop(Bed + "Arrow_01_free_obs.prefab", arrows, path.p[j] + path.right[j] * lat + Vector3.up * (RoadLift + 0.03f),
                        Quaternion.LookRotation(-Flat(path.fwd[j])), 1.2f, dir[i] > 0);
            }
            for (int i = 45; i < n - 30; i += 55)
            {
                bool straight = true;
                for (int k = -26; k <= 26 && straight; k++) if (!OnGround(path, i + k) || radius[Mathf.Clamp(i + k, 0, n - 1)] < 90f) straight = false;
                if (!straight) continue;
                foreach (float lat in new[] { -3f, 3f })
                    Prop(Bed + "Arrow_02_free_obs.prefab", arrows, path.p[i] + path.right[i] * lat + Vector3.up * (RoadLift + 0.03f),
                        Quaternion.LookRotation(-Flat(path.fwd[i])), 1.1f);
            }

            // 3. Arches vertes et blanches au-dessus de la piste, tous les ~200 m.
            for (int i = 150; i < n - 70; i += 200)
            {
                for (int k = 0; k <= 40; k++)
                {
                    int j = i + (k % 2 == 0 ? k / 2 : -(k + 1) / 2);
                    if (j < 30 || j >= n - 30 || !OnGround(path, j) || !OnGround(path, j - 3) || !OnGround(path, j + 3) || radius[j] < 60f) continue;
                    Vector3 p = path.p[j]; p.y = 0f;
                    Vector3 r = Flat(path.right[j]);
                    if (NearMonument(p + r * 8.6f) || NearMonument(p - r * 8.6f) || UnderBridge(path, p)) continue;
                    Prop(Bed + "Arch_01_free_obs.prefab", arches, p, Quaternion.LookRotation(-Flat(path.fwd[j])), 1.15f);
                    break;
                }
            }

            // 4. Départ : arche treillis + feux tricolores (dans le hall).
            if (!cityMode)
            {
            Vector3 hf = Flat(Hall(0, 1) - Hall(0, 0));
            var startArch = Prop(Bed + "Arch_for_banner_free_obs.prefab", parent, Hall(DoorLx, 13.6f, 0.03f), Quaternion.LookRotation(-hf), 1f);
            if (startArch != null)
            {
                startArch.name = "Arche_Depart";
                var tl = Prop(Bed + "Traffic_light_free_obs.prefab", parent, startArch.TransformPoint(new Vector3(0, 3.25f, 0.19f)), startArch.rotation, 1f);
                if (tl != null) tl.name = "Feux_Depart";
            }
            }

            // 5. Arrivée : arche treillis + bannière FINISH.
            {
                Vector3 p = path.p[finishIdx]; p.y = 0f;
                var arch = Prop(Bed + "Arch_for_banner_free_obs.prefab", parent, p, Quaternion.LookRotation(-Flat(path.fwd[finishIdx])), 1.15f);
                if (arch != null)
                {
                    arch.name = "Arche_Arrivee";
                    var banner = Prop(Bed + "Arch_banner_finish_free_obs.prefab", parent, arch.TransformPoint(new Vector3(0, 3.36f, 0.115f)), arch.rotation, 1.15f);
                    if (banner != null) banner.name = "Banniere_Finish";
                    if (cityMode)
                    {
                        var tl = Prop(Bed + "Traffic_light_free_obs.prefab", parent, arch.TransformPoint(new Vector3(0, 4.4f, -0.2f)), arch.rotation, 1.15f);
                        if (tl != null) tl.name = "Feux_Depart";
                    }
                }
            }

            // 6. Lampadaires (bras tourné vers la route), en quinconce.
            for (int i = 35, k = 0; i < n - 8; i += 30, k++)
            {
                if (!OnGround(path, i)) continue;
                Vector3 outDir = Flat(path.right[i]) * (k % 2 == 0 ? 1f : -1f);
                Vector3 q = path.p[i] + outDir * (Edge + 1.4f); q.y = 0f;
                if (NearMonument(q)) continue;
                Prop(Bed + "Pole_light_free_obs.prefab", lights, q, Quaternion.LookRotation(-outDir), 1f);
            }

            // 7. Drapeaux à damier : sortie du campus et dernière ligne droite.
            for (int i = 0; i < n; i += 9)
            {
                if (!OnGround(path, i)) continue;
                bool nearStart = i < 150, nearFinish = i > finishIdx - 110 && i < finishIdx - 12;
                if (!nearStart && !nearFinish) continue;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 outDir = Flat(path.right[i]) * side;
                    Vector3 q = path.p[i] + outDir * (Edge + 0.9f); q.y = 0f;
                    if (NearMonument(q)) continue;
                    Prop(Bed + "Flag_free_obs.prefab", flags, q, Quaternion.LookRotation(Flat(path.fwd[i])), 1.15f);
                }
            }
        }

        // ------------------------------------------------------------------ tremplin (intégré à la chaussée)

        /// <summary>Tremplin sur toute la largeur de la piste, sous la Grande Arche : entrée à fleur de route, sortie à 1,6 m.</summary>
        static void BuildKicker(PathData path, Transform root) => BuildKickerAt(path, root, path.Nearest(A(38f, ArchFloor)), "Tremplin_Arche");

        static void BuildKickerAt(PathData path, Transform root, int i0, string name, int len = 9, float h = 1.6f, float hw = -1f)
        {
            var parent = Group(name, root);
            if (i0 < 0 || i0 + len + 1 >= path.Count) return;
            if (hw <= 0f) hw = RoadHalf + CurbW;
            System.Func<int, float, float, Vector3> P = (k, lat, dy) => path.p[i0 + k] + path.right[i0 + k] * lat + Vector3.up * (RoadLift + dy);
            System.Func<int, float> Y = k => 0.012f + h * (k / (float)len) * (k / (float)len);

            var top = new MB(); var sides = new MB();
            for (int k = 0; k < len; k++)
            {
                float y0 = Y(k), y1 = Y(k + 1);
                top.Quad(P(k, -hw, y0), P(k + 1, -hw, y1), P(k + 1, hw, y1), P(k, hw, y0),
                    new Vector2(0, k / 3f), new Vector2(0, (k + 1) / 3f), new Vector2(1, (k + 1) / 3f), new Vector2(1, k / 3f), Vector3.up);
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 inward = -path.right[i0 + k] * side;
                    Vector3 a = P(k, side * hw, -0.1f), b = P(k + 1, side * hw, -0.1f), c = P(k + 1, side * hw, y1 + 0.9f), d = P(k, side * hw, y0 + 0.9f);
                    var ua = new Vector2(k / 4f, 0); var ub = new Vector2((k + 1) / 4f, 0); var uc = new Vector2((k + 1) / 4f, 1); var ud = new Vector2(k / 4f, 1);
                    sides.Quad(a, b, c, d, ua, ub, uc, ud, inward);
                    sides.Quad(a, b, c, d, ua, ub, uc, ud, -inward);
                }
            }
            Vector3 f = path.fwd[i0 + len];
            sides.Quad(P(len, -hw, -0.1f), P(len, hw, -0.1f), P(len, hw, Y(len)), P(len, -hw, Y(len)),
                new Vector2(0, 0), new Vector2(3.5f, 0), new Vector2(3.5f, 1), new Vector2(0, 1), f);
            MeshGO("Tremplin_Rampe", parent, top, "Boost", true);
            MeshGO("Tremplin_Flancs", parent, sides, "Barrier", true);
        }

        // ------------------------------------------------------------------ volant DeVinci

        /// <summary>Variante du kart KartLab dont le volant (cockpit + vue extérieure) est remplacé par VolantDevinciKart.glb.</summary>
        static GameObject BuildKartVariant()
        {
            var basePrefab = KLPrefab("PlayerKart");
            if (basePrefab == null) return null;
            var wheelModel = FindModel("VolantDevinciKart");
            if (wheelModel == null)
            {
                Warnings.Add("VolantDevinciKart.glb introuvable dans Assets/3DModels : le volant d'origine est conservé.");
                return null;
            }
            if (!AssetDatabase.IsValidFolder("Assets/DeVinciKart/Prefabs")) AssetDatabase.CreateFolder("Assets/DeVinciKart", "Prefabs");

            var inst = (GameObject)PrefabUtility.InstantiatePrefab(basePrefab);
            var cockpit = inst.GetComponentInChildren<KartCockpit>(true);
            if (cockpit == null || cockpit.wheel == null)
            {
                Object.DestroyImmediate(inst);
                Warnings.Add("KartCockpit.wheel introuvable : le volant d'origine est conservé.");
                return null;
            }
            // Le volant d'origine fait 0,36 m ; le modèle Tripo fait 1 m, base à y = 0, face avant côté +Z (d'où le demi-tour).
            const float s = 0.37f;
            foreach (var pivot in new[] { cockpit.wheel, cockpit.exteriorWheel })
            {
                if (pivot == null) continue;
                for (int c = 0; c < pivot.childCount; c++) pivot.GetChild(c).gameObject.SetActive(false);
                var holder = new GameObject("Volant_DeVinci").transform;
                holder.SetParent(pivot, false);
                holder.localRotation = Quaternion.Euler(0f, 180f, 0f);
                holder.localScale = Vector3.one * s;
                holder.localPosition = new Vector3(0f, -0.495f * s, 0.03f);
                PrefabUtility.InstantiatePrefab(wheelModel, holder);
                foreach (var t in holder.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = pivot.gameObject.layer;
            }
            var saved = PrefabUtility.SaveAsPrefabAsset(inst, "Assets/DeVinciKart/Prefabs/PlayerKart_DeVinci.prefab");
            Object.DestroyImmediate(inst);
            return saved;
        }

        // ------------------------------------------------------------------ public (personnages low-poly animés)

        static void OrientedBox(MB mb, Vector3 c, Vector3 r, Vector3 u, Vector3 f, Vector3 half, Vector2 uv)
        {
            Vector3 x = r * half.x, y = u * half.y, z = f * half.z;
            mb.Quad(c - x - y - z, c + x - y - z, c + x + y - z, c - x + y - z, uv, uv, uv, uv, -f);
            mb.Quad(c - x - y + z, c + x - y + z, c + x + y + z, c - x + y + z, uv, uv, uv, uv, f);
            mb.Quad(c - x - y - z, c - x - y + z, c - x + y + z, c - x + y - z, uv, uv, uv, uv, -r);
            mb.Quad(c + x - y - z, c + x - y + z, c + x + y + z, c + x + y - z, uv, uv, uv, uv, r);
            mb.Quad(c - x + y - z, c + x + y - z, c + x + y + z, c - x + y + z, uv, uv, uv, uv, u);
            mb.Quad(c - x - y - z, c + x - y - z, c + x - y + z, c - x - y + z, uv, uv, uv, uv, -u);
        }

        /// <summary>Un spectateur (corps, tête, bras parfois levés), couleurs prises dans la palette « Crowd ».</summary>
        static void AddPerson(MB mb, Vector3 foot, Vector3 facing, bool seated)
        {
            Vector3 f = Flat(facing), u = Vector3.up, r = Vector3.Cross(u, f);
            float scale = Rnd(0.9f, 1.12f);
            float bodyH = (seated ? 0.5f : 0.95f) * scale;
            Vector2 shirt = new Vector2((prng.Next(6) + 0.5f) / 8f, 0.5f);
            Vector2 skin = new Vector2((6 + prng.Next(2) + 0.5f) / 8f, 0.5f);
            OrientedBox(mb, foot + u * (bodyH * 0.5f), r, u, f, new Vector3(0.21f, bodyH * 0.5f, 0.13f) * 1f, shirt);
            OrientedBox(mb, foot + u * (bodyH + 0.15f * scale), r, u, f, new Vector3(0.13f, 0.14f, 0.13f) * scale, skin);
            if (prng.Next(3) == 0)   // bras levés
                foreach (float side in new[] { -1f, 1f })
                    OrientedBox(mb, foot + u * (bodyH + 0.2f * scale) + r * side * 0.3f, r, u, f, new Vector3(0.05f, 0.24f, 0.05f) * scale, shirt);
            crowdCount++;
        }

        /// <summary>Crée les objets animés (2 groupes qui sautillent à des rythmes différents) à partir des maillages de public.</summary>
        static void FlushCrowd(MB[] groups, string name, Transform parent)
        {
            for (int g = 0; g < groups.Length; g++)
            {
                if (groups[g].Empty) continue;
                var go = MeshGO($"{name}_{meshSerial++:000}", parent, groups[g], "Crowd", false);
                GameObjectUtility.SetStaticEditorFlags(go, 0);
                var sp = go.AddComponent<Spinner>();
                sp.degreesPerSecond = Vector3.zero;
                sp.bobAmplitude = 0.07f + 0.04f * g;
                sp.bobFrequency = 1.3f + 0.55f * g + Rnd(0f, 0.3f);
            }
        }

        static bool RayNearest(List<MeshCollider> cols, Vector3 origin, Vector3 dir, float max, out RaycastHit best)
        {
            best = default(RaycastHit);
            float bd = float.MaxValue; bool any = false;
            var ray = new Ray(origin, dir);
            foreach (var c in cols)
            {
                if (c.Raycast(ray, out RaycastHit hit, max) && hit.distance < bd) { bd = hit.distance; best = hit; any = true; }
            }
            return any;
        }

        /// <summary>
        /// Assoit des spectateurs sur les gradins d'une tribune : on lance des rayons depuis l'avant, rangée par rangée,
        /// pour trouver chaque marche, puis un rayon vers le bas pour poser le personnage.
        /// </summary>
        static int PopulateStand(Transform stand, Vector3 centre, Vector3 front, float halfWidth, float halfDepth, float height, Transform crowdParent)
        {
            var temp = new List<MeshCollider>();
            foreach (var mf in stand.GetComponentsInChildren<MeshFilter>())
            {
                if (mf.sharedMesh == null) continue;
                var mc = mf.gameObject.AddComponent<MeshCollider>();
                mc.sharedMesh = mf.sharedMesh;
                temp.Add(mc);
            }
            Physics.SyncTransforms();
            front = Flat(front);
            Vector3 right = Vector3.Cross(Vector3.up, front);
            var groups = new[] { new MB(), new MB() };
            int before = crowdCount;
            for (float x = -halfWidth + 0.5f; x <= halfWidth - 0.5f; x += 0.85f)
            {
                float lastD = float.MaxValue;
                for (float yh = 0.55f; yh < height; yh += 0.3f)
                {
                    Vector3 origin = centre + right * x + Vector3.up * yh + front * (halfDepth + 3f);
                    if (!RayNearest(temp, origin, -front, halfDepth * 2f + 6f, out RaycastHit hit)) continue;
                    float d = Vector3.Dot(hit.point - centre, front);
                    if (d > lastD - 0.4f) continue;
                    Vector3 o2 = centre + right * x + Vector3.up * (yh + 0.45f) + front * (d + 0.32f);
                    if (!RayNearest(temp, o2, Vector3.down, 1.5f, out RaycastHit floor) || floor.normal.y < 0.7f) continue;
                    lastD = d;
                    if (prng.Next(100) < 22) continue;   // place vide
                    AddPerson(groups[prng.Next(2)], floor.point, front, true);
                }
            }
            foreach (var mc in temp) Object.DestroyImmediate(mc);
            FlushCrowd(groups, "Public_Tribune", crowdParent);
            return crowdCount - before;
        }

        /// <summary>Public debout derrière des barrières de foule, le long de la piste.</summary>
        static void StandingCrowd(Vector3 p, Vector3 f, Vector3 o, float halfAlong, Transform parent, Transform crowdParent)
        {
            for (float a = -halfAlong + 1.6f; a <= halfAlong - 1.5f; a += 3.25f)
                Prop(Bed + "Barrier_free_obs.prefab", parent, p + o * (Edge + 1.6f) + f * a, Quaternion.LookRotation(o), 1f);
            var groups = new[] { new MB(), new MB() };
            for (int row = 0; row < 3; row++)
                for (float a = -halfAlong + 0.5f; a <= halfAlong - 0.5f; a += 0.75f)
                {
                    if (prng.Next(100) < 30) continue;
                    Vector3 q = p + o * (Edge + 2.5f + row * 0.8f + Rnd(-0.15f, 0.15f)) + f * (a + Rnd(-0.2f, 0.2f));
                    q.y = 0f;
                    AddPerson(groups[prng.Next(2)], q, -o, false);
                }
            FlushCrowd(groups, "Public_Debout", crowdParent);
        }

        // ------------------------------------------------------------------ vie autour de la piste

        static void BuildLife(PathData path, int finishIdx, Transform root)
        {
            var parent = Group("Vie_et_Animation", root);
            var crowd = Group("Public", parent);
            var stands = Group("Tribunes", parent);
            int n = path.Count;
            float[] radius = TurnRadius(path, out int[] dir);
            System.Func<int, bool> Straight = i =>
            {
                for (int k = -9; k <= 9; k += 3) if (!OnGround(path, i + k) || radius[Mathf.Clamp(i + k, 0, n - 1)] < 110f) return false;
                return true;
            };

            // --- Paddock près du départ : stands, monoplaces, tente, radar
            bool paddock = false;
            for (int i = 70; i < 260 && !paddock; i += 4)
                foreach (float side in new[] { 1f, -1f })
                {
                    if (paddock || !Straight(i)) continue;
                    Vector3 p = path.p[i]; p.y = 0f;
                    Vector3 f = Flat(path.fwd[i]), o = Flat(path.right[i]) * side;
                    Vector3 c = p + o * (Edge + 13f);
                    if (!RectFree(c, f, o, 27f, 12f)) continue;
                    var pad = Group("Paddock", parent);
                    var slab = new MB();
                    Vector3 s0 = c - f * 27f - o * 12f, s1 = c + f * 27f - o * 12f, s2 = c + f * 27f + o * 12f, s3 = c - f * 27f + o * 12f;
                    Vector3 up = Vector3.up * 0.04f;
                    slab.Quad(s0 + up, s1 + up, s2 + up, s3 + up, new Vector2(0, 0), new Vector2(9, 0), new Vector2(9, 4), new Vector2(0, 4), Vector3.up);
                    MeshGO("Paddock_Dalle", pad, slab, "Concrete", false);
                    string[] cars = { "raceCarRed", "raceCarGreen", "raceCarOrange", "raceCarWhite" };
                    for (int k = 0; k < 4; k++)
                    {
                        float along = -15f + k * 10f;
                        PropH(RK + (k == 3 ? "pitsOffice.obj" : "pitsGarage.obj"), pad, p + o * (Edge + 18f) + f * along, Quaternion.LookRotation(o), k == 3 ? 5.1f : 7f);
                        PropH(RK + cars[k] + ".obj", pad, p + o * (Edge + 7.5f) + f * along + Vector3.up * 0.04f, Quaternion.LookRotation(-f), 1.25f);
                    }
                    PropH(RK + "tentLong.obj", pad, p + o * (Edge + 6f) + f * -23f, Quaternion.LookRotation(f), 3.6f);
                    PropH(RK + "radarEquipment.obj", pad, p + o * (Edge + 20f) + f * 24f, Quaternion.LookRotation(-o), 5.5f);
                    PropH(RK + "bannerTowerRed.obj", pad, p + o * (Edge + 3f) + f * 26f, Quaternion.LookRotation(f), 8f);
                    PropH(RK + "bannerTowerGreen.obj", pad, p + o * (Edge + 3f) + f * -27f, Quaternion.LookRotation(f), 8f);
                    for (int k = 0; k < 12; k++)
                        Prop(Bed + "Cone_free_obs.prefab", pad, p + o * (Edge + 2.2f) + f * (-20f + k * 3.6f), Quaternion.identity, 1.6f);
                    Reserve(c, 31f);
                    paddock = true;
                }

            // --- Tribunes (pack Track Environment + tribune couverte Kenney), avec spectateurs assis
            float[] spots = { 0.045f, 0.105f, 0.19f, 0.30f, 0.335f, 0.54f, 0.60f, 0.67f, 0.76f, 0.84f, 0.905f, 0.945f };
            int placed = 0;
            for (int sIdx = 0; sIdx < spots.Length; sIdx++)
            {
                int target = Mathf.RoundToInt(spots[sIdx] * (n - 1));
                bool done = false;
                for (int k = 0; k <= 60 && !done; k++)
                {
                    int i = target + (k % 2 == 0 ? k / 2 : -(k + 1) / 2);
                    if (i < 12 || i >= n - 12 || !Straight(i)) continue;
                    foreach (float side in sIdx % 2 == 0 ? new[] { -1f, 1f } : new[] { 1f, -1f })
                    {
                        if (done) continue;
                        Vector3 p = path.p[i]; p.y = 0f;
                        Vector3 f = Flat(path.fwd[i]), o = Flat(path.right[i]) * side;
                        bool kenney = placed % 3 == 2;
                        float halfW = kenney ? 6f : 6.35f, halfD = kenney ? 6.1f : 5.9f;
                        Vector3 c = p + o * (Edge + 3.4f + halfD);
                        if (!RectFree(c, f, o, halfW + 1.5f, halfD + 1f)) continue;
                        Transform t;
                        float h;
                        if (kenney)
                        {
                            // gradins Kenney : les spectateurs regardent vers -Z
                            h = 14.2f;
                            t = PropH(RK + "grandStandCovered.obj", stands, c, Quaternion.LookRotation(o), h);
                        }
                        else
                        {
                            // tribune du pack : face avant = +Z
                            h = 7.8f;
                            t = Prop(Bed + "Tribune_free_obs.prefab", stands, c, Quaternion.LookRotation(-o), 1f);
                        }
                        if (t == null) continue;
                        PopulateStand(t, c, -o, halfW, halfD, h, crowd);
                        for (float a = -halfW + 1.6f; a <= halfW - 1.5f; a += 3.25f)
                            Prop(Bed + "Barrier_free_obs.prefab", stands, p + o * (Edge + 1.5f) + f * a, Quaternion.LookRotation(o), 1f);
                        foreach (float e in new[] { -1f, 1f })
                            Prop(Bed + "Flag_free_obs.prefab", stands, c + f * e * (halfW + 1.2f) - o * (halfD - 0.5f), Quaternion.LookRotation(f), 1.2f);
                        Reserve(c, 9.5f);
                        placed++;
                        done = true;
                    }
                }
            }

            // --- Public debout derrière des barrières
            int crowds = 0;
            for (int i = 60; i < n - 30 && crowds < 14; i += 37)
            {
                if (!OnGround(path, i) || !OnGround(path, i - 6) || !OnGround(path, i + 6)) continue;
                float side = crowds % 2 == 0 ? 1f : -1f;
                Vector3 p = path.p[i]; p.y = 0f;
                Vector3 f = Flat(path.fwd[i]), o = Flat(path.right[i]) * side;
                if (radius[i] < 60f) o = Flat(path.right[i]) * (dir[i] > 0 ? 1f : -1f);   // en virage : à l'intérieur (l'extérieur a les flèches et les pneus)
                if (radius[i] < 30f) continue;
                Vector3 c = p + o * (Edge + 3.2f);
                if (!RectFree(c, f, o, 6.5f, 1.8f)) continue;
                StandingCrowd(p, f, o, 6.5f, stands, crowd);
                Reserve(c, 6.5f);
                crowds++;
            }

            // --- Caméras TV et tours à bannière dans les virages
            var tv = Group("Cameras_TV", parent);
            int lastCam = -200;
            for (int i = 20; i < n - 20; i++)
            {
                if (radius[i] > 40f || !OnGround(path, i) || i - lastCam < 90) continue;
                Vector3 o = Flat(path.right[i]) * (dir[i] > 0 ? 1f : -1f);   // intérieur du virage
                Vector3 q = path.p[i] + o * (Edge + 3.5f); q.y = 0f;
                if (RoadDistance(q) < Edge + 2f || LandmarkBlocked(q) || IsReserved(q.x, q.z, 2f)) continue;
                var plat = new MB();
                plat.Box(q - new Vector3(1.2f, 0, 1.2f), q + new Vector3(1.2f, 1.6f, 1.2f), 3f);
                MeshGO($"Estrade_Camera_{meshSerial++:000}", tv, plat, "DarkMetal", false);
                PropH(RK + "camera_exclusive.obj", tv, q + Vector3.up * 1.6f, Quaternion.LookRotation(o), 2.4f);
                PropH(RK + (lastCam % 2 == 0 ? "bannerTowerGreen.obj" : "bannerTowerRed.obj"), tv, q + Flat(path.fwd[i]) * 4f, Quaternion.LookRotation(o), 8f);
                Reserve(q, 3.5f);
                lastCam = i;
            }

            // --- Parkings (voitures garées)
            string[] models = { "sedan", "taxi", "police", "truck", "van", "suv", "hatchback-sports", "sedan-sports", "delivery", "suv-luxury" };
            int parks = 0;
            for (int i = 120; i < n - 40 && parks < 4; i += 23)
            {
                if (!Straight(i)) continue;
                float side = parks % 2 == 0 ? -1f : 1f;
                Vector3 p = path.p[i]; p.y = 0f;
                Vector3 f = Flat(path.fwd[i]), o = Flat(path.right[i]) * side;
                Vector3 c = p + o * (Edge + 13f);
                if (!RectFree(c, f, o, 15f, 9f)) continue;
                var park = Group("Parking_" + parks, parent);
                var slab = new MB();
                Vector3 up = Vector3.up * 0.04f;
                slab.Quad(c - f * 15f - o * 9f + up, c + f * 15f - o * 9f + up, c + f * 15f + o * 9f + up, c - f * 15f + o * 9f + up,
                    new Vector2(0, 0), new Vector2(5, 0), new Vector2(5, 3), new Vector2(0, 3), Vector3.up);
                MeshGO($"Parking_Dalle_{meshSerial++:000}", park, slab, "Concrete", false);
                for (int row = 0; row < 2; row++)
                    for (int k = 0; k < 9; k++)
                    {
                        if (prng.Next(100) < 25) continue;
                        Vector3 q = c + f * (-13f + k * 3.2f) + o * (row == 0 ? -4.5f : 4.5f) + up;
                        Prop(CK + models[prng.Next(models.Length)] + ".obj", park, q, Quaternion.LookRotation(row == 0 ? o : -o), 1.75f);
                    }
                if (parks == 0) Prop(CK + "firetruck.obj", park, c + f * 17.5f + up, Quaternion.LookRotation(-o), 2.1f);
                if (parks == 1) Prop(CK + "ambulance.obj", park, c - f * 17.5f + up, Quaternion.LookRotation(-o), 1.9f);
                Reserve(c, 19f);
                parks++;
                i += 150;
            }

            // --- Circulation sur le boulevard circulaire (dans la tranchée, sous le pont)
            if (!cityMode)
            {
            var traffic = FrameGO("Circulation_Boulevard", parent, bridgeL2W);
            float[] lanes = { 7f, 14f, 21f, 28f, -7f, -14f, -21f, -28f };
            for (int k = 0; k < 16; k++)
            {
                float z = lanes[k % lanes.Length];
                bool east = z > 0f;
                float x0 = east ? -TrenchHalfLen - 4f : TrenchHalfLen + 4f;
                Vector3 a = new Vector3(x0, -TrenchDepth + 0.05f, z), b = new Vector3(-x0, -TrenchDepth + 0.05f, z);
                string model = k == 5 ? "garbage-truck" : k == 11 ? "delivery" : models[prng.Next(models.Length)];
                var car = Prop(CK + model + ".obj", traffic, traffic.TransformPoint(a), traffic.rotation * Quaternion.Euler(0, east ? 90f : -90f, 0), 1.8f, false, false);
                if (car == null) continue;
                var mover = car.gameObject.AddComponent<TrafficMover>();
                mover.localA = a; mover.localB = b;
                mover.speed = Rnd(13f, 21f);
                mover.phase = (k / lanes.Length * 0.5f + Rnd(0f, 0.45f)) % 1f;
                car.localPosition = Vector3.Lerp(a, b, mover.phase);
            }
            }

            // --- Montgolfières
            var sky = Group("Montgolfieres", parent);
            string[] balloonMats = { "BalloonRed", "BalloonBlue", "BalloonYellow" };
            Vector3[] skySpots = { new Vector3(40, 75, 170), new Vector3(260, 110, -150), new Vector3(-150, 90, -40), new Vector3(-330, 120, 330),
                                new Vector3(-520, 85, 60), new Vector3(120, 140, 330), new Vector3(-760, 100, 300) };
            for (int k = 0; k < skySpots.Length; k++)
            {
                var b = Group("Montgolfiere_" + k, sky);
                b.position = skySpots[k];
                Prim(PrimitiveType.Sphere, "Ballon", b, new Vector3(0, 9f, 0), new Vector3(15f, 18f, 15f), balloonMats[k % 3]);
                Prim(PrimitiveType.Cylinder, "Jupe", b, new Vector3(0, 0.6f, 0), new Vector3(4.5f, 1.4f, 4.5f), balloonMats[(k + 1) % 3]);
                Prim(PrimitiveType.Cube, "Nacelle", b, new Vector3(0, -4.2f, 0), new Vector3(2.6f, 2f, 2.6f), "Bark");
                foreach (var cnr in new[] { new Vector2(1, 1), new Vector2(-1, 1), new Vector2(1, -1), new Vector2(-1, -1) })
                    Prim(PrimitiveType.Cylinder, "Corde", b, new Vector3(cnr.x * 1.25f, -2.2f, cnr.y * 1.25f), new Vector3(0.08f, 1.6f, 0.08f), "DarkMetal");
                var sp = b.gameObject.AddComponent<Spinner>();
                sp.degreesPerSecond = new Vector3(0, Rnd(-4f, 4f), 0);
                sp.bobAmplitude = Rnd(3f, 6f);
                sp.bobFrequency = Rnd(0.02f, 0.05f);
            }

            Debug.Log($"[DeVinci Kart] Vie : {placed} tribunes, {crowds} groupes de public debout, {crowdCount} spectateurs, paddock {(paddock ? "oui" : "non")}, {parks} parkings.");
            if (crowdCount == 0) Warnings.Add("Aucun spectateur placé (les lancers de rayons sur les tribunes n'ont rien touché).");
        }

        // ------------------------------------------------------------------ végétation (kits Kenney)

        static void BuildVegetation(PathData path, Transform parent, System.Random rng, System.Func<float, float, bool> blocked)
        {
            var veg = Group("Vegetation_Kenney", parent);
            int n = path.Count;
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
            System.Func<Vector3, float, bool> Free = (q, clear) =>
                RoadDistance(q) >= clear && !LandmarkBlocked(q) && !IsReserved(q.x, q.z, 2.5f);

            // alignements d'arbres le long de la piste
            for (int i = 20, k = 0; i < n - 10; i += 13, k++)
            {
                if (!OnGround(path, i)) continue;
                foreach (float side in new[] { -1f, 1f })
                {
                    Vector3 q = path.p[i] + Flat(path.right[i]) * side * (Edge + R(5.5f, 7.5f)); q.y = 0f;
                    if (!Free(q, Edge + 4.5f)) continue;
                    bool big = (k + (side > 0 ? 1 : 0)) % 2 == 0;
                    PropH(RK + (big ? "treeLarge.obj" : "treeSmall.obj"), veg, q, Quaternion.Euler(0, R(0, 360), 0), big ? R(9f, 11.5f) : R(6f, 8f));
                }
            }
            // sapins et arbres ronds : parc et bosquets
            string[] kinds = { PK + "tree-pine.obj", PK + "tree.obj", PK + "tree-pine.obj" };
            int placedTrees = 0;
            for (int tries = 0; tries < 4000 && placedTrees < 150; tries++)
            {
                Vector3 q;
                if (tries % 3 == 0 && !cityMode)
                {
                    float ang = R(0, Mathf.PI * 2f), rad = ParkRadius * Mathf.Sqrt(R(0, 1));
                    q = new Vector3(ParkCenter.x + Mathf.Cos(ang) * rad, 0, ParkCenter.z + Mathf.Sin(ang) * rad);
                }
                else
                {
                    int i = rng.Next(n);
                    q = path.p[i] + Flat(path.right[i]) * (rng.Next(2) == 0 ? -1f : 1f) * R(Edge + 9f, 60f); q.y = 0f;
                }
                if (!Free(q, Edge + 8f)) continue;
                PropH(kinds[rng.Next(kinds.Length)], veg, q, Quaternion.Euler(0, R(0, 360), 0), R(6f, 11f));
                placedTrees++;
            }
            // fleurs, plantes et rochers au bord de la piste
            string[] small = { PK + "flowers.obj", PK + "flowers-tall.obj", PK + "plant.obj", PK + "rocks.obj", PK + "flowers.obj" };
            float[] smallH = { 0.45f, 0.9f, 1.1f, 1.3f, 0.45f };
            int placedSmall = 0;
            for (int tries = 0; tries < 3000 && placedSmall < 170; tries++)
            {
                int i = rng.Next(n);
                if (!OnGround(path, i)) continue;
                Vector3 q = path.p[i] + Flat(path.right[i]) * (rng.Next(2) == 0 ? -1f : 1f) * R(Edge + 1.6f, Edge + 12f); q.y = 0f;
                if (!Free(q, Edge + 1.5f)) continue;
                int kind = rng.Next(small.Length);
                PropH(small[kind], veg, q, Quaternion.Euler(0, R(0, 360), 0), smallH[kind] * R(0.9f, 1.4f));
                placedSmall++;
            }
        }

        // ------------------------------------------------------------------ tours : plusieurs styles

        static MB TowerMB(Dictionary<string, MB> mbs, string mat)
        {
            if (!mbs.TryGetValue(mat, out var mb)) mbs[mat] = mb = new MB();
            return mb;
        }

        static void Cyl(Dictionary<string, List<CombineInstance>> lists, string mat, Vector3 basePos, float diameter, float height) =>
            AddCombined(lists, mat, cylMesh, Matrix4x4.TRS(basePos + Vector3.up * height * 0.5f, Quaternion.identity, new Vector3(diameter, height * 0.5f, diameter)));

        static void AddTower(Dictionary<string, MB> mbs, Dictionary<string, List<CombineInstance>> cyl, int style, float x, float z, float w, float d, float h, System.Func<float, float, float> R)
        {
            System.Action<string, float, float, float, float, float, float> Box = (mat, cx, cz, bw, bd, y0, y1) =>
                TowerMB(mbs, mat).Box(new Vector3(cx - bw / 2, y0, cz - bd / 2), new Vector3(cx + bw / 2, y1, cz + bd / 2), 14f, false);
            switch (style)
            {
                case 0:   // tour de verre bleue classique + édicule et antenne
                    Box("Glass", x, z, w, d, 0, h);
                    Box("DarkMetal", x, z, w * 0.6f, d * 0.6f, h, h + 4f);
                    Cyl(cyl, "DarkMetal", new Vector3(x, h + 4f, z), 0.8f, R(12f, 30f));
                    break;
                case 1:   // tour sombre à gradins
                    Box("TowerDark", x, z, w, d, 0, h * 0.55f);
                    Box("TowerDark", x, z, w * 0.76f, d * 0.76f, h * 0.55f, h * 0.84f);
                    Box("TowerDark", x, z, w * 0.5f, d * 0.5f, h * 0.84f, h);
                    Cyl(cyl, "LampHead", new Vector3(x, h, z), 1.2f, 6f);
                    break;
                case 2:   // tour ronde turquoise
                    Cyl(cyl, "TowerRound", new Vector3(x, 0, z), w, h);
                    Cyl(cyl, "DarkMetal", new Vector3(x, h, z), w * 0.55f, 5f);
                    break;
                case 3:   // barre blanche à bandeaux
                    Box("TowerBands", x, z, w * 1.5f, d * 0.55f, 0, h * 0.7f);
                    Box("Concrete", x, z, w * 1.55f, d * 0.6f, h * 0.7f, h * 0.7f + 1.5f);
                    break;
                case 4:   // tours jumelles bronze reliées par une passerelle
                    Box("TowerGold", x - w * 0.36f, z, w * 0.42f, d, 0, h);
                    Box("TowerGold", x + w * 0.36f, z, w * 0.42f, d, 0, h * 0.9f);
                    Box("DarkMetal", x, z, w * 0.4f, d * 0.5f, h * 0.55f, h * 0.62f);
                    break;
                default:  // tour verte effilée + flèche
                    for (int k = 0; k < 5; k++)
                    {
                        float sc = 1f - k * 0.13f;
                        Box("TowerGreen", x, z, w * sc, d * sc, h * k / 5f, h * (k + 1) / 5f);
                    }
                    Cyl(cyl, "DarkMetal", new Vector3(x, h, z), 0.7f, R(15f, 35f));
                    break;
            }
        }

        static void BuildSkyline(Transform parent, System.Random rng, System.Func<float, float, float, bool> nearRoad, System.Func<float, float, bool> blocked)
        {
            System.Func<float, float, float> R = (a, b) => a + (float)rng.NextDouble() * (b - a);
            var mbs = new Dictionary<string, MB>();
            var cyl = new Dictionary<string, List<CombineInstance>>();
            var spots = new List<Vector3>();
            System.Func<float, float, float, bool> Clash = (x, z, r) =>
            {
                foreach (var s in spots) if ((s.x - x) * (s.x - x) + (s.z - z) * (s.z - z) < (s.y + r) * (s.y + r)) return true;
                return false;
            };

            // Quartier d'affaires de La Défense (à l'est de la Grande Arche)
            int nt = 0;
            for (int tries = 0; tries < 4000 && nt < 54; tries++)
            {
                float x = R(110f, 900f), z = R(-440f, 340f);
                float w = R(24f, 46f), d = R(24f, 46f), h = R(70f, 240f);
                float rad = Mathf.Max(w, d) * 0.8f;
                if (nearRoad(x, z, 48f) || blocked(x, z) || Clash(x, z, rad)
                    || new Vector2(x - StatueCenter.x, z - StatueCenter.z).magnitude < 70f
                    || new Vector2(x - PoucePos.x, z - PoucePos.z).magnitude < 48f) continue;
                AddTower(mbs, cyl, nt % 6, x, z, w, d, h, R);
                spots.Add(new Vector3(x, rad, z));
                nt++;
            }

            // Nanterre : immeubles variés + « Tours Nuages » (cylindres pastel près du parc)
            Vector3 nrm = new Vector3(-AxisEast.z, 0, AxisEast.x);
            int nb = 0;
            string[] blockMats = { "Building", "TowerBrick", "TowerBands", "Building", "TowerBrick" };
            for (int tries = 0; tries < 5000 && nb < 80; tries++)
            {
                float s = R(-780f, -270f), side = rng.Next(2) == 0 ? -1f : 1f;
                Vector3 p = A(s) + nrm * side * R(30f, 320f);
                float w = R(20f, 42f), d = R(15f, 30f), h = R(15f, 58f);
                float rad = Mathf.Max(w, d) * 0.75f;
                if (nearRoad(p.x, p.z, 36f) || blocked(p.x, p.z) || Clash(p.x, p.z, rad)
                    || new Vector2(p.x - ParkCenter.x, p.z - ParkCenter.z).magnitude < ParkRadius + 20f) continue;
                string mat = blockMats[nb % blockMats.Length];
                var mb = TowerMB(mbs, mat);
                mb.Box(new Vector3(p.x - w / 2, 0, p.z - d / 2), new Vector3(p.x + w / 2, h, p.z + d / 2), 10f, false);
                if (nb % 4 == 1) mb.Box(new Vector3(p.x - w / 2, h, p.z - d / 4), new Vector3(p.x, h + R(6f, 12f), p.z + d / 4), 10f, false);   // attique
                if (nb % 5 == 3) TowerMB(mbs, "Leaves2").Box(new Vector3(p.x - w / 2 + 1, h, p.z - d / 2 + 1), new Vector3(p.x + w / 2 - 1, h + 0.5f, p.z + d / 2 - 1), 6f, false); // toit végétalisé
                spots.Add(new Vector3(p.x, rad, p.z));
                nb++;
            }
            int nn = 0;
            for (int tries = 0; tries < 2500 && nn < 9; tries++)
            {
                float ang = R(0, Mathf.PI * 2f), dist = ParkRadius + R(35f, 150f);
                float x = ParkCenter.x + Mathf.Cos(ang) * dist, z = ParkCenter.z + Mathf.Sin(ang) * dist;
                if (nearRoad(x, z, 45f) || blocked(x, z) || Clash(x, z, 22f)) continue;
                for (int k = 0; k < 3; k++)
                {
                    float a2 = k * 2.1f + ang;
                    Cyl(cyl, "TowerClouds", new Vector3(x + Mathf.Cos(a2) * 8.5f, 0, z + Mathf.Sin(a2) * 8.5f), R(15f, 19f), R(38f, 95f));
                }
                spots.Add(new Vector3(x, 22f, z));
                nn++;
            }

            foreach (var kv in mbs) MeshGO("Tours_" + kv.Key, parent, kv.Value, kv.Key, false);
            FlushCombined(cyl, "Tours_Rondes", parent);
        }

        // ------------------------------------------------------------------ matériaux supplémentaires

        static void CreateExtraMaterials()
        {
            System.Func<string, Color, Color, Color, float, Texture2D> Facade = (name, frame, glassA, lit, litRate) =>
                Tex(name, 256, 256, (u, v, x, y) =>
                {
                    bool fr = x % 32 < 3 || y % 42 < 4;
                    if (fr) return frame;
                    bool on = Hash(x / 32, y / 42, name.Length * 13 + 5) > 1f - litRate;
                    float n = Hash(x / 32, y / 42, 91) * 0.08f;
                    return on ? lit : new Color(glassA.r + n, glassA.g + n, glassA.b + n);
                });
            var dark = Facade("towerdark", new Color(0.12f, 0.13f, 0.16f), new Color(0.07f, 0.1f, 0.17f), new Color(0.95f, 0.82f, 0.5f), 0.14f);
            var green = Facade("towergreen", new Color(0.75f, 0.8f, 0.78f), new Color(0.12f, 0.42f, 0.38f), new Color(0.55f, 0.85f, 0.78f), 0.2f);
            var gold = Facade("towergold", new Color(0.3f, 0.22f, 0.12f), new Color(0.62f, 0.45f, 0.2f), new Color(0.95f, 0.8f, 0.45f), 0.25f);
            var round = Facade("towerround", new Color(0.85f, 0.9f, 0.92f), new Color(0.15f, 0.5f, 0.62f), new Color(0.6f, 0.88f, 0.95f), 0.2f);
            var bands = Tex("towerbands", 128, 128, (u, v, x, y) =>
            {
                int yy = y % 32;
                if (yy < 14) return new Color(0.93f, 0.93f, 0.9f);
                bool mullion = x % 16 < 2;
                return mullion ? new Color(0.5f, 0.52f, 0.55f) : new Color(0.16f, 0.24f, 0.34f);
            });
            var brick = Tex("towerbrick", 256, 256, (u, v, x, y) =>
            {
                bool win = (x % 64 > 16 && x % 64 < 48) && (y % 64 > 18 && y % 64 < 50);
                if (win) return Hash(x / 64, y / 64, 17) > 0.75f ? new Color(0.95f, 0.85f, 0.55f) : new Color(0.2f, 0.26f, 0.33f);
                bool mortar = y % 8 == 0 || (x + (y / 8 % 2) * 8) % 16 == 0;
                float n = Hash(x / 16, y / 8, 3) * 0.08f;
                return mortar ? new Color(0.7f, 0.66f, 0.6f) : new Color(0.62f + n, 0.33f + n, 0.25f + n);
            });
            var clouds = Tex("towerclouds", 256, 256, (u, v, x, y) =>
            {
                float n = Fbm(u, v, 3, 4, 77), m = Fbm(u, v, 4, 3, 123);
                Color c = n > 0.56f ? new Color(0.96f, 0.96f, 0.95f) : m > 0.6f ? new Color(0.55f, 0.75f, 0.6f) : new Color(0.52f, 0.72f, 0.9f);
                float dx = (x % 32) - 16, dy = (y % 40) - 20;
                if (dx * dx + dy * dy < 20f) c = new Color(0.18f, 0.24f, 0.32f);   // fenêtres rondes
                return c;
            });
            var crowd = Tex("crowd", 64, 8, (u, v, x, y) =>
            {
                Color[] pal = { new Color(0.9f, 0.15f, 0.15f), new Color(0.1f, 0.4f, 0.85f), new Color(0.98f, 0.8f, 0.1f), new Color(0.95f, 0.95f, 0.95f),
                                new Color(0.15f, 0.65f, 0.3f), new Color(0.95f, 0.5f, 0.1f), new Color(0.93f, 0.75f, 0.6f), new Color(0.55f, 0.36f, 0.24f) };
                return pal[Mathf.Clamp(x / 8, 0, 7)];
            });
            System.Func<string, Color, Color, Texture2D> Stripes = (name, a, b) =>
                Tex(name, 128, 32, (u, v, x, y) => (x / 16) % 2 == 0 ? a : b);
            var bRed = Stripes("balloonred", new Color(0.9f, 0.12f, 0.12f), Color.white);
            var bBlue = Stripes("balloonblue", new Color(0.1f, 0.35f, 0.85f), new Color(0.98f, 0.8f, 0.1f));
            var bYellow = Stripes("balloonyellow", new Color(0.98f, 0.75f, 0.1f), new Color(0.2f, 0.65f, 0.3f));

            Mat("TowerDark", Color.white, dark, 0.9f, 0.6f, new Color(0.25f, 0.2f, 0.1f), dark);
            Mat("TowerGreen", Color.white, green, 0.85f, 0.4f);
            Mat("TowerGold", Color.white, gold, 0.9f, 0.7f);
            Mat("TowerBands", Color.white, bands, 0.5f, 0.1f);
            Mat("TowerBrick", Color.white, brick, 0.15f);
            var mr = Mat("TowerRound", Color.white, round, 0.88f, 0.5f);
            mr.SetTextureScale("_BaseMap", new Vector2(8f, 10f));
            var mc = Mat("TowerClouds", Color.white, clouds, 0.25f);
            mc.SetTextureScale("_BaseMap", new Vector2(3f, 3f));
            Mat("Crowd", Color.white, crowd, 0.15f);
            var dirt = Tex("dirt", 128, 128, (u, v, x, y) =>
            {
                float n = Fbm(u, v, 8, 4, 31) * 0.35f + Hash(x, y, 7) * 0.12f;
                return new Color(0.42f + n * 0.5f, 0.29f + n * 0.35f, 0.17f + n * 0.2f);
            });
            Mat("Dirt", Color.white, dirt, 0.05f);
            Mat("BalloonRed", Color.white, bRed, 0.35f).SetTextureScale("_BaseMap", new Vector2(2f, 1f));
            Mat("BalloonBlue", Color.white, bBlue, 0.35f).SetTextureScale("_BaseMap", new Vector2(2f, 1f));
            Mat("BalloonYellow", Color.white, bYellow, 0.35f).SetTextureScale("_BaseMap", new Vector2(2f, 1f));
            AssetDatabase.SaveAssets();
        }

        // ------------------------------------------------------------------ test de la piste

        /// <summary>Lance le jeu avec un pilote automatique qui parcourt la piste et écrit Logs/DeVinciKart_TestPiste.txt.</summary>
        [MenuItem("DeVinci Kart/Tester la piste (pilote auto)")]
        public static void TestTrack() => TestOn("");

        [MenuItem("DeVinci Kart/Tester une variante/Pont en arc avec saut (piste 1)")]
        public static void TestAltBridge() => TestOn("TestRoute_Variante_PontArc");

        [MenuItem("DeVinci Kart/Tester une variante/Tunnel sous la colline (piste 1)")]
        public static void TestAltTunnel() => TestOn("TestRoute_Raccourci_Tunnel");

        [MenuItem("DeVinci Kart/Tester une variante/Piste en terre (piste 1)")]
        public static void TestAltDirt() => TestOn("TestRoute_Raccourci_Terre");

        static void TestOn(string route)
        {
            if (EditorApplication.isPlaying) return;
            SessionState.SetBool(TrackAutopilotTest.SessionKey, true);
            SessionState.SetString(TrackAutopilotTest.RouteKey, route);
            EditorApplication.isPlaying = true;
        }
    }
}
