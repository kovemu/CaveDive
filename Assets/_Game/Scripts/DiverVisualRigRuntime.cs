using UnityEngine;

// Plug-and-play diver artwork loader.
// Drop either a single diver_full.png or the optional part sprites into
// Assets/_Game/Resources/Diver and the runtime swaps out the prototype blocks.
[DefaultExecutionOrder(-450)]
public sealed class DiverVisualRigRuntime : MonoBehaviour
{
    private const string ResourceRoot = "Diver/";

    private GameObject diver;
    private DiverMotor2D motor;
    private Transform finTop;
    private Transform finBottom;
    private Vector3 finTopBaseEuler;
    private Vector3 finBottomBaseEuler;
    private Transform artRoot;
    private float retryTimer;
    private bool configured;

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

        // Otherwise replace whichever individual parts are available.
        bool anyPart = ApplyPartSprites();
        if (anyPart)
        {
            configured = true;
            Debug.Log("CaveDive: loaded modular Diver sprites.");
            return;
        }

        // Keep the current prototype art until real sprites are added.
        configured = true;
        Debug.Log("CaveDive: no custom diver sprites found; using prototype art.");
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

        // Normalize wildly different source image resolutions to a useful in-game size.
        FitSpriteToSize(renderer, new Vector2(1.72f, 0.74f));
    }

    private bool ApplyPartSprites()
    {
        bool any = false;

        any |= ReplacePart("Body", "diver_body", new Vector2(1.16f, 0.48f), 5, out _);
        any |= ReplacePart("Tank", "diver_tank", new Vector2(0.72f, 0.26f), 4, out _);
        any |= ReplacePart("Head", "diver_head", new Vector2(0.36f, 0.36f), 6, out _);
        any |= ReplacePart("FinTop", "diver_fin_top", new Vector2(0.52f, 0.18f), 4, out finTop);
        any |= ReplacePart("FinBottom", "diver_fin_bottom", new Vector2(0.52f, 0.18f), 4, out finBottom);

        // Optional parts. If no matching prototype child exists, create one.
        any |= CreateOptionalPart("Arm", "diver_arm", new Vector2(0.28f, -0.08f), new Vector2(0.72f, 0.18f), 7);
        any |= CreateOptionalPart("Lamp", "diver_lamp", new Vector2(0.58f, 0.12f), new Vector2(0.26f, 0.12f), 8);

        if (finTop != null)
            finTopBaseEuler = finTop.localEulerAngles;
        if (finBottom != null)
            finBottomBaseEuler = finBottom.localEulerAngles;

        return any;
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
        float kick = Mathf.Sin(Time.time * Mathf.Lerp(2.4f, 7.2f, speed01));
        float amplitude = Mathf.Lerp(2f, 13f, speed01);

        if (finTop != null)
            finTop.localRotation = Quaternion.Euler(finTopBaseEuler + new Vector3(0f, 0f, kick * amplitude));
        if (finBottom != null)
            finBottom.localRotation = Quaternion.Euler(finBottomBaseEuler + new Vector3(0f, 0f, -kick * amplitude));

        if (artRoot != null)
        {
            float bob = Mathf.Sin(Time.time * 1.7f) * 0.012f;
            artRoot.localPosition = new Vector3(0f, bob, 0f);
        }
    }
}
