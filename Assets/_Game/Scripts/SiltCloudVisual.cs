using System.Collections.Generic;
using UnityEngine;

// World-space silt. It does not tint the whole screen; it exists only around the
// wall-contact point and becomes visible when the diver's 2D light reaches it.
public sealed class SiltCloudVisual : MonoBehaviour
{
    private sealed class Blob
    {
        public Transform Transform;
        public SpriteRenderer Renderer;
        public Vector2 BaseOffset;
        public float BaseScale;
        public float Phase;
        public bool Speck;
    }

    private static Sprite softSprite;
    private static Sprite speckSprite;
    private static Material litMaterial;

    private readonly List<Blob> blobs = new List<Blob>();

    private float bornAt;
    private float strength = 0.7f;
    private Vector2 origin;

    public static SiltCloudVisual Create(Vector2 position, float initialStrength)
    {
        GameObject root = new GameObject("Silt Cloud");
        root.transform.position = new Vector3(position.x, position.y, -0.05f);

        SiltCloudVisual visual = root.AddComponent<SiltCloudVisual>();
        visual.origin = position;
        visual.bornAt = Time.time;
        visual.strength = Mathf.Clamp(initialStrength, 0.25f, 1.6f);
        visual.Build();
        return visual;
    }

    public void Boost(float amount)
    {
        strength = Mathf.Clamp(strength + amount, 0.25f, 1.6f);
    }

    private void Build()
    {
        EnsureSharedResources();

        // Broad suspended sediment masses. These are intentionally soft and dim;
        // the flashlight reveals them instead of them glowing by themselves.
        for (int i = 0; i < 9; i++)
        {
            float angle = i * 2.399963f;
            float radius = 0.18f + (i % 4) * 0.16f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            float scale = 0.85f + (i % 5) * 0.18f;
            CreateBlob("Haze", softSprite, offset, scale, i * 0.73f, false, 2);
        }

        // Small particles catch the lamp and make the water read as dirty rather than
        // like a flat translucent fog layer.
        for (int i = 0; i < 22; i++)
        {
            float angle = i * 1.618034f * Mathf.PI;
            float radius = 0.20f + (i % 7) * 0.085f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            float scale = 0.045f + (i % 4) * 0.018f;
            CreateBlob("Particle", speckSprite, offset, scale, i * 1.17f, true, 3);
        }
    }

    private void CreateBlob(string name, Sprite sprite, Vector2 offset, float scale, float phase, bool speck, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
        go.transform.localScale = Vector3.one * scale;

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sharedMaterial = litMaterial;
        renderer.sortingOrder = sortingOrder;
        renderer.color = speck
            ? new Color(0.74f, 0.72f, 0.61f, 0f)
            : new Color(0.45f, 0.46f, 0.39f, 0f);

        blobs.Add(new Blob
        {
            Transform = go.transform,
            Renderer = renderer,
            BaseOffset = offset,
            BaseScale = scale,
            Phase = phase,
            Speck = speck
        });
    }

    private void Update()
    {
        float age = Time.time - bornAt;
        float maturity = Maturity(age);
        float fade = age <= 140f ? 1f : Mathf.Clamp01((180f - age) / 40f);
        float amount = maturity * fade * strength;

        // The cloud grows slowly after the contact. At first it is essentially invisible,
        // so the consequence is mainly encountered on the return trip.
        float spread = Mathf.Lerp(0.8f, 3.2f, maturity);
        transform.position = new Vector3(origin.x, origin.y + maturity * 0.10f, -0.05f);

        for (int i = 0; i < blobs.Count; i++)
        {
            Blob blob = blobs[i];
            float t = Time.time;
            float driftX = Mathf.Sin(t * (0.11f + (i % 3) * 0.025f) + blob.Phase) * 0.10f * maturity;
            float driftY = Mathf.Cos(t * (0.08f + (i % 4) * 0.017f) + blob.Phase * 0.7f) * 0.07f * maturity;

            Vector2 local = blob.BaseOffset * spread + new Vector2(driftX, driftY);
            blob.Transform.localPosition = new Vector3(local.x, local.y, 0f);

            if (blob.Speck)
            {
                float pulse = 0.78f + Mathf.Sin(t * 1.4f + blob.Phase) * 0.22f;
                blob.Transform.localScale = Vector3.one * blob.BaseScale * Mathf.Lerp(0.7f, 1.35f, maturity);
                Color c = blob.Renderer.color;
                c.a = Mathf.Clamp01(amount * 0.42f * pulse);
                blob.Renderer.color = c;
            }
            else
            {
                float breathing = 0.95f + Mathf.Sin(t * 0.23f + blob.Phase) * 0.05f;
                blob.Transform.localScale = Vector3.one * blob.BaseScale * Mathf.Lerp(0.85f, 2.15f, maturity) * breathing;
                Color c = blob.Renderer.color;
                c.a = Mathf.Clamp01(amount * 0.18f);
                blob.Renderer.color = c;
            }
        }
    }

    private static float Maturity(float age)
    {
        if (age < 5f) return 0f;
        if (age < 15f) return Mathf.Lerp(0f, 0.18f, Mathf.InverseLerp(5f, 15f, age));
        if (age < 30f) return Mathf.Lerp(0.18f, 0.58f, Mathf.InverseLerp(15f, 30f, age));
        if (age < 55f) return Mathf.Lerp(0.58f, 1f, Mathf.InverseLerp(30f, 55f, age));
        return 1f;
    }

    private static void EnsureSharedResources()
    {
        if (softSprite == null)
            softSprite = BuildSoftSprite(48, false);
        if (speckSprite == null)
            speckSprite = BuildSoftSprite(16, true);

        if (litMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader != null)
                litMaterial = new Material(shader) { name = "Runtime Silt Lit Material" };
        }
    }

    private static Sprite BuildSoftSprite(int size, bool compact)
    {
        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            name = compact ? "Runtime Silt Speck" : "Runtime Silt Haze"
        };

        Color[] pixels = new Color[size * size];
        float half = (size - 1) * 0.5f;
        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float dx = (x - half) / half;
                float dy = (y - half) / half;
                float d = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = compact
                    ? 1f - Mathf.SmoothStep(0.20f, 1f, d)
                    : 1f - Mathf.SmoothStep(0.05f, 1f, d);
                pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size * 0.5f);
        sprite.name = texture.name + " Sprite";
        return sprite;
    }
}
