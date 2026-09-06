using System.Collections.Generic;
using UnityEngine;

// World-space silt. It exists around wall-contact points and is revealed by the diver's light.
// Mature silt is deliberately dense enough to hide cave silhouettes and attenuate the flashlight.
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
    private static readonly List<SiltCloudVisual> activeClouds = new List<SiltCloudVisual>();

    private readonly List<Blob> blobs = new List<Blob>();

    private float bornAt;
    private float strength = 0.7f;
    private Vector2 origin;
    private float currentMaturity;
    private float currentFade = 1f;

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

    private void OnEnable()
    {
        if (!activeClouds.Contains(this))
            activeClouds.Add(this);
    }

    private void OnDisable()
    {
        activeClouds.Remove(this);
    }

    public void Boost(float amount)
    {
        strength = Mathf.Clamp(strength + amount, 0.25f, 1.6f);
    }

    // Used by the flashlight to estimate how much suspended sediment lies along the beam.
    public static float GetOpticalDensityAtPoint(Vector2 worldPoint)
    {
        float density = 0f;

        for (int i = activeClouds.Count - 1; i >= 0; i--)
        {
            SiltCloudVisual cloud = activeClouds[i];
            if (cloud == null)
            {
                activeClouds.RemoveAt(i);
                continue;
            }

            density += cloud.SampleOpticalDensity(worldPoint);
        }

        return Mathf.Clamp(density, 0f, 2.5f);
    }

    private float SampleOpticalDensity(Vector2 worldPoint)
    {
        if (currentMaturity <= 0.01f || currentFade <= 0.01f)
            return 0f;

        float radius = Mathf.Lerp(0.65f, 4.0f, currentMaturity);
        float distance = Vector2.Distance(worldPoint, transform.position);
        if (distance >= radius)
            return 0f;

        float radial = 1f - distance / radius;
        radial = radial * radial * (3f - 2f * radial);

        // A mature, repeatedly disturbed cloud should behave like a wall of dirty water.
        return radial * currentMaturity * currentFade * strength * 1.35f;
    }

    private void Build()
    {
        EnsureSharedResources();

        // Dense overlapping haze masses. They stay dark and low-contrast so that, once mature,
        // rock edges inside them are very difficult to distinguish.
        for (int i = 0; i < 14; i++)
        {
            float angle = i * 2.399963f;
            float radius = 0.14f + (i % 5) * 0.14f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            float scale = 0.88f + (i % 6) * 0.17f;
            CreateBlob("Haze", softSprite, offset, scale, i * 0.73f, false, 2);
        }

        // More particles than before so the beam reads as travelling through suspended sediment.
        for (int i = 0; i < 36; i++)
        {
            float angle = i * 1.618034f * Mathf.PI;
            float radius = 0.18f + (i % 9) * 0.075f;
            Vector2 offset = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * radius;
            float scale = 0.040f + (i % 5) * 0.017f;
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
            ? new Color(0.64f, 0.62f, 0.53f, 0f)
            : new Color(0.29f, 0.30f, 0.27f, 0f);

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
        currentMaturity = Maturity(age);
        currentFade = age <= 140f ? 1f : Mathf.Clamp01((180f - age) / 40f);
        float amount = currentMaturity * currentFade * strength;

        float spread = Mathf.Lerp(0.8f, 3.55f, currentMaturity);
        transform.position = new Vector3(origin.x, origin.y + currentMaturity * 0.10f, -0.05f);

        for (int i = 0; i < blobs.Count; i++)
        {
            Blob blob = blobs[i];
            float t = Time.time;
            float driftX = Mathf.Sin(t * (0.11f + (i % 3) * 0.025f) + blob.Phase) * 0.12f * currentMaturity;
            float driftY = Mathf.Cos(t * (0.08f + (i % 4) * 0.017f) + blob.Phase * 0.7f) * 0.08f * currentMaturity;

            Vector2 local = blob.BaseOffset * spread + new Vector2(driftX, driftY);
            blob.Transform.localPosition = new Vector3(local.x, local.y, 0f);

            if (blob.Speck)
            {
                float pulse = 0.78f + Mathf.Sin(t * 1.4f + blob.Phase) * 0.22f;
                blob.Transform.localScale = Vector3.one * blob.BaseScale * Mathf.Lerp(0.7f, 1.45f, currentMaturity);
                Color c = blob.Renderer.color;
                c.a = Mathf.Clamp01(amount * 0.62f * pulse);
                blob.Renderer.color = c;
            }
            else
            {
                float breathing = 0.95f + Mathf.Sin(t * 0.23f + blob.Phase) * 0.05f;
                blob.Transform.localScale = Vector3.one * blob.BaseScale * Mathf.Lerp(0.88f, 2.45f, currentMaturity) * breathing;
                Color c = blob.Renderer.color;
                c.a = Mathf.Clamp01(amount * 0.43f);
                blob.Renderer.color = c;
            }
        }
    }

    private static float Maturity(float age)
    {
        // Slightly faster than the original 5/15/30/55 second ramp.
        // The cloud still has a delayed build-up, but becomes tactically relevant sooner.
        if (age < 4f) return 0f;
        if (age < 12f) return Mathf.Lerp(0f, 0.18f, Mathf.InverseLerp(4f, 12f, age));
        if (age < 24f) return Mathf.Lerp(0.18f, 0.58f, Mathf.InverseLerp(12f, 24f, age));
        if (age < 44f) return Mathf.Lerp(0.58f, 1f, Mathf.InverseLerp(24f, 44f, age));
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
                    : 1f - Mathf.SmoothStep(0.02f, 0.92f, d);
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
