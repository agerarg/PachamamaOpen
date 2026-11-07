using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using static HeyHeyThere.PSXLookFree.Shapes;

namespace HeyHeyThere.PSXLookFree
{
    /// <summary>
    /// The demo level, made in URP's own Lit and Unlit materials as any project's would be: a ruined
    /// courtyard at dusk, with torches, a pool and a floating gem over an altar. <see cref="Build"/>
    /// makes it in the editor (the scene keeps it); PSXDemo converts it on Play. The camera tours it,
    /// or walks with <see cref="move"/> and turns with <see cref="look"/>.
    /// </summary>
    public class World : MonoBehaviour
    {
        const float Torch = 6f;  // URP's inverse-square falloff, lit about as Godot's 2.2 at a few metres

        [Tooltip("Seconds into the tour.")]
        public float t;
        public bool touring = true;
        public Camera cam;
        [Tooltip("The level's textures, found by name.")]
        public Texture2D[] art;
        public Shader sky;

        [HideInInspector] public Transform gem;
        [HideInInspector] public Renderer water;
        [HideInInspector] public Light[] torches;
        [HideInInspector] public Material skyMaterial;

        /// <summary>This frame's walk (x right, y forward) and turn (screen pixels dragged, y down).</summary>
        [System.NonSerialized] public Vector2 move, look;

        float yaw, pitch;
        readonly Dictionary<string, Material> lit = new Dictionary<string, Material>();
        readonly List<Light> lights = new List<Light>();

        void Start() => Ambient();

        void Update()
        {
            if (touring && (move != Vector2.zero || look != Vector2.zero))
            {
                touring = false;
                var e = cam.transform.eulerAngles;
                yaw = e.y;
                pitch = Mathf.DeltaAngle(0f, e.x);
            }
            if (!touring)
            {
                yaw += look.x * 0.29f;
                pitch = Mathf.Clamp(pitch + look.y * 0.29f, -69f, 69f);
                cam.transform.rotation = Quaternion.Euler(pitch, yaw, 0f);
                var dir = Quaternion.Euler(0f, yaw, 0f) * new Vector3(move.x, 0f, move.y);
                var p = cam.transform.position + (move != Vector2.zero ? dir.normalized * Time.deltaTime * 4f : Vector3.zero);
                cam.transform.position = new Vector3(p.x, 1.7f, p.z);
            }
            look = Vector2.zero;
            Pose(t + Time.deltaTime);
        }

        /// <summary>
        /// Everything that moves, at <paramref name="time"/> seconds: the camera on its tour (while
        /// touring), the gem, the torches' flicker and the water.
        /// </summary>
        public void Pose(float time)
        {
            t = time;
            if (touring)
            {
                // up and down the aisle between the pillars, facing the altar and looking about
                float a = t * 0.25f;
                cam.transform.position = G(Mathf.Sin(a) * 1.2f, 1.7f + Mathf.Sin(t * 1.3f) * 0.05f, 2f + Mathf.Cos(a) * 4.5f);
                cam.transform.LookAt(G(Mathf.Sin(t * 0.37f) * 6f, 1.3f, -9f));
            }
            gem.localRotation = Quaternion.Euler(0f, -t * 1.2f * Mathf.Rad2Deg, 0f);
            gem.localPosition = new Vector3(gem.localPosition.x, 2.4f + Mathf.Sin(t * 1.8f) * 0.15f, gem.localPosition.z);
            for (int i = 0; i < torches.Length; i++)
                torches[i].intensity = Torch * (2.2f + Mathf.Sin(t * 11f + i * 2f) * 0.25f + Mathf.Sin(t * 17f + i) * 0.15f) / 2.2f;
            water.sharedMaterial.mainTextureOffset = new Vector2(t * 0.05f, t * 0.03f);
        }

        /// <summary>Makes the level under this object, and the scene's sky, sun, ambient and fog.</summary>
        [ContextMenu("Rebuild")]
        public void Build()
        {
            for (int i = transform.childCount - 1; i >= 0; i--)
                DestroyImmediate(transform.GetChild(i).gameObject);
            lit.Clear();
            lights.Clear();
            Environment();
            Add(Ground(40, 40, 2f), Lit("grass"), Vector3.zero);
            for (int i = 0; i < 7; i++)
                Add(Box(new Vector3(2.4f, 0.06f, 2.4f), 2.4f), Lit("stone"), G(0, 0, 6 - i * 2.4f));
            Walls();
            foreach (int side in new[] { -1, 1 })
                for (int i = 0; i < 4; i++)
                {
                    var at = G(side * 3.2f, 0, 5 - i * 3.6f);
                    bool broken = (i + side + 3) % 3 == 0;
                    Add(Pillar(0.42f, broken ? 1.9f : 3.6f, 8, 1.5f), Lit("stone"), at);
                    Add(Box(new Vector3(1.1f, 0.3f, 1.1f), 1.1f), Lit("stone"), at);
                    if (!broken && (i + (side > 0 ? 1 : 0)) % 2 == 1)
                        MakeTorch(at + Vector3.up * 3.6f);
                }
            var crates = new[] { (G(-7.5f, 0, 6.5f), 0f), (G(-6.4f, 0, 7f), 0.4f), (G(-7f, 1.2f, 6.8f), 0.2f), (G(7.2f, 0, -2f), 0.7f), (G(6.8f, 0, 3.8f), 0.1f) };
            foreach (var (at, turn) in crates)
                Add(Box(Vector3.one * 1.2f, 0f), Lit("crate"), at, turn);
            foreach (var at in new[] { G(-7, 0, -6), G(7.5f, 0, 8), G(-8, 0, 1), G(8, 0, -8.5f) })
                Tree(at);
            Pool(G(6.5f, 0, 1));
            Altar(G(0, 0, -9));
            torches = lights.ToArray();
            cam.fieldOfView = 70f;
            cam.nearClipPlane = 0.05f;
            cam.clearFlags = CameraClearFlags.Skybox;
            Pose(t);
        }

        void Environment()
        {
            skyMaterial = new Material(sky) { name = "Demo Sky" };
            RenderSettings.skybox = skyMaterial;
            RenderSettings.ambientMode = AmbientMode.Flat;
            RenderSettings.ambientLight = (new Color(0.42f, 0.34f, 0.56f).linear * 0.8f).gamma;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.46f, 0.3f, 0.42f);
            RenderSettings.fogStartDistance = 5f;
            RenderSettings.fogEndDistance = 30f;
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.transform.SetParent(transform, false);
            sun.type = LightType.Directional;
            sun.transform.rotation = Quaternion.LookRotation(new Vector3(0.79f, -0.41f, 0.46f));  // Godot's (-24, -60, 0) degrees
            sun.color = new Color(1f, 0.66f, 0.46f);
            sun.intensity = 0.9f;
            sun.shadows = LightShadows.None;
            RenderSettings.sun = sun;
            Ambient();
        }

        /// <summary>The flat ambient as URP reads it, from the probe: a scene with no baked lighting may have none.</summary>
        static void Ambient()
        {
            var sh = new SphericalHarmonicsL2();
            sh.AddAmbientLight(QualitySettings.activeColorSpace == ColorSpace.Linear ? RenderSettings.ambientLight.linear : RenderSettings.ambientLight);
            RenderSettings.ambientProbe = sh;
        }

        /// <summary>Fog on or off, and the sky's haze with it.</summary>
        public void Fog(bool on)
        {
            RenderSettings.fog = on;
            skyMaterial.SetFloat("_FogMix", on ? 0.4f : 0f);
        }

        void Walls()
        {
            // centre x, z, length, height, along x
            var walls = new[] { (-6f, 10f, 8f, 3.2f, true), (6.5f, 10f, 7f, 2.2f, true), (0f, -12.5f, 20f, 4f, true),
                                (-10f, 3f, 12f, 3f, false), (-10f, -9f, 7f, 1.6f, false), (10f, -4f, 17f, 3.4f, false) };
            foreach (var (x, z, length, height, alongX) in walls)
                Add(Box(alongX ? new Vector3(length, height, 0.8f) : new Vector3(0.8f, height, length), 2f), Lit("brick"), G(x, 0, z));
        }

        void MakeTorch(Vector3 at)
        {
            Add(Box(new Vector3(0.5f, 0.25f, 0.5f), 0.5f), Lit("planks"), at);
            Add(Cross(0.35f, 0.55f), Unlit(null, new Color(1f, 0.62f, 0.2f)), at + Vector3.up * 0.25f);
            var light = new GameObject("Torch Light").AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.localPosition = at + Vector3.up * 0.6f;
            light.type = LightType.Point;
            light.color = new Color(1f, 0.6f, 0.3f);
            light.range = 7f;
            light.intensity = Torch;
            light.shadows = LightShadows.None;
            lights.Add(light);
        }

        void Tree(Vector3 at)
        {
            Add(Pillar(0.25f, 2.6f, 6, 1f), Lit("bark"), at);
            if (!PSX.Has(PSX.Kind.Cutout))  // the free sampler's trees go bare
                return;
            var leaves = Lit("leaves", shared: false);
            leaves.SetFloat("_AlphaClip", 1f);
            leaves.SetFloat("_Cull", (float)CullMode.Off);
            leaves.EnableKeyword("_ALPHATEST_ON");
            leaves.renderQueue = (int)RenderQueue.AlphaTest;
            for (int i = 0; i < 5; i++)
            {
                float a = 2f * Mathf.PI * i / 5;
                Add(Cross(2.4f, 2f), leaves, at + G(Mathf.Cos(a) * 0.7f, 1.9f + i % 2 * 0.7f, Mathf.Sin(a) * 0.7f), a);
            }
            Add(Cross(2.2f, 2f), leaves, at + Vector3.up * 2.8f);
        }

        void Pool(Vector3 at)
        {
            var rims = new[] { (G(0, 0, -2.2f), new Vector3(4.4f, 0.5f, 0.5f)), (G(0, 0, 2.2f), new Vector3(4.4f, 0.5f, 0.5f)),
                               (G(-2.2f, 0, 0), new Vector3(0.5f, 0.5f, 3.9f)), (G(2.2f, 0, 0), new Vector3(0.5f, 0.5f, 3.9f)) };
            foreach (var (offset, size) in rims)
                Add(Box(size, 1f), Lit("stone"), at + offset);
            var m = Lit("water", shared: false);
            m.SetColor("_BaseColor", new Color(1f, 1f, 1f, 0.8f));
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            m.SetFloat("_ZWrite", 0f);
            m.SetOverrideTag("RenderType", "Transparent");
            m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            m.renderQueue = (int)RenderQueue.Transparent;
            water = Add(Ground(3.9f, 4, 1.5f), m, at + Vector3.up * 0.35f).GetComponent<Renderer>();
        }

        void Altar(Vector3 at)
        {
            Add(Box(new Vector3(3.2f, 0.4f, 2.2f), 1f), Lit("stone"), at);
            Add(Box(new Vector3(1.8f, 0.9f, 1.2f), 1f), Lit("stone"), at + Vector3.up * 0.4f);
            Add(Box(new Vector3(1.8f, 0.02f, 1.2f), 0f), Unlit(Art("rune"), Color.white), at + Vector3.up * 1.3f);
            var chrome = Urp("Lit");
            chrome.SetColor("_BaseColor", new Color(0.7f, 0.95f, 1f));
            chrome.SetFloat("_Metallic", 1f);
            chrome.SetFloat("_Smoothness", 0.85f);
            gem = Add(Gem(0.45f, 1.3f), chrome, at + Vector3.up * 2.4f).transform;
        }

        Material Lit(string texture, bool shared = true)
        {
            if (shared && lit.TryGetValue(texture, out var m))
                return m;
            m = Urp("Lit");
            m.name = texture;
            m.SetTexture("_BaseMap", Art(texture));
            m.SetFloat("_Smoothness", 0.1f);
            if (shared)
                lit[texture] = m;
            return m;
        }

        Material Unlit(Texture2D texture, Color color)
        {
            var m = Urp("Unlit");
            m.SetTexture("_BaseMap", texture);
            m.SetColor("_BaseColor", color);
            return m;
        }

        static Material Urp(string shader) => new Material(Shader.Find($"Universal Render Pipeline/{shader}"));

        Texture2D Art(string name) => System.Array.Find(art, a => a && a.name == name);

        GameObject Add(Mesh mesh, Material material, Vector3 at, float turn = 0f)
        {
            var go = new GameObject(mesh.name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(transform, false);
            go.transform.localPosition = at;
            go.transform.localRotation = Quaternion.Euler(0f, -turn * Mathf.Rad2Deg, 0f);  // Godot's radians, mirrored
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = material;
            r.shadowCastingMode = ShadowCastingMode.Off;
            return go;
        }
    }
}
