using UnityEngine;
using UnityEngine.UI;

namespace HeyHeyThere.PSXLookFree
{
    /// <summary>
    /// Draws its camera at a PS1 resolution, scaled up in sharp square pixels, in 15-bit colour with
    /// the PS1's dither. The camera renders to a texture of its own that a hidden canvas shows, under
    /// every other Screen Space - Overlay canvas, so those stay sharp.
    /// </summary>
    [RequireComponent(typeof(Camera)), DisallowMultipleComponent]
    public class PSXScreen : MonoBehaviour
    {
        [Tooltip("Lines of the low-resolution picture, at least: 240 is the PS1's usual, 480 its high mode. The picture " +
                 "is the screen divided by a whole number, so 1080p at 240 lines draws 270.")]
        [Range(120, 1080)] public int lines = 240;
        [Tooltip("The PS1's 4x4 ordered dither.")]
        public bool dither = true;
        [Tooltip("Bits per colour channel: 5 is the PS1's, 8 turns the colour reduction off.")]
        [Range(1, 8)] public int colorBits = 5;

        static readonly int Dither = Shader.PropertyToID("_Dither"), ColorBits = Shader.PropertyToID("_ColorBits");

        Camera cam;
        RenderTexture picture;
        GameObject output;
        RawImage image;
        Material material;
        int scale = 1;

        /// <summary>The low-resolution picture the camera draws to.</summary>
        public RenderTexture Picture => picture;

        void OnEnable()
        {
            cam = GetComponent<Camera>();
            var shader = Resources.Load<Shader>($"{PSX.Folder}/Screen");
            material = shader ? new Material(shader) : null;
            // a camera that draws nothing, so the screen is cleared and the overlay canvases drawn
            output = new GameObject("PSXScreen", typeof(Camera)) { hideFlags = HideFlags.HideAndDontSave };
            var clear = output.GetComponent<Camera>();
            clear.cullingMask = 0;
            clear.clearFlags = CameraClearFlags.SolidColor;
            clear.backgroundColor = Color.black;
            clear.depth = cam.depth + 1;
            var canvas = new GameObject("Canvas", typeof(Canvas)) { hideFlags = HideFlags.HideAndDontSave }.GetComponent<Canvas>();
            canvas.transform.SetParent(output.transform, false);
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = short.MinValue;
            var go = new GameObject("Picture", typeof(RawImage)) { hideFlags = HideFlags.HideAndDontSave };
            go.transform.SetParent(canvas.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;
            image = go.GetComponent<RawImage>();
            image.raycastTarget = false;
            image.material = material;
            LateUpdate();
        }

        void OnDisable()
        {
            if (cam)
                cam.targetTexture = null;
            Destroy(picture);
            Destroy(output);
            Destroy(material);
            picture = null;
        }

        void LateUpdate()
        {
            scale = Mathf.Max(1, Screen.height / Mathf.Max(1, lines));
            int w = (Screen.width + scale - 1) / scale, h = (Screen.height + scale - 1) / scale;
            if (picture == null || picture.width != w || picture.height != h)
            {
                cam.targetTexture = null;
                Destroy(picture);
                // HDR si la cámara lo permite: con una textura LDR los valores mayores a 1 (emisión) se recortan
                // y el Bloom no tiene nada que hacer brillar. (Cambio propio, no del paquete original.)
                var formato = cam.allowHDR ? RenderTextureFormat.DefaultHDR : RenderTextureFormat.Default;
                picture = new RenderTexture(w, h, 24, formato) { name = "PSXScreen", filterMode = FilterMode.Point };
                cam.targetTexture = picture;
                image.texture = picture;
            }
            // the picture's pixels whole on screen: what the rounding up adds past the screen is cropped
            var shown = new Vector2(Screen.width / (float)(w * scale), Screen.height / (float)(h * scale));
            image.uvRect = new Rect((Vector2.one - shown) * 0.5f, shown);
            if (material)
            {
                material.SetFloat(Dither, dither ? 1f : 0f);
                material.SetFloat(ColorBits, colorBits);
            }
        }

        /// <summary>
        /// A screen position (the mouse's, a touch's) as a position on the camera's picture, for
        /// Camera.ScreenPointToRay and the like, which count the picture's pixels once it has one.
        /// </summary>
        public Vector3 ScreenToCamera(Vector3 screen)
        {
            if (picture == null)
                return screen;
            var uv = image.uvRect.min + new Vector2(screen.x / Screen.width, screen.y / Screen.height) * image.uvRect.size;
            return new Vector3(uv.x * picture.width, uv.y * picture.height, screen.z);
        }
    }
}
