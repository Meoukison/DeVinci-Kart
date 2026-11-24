using KartLab;
using UnityEngine;

namespace DeVinciKart
{
    /// <summary>
    /// Mini-carte en temps réel : le tracé (texture générée par l'éditeur) + une flèche qui suit le kart.
    /// Affichée à l'écran (coin bas gauche) ET sur un petit écran GPS dans le cockpit (visible en VR).
    /// </summary>
    public class TrackMinimap : MonoBehaviour
    {
        public Texture2D map;
        [Tooltip("Axes monde de la carte : u = horizontal (gauche → droite), v = vertical (bas → haut).")]
        public Vector3 axisU = Vector3.right, axisV = Vector3.forward;
        public Vector2 min, max;               // bornes (u, v) en mètres couvertes par la texture
        public bool showOnScreen = true, showCockpitPanel;   // petit écran dans le cockpit : désactivé
        public float screenWidth = 460f;
        public Vector3 cockpitLocalPos = new Vector3(0.5f, 0.98f, 0.85f);
        public float cockpitWidth = 0.3f, cockpitTilt = 25f, cockpitYaw = 28f;

        KartMotor kart;
        Texture2D arrow;
        Transform panel, marker;

        void Start()
        {
            kart = FindAnyObjectByType<KartMotor>();
            arrow = MakeArrow(64);
            if (showCockpitPanel && kart && map) BuildPanel();
        }

        Vector3 KartPos => kart.Body ? kart.Body.position : kart.transform.position;

        public Vector2 UV(Vector3 p) => new Vector2(
            Mathf.InverseLerp(min.x, max.x, Vector3.Dot(p, axisU)),
            Mathf.InverseLerp(min.y, max.y, Vector3.Dot(p, axisV)));

        /// <summary>Cap du kart sur la carte, en degrés, sens trigonométrique depuis l'axe u.</summary>
        float Heading()
        {
            Vector3 f = kart.transform.forward;
            return Mathf.Atan2(Vector3.Dot(f, axisV), Vector3.Dot(f, axisU)) * Mathf.Rad2Deg;
        }

        void BuildPanel()
        {
            float w = cockpitWidth, h = w * map.height / (float)map.width;
            panel = new GameObject("MiniCarte_Cockpit").transform;
            panel.SetParent(kart.transform, false);
            panel.localPosition = cockpitLocalPos;
            panel.localRotation = Quaternion.Euler(cockpitTilt, cockpitYaw, 0f);   // tourné vers le pilote, à droite du volant

            var back = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(back.GetComponent<Collider>());
            back.name = "Fond";
            back.transform.SetParent(panel, false);
            back.transform.localPosition = new Vector3(0f, 0f, 0.003f);
            back.transform.localScale = new Vector3(w + 0.025f, h + 0.025f, 1f);
            back.GetComponent<MeshRenderer>().material = Mat(Texture2D.whiteTexture, new Color(0.05f, 0.07f, 0.12f, 0.85f));

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(quad.GetComponent<Collider>());
            quad.name = "Carte";
            quad.transform.SetParent(panel, false);
            quad.transform.localScale = new Vector3(w, h, 1f);
            quad.GetComponent<MeshRenderer>().material = Mat(map, Color.white);

            var m = GameObject.CreatePrimitive(PrimitiveType.Quad);
            DestroyImmediate(m.GetComponent<Collider>());
            m.name = "Kart";
            marker = m.transform;
            marker.SetParent(panel, false);
            marker.localScale = Vector3.one * w * 0.075f;
            m.GetComponent<MeshRenderer>().material = Mat(arrow, Color.white);
        }

        static Material Mat(Texture tex, Color c)
        {
            var mat = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, color = c };
            mat.renderQueue = 3100;
            return mat;
        }

        void LateUpdate()
        {
            if (!kart || !marker || !map) return;
            Vector2 uv = UV(KartPos);
            float w = cockpitWidth, h = w * map.height / (float)map.width;
            marker.localPosition = new Vector3((uv.x - 0.5f) * w, (uv.y - 0.5f) * h, -0.004f);
            marker.localRotation = Quaternion.Euler(0f, 0f, Heading() - 90f);
        }

        void OnGUI()
        {
            if (!showOnScreen || !map || !kart || KartLab.KartRaceSkills.CinematicActive) return;
            float w = Mathf.Min(screenWidth, Screen.width * 0.32f), h = w * map.height / (float)map.width;
            var r = new Rect(16f, Screen.height - h - 16f, w, h);
            GUI.DrawTexture(r, map, ScaleMode.StretchToFill, true);
            Vector2 uv = UV(KartPos);
            Vector2 c = new Vector2(r.x + uv.x * w, r.y + (1f - uv.y) * h);
            float s = Mathf.Max(18f, w * 0.06f);
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(90f - Heading(), c);
            GUI.DrawTexture(new Rect(c.x - s / 2f, c.y - s / 2f, s, s), arrow);
            GUI.matrix = saved;
        }

        /// <summary>Flèche rouge cerclée de blanc, pointe vers le haut.</summary>
        static Texture2D MakeArrow(int n)
        {
            var t = new Texture2D(n, n, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color32[n * n];
            Vector2 a = new Vector2(0.5f, 0.95f), b = new Vector2(0.12f, 0.1f), c = new Vector2(0.88f, 0.1f), d = new Vector2(0.5f, 0.32f);
            for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / n, (y + 0.5f) / n);
                bool inside = InTri(p, a, b, d) || InTri(p, a, d, c);
                float edge = Mathf.Min(Mathf.Min(SegDist(p, a, b), SegDist(p, b, d)), Mathf.Min(SegDist(p, d, c), SegDist(p, c, a)));
                Color32 col = new Color32(0, 0, 0, 0);
                if (inside) col = edge < 0.07f ? new Color32(255, 255, 255, 255) : new Color32(230, 30, 40, 255);
                px[y * n + x] = col;
            }
            t.SetPixels32(px); t.Apply();
            return t;
        }

        static bool InTri(Vector2 p, Vector2 a, Vector2 b, Vector2 c)
        {
            float d1 = Cross(p, a, b), d2 = Cross(p, b, c), d3 = Cross(p, c, a);
            bool neg = d1 < 0 || d2 < 0 || d3 < 0, pos = d1 > 0 || d2 > 0 || d3 > 0;
            return !(neg && pos);
        }
        static float Cross(Vector2 p, Vector2 a, Vector2 b) => (p.x - b.x) * (a.y - b.y) - (a.x - b.x) * (p.y - b.y);
        static float SegDist(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a; float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).magnitude;
        }
    }
}
