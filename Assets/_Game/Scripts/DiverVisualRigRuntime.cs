using System.Collections.Generic;
using UnityEngine;

// Plug-and-play diver artwork loader.
// Priority:
// 1) Resources/Diver/diver_full.png
// 2) modular Resources/Diver/diver_*.png parts
// 3) built-in procedural modular diver (keeps the prototype playable without external art)
[DefaultExecutionOrder(-450)]
public sealed class DiverVisualRigRuntime : MonoBehaviour
{
    private const string ResourceRoot = "Diver/";

    private GameObject diver;
    private DiverMotor2D motor;
    private Transform finTop;
    private Transform finBottom;
    private Transform rearLegTop;
    private Transform rearLegBottom;
    private Vector3 finTopBaseEuler;
    private Vector3 finBottomBaseEuler;
    private Vector3 rearLegTopBaseEuler;
    private Vector3 rearLegBottomBaseEuler;
    private Transform artRoot;
    private float retryTimer;
    private bool configured;

    private static Material generatedLitMaterial;
    private static Sprite roundedLongSprite;
    private static Sprite roundedShortSprite;
    private static Sprite ellipseSprite;
    private static Sprite finSprite;
    private static Sprite maskSprite;
    private static Sprite tankSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("DiverVisualRigRuntime") != null)
            return;

        GameObject go = new GameObject("DiverVisualRigRuntime");
        go.AddComponent<DiverVisualRigRuntime>();
    }

    private void Update()
    {
        if (!configured)
        {
            retryTimer -= Time.deltaTime;
            if (retryTimer <= 0f)
            {
                retryTimer = 0.15f;
                TryConfigure();
            }
            return;
        }

        AnimateParts();
    }

    private void TryConfigure()
    {
        diver = GameObject.Find("Diver");
        if (diver == null)
            return;

        motor = diver.GetComponent<DiverMotor2D>();

        // A single full-body sprite is the easiest path and takes priority.
        Sprite full = Resources.Load<Sprite>(ResourceRoot + "diver_full");
        if (full != null)
        {
            ApplyFullBody(full);
            configured = true;
            Debug.Log("CaveDive: loaded Diver/diver_full sprite.");
            return;
        }

        // Otherwise replace whichever individual external parts are available.
        bool anyPart = ApplyPartSprites();
        if (anyPart)
        {
            configured = true;
            Debug.Log("CaveDive: loaded modular Diver sprites.");
            return;
        }

        // No external asset yet: use a more realistic modular side-view cave diver
        // instead of the original rectangular placeholder blocks.
        BuildProceduralModularDiver();
        configured = true;
        Debug.Log("CaveDive: using built-in procedural modular cave diver art.");
    }

    private void ApplyFullBody(Sprite sprite)
    {
        DisablePrototypeRenderers();

        artRoot = new GameObject("DiverArt").transform;
        artRoot.SetParent(diver.transform, false);
        artRoot.localPosition = Vector3.zero;
        artRoot.localRotation = Quaternion.identity;

        SpriteRenderer renderer = artRoot.gameObject.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = 6;
        renderer.color = Color.white;

        FitSpriteToSize(renderer, new Vector2(1.90f, 0.84f));
    }

    private bool ApplyPartSprites()
    {
        bool any = false;

        any |= ReplacePart("Body", "diver_body", new Vector2(1.18f, 0.48f), 5, out _);
        any |= ReplacePart("Tank", "diver_tank", new Vector2(0.76f, 0.28f), 4, out _);
        any |= ReplacePart("Head", "diver_head", new Vector2(0.38f, 0.38f), 6, out _);
        any |= ReplacePart("FinTop", "diver_fin_top", new Vector2(0.62f, 0.22f), 4, out finTop);
        any |= ReplacePart("FinBottom", "diver_fin_bottom", new Vector2(0.62f, 0.22f), 4, out finBottom);

        any |= CreateOptionalPart("Arm", "diver_arm", new Vector2(0.43f, -0.10f), new Vector2(0.72f, 0.18f), 7);
        any |= CreateOptionalPart("Lamp", "diver_lamp", new Vector2(0.76f, 0.08f), new Vector2(0.22f, 0.10f), 8);

        CacheAnimationBases();
        return any;
    }

    private void BuildProceduralModularDiver()
    {
        DisablePrototypeRenderers();
        EnsureGeneratedResources();

        artRoot = new GameObject("DiverArt").transform;
        artRoot.SetParent(diver.transform, false);
        artRoot.localPosition = Vector3.zero;
        artRoot.localRotation = Quaternion.identity;

        // Palette: dark technical cave-diving suit with muted metal tank and blue-grey fins.
        Color suit = new Color(0.075f, 0.095f, 0.105f, 1f);
        Color suitHighlight = new Color(0.13f, 0.16f, 0.17f, 1f);
        Color tank = new Color(0.43f, 0.46f, 0.45f, 1f);
        Color tankHighlight = new Color(0.62f, 0.65f, 0.62f, 1f);
        Color mask = new Color(0.12f, 0.31f, 0.36f, 1f);
        Color fin = new Color(0.16f, 0.29f, 0.35f, 1f);
        Color strap = new Color(0.055f, 0.062f, 0.065f, 1f);
        Color lamp = new Color(0.72f, 0.76f, 0.67f, 1f);

        // Rear equipment first.
        CreateGeneratedPart("TankShadow", tankSprite, new Vector2(-0.16f, 0.265f), new Vector2(0.90f, 0.24f), -3f, tank, 4);
        CreateGeneratedPart("TankHighlight", roundedLongSprite, new Vector2(-0.06f, 0.305f), new Vector2(0.56f, 0.055f), -3f, tankHighlight, 5);
        CreateGeneratedPart("TankValve", roundedShortSprite, new Vector2(0.30f, 0.275f), new Vector2(0.14f, 0.10f), -3f, strap, 5);

        // Legs are separate so the fins can kick without rotating the torso.
        rearLegTop = CreateGeneratedPart("RearLegTop", roundedLongSprite, new Vector2(-0.54f, 0.095f), new Vector2(0.58f, 0.145f), 6f, suit, 5);
        rearLegBottom = CreateGeneratedPart("RearLegBottom", roundedLongSprite, new Vector2(-0.55f, -0.095f), new Vector2(0.58f, 0.145f), -6f, suit, 5);

        finTop = CreateGeneratedPart("FinTop", finSprite, new Vector2(-0.93f, 0.16f), new Vector2(0.68f, 0.23f), 7f, fin, 4);
        finBottom = CreateGeneratedPart("FinBottom", finSprite, new Vector2(-0.93f, -0.16f), new Vector2(0.68f, 0.23f), -7f, fin, 4);

        // Main body / buoyancy compensator.
        CreateGeneratedPart("Torso", roundedLongSprite, new Vector2(0.00f, 0.00f), new Vector2(1.20f, 0.38f), 0f, suit, 6);
        CreateGeneratedPart("BCD", roundedLongSprite, new Vector2(-0.10f, 0.02f), new Vector2(0.72f, 0.31f), 0f, suitHighlight, 7);
        CreateGeneratedPart("WaistStrap", roundedLongSprite, new Vector2(-0.23f, -0.03f), new Vector2(0.08f, 0.42f), 0f, strap, 8);
        CreateGeneratedPart("ChestStrap", roundedLongSprite, new Vector2(0.12f, 0.04f), new Vector2(0.055f, 0.34f), -12f, strap, 8);

        // Hooded head and low-profile dive mask.
        CreateGeneratedPart("Head", ellipseSprite, new Vector2(0.63f, 0.035f), new Vector2(0.34f, 0.34f), 0f, suit, 8);
        CreateGeneratedPart("Mask", maskSprite, new Vector2(0.755f, 0.065f), new Vector2(0.27f, 0.15f), 0f, mask, 10);
        CreateGeneratedPart("MaskGlass", roundedShortSprite, new Vector2(0.79f, 0.078f), new Vector2(0.16f, 0.07f), 0f, new Color(0.26f, 0.50f, 0.55f, 0.82f), 11);
        CreateGeneratedPart("Regulator", ellipseSprite, new Vector2(0.765f, -0.055f), new Vector2(0.115f, 0.105f), 0f, strap, 10);

        // Forward arm, similar to the reference silhouette: one arm reaching ahead.
        Transform upperArm = CreateGeneratedPart("UpperArm", roundedLongSprite, new Vector2(0.43f, -0.105f), new Vector2(0.52f, 0.145f), -18f, suit, 8);
        Transform foreArm = CreateGeneratedPart("ForeArm", roundedLongSprite, new Vector2(0.72f, -0.15f), new Vector2(0.46f, 0.125f), 5f, suitHighlight, 9);
        CreateGeneratedPart("Glove", ellipseSprite, new Vector2(0.955f, -0.13f), new Vector2(0.14f, 0.12f), 0f, strap, 10);

        // Small head-mounted cave light keeps the visual consistent with the current light source.
        CreateGeneratedPart("LampMount", roundedShortSprite, new Vector2(0.69f, 0.205f), new Vector2(0.18f, 0.08f), -4f, strap, 11);
        CreateGeneratedPart("Lamp", roundedShortSprite, new Vector2(0.79f, 0.205f), new Vector2(0.16f, 0.09f), -4f, lamp, 12);

        // Hose gives the equipment a less toy-like silhouette. It is deliberately subtle.
        CreateHose(new Vector2(0.18f, 0.25f), new Vector2(0.54f, 0.12f), new Vector2(0.70f, -0.02f), strap);

        // Prevent compiler warnings if later we decide to animate the arms.
        _ = upperArm;
        _ = foreArm;

        CacheAnimationBases();
    }

    private void CreateHose(Vector2 a, Vector2 b, Vector2 c, Color color)
    {
        GameObject hose = new GameObject("RegulatorHose");
        hose.transform.SetParent(artRoot, false);
        LineRenderer line = hose.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = 3;
        line.SetPosition(0, a);
        line.SetPosition(1, b);
        line.SetPosition(2, c);
        line.startWidth = 0.025f;
        line.endWidth = 0.025f;
        line.startColor = color;
        line.endColor = color;
        line.numCapVertices = 3;
        line.numCornerVertices = 3;
        line.sortingOrder = 9;

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader != null)
            line.material = new Material(shader) { name = "Runtime Diver Hose Material" };
    }

    private Transform CreateGeneratedPart(string name, Sprite sprite, Vector2 localPosition, Vector2 targetSize, float rotation, Color color, int sortingOrder)
    {
        GameObject go = new GameObject(name);
        Transform child = go.transform;
        child.SetParent(artRoot, false);
        child.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        child.localRotation = Quaternion.Euler(0f, 0f, rotation);

        SpriteRenderer renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        if (generatedLitMaterial != null)
            renderer.sharedMaterial = generatedLitMaterial;
        FitSpriteToSize(renderer, targetSize);
        return child;
    }

    private void CacheAnimationBases()
    {
        if (finTop != null)
            finTopBaseEuler = finTop.localEulerAngles;
        if (finBottom != null)
            finBottomBaseEuler = finBottom.localEulerAngles;
        if (rearLegTop != null)
            rearLegTopBaseEuler = rearLegTop.localEulerAngles;
        if (rearLegBottom != null)
            rearLegBottomBaseEuler = rearLegBottom.localEulerAngles;
    }

    private bool ReplacePart(string childName, string resourceName, Vector2 targetSize, int sortingOrder, out Transform part)
    {
        part = diver.transform.Find(childName);
        Sprite sprite = Resources.Load<Sprite>(ResourceRoot + resourceName);
        if (sprite == null || part == null)
            return false;

        SpriteRenderer renderer = part.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = part.gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingOrder = sortingOrder;
        FitSpriteToSize(renderer, targetSize);
        return true;
    }

    private bool CreateOptionalPart(string childName, string resourceName, Vector2 localPosition, Vector2 targetSize, int sortingOrder)
    {
        Sprite sprite = Resources.Load<Sprite>(ResourceRoot + resourceName);
        if (sprite == null)
            return false;

        Transform child = diver.transform.Find(childName);
        if (child == null)
        {
            GameObject go = new GameObject(childName);
            child = go.transform;
            child.SetParent(diver.transform, false);
            child.localPosition = localPosition;
        }

        SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
        if (renderer == null)
            renderer = child.gameObject.AddComponent<SpriteRenderer>();

        renderer.sprite = sprite;
        renderer.color = Color.white;
        renderer.sortingOrder = sortingOrder;
        FitSpriteToSize(renderer, targetSize);
        return true;
    }

    private void DisablePrototypeRenderers()
    {
        string[] names = { "Body", "Tank", "Head", "FinTop", "FinBottom" };
        for (int i = 0; i < names.Length; i++)
        {
            Transform child = diver.transform.Find(names[i]);
            if (child == null)
                continue;

            SpriteRenderer renderer = child.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.enabled = false;
        }
    }

    private static void EnsureGeneratedResources()
    {
        if (generatedLitMaterial == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
            if (shader == null)
                shader = Shader.Find("Sprites/Default");
            if (shader != null)
                generatedLitMaterial = new Material(shader) { name = "Runtime Procedural Diver Lit" };
        }

        if (roundedLongSprite == null)
            roundedLongSprite = BuildRoundedRectSprite("Diver Rounded Long", 128, 48, 0.32f);
        if (roundedShortSprite == null)
            roundedShortSprite = BuildRoundedRectSprite("Diver Rounded Short", 64, 40, 0.42f);
        if (ellipseSprite == null)
            ellipseSprite = BuildEllipseSprite("Diver Ellipse", 64, 64);
        if (maskSprite == null)
            maskSprite = BuildRoundedRectSprite("Diver Mask", 72, 42, 0.28f);
        if (tankSprite == null)
            tankSprite = BuildCapsuleSprite("Diver Tank", 128, 42);
        if (finSprite == null)
        {
            Vector2[] polygon =
            {
                new Vector2(-0.50f, -0.20f),
                new Vector2(-0.37f,  0.00f),
                new Vector2(-0.50f,  0.32f),
                new Vector2( 0.32f,  0.18f),
                new Vector2( 0.50f,  0.04f),
                new Vector2( 0.34f, -0.14f)
            };
            finSprite = BuildPolygonSprite("Diver Fin", 128, 64, polygon);
        }
    }

    private static Sprite BuildRoundedRectSprite(string name, int width, int height, float radius01)
    {
        Texture2D texture = NewMaskTexture(name, width, height);
        Color[] pixels = new Color[width * height];
        float radius = Mathf.Min(width, height) * radius01;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float px = Mathf.Abs(x + 0.5f - width * 0.5f);
                float py = Mathf.Abs(y + 0.5f - height * 0.5f);
                float qx = Mathf.Max(px - (width * 0.5f - radius), 0f);
                float qy = Mathf.Max(py - (height * 0.5f - radius), 0f);
                float outside = Mathf.Sqrt(qx * qx + qy * qy) - radius;
                float alpha = Mathf.Clamp01(0.75f - outside);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        return FinishMaskSprite(texture, pixels, name);
    }

    private static Sprite BuildCapsuleSprite(string name, int width, int height)
    {
        Texture2D texture = NewMaskTexture(name, width, height);
        Color[] pixels = new Color[width * height];
        float radius = height * 0.46f;
        float halfStraight = width * 0.5f - radius;

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float px = Mathf.Abs(x + 0.5f - width * 0.5f);
                float py = y + 0.5f - height * 0.5f;
                float dx = Mathf.Max(px - halfStraight, 0f);
                float d = Mathf.Sqrt(dx * dx + py * py) - radius;
                float alpha = Mathf.Clamp01(0.75f - d);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        return FinishMaskSprite(texture, pixels, name);
    }

    private static Sprite BuildEllipseSprite(string name, int width, int height)
    {
        Texture2D texture = NewMaskTexture(name, width, height);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float nx = ((x + 0.5f) / width - 0.5f) * 2f;
                float ny = ((y + 0.5f) / height - 0.5f) * 2f;
                float d = Mathf.Sqrt(nx * nx + ny * ny);
                float alpha = 1f - Mathf.SmoothStep(0.94f, 1.02f, d);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        return FinishMaskSprite(texture, pixels, name);
    }

    private static Sprite BuildPolygonSprite(string name, int width, int height, Vector2[] polygon)
    {
        Texture2D texture = NewMaskTexture(name, width, height);
        Color[] pixels = new Color[width * height];

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Vector2 p = new Vector2((x + 0.5f) / width - 0.5f, (y + 0.5f) / height - 0.5f);
                float alpha = PointInPolygon(p, polygon) ? 1f : 0f;
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        return FinishMaskSprite(texture, pixels, name);
    }

    private static bool PointInPolygon(Vector2 p, Vector2[] polygon)
    {
        bool inside = false;
        for (int i = 0, j = polygon.Length - 1; i < polygon.Length; j = i++)
        {
            Vector2 a = polygon[i];
            Vector2 b = polygon[j];
            bool intersect = ((a.y > p.y) != (b.y > p.y)) &&
                             (p.x < (b.x - a.x) * (p.y - a.y) / Mathf.Max(0.00001f, b.y - a.y) + a.x);
            if (intersect)
                inside = !inside;
        }
        return inside;
    }

    private static Texture2D NewMaskTexture(string name, int width, int height)
    {
        return new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = name + " Texture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };
    }

    private static Sprite FinishMaskSprite(Texture2D texture, Color[] pixels, string name)
    {
        texture.SetPixels(pixels);
        texture.Apply(false, true);
        Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f), 64f);
        sprite.name = name + " Sprite";
        return sprite;
    }

    private static void FitSpriteToSize(SpriteRenderer renderer, Vector2 targetSize)
    {
        if (renderer == null || renderer.sprite == null)
            return;

        Vector2 source = renderer.sprite.bounds.size;
        if (source.x <= 0.0001f || source.y <= 0.0001f)
            return;

        renderer.transform.localScale = new Vector3(
            targetSize.x / source.x,
            targetSize.y / source.y,
            1f);
    }

    private void AnimateParts()
    {
        if (motor == null)
            return;

        float speed01 = Mathf.Clamp01(motor.Speed / 4f);
        float kick = Mathf.Sin(Time.time * Mathf.Lerp(2.1f, 6.4f, speed01));
        float finAmplitude = Mathf.Lerp(1.5f, 11f, speed01);
        float legAmplitude = Mathf.Lerp(0.5f, 4.0f, speed01);

        if (finTop != null)
            finTop.localRotation = Quaternion.Euler(finTopBaseEuler + new Vector3(0f, 0f, kick * finAmplitude));
        if (finBottom != null)
            finBottom.localRotation = Quaternion.Euler(finBottomBaseEuler + new Vector3(0f, 0f, -kick * finAmplitude));
        if (rearLegTop != null)
            rearLegTop.localRotation = Quaternion.Euler(rearLegTopBaseEuler + new Vector3(0f, 0f, kick * legAmplitude));
        if (rearLegBottom != null)
            rearLegBottom.localRotation = Quaternion.Euler(rearLegBottomBaseEuler + new Vector3(0f, 0f, -kick * legAmplitude));

        if (artRoot != null)
        {
            float bob = Mathf.Sin(Time.time * 1.45f) * 0.010f;
            artRoot.localPosition = new Vector3(0f, bob, 0f);
        }
    }
}
