using System.IO;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Generates the particle stroke atlas used by VfxManager, plus the material that draws it.
///
/// The art is generated rather than drawn by hand so the pen weight can be matched exactly to the
/// rest of the game: strokes are stamped at ~4px on a 32px cell at PPU 100, which is 0.04 world
/// units — the same width as the grapple rope (GrappleLine width 0.0426), the project's de-facto
/// "one pen stroke". A fixed seed keeps regeneration reproducible.
///
/// The PNG is a completely normal asset. Overwrite it with a hand-drawn sheet whenever you like —
/// nothing else needs to change as long as it stays a 4x4 grid of square cells.
///
/// Run via: Tools > VFX > Generate Sketch Stroke Atlas
/// </summary>
public static class SketchStrokeGenerator
{
    private const string TexturePath = "Assets/2D Sprites/Original/VFX/SketchStrokes.png";
    private const string MaterialPath = "Assets/Materials/SketchParticle.mat";

    private const int Columns = 4;
    private const int Rows = 4;
    private const int CellSize = 32;
    private const float StrokeWidth = 4f;   // px, = 0.04 world units at PPU 100
    private const int Seed = 20260725;

    [MenuItem("Tools/VFX/Generate Sketch Stroke Atlas")]
    public static void Generate()
    {
        int width = Columns * CellSize;
        int height = Rows * CellSize;

        // White RGB with alpha carrying the coverage: the particle system multiplies this by
        // VfxEventSO.Tint, so the texture must stay colourless or the orange comes out muddy.
        var pixels = new Color[width * height];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color(1f, 1f, 1f, 0f);

        var rng = new System.Random(Seed);
        for (int cell = 0; cell < Columns * Rows; cell++)
        {
            int originX = (cell % Columns) * CellSize;
            int originY = (cell / Columns) * CellSize;
            DrawCell(pixels, width, originX, originY, cell, rng);
        }

        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.SetPixels(pixels);
        texture.Apply();

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(TexturePath)));
        File.WriteAllBytes(TexturePath, texture.EncodeToPNG());
        Object.DestroyImmediate(texture);

        AssetDatabase.ImportAsset(TexturePath, ImportAssetOptions.ForceUpdate);
        ApplyImportSettings();
        EnsureMaterial();

        AssetDatabase.SaveAssets();
        Debug.Log($"[SketchStrokeGenerator] Wrote {TexturePath} ({Columns}x{Rows} cells) and {MaterialPath}.");
    }

    /// <summary>
    /// 16 marks in four families. Variety matters more than any individual shape here — a burst is
    /// only ever seen for a third of a second, and repeating one sprite is what makes particles read
    /// as "stamped" instead of hand-drawn.
    /// </summary>
    private static void DrawCell(Color[] pixels, int stride, int originX, int originY, int cell, System.Random rng)
    {
        float c = CellSize * 0.5f;

        if (cell < 6)
        {
            // Dashes at spread angles — the workhorse mark, reads as a flick of the pen.
            float angle = cell * 30f + Rand(rng, -8f, 8f);
            float halfLength = Rand(rng, 6f, 11f);
            Vector2 dir = new Vector2(Mathf.Cos(angle * Mathf.Deg2Rad), Mathf.Sin(angle * Mathf.Deg2Rad));
            Stroke(pixels, stride, originX, originY,
                new Vector2(c, c) - dir * halfLength, new Vector2(c, c) + dir * halfLength, rng);
        }
        else if (cell < 10)
        {
            // Chevrons — echo the zig-zag of the spike tiles in OneLastJumpTileMap.
            float rotation = (cell - 6) * 45f + Rand(rng, -10f, 10f);
            float span = Rand(rng, 7f, 10f);
            Vector2 tip = new Vector2(c, c) + Rotate(new Vector2(0f, span * 0.6f), rotation);
            Vector2 left = new Vector2(c, c) + Rotate(new Vector2(-span, -span * 0.5f), rotation);
            Vector2 right = new Vector2(c, c) + Rotate(new Vector2(span, -span * 0.5f), rotation);
            Stroke(pixels, stride, originX, originY, left, tip, rng);
            Stroke(pixels, stride, originX, originY, tip, right, rng);
        }
        else if (cell < 14)
        {
            // Scribbles — a short run of connected segments, matching the hatch fill on the crate.
            int segments = 3 + (cell - 10) % 2;
            var point = new Vector2(c + Rand(rng, -8f, -4f), c + Rand(rng, -6f, 6f));
            for (int s = 0; s < segments; s++)
            {
                var next = new Vector2(point.x + Rand(rng, 4f, 7f), c + Rand(rng, -8f, 8f));
                Stroke(pixels, stride, originX, originY, point, next, rng);
                point = next;
            }
        }
        else
        {
            // Dots — a near-zero-length stroke, so it picks up the same rough edge as everything else.
            float jitter = Rand(rng, -2f, 2f);
            Stroke(pixels, stride, originX, originY,
                new Vector2(c + jitter, c + jitter), new Vector2(c + jitter + 1f, c + jitter), rng,
                widthScale: cell == 15 ? 1.5f : 1.1f);
        }
    }

    /// <summary>
    /// Stamps a soft round brush along the segment. The perpendicular sine wobble and the per-stamp
    /// radius variation are what stop it looking like a vector line — this is the whole reason the
    /// marks sit next to the hand-drawn tileset without looking imported.
    /// </summary>
    private static void Stroke(Color[] pixels, int stride, int originX, int originY,
        Vector2 from, Vector2 to, System.Random rng, float widthScale = 1f)
    {
        float radius = StrokeWidth * 0.5f * widthScale;
        Vector2 delta = to - from;
        float length = delta.magnitude;
        if (length < 0.001f) { delta = Vector2.right; length = 1f; }

        Vector2 perpendicular = new Vector2(-delta.y, delta.x) / length;
        float wobbleAmplitude = Rand(rng, 0.4f, 1.1f);
        float wobbleFrequency = Rand(rng, 3f, 7f);
        float wobblePhase = Rand(rng, 0f, 6.28f);

        int steps = Mathf.Max(4, Mathf.CeilToInt(length * 3f));
        for (int i = 0; i <= steps; i++)
        {
            float t = i / (float)steps;
            Vector2 point = from + delta * t
                + perpendicular * wobbleAmplitude * Mathf.Sin(t * wobbleFrequency + wobblePhase);

            // Taper the ends slightly so strokes look lifted off the page rather than chopped.
            float taper = Mathf.Lerp(0.75f, 1f, Mathf.Sin(t * Mathf.PI));
            Stamp(pixels, stride, originX, originY, point, radius * taper);
        }
    }

    private static void Stamp(Color[] pixels, int stride, int originX, int originY, Vector2 center, float radius)
    {
        int minX = Mathf.FloorToInt(center.x - radius - 1f);
        int maxX = Mathf.CeilToInt(center.x + radius + 1f);
        int minY = Mathf.FloorToInt(center.y - radius - 1f);
        int maxY = Mathf.CeilToInt(center.y + radius + 1f);

        for (int y = minY; y <= maxY; y++)
        {
            // Clamp to this cell so a stroke can never bleed into its neighbour in the atlas.
            if (y < 0 || y >= CellSize) continue;
            for (int x = minX; x <= maxX; x++)
            {
                if (x < 0 || x >= CellSize) continue;

                float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(radius - 0.9f, radius + 0.4f, distance));
                if (alpha <= 0f) continue;

                int index = (originY + y) * stride + (originX + x);
                pixels[index].a = Mathf.Max(pixels[index].a, alpha);
            }
        }
    }

    private static void ApplyImportSettings()
    {
        var importer = AssetImporter.GetAtPath(TexturePath) as TextureImporter;
        if (importer == null) return;

        importer.textureType = TextureImporterType.Sprite;
        // Single, not Multiple: the particle system slices the atlas itself through the Texture Sheet
        // Animation module (numTilesX/Y on VfxManager), so sprite sub-rects would be dead metadata.
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.spritePixelsPerUnit = 100f;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.SaveAndReimport();
    }

    private static void EnsureMaterial()
    {
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(TexturePath);
        var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

        if (material == null)
        {
            // Sprites/Default, matching GrappleLine.mat — alpha blended, NOT additive. Additive
            // washes out to nothing against the near-white grid paper background.
            material = new Material(Shader.Find("Sprites/Default")) { name = "SketchParticle" };
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(MaterialPath)));
            AssetDatabase.CreateAsset(material, MaterialPath);
        }

        material.mainTexture = texture;
        EditorUtility.SetDirty(material);
    }

    private static float Rand(System.Random rng, float min, float max) => (float)(min + rng.NextDouble() * (max - min));

    private static Vector2 Rotate(Vector2 v, float degrees)
    {
        float rad = degrees * Mathf.Deg2Rad;
        float sin = Mathf.Sin(rad);
        float cos = Mathf.Cos(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }
}
