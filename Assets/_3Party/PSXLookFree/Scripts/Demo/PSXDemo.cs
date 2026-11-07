using System.Collections.Generic;
using UnityEngine;

namespace HeyHeyThere.PSXLookFree
{
    /// <summary>
    /// The level through PSXScreen with its materials converted by PSX.Convert, and a switch for every
    /// part of the look. Keys and the mouse come through IMGUI, so it runs on either input system.
    /// </summary>
    public class PSXDemo : MonoBehaviour
    {
        public World world;
        public PSXScreen screen;
        [Tooltip("Hides the panel, for store captures.")]
        public bool noUI;

        static readonly (int lines, string name)[] Resolutions = { (240, "240p"), (480, "480p"), (1080, "Native") };
        static readonly Dictionary<KeyCode, Vector2> Keys = new Dictionary<KeyCode, Vector2>
        {
            { KeyCode.A, Vector2.left }, { KeyCode.LeftArrow, Vector2.left }, { KeyCode.D, Vector2.right }, { KeyCode.RightArrow, Vector2.right },
            { KeyCode.W, Vector2.up }, { KeyCode.UpArrow, Vector2.up }, { KeyCode.S, Vector2.down }, { KeyCode.DownArrow, Vector2.down },
        };

        readonly HashSet<KeyCode> held = new HashSet<KeyCode>();
        int resolution;
        bool snap = true, affine = true, fog = true;

        void Start() => PSX.Convert(world.gameObject);

        void Update()
        {
            var move = Vector2.zero;
            foreach (var k in held)
                move += Keys[k];
            world.move = move;
        }

        void OnApplicationFocus(bool focused) => held.Clear();

        void OnGUI()
        {
            var e = Event.current;
            if (e.type == EventType.KeyDown && Keys.ContainsKey(e.keyCode))
                held.Add(e.keyCode);
            else if (e.type == EventType.KeyUp)
                held.Remove(e.keyCode);
            else if (e.type == EventType.MouseDrag && e.button == 0)
                world.look += e.delta;
            if (noUI)
                return;
            DemoStyle.Init();
            GUILayout.BeginArea(new Rect(16, 16, 220, Screen.height - 32));
            GUILayout.Label("PSX LOOK", DemoStyle.Hint);
            if (GUILayout.Button("Resolution: " + Resolutions[resolution].name, DemoStyle.Button, GUILayout.ExpandWidth(false)))
            {
                resolution = (resolution + 1) % Resolutions.Length;
                screen.lines = Resolutions[resolution].lines;
            }
            if (Toggle(ref snap, "Vertex snap"))
                PSX.SetFloat(world.gameObject, PSX.SnapPixels, snap ? 1f : 0f);
            if (Toggle(ref affine, "Affine textures"))
                PSX.SetFloat(world.gameObject, PSX.Affine, affine ? 1f : 0f);
            if (PSX.HasScreen)
            {
                Toggle(ref screen.dither, "Dither");
                bool full = screen.colorBits < 8;
                if (Toggle(ref full, "15-bit colour"))
                    screen.colorBits = full ? 5 : 8;
            }
            if (Toggle(ref fog, "Fog"))
                world.Fog(fog);
            GUILayout.Label("WASD / arrows to walk\ndrag to look", DemoStyle.Hint);
            GUILayout.EndArea();
        }

        static bool Toggle(ref bool on, string text)
        {
            bool v = GUILayout.Toggle(on, text, DemoStyle.Button, GUILayout.ExpandWidth(false));
            if (v == on)
                return false;
            on = v;
            return true;
        }
    }
}
