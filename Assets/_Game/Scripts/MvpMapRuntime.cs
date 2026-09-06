using System.Collections;
using UnityEngine;
using UnityEngine.Rendering.Universal;

// MVP map built from the user's single cave reference image. The source image is
// reduced to a rock/water mask: opaque pixels are rock, transparent pixels are water.
// No outline renderer is used; the cave only becomes visible where a 2D light reaches it.
[DefaultExecutionOrder(-250)]
public sealed class MvpMapRuntime : MonoBehaviour
{
    // Doubled from the earlier 24-unit prototype. The diver keeps the same size,
    // so the cave now reads as a much larger physical space around the player.
    public const float WorldWidth = 48f;
    public const int CollisionColumns = 192;
    public const int CollisionRows = 192;

    // Keep approximately the same 0.25-world-unit collision sampling density as before.
    // Eroding one cell therefore preserves the same small gameplay clearance at the wall.
    private const int CollisionErosionCells = 1;

    private static Texture2D mapMask;
    private static GameObject mapRoot;
    private static Sprite shadowRectSprite;
    private static Material shadowRectMaterial;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("MvpMapRuntime") != null)
            return;

        GameObject runtime = new GameObject("MvpMapRuntime");
        runtime.AddComponent<MvpMapRuntime>();
    }

    private IEnumerator Start()
    {
        yield return null;
        yield return null;

        DisablePrototypeGeometry();
        BuildMap();
        ConfigureDiverCollision();
    }

    private static void DisablePrototypeGeometry()
    {
        GameObject organic = GameObject.Find("OrganicCave");
        if (organic != null)
            organic.SetActive(false);

        string[] legacyNames =
        {
            "Ceiling", "Floor", "LeftWall", "RightWall",
            "Shelf_A", "Shelf_B", "Shelf_C", "Shelf_D",
            "PinchTop", "PinchBottom"
        };

        for (int i = 0; i < legacyNames.Length; i++)
        {
            GameObject legacy = GameObject.Find(legacyNames[i]);
            if (legacy != null)
                legacy.SetActive(false);
        }
    }

    private static void BuildMap()
    {
        if (mapRoot != null || GameObject.Find("MVP Cave Map") != null)
            return;

        mapMask = Resources.Load<Texture2D>("MvpCaveMask");
        if (mapMask == null)
        {
            Debug.LogError("CaveDive: Resources/MvpCaveMask could not be loaded.");
            return;
        }

        mapRoot = new GameObject("MVP Cave Map");
        BuildRockVisual(mapRoot.transform);
        BuildCollisionAndShadowGrid(mapRoot.transform);
        BuildOuterBoundary(mapRoot.transform);
    }

    private static void ConfigureDiverCollision()
    {
        GameObject diver = GameObject.Find("Diver");
        if (diver == null)
            return;

        CapsuleCollider2D capsule = diver.GetComponent<CapsuleCollider2D>();
        if (capsule == null)
            return;

        capsule.size = new Vector2(0.92f, 0.34f);
        capsule.offset = Vector2.zero;
    }

    private static void BuildRockVisual(Transform parent)
    {
        float pixelsPerUnit = mapMask.width / WorldWidth;
        Sprite sprite = Sprite.Create(
            mapMask,
            new Rect(0f, 0f, mapMask.width, mapMask.height),
            new Vector2(0.5f, 0.5f),
            pixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        sprite.name = "MVP Cave Rock Mask Sprite";

        GameObject visual = new GameObject("Rock Visual");
        visual.transform.SetParent(parent, false);

        SpriteRenderer renderer = visual.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = new Color(0.33f, 0.36f, 0.35f, 1f);
        renderer.sortingOrder = 0;

        Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (litShader != null)
            renderer.material = new Material(litShader) { name = "MVP Cave Rock Lit" };
    }

    private static void BuildCollisionAndShadowGrid(Transform parent)
    {
        GameObject collisionRoot = new GameObject("Rock Collision And Shadows");
        collisionRoot.transform.SetParent(parent, false);

        float worldHeight = WorldHeight;
        float cellWidth = WorldWidth / CollisionColumns;
        float cellHeight = worldHeight / CollisionRows;

        for (int row = 0; row < CollisionRows; row++)
        {
            int runStart = -1;

            for (int col = 0; col <= CollisionColumns; col++)
            {
                bool rock = col < CollisionColumns && CellIsRock(col, row);

                if (rock && runStart < 0)
                {
                    runStart = col;
                    continue;
                }

                if (rock || runStart < 0)
                    continue;

                int runEnd = col - 1;
                AddRunColliderAndShadow(collisionRoot.transform, row, runStart, runEnd, cellWidth, cellHeight, worldHeight);
                runStart = -1;
            }
        }
    }

    private static bool CellIsRock(int col, int row)
    {
        if (!RawCellIsRock(col, row))
            return false;

        for (int dy = -CollisionErosionCells; dy <= CollisionErosionCells; dy++)
        {
            for (int dx = -CollisionErosionCells; dx <= CollisionErosionCells; dx++)
            {
                if (dx == 0 && dy == 0)
                    continue;

                if (!RawCellIsRock(col + dx, row + dy))
                    return false;
            }
        }

        return true;
    }

    private static bool RawCellIsRock(int col, int row)
    {
        if (col < 0 || row < 0 || col >= CollisionColumns || row >= CollisionRows)
            return true;

        float u0 = (col + 0.25f) / CollisionColumns;
        float u1 = (col + 0.75f) / CollisionColumns;
        float v0 = (row + 0.25f) / CollisionRows;
        float v1 = (row + 0.75f) / CollisionRows;

        int rockSamples = 0;
        if (mapMask.GetPixelBilinear(u0, v0).a > 0.5f) rockSamples++;
        if (mapMask.GetPixelBilinear(u1, v0).a > 0.5f) rockSamples++;
        if (mapMask.GetPixelBilinear(u0, v1).a > 0.5f) rockSamples++;
        if (mapMask.GetPixelBilinear(u1, v1).a > 0.5f) rockSamples++;
        return rockSamples >= 2;
    }

    private static void AddRunColliderAndShadow(
        Transform root,
        int row,
        int startCol,
        int endCol,
        float cellWidth,
        float cellHeight,
        float worldHeight)
    {
        int count = endCol - startCol + 1;
        float centerX = -WorldWidth * 0.5f + (startCol + count * 0.5f) * cellWidth;
        float centerY = -worldHeight * 0.5f + (row + 0.5f) * cellHeight;
        Vector2 size = new Vector2(count * cellWidth + 0.01f, cellHeight + 0.01f);

        GameObject segment = new GameObject($"Rock Shadow {row}_{startCol}_{endCol}");
        segment.transform.SetParent(root, false);
        segment.transform.localPosition = new Vector3(centerX, centerY, 0f);
        segment.transform.localScale = new Vector3(size.x, size.y, 1f);

        BoxCollider2D collider = segment.AddComponent<BoxCollider2D>();
        collider.size = Vector2.one;

        SpriteRenderer silhouette = segment.AddComponent<SpriteRenderer>();
        silhouette.sprite = GetShadowRectSprite();
        silhouette.color = new Color(1f, 1f, 1f, 0.001f);
        silhouette.sortingOrder = -100;
        if (shadowRectMaterial != null)
            silhouette.sharedMaterial = shadowRectMaterial;

        ShadowCaster2D caster = segment.AddComponent<ShadowCaster2D>();
        caster.useRendererSilhouette = true;
        caster.selfShadows = false;
        caster.castsShadows = true;
    }

    private static Sprite GetShadowRectSprite()
    {
        if (shadowRectSprite != null)
            return shadowRectSprite;

        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "RuntimeShadowRectPixel";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply(false, true);

        shadowRectSprite = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);
        shadowRectSprite.name = "RuntimeShadowRectSprite";

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader != null)
            shadowRectMaterial = new Material(shader) { name = "Runtime Shadow Silhouette Material" };

        return shadowRectSprite;
    }

    private static void BuildOuterBoundary(Transform parent)
    {
        GameObject boundary = new GameObject("Map Boundary");
        boundary.transform.SetParent(parent, false);

        float h = WorldHeight;
        const float thickness = 0.55f;

        AddBoundaryCollider(boundary, new Vector2(0f, h * 0.5f + thickness * 0.5f), new Vector2(WorldWidth + thickness * 2f, thickness));
        AddBoundaryCollider(boundary, new Vector2(0f, -h * 0.5f - thickness * 0.5f), new Vector2(WorldWidth + thickness * 2f, thickness));
        AddBoundaryCollider(boundary, new Vector2(-WorldWidth * 0.5f - thickness * 0.5f, 0f), new Vector2(thickness, h));
        AddBoundaryCollider(boundary, new Vector2(WorldWidth * 0.5f + thickness * 0.5f, 0f), new Vector2(thickness, h));
    }

    private static void AddBoundaryCollider(GameObject root, Vector2 offset, Vector2 size)
    {
        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.offset = offset;
        collider.size = size;
    }

    public static float WorldHeight
    {
        get
        {
            if (mapMask != null && mapMask.width > 0)
                return WorldWidth * mapMask.height / mapMask.width;

            return WorldWidth * 510f / 512f;
        }
    }
}
