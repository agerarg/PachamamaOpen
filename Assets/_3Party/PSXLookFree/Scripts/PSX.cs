using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace HeyHeyThere.PSXLookFree
{
    /// <summary>
    /// PS1-style materials: make one, or turn a whole level's materials into them.
    /// <code>
    /// PSX.Convert(level);                                  // every mesh under level
    /// crate.sharedMaterial = PSX.Make(PSX.Kind.Lit, crateTexture);
    /// PSX.SetFloat(level, PSX.Affine, 0f);                 // on every PSX material under level
    /// </code>
    /// </summary>
    public static class PSX
    {
        /// <summary>Material kinds, each a shader in Resources/PSX Look Free/.</summary>
        public enum Kind { Lit, Unlit, Cutout, Transparent, Chrome }

        /// <summary>The shaders' folder under Resources, and their names' prefix.</summary>
        public const string Folder = "PSX Look Free";

        public static readonly int BaseMap = Shader.PropertyToID("_BaseMap"), BaseColor = Shader.PropertyToID("_BaseColor"),
            UVScroll = Shader.PropertyToID("_UVScroll"), SnapPixels = Shader.PropertyToID("_SnapPixels"),
            Affine = Shader.PropertyToID("_Affine"), VertexColors = Shader.PropertyToID("_VertexColors"),
            Cutoff = Shader.PropertyToID("_Cutoff"), ReflectionTex = Shader.PropertyToID("_ReflectionTex"),
            Reflection = Shader.PropertyToID("_Reflection");

        static readonly Dictionary<Kind, Shader> shaders = new Dictionary<Kind, Shader>();

        public static bool Has(Kind kind) => Find(kind) != null;

        /// <summary>Whether PSXScreen's dither and colour reduction are installed.</summary>
        public static bool HasScreen => Resources.Load<Shader>($"{Folder}/Screen") != null;

        /// <summary>A PSX material of <paramref name="kind"/>, or lit where that kind isn't installed.</summary>
        public static Material Make(Kind kind = Kind.Lit, Texture texture = null, Color? color = null)
        {
            kind = Has(kind) ? kind : Kind.Lit;
            var m = new Material(Find(kind)) { name = $"PSX {kind}" };
            m.SetColor(BaseColor, color ?? Color.white);
            if (texture)
                m.SetTexture(BaseMap, texture);
            if (kind == Kind.Chrome)
                m.SetTexture(ReflectionTex, Resources.Load<Texture2D>($"{Folder}/Chrome"));
            return m;
        }

        /// <summary>
        /// The PSX material closest to any other (URP's, Built-in's): an unlit shader becomes unlit, alpha
        /// clipping or the AlphaTest queue cutout, a transparent queue transparent, metallic over 0.5
        /// chrome. The main texture with its tiling and offset, the colour and the alpha cutoff carry over.
        /// </summary>
        public static Material FromMaterial(Material source)
        {
            var kind = Kind.Lit;
            if (source.shader.name.Contains("Unlit"))
                kind = Kind.Unlit;
            else if (source.renderQueue >= (int)RenderQueue.Transparent)
                kind = Kind.Transparent;
            else if (source.renderQueue >= (int)RenderQueue.AlphaTest || Float(source, "_AlphaClip") > 0.5f)
                kind = Kind.Cutout;
            else if (Float(source, "_Metallic") > 0.5f)
                kind = Kind.Chrome;
            var color = source.HasProperty(BaseColor) ? source.GetColor(BaseColor) : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
            var m = Make(kind, source.mainTexture, color);
            if (source.mainTexture)
            {
                m.SetTextureScale(BaseMap, source.mainTextureScale);
                m.SetTextureOffset(BaseMap, source.mainTextureOffset);
            }
            if (source.HasProperty(Cutoff))
                m.SetFloat(Cutoff, source.GetFloat(Cutoff));
            m.name = $"{source.name} (PSX {kind})";
            return m;
        }

        /// <summary>
        /// Swaps every material on the MeshRenderers and SkinnedMeshRenderers under <paramref name="root"/>
        /// for its PSX material, one per source material, with vertex colours on for the meshes that have
        /// them. Returns how many it made.
        /// </summary>
        public static int Convert(GameObject root)
        {
            var made = new Dictionary<Material, Material>();
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
            {
                var mesh = r is SkinnedMeshRenderer s ? s.sharedMesh
                    : r is MeshRenderer && r.TryGetComponent(out MeshFilter f) ? f.sharedMesh : null;
                if (mesh == null)
                    continue;
                bool colors = mesh.HasVertexAttribute(VertexAttribute.Color);
                var mats = r.sharedMaterials;
                for (int i = 0; i < mats.Length; i++)
                {
                    if (mats[i] == null || IsPSX(mats[i]))
                        continue;
                    if (!made.TryGetValue(mats[i], out var m))
                        made[mats[i]] = m = FromMaterial(mats[i]);
                    if (colors)
                        m.SetFloat(VertexColors, 1f);
                    mats[i] = m;
                }
                r.sharedMaterials = mats;
            }
            return made.Count;
        }

        /// <summary>
        /// Sets a float on every PSX material under <paramref name="root"/>, for an options menu say. It
        /// changes the materials themselves: a material asset's change stays after Play in the editor.
        /// </summary>
        public static void SetFloat(GameObject root, int property, float value)
        {
            foreach (var r in root.GetComponentsInChildren<Renderer>(true))
                foreach (var m in r.sharedMaterials)
                    if (IsPSX(m))
                        m.SetFloat(property, value);
        }

        public static bool IsPSX(Material m) => m != null && m.shader.name.StartsWith(Folder + "/");

        static Shader Find(Kind kind)
        {
            if (!shaders.TryGetValue(kind, out var s) || s == null)
                shaders[kind] = s = Resources.Load<Shader>($"{Folder}/{kind}");
            return s;
        }

        static float Float(Material m, string property) => m.HasProperty(property) ? m.GetFloat(property) : 0f;
    }
}
