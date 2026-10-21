using System;
using UnityEngine;

#pragma warning disable IDE0130
namespace ItemFavoriteFramework {
#pragma warning restore IDE0130

    // The mod's few sprites, drawn in code so it ships no asset bundle and no image files. All white (or
    // white with a dark outline) so a single sprite can be tinted to any configured colour.
    internal static class SpriteFactory {
        private static readonly Color OutlineColor = new Color(0.08f, 0.06f, 0.03f, 1f);

        private static Sprite weaveOuter;
        private static Sprite weaveInner;
        private static Sprite star;
        private static Sprite trash;
        private static Sprite heart;
        private static Sprite dice;

        // Explicit null checks, not ??=: these are UnityEngine.Objects and only Unity's == sees a destroyed
        // one as null.

        // The two cords of the woven cell frame, one sprite each so each takes its own tint.
        internal static Sprite WeaveOuter {
            get {
                if (weaveOuter == null) { CreateWeave(); }
                return weaveOuter;
            }
        }

        internal static Sprite WeaveInner {
            get {
                if (weaveInner == null) { CreateWeave(); }
                return weaveInner;
            }
        }

        internal static Sprite Star {
            get {
                if (star == null) { star = CreateStar(); }
                return star;
            }
        }

        internal static Sprite Trash {
            get {
                if (trash == null) { trash = CreateTrash(); }
                return trash;
            }
        }

        // The wishlist mark.
        internal static Sprite Heart {
            get {
                if (heart == null) { heart = CreateHeart(); }
                return heart;
            }
        }

        // The loot roll window's icon.
        internal static Sprite Dice {
            get {
                if (dice == null) { dice = CreateDice(); }
                return dice;
            }
        }

        // A Norse-style interlace around the cell's edge: an outer and an inner cord, each on its own track,
        // that swap across twice per side, over on the way in and under on the way out. Corners and side
        // middles keep both cords home, so a two-coloured weave reads as one colour outside and the other
        // inside. Each sprite holds one cord with the other's crossings already cut out of it, so the two
        // layer in either order. Stretched over the whole cell, so it is mipmapped to stay clean when small.
        private static void CreateWeave() {
            const int size = 128;
            const float inset = 1f;           // clear pixels at the texture's edge
            const float halfBand = 9f;
            const float cordRadius = 2.9f;    // outline included
            const float outline = 1f;
            const float clearance = 0.7f;     // extra cut around a cord where it passes over the other
            const int linksPerSide = 2;
            const float ramp = 8f;            // length along the band of one crossing
            const float plateau = 2f;         // half the length the cords stay swapped
            const float step = 0.25f;         // sampling step along a cord, for its true distance

            float track = halfBand - cordRadius - 0.3f;              // a cord's home offset from the centre line
            float radius = track + cordRadius + 2f;                  // the centre line's corner radius
            float straight = size / 2f - inset - halfBand - radius;  // half a straight edge
            float quarter = 2f * straight + Mathf.PI * radius / 2f;  // one edge and one corner
            float period = quarter / linksPerSide;
            float fillRadius = cordRadius - outline;

            Color[] outer = new Color[size * size];
            Color[] inner = new Color[size * size];
            Color clear = new Color(OutlineColor.r, OutlineColor.g, OutlineColor.b, 0f);
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    int index = y * size + x;
                    outer[index] = clear;
                    inner[index] = clear;

                    // The offset from the nearest point of the square the corners turn around: its length
                    // gives the signed distance across the band (outward positive), its direction the edge.
                    float px = x + 0.5f - size / 2f, py = y + 0.5f - size / 2f;
                    float vx = px - Mathf.Clamp(px, -straight, straight);
                    float vy = py - Mathf.Clamp(py, -straight, straight);
                    float length = Mathf.Sqrt(vx * vx + vy * vy);
                    float across = length - radius;
                    if (length == 0f || Mathf.Abs(across) > halfBand + 1f) { continue; }

                    // Distance along the centre line, anticlockwise from the right edge's middle, measured
                    // after turning the point into the first quarter (right edge, then top-right corner).
                    float angle = Mathf.Atan2(vy, vx);
                    if (angle < 0f) { angle += 2f * Mathf.PI; }
                    int turns = (int)(angle / (Mathf.PI / 2f)) & 3;
                    float rx = px, ry = py;
                    for (int t = 0; t < turns; t++) {
                        float swap = rx;
                        rx = ry;
                        ry = -swap;
                    }
                    float along, stretch;
                    if (ry <= straight) {
                        along = ry;
                        stretch = 1f;
                    } else {
                        along = straight + radius * Mathf.Atan2(ry - straight, Mathf.Max(rx - straight, 1e-6f));
                        stretch = (radius + across) / radius;  // arc length per unit along, at this offset
                    }
                    along += turns * quarter;

                    // Nearest point on each cord, sampled: the crossings are too steep to estimate it from
                    // the offset alone. The inner cord mirrors the outer one across the centre line.
                    float toOuter = float.MaxValue, toInner = float.MaxValue;
                    int samples = (int)((cordRadius + 1.5f) / Mathf.Max(stretch, 0.3f) / step);
                    for (int s = -samples; s <= samples; s++) {
                        float offset = track * CordSide(along + s * step, period, ramp, plateau);
                        float dAlong = s * step * stretch;
                        toOuter = Mathf.Min(toOuter, dAlong * dAlong + (across - offset) * (across - offset));
                        toInner = Mathf.Min(toInner, dAlong * dAlong + (across + offset) * (across + offset));
                    }
                    toOuter = Mathf.Sqrt(toOuter);
                    toInner = Mathf.Sqrt(toInner);

                    // The outer cord passes over on the way into a swap, the inner one on the way out.
                    float outerCover = Mathf.Clamp01(cordRadius - toOuter + 0.5f);
                    float innerCover = Mathf.Clamp01(cordRadius - toInner + 0.5f);
                    if (Mathf.Repeat(along, period) < period / 2f) {
                        innerCover *= 1f - Mathf.Clamp01(cordRadius + clearance - toOuter + 0.5f);
                    } else {
                        outerCover *= 1f - Mathf.Clamp01(cordRadius + clearance - toInner + 0.5f);
                    }
                    outer[index] = CordPixel(toOuter, outerCover, fillRadius);
                    inner[index] = CordPixel(toInner, innerCover, fillRadius);
                }
            }

            weaveOuter = WeaveSprite(size, outer, "IFF_WeaveOuter");
            weaveInner = WeaveSprite(size, inner, "IFF_WeaveInner");
        }

        // +1 where the outer cord is on its own track, -1 where it has swapped with the inner one.
        private static float CordSide(float along, float period, float ramp, float plateau) {
            float fromSwap = Mathf.Abs(Mathf.Repeat(along, period) - period / 2f);
            if (fromSwap <= plateau) { return -1f; }
            if (fromSwap >= plateau + ramp) { return 1f; }
            return -Mathf.Cos(Mathf.PI * (fromSwap - plateau) / ramp);
        }

        // A white cord, shaded toward its sides so it reads as round, inside a dark outline.
        private static Color CordPixel(float distance, float cover, float fillRadius) {
            float fill = Mathf.Clamp01(fillRadius - distance + 0.5f);
            float edge = distance / fillRadius;
            float shade = 0.7f + 0.3f * Mathf.Max(0f, 1f - edge * edge);
            Color color = Color.Lerp(OutlineColor, new Color(shade, shade, shade, 1f), fill);
            color.a = cover;
            return color;
        }

        private static Sprite WeaveSprite(int size, Color[] pixels, string name) {
            Texture2D texture = NewTexture(size, name, mipmaps: true);
            texture.SetPixels(pixels);
            texture.Apply(true, true);
            return NewSprite(texture, Vector4.zero, name);
        }

        // A five-point star with a dark outline, point up.
        private static Sprite CreateStar() {
            const int size = 64;
            Vector2 center = new Vector2(size / 2f, size / 2f - 1f);
            Vector2[] fill = StarPolygon(center, 27f, 11.5f);
            Vector2[] outline = StarPolygon(center, 31f, 14.5f);
            return Rasterize(size, "IFF_Star", p => Coverage(p, fill), p => Coverage(p, outline));
        }

        // A bold X with a dark outline.
        private static Sprite CreateTrash() {
            const int size = 64;
            Vector2 a = new Vector2(16f, 16f), b = new Vector2(48f, 48f);
            Vector2 c = new Vector2(16f, 48f), d = new Vector2(48f, 16f);
            Func<Vector2, float, bool> inX = (p, halfWidth) => DistanceToSegment(p, a, b) <= halfWidth || DistanceToSegment(p, c, d) <= halfWidth;
            return Rasterize(size, "IFF_Trash", p => inX(p, 5f) ? 1f : 0f, p => inX(p, 8f) ? 1f : 0f);
        }

        // The classic implicit heart, (x^2 + y^2 - 1)^3 <= x^2 y^3, scaled into the square, with a dark outline.
        private static Sprite CreateHeart() {
            const int size = 64;
            Func<Vector2, float, float> inHeart = (p, scale) => {
                float x = (p.x - size / 2f) / scale;
                float y = (p.y - size / 2f + 2f) / scale;
                float a = x * x + y * y - 1f;
                return a * a * a - x * x * y * y * y <= 0f ? 1f : 0f;
            };
            return Rasterize(size, "IFF_Heart", p => inHeart(p, 20f), p => inHeart(p, 24f));
        }

        // A rounded die face showing three pips, the pips left dark.
        private static Sprite CreateDice() {
            const int size = 64;
            Vector2[] pips = { new Vector2(20f, 44f), new Vector2(32f, 32f), new Vector2(44f, 20f) };
            Func<Vector2, float, bool> inSquare = (p, half) => {
                Vector2 d = new Vector2(Mathf.Max(Mathf.Abs(p.x - 32f) - (half - 8f), 0f), Mathf.Max(Mathf.Abs(p.y - 32f) - (half - 8f), 0f));
                return d.magnitude <= 8f;
            };
            Func<Vector2, bool> inPip = p => {
                foreach (Vector2 pip in pips) {
                    if (Vector2.Distance(p, pip) <= 5f) { return true; }
                }
                return false;
            };
            return Rasterize(size, "IFF_Dice", p => inSquare(p, 24f) && inPip(p) == false ? 1f : 0f, p => inSquare(p, 28f) ? 1f : 0f);
        }

        // Supersampled 4x4 per pixel: fill is white, the band between fill and outline is near-black.
        private static Sprite Rasterize(int size, string name, Func<Vector2, float> fillAt, Func<Vector2, float> outlineAt) {
            const int samples = 4;
            Texture2D texture = NewTexture(size, name);
            Color[] pixels = new Color[size * size];
            for (int y = 0; y < size; y++) {
                for (int x = 0; x < size; x++) {
                    float fill = 0f, outer = 0f;
                    for (int sy = 0; sy < samples; sy++) {
                        for (int sx = 0; sx < samples; sx++) {
                            Vector2 p = new Vector2(x + (sx + 0.5f) / samples, y + (sy + 0.5f) / samples);
                            fill += fillAt(p);
                            outer += outlineAt(p);
                        }
                    }
                    fill /= samples * samples;
                    outer /= samples * samples;
                    Color color = Color.Lerp(OutlineColor, Color.white, outer > 0f ? fill / outer : 0f);
                    color.a = outer;
                    pixels[y * size + x] = color;
                }
            }
            texture.SetPixels(pixels);
            texture.Apply(false, true);
            return NewSprite(texture, Vector4.zero, name);
        }

        private static Vector2[] StarPolygon(Vector2 center, float outerRadius, float innerRadius) {
            Vector2[] points = new Vector2[10];
            for (int i = 0; i < points.Length; i++) {
                float angle = Mathf.PI / 2f + i * Mathf.PI / 5f;
                float radius = i % 2 == 0 ? outerRadius : innerRadius;
                points[i] = center + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            }
            return points;
        }

        private static float Coverage(Vector2 p, Vector2[] polygon) {
            bool inside = false;
            for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++) {
                if ((polygon[i].y > p.y) != (polygon[j].y > p.y) &&
                    p.x < (polygon[j].x - polygon[i].x) * (p.y - polygon[i].y) / (polygon[j].y - polygon[i].y) + polygon[i].x) {
                    inside = !inside;
                }
            }
            return inside ? 1f : 0f;
        }

        private static float DistanceToSegment(Vector2 p, Vector2 a, Vector2 b) {
            Vector2 ab = b - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return Vector2.Distance(p, a + ab * t);
        }

        // DontUnloadUnusedAsset (part of HideAndDontSave): only static fields reference these, and
        // Resources.UnloadUnusedAssets on a scene change would otherwise be free to destroy them.
        private static Texture2D NewTexture(int size, string name, bool mipmaps = false) {
            return new Texture2D(size, size, TextureFormat.RGBA32, mipChain: mipmaps) {
                name = name,
                filterMode = mipmaps ? FilterMode.Trilinear : FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave,
            };
        }

        private static Sprite NewSprite(Texture2D texture, Vector4 sliceBorder, string name) {
            Sprite sprite = Sprite.Create(texture, new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f),
                100f, 0, SpriteMeshType.FullRect, sliceBorder);
            sprite.name = name;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
