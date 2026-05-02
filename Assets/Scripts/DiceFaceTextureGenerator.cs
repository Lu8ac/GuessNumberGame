using UnityEngine;

/// <summary>
/// DiceFaceTextureGenerator
/// ─────────────────────────
/// Attach this to the same GameManager object as GuessTheNumber.
/// On Awake it procedurally draws all 6 pip‑face textures
/// (white background, black dots, rounded corners) and injects
/// them into GuessTheNumber.pipTextures[] automatically.
///
/// You can override individual faces by assigning PNG textures
/// in the GuessTheNumber Inspector — this script only fills
/// slots that are null.
/// </summary>
[RequireComponent(typeof(GuessTheNumber))]
public class DiceFaceTextureGenerator : MonoBehaviour
{
    [Header("Texture Settings")]
    [Tooltip("Pixel resolution per face (power of 2). 256 = sharp enough for most screens.")]
    public int textureSize = 256;

    [Tooltip("Background color of the dice face")]
    public Color faceColor = Color.white;

    [Tooltip("Color of the pip dots")]
    public Color pipColor = Color.black;

    [Tooltip("Pip radius as fraction of texture width")]
    [Range(0.04f, 0.18f)]
    public float pipRadiusFraction = 0.09f;

    // ──────────────────────────────────────────────────────────────────────

    private void Awake()
    {
        var game = GetComponent<GuessTheNumber>();
        if (game == null) return;

        if (game.pipTextures == null || game.pipTextures.Length != 6)
            game.pipTextures = new Texture2D[6];

        for (int face = 1; face <= 6; face++)
        {
            int idx = face - 1;
            if (game.pipTextures[idx] == null)
                game.pipTextures[idx] = GenerateFaceTexture(face);
        }

        // Rebuild pip materials now that textures are ready
        game.BuildPipMaterials();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Texture generation
    // ──────────────────────────────────────────────────────────────────────

    private Texture2D GenerateFaceTexture(int faceValue)
    {
        var tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false)
        {
            name        = $"DiceFace_{faceValue}",
            filterMode  = FilterMode.Bilinear,
            wrapMode    = TextureWrapMode.Clamp
        };

        // Fill background
        Color[] pixels = new Color[textureSize * textureSize];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = faceColor;
        tex.SetPixels(pixels);

        // Draw pips
        float r   = textureSize * pipRadiusFraction;
        float pad = textureSize * 0.28f;
        float mid = textureSize * 0.5f;

        foreach (var center in GetPipCenters(faceValue, mid, pad))
            DrawCircle(tex, center, r, pipColor);

        // Round corners
        DrawRoundedCornerMask(tex, textureSize * 0.12f);

        tex.Apply();
        return tex;
    }

    private Vector2[] GetPipCenters(int face, float mid, float pad)
    {
        float lo = pad;
        float hi = textureSize - pad;

        switch (face)
        {
            case 1: return new[] { new Vector2(mid, mid) };
            case 2: return new[] { new Vector2(hi, hi), new Vector2(lo, lo) };
            case 3: return new[] { new Vector2(hi, hi), new Vector2(mid, mid), new Vector2(lo, lo) };
            case 4: return new[] { new Vector2(lo, hi), new Vector2(hi, hi), new Vector2(lo, lo), new Vector2(hi, lo) };
            case 5: return new[] { new Vector2(lo, hi), new Vector2(hi, hi), new Vector2(mid, mid), new Vector2(lo, lo), new Vector2(hi, lo) };
            case 6: return new[] { new Vector2(lo, hi), new Vector2(hi, hi), new Vector2(lo, mid), new Vector2(hi, mid), new Vector2(lo, lo), new Vector2(hi, lo) };
            default: return new Vector2[0];
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // Drawing utilities
    // ──────────────────────────────────────────────────────────────────────

    private void DrawCircle(Texture2D tex, Vector2 center, float radius, Color color)
    {
        int x0 = Mathf.Clamp(Mathf.FloorToInt(center.x - radius), 0, tex.width  - 1);
        int x1 = Mathf.Clamp(Mathf.CeilToInt (center.x + radius), 0, tex.width  - 1);
        int y0 = Mathf.Clamp(Mathf.FloorToInt(center.y - radius), 0, tex.height - 1);
        int y1 = Mathf.Clamp(Mathf.CeilToInt (center.y + radius), 0, tex.height - 1);

        float aa = radius * 0.6f;

        for (int px = x0; px <= x1; px++)
        for (int py = y0; py <= y1; py++)
        {
            float dist = Vector2.Distance(new Vector2(px, py), center);
            if      (dist < radius - aa) tex.SetPixel(px, py, color);
            else if (dist < radius)
            {
                float alpha = (radius - dist) / aa;
                tex.SetPixel(px, py, Color.Lerp(tex.GetPixel(px, py), color, alpha));
            }
        }
    }

    private void DrawRoundedCornerMask(Texture2D tex, float cr)
    {
        int size = tex.width;
        for (int px = 0; px < size; px++)
        for (int py = 0; py < size; py++)
            if (InCorner(px, py, size, cr))
                tex.SetPixel(px, py, Color.clear);
    }

    private bool InCorner(int px, int py, int size, float r)
    {
        float dx, dy;
        if (px < r && py < r)         { dx = r - px; dy = r - py;           return dx*dx+dy*dy > r*r; }
        if (px > size-r && py < r)    { dx = px-(size-r); dy = r-py;        return dx*dx+dy*dy > r*r; }
        if (px < r && py > size-r)    { dx = r-px; dy = py-(size-r);        return dx*dx+dy*dy > r*r; }
        if (px > size-r && py > size-r){ dx = px-(size-r); dy = py-(size-r); return dx*dx+dy*dy > r*r; }
        return false;
    }
}
