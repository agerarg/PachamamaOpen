using System.Collections.Generic;
using UnityEngine;

namespace HeyHeyThere.PSXLookFree
{
    /// <summary>
    /// Low-poly meshes for the demo world, UV'd in world units so textures tile at one size everywhere.
    /// Each is laid out as the Godot demo's, whose world is this one mirrored in z (Godot's forward is
    /// -z): <see cref="G"/> turns one of its points into ours.
    /// </summary>
    public static class Shapes
    {
        static readonly Vector3 Forward = new Vector3(0, 0, -1), Back = new Vector3(0, 0, 1);  // Godot's

        public static Vector3 G(float x, float y, float z) => new Vector3(x, y, -z);

        /// <summary>
        /// A box on the ground (its base at y = 0). <paramref name="tile"/> metres per texture repeat; 0
        /// maps each face once. Tiled faces are cut into quads of about 2 m, which keeps the affine warp
        /// PS1-sized.
        /// </summary>
        public static Mesh Box(Vector3 size, float tile = 1f)
        {
            var b = new Builder();
            var faces = new[] { (Forward, Vector3.left, Vector3.up), (Back, Vector3.right, Vector3.up), (Vector3.left, Back, Vector3.up),
                                (Vector3.right, Forward, Vector3.up), (Vector3.up, Vector3.right, Forward), (Vector3.down, Vector3.right, Back) };
            foreach (var (n, r, u) in faces)
            {
                float w = Mathf.Abs(Vector3.Dot(r, size)), ht = Mathf.Abs(Vector3.Dot(u, size));
                var center = new Vector3(0, size.y * 0.5f, 0) + n * Mathf.Abs(Vector3.Dot(n, size)) * 0.5f;
                if (tile <= 0f)
                {
                    b.Quad(center, r * w * 0.5f, u * ht * 0.5f, n, Vector2.one, Vector2.zero);
                    continue;
                }
                int cx = Mathf.CeilToInt(w / 2f), cy = Mathf.CeilToInt(ht / 2f);
                var cell = new Vector2(w / cx, ht / cy);
                for (int j = 0; j < cy; j++)
                    for (int i = 0; i < cx; i++)
                    {
                        var c = center + r * (cell.x * (i + 0.5f) - w * 0.5f) - u * (cell.y * (j + 0.5f) - ht * 0.5f);
                        b.Quad(c, r * cell.x * 0.5f, u * cell.y * 0.5f, n, cell / tile, Vector2.Scale(new Vector2(i, j), cell) / tile);
                    }
            }
            return b.Mesh("Box");
        }

        /// <summary>An upright n-sided prism with a cap, base at y = 0.</summary>
        public static Mesh Pillar(float radius, float height, int sides = 8, float tile = 1f)
        {
            var b = new Builder();
            float around = 2f * Mathf.PI * radius, v = height / tile;
            var top = Vector3.up * height;
            for (int i = 0; i < sides; i++)
            {
                float a0 = 2f * Mathf.PI * i / sides, a1 = 2f * Mathf.PI * (i + 1) / sides;
                var p0 = new Vector3(Mathf.Cos(a0), 0, Mathf.Sin(a0)) * radius;
                var p1 = new Vector3(Mathf.Cos(a1), 0, Mathf.Sin(a1)) * radius;
                var n = ((p0 + p1) * 0.5f).normalized;
                float u0 = around * i / sides / tile, u1 = around * (i + 1) / sides / tile;
                b.Tri(p0, p1 + top, p1, new Vector2(u0, v), new Vector2(u1, 0), new Vector2(u1, v), n);
                b.Tri(p0, p0 + top, p1 + top, new Vector2(u0, v), new Vector2(u0, 0), new Vector2(u1, 0), n);
                b.Tri(top, top + p1, top + p0, new Vector2(0.5f, 0.5f), Cap(a1), Cap(a0), Vector3.up);
            }
            return b.Mesh("Pillar");
        }

        /// <summary>
        /// A ground plane centred on the origin, cut into cuts x cuts quads: the PS1 look warps big
        /// polygons, and vertex lighting only lights at vertices.
        /// </summary>
        public static Mesh Ground(float size, int cuts, float tile = 1f)
        {
            var b = new Builder();
            float step = size / cuts;
            for (int z = 0; z < cuts; z++)
                for (int x = 0; x < cuts; x++)
                {
                    var c = new Vector3(-size * 0.5f + (x + 0.5f) * step, 0, -size * 0.5f + (z + 0.5f) * step);
                    b.Quad(c, Vector3.right * step * 0.5f, Forward * step * 0.5f, Vector3.up, Vector2.one * step / tile, new Vector2(x, z) * step / tile);
                }
            return b.Mesh("Ground");
        }

        /// <summary>Two crossed upright quads, for leaves and flames. Base at y = 0.</summary>
        public static Mesh Cross(float width, float height)
        {
            var b = new Builder();
            foreach (var r in new[] { Vector3.right, Back })
                b.Quad(Vector3.up * height * 0.5f, r * width * 0.5f, Vector3.up * height * 0.5f, Vector3.Cross(r, Vector3.up), Vector2.one, Vector2.zero);
            return b.Mesh("Cross");
        }

        /// <summary>An octahedron stretched tall: the gem.</summary>
        public static Mesh Gem(float radius, float height)
        {
            var b = new Builder();
            var ring = new Vector3[6];
            for (int i = 0; i < 6; i++)
                ring[i] = new Vector3(Mathf.Cos(2f * Mathf.PI * i / 6), 0, Mathf.Sin(2f * Mathf.PI * i / 6)) * radius;
            for (int i = 0; i < 6; i++)
                foreach (var tip in new[] { Vector3.up * height * 0.5f, Vector3.down * height * 0.5f })
                {
                    var (p, q) = tip.y > 0 ? (ring[(i + 1) % 6], ring[i]) : (ring[i], ring[(i + 1) % 6]);
                    var n = Vector3.Cross(p - tip, q - tip).normalized;  // outward
                    b.Tri(tip, p, q, new Vector2(0.5f, 0), new Vector2(1, 1), new Vector2(0, 1), n);
                }
            return b.Mesh("Gem");
        }

        static Vector2 Cap(float a) => new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * 0.5f + new Vector2(0.5f, 0.5f);

        /// <summary>
        /// Takes triangles laid out as Godot's (its points, its UVs counting down from the top) and
        /// stores them as Unity's: mirrored in z, V counting up, and wound to face their normal.
        /// </summary>
        class Builder
        {
            readonly List<Vector3> points = new List<Vector3>(), normals = new List<Vector3>();
            readonly List<Vector2> uvs = new List<Vector2>();

            public void Quad(Vector3 c, Vector3 r, Vector3 u, Vector3 n, Vector2 size, Vector2 uv0)
            {
                Vector3 p0 = c - r - u, p1 = c + r - u, p2 = c + r + u, p3 = c - r + u;
                Vector2 t0 = uv0 + new Vector2(0, size.y), t1 = uv0 + size, t2 = uv0 + new Vector2(size.x, 0), t3 = uv0;
                Tri(p0, p2, p1, t0, t2, t1, n);
                Tri(p0, p3, p2, t0, t3, t2, n);
            }

            public void Tri(Vector3 a, Vector3 b, Vector3 c, Vector2 ta, Vector2 tb, Vector2 tc, Vector3 n)
            {
                a = G(a.x, a.y, a.z);
                b = G(b.x, b.y, b.z);
                c = G(c.x, c.y, c.z);
                n = G(n.x, n.y, n.z);
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), n) < 0f)
                {
                    (b, c) = (c, b);
                    (tb, tc) = (tc, tb);
                }
                points.AddRange(new[] { a, b, c });
                normals.AddRange(new[] { n, n, n });
                uvs.AddRange(new[] { Flip(ta), Flip(tb), Flip(tc) });
            }

            static Vector2 Flip(Vector2 uv) => new Vector2(uv.x, -uv.y);

            public Mesh Mesh(string name)
            {
                var m = new Mesh { name = name };
                m.SetVertices(points);
                m.SetNormals(normals);
                m.SetUVs(0, uvs);
                var tris = new int[points.Count];
                for (int i = 0; i < tris.Length; i++)
                    tris[i] = i;
                m.SetTriangles(tris, 0);
                m.RecalculateBounds();
                return m;
            }
        }
    }
}
