using System.Collections;
using UnityEngine;

// MVP map built from the user's single cave reference image. The source image is
// reduced to a rock/water mask: opaque pixels are rock, transparent pixels are water.
// No outline renderer is used; the cave only becomes visible where a 2D light reaches it.
[DefaultExecutionOrder(-250)]
public sealed class MvpMapRuntime : MonoBehaviour
{
    public const float WorldWidth = 24f;
    public const int CollisionColumns = 96;
    public const int CollisionRows = 96;

    // Collision is intentionally a little more forgiving than the visual silhouette.
    // One grid cell is roughly 0.25 world units, so eroding one cell opens a narrow
    // passage by about 0.5 units total without visibly changing the map art.
    private const int CollisionErosionCells = 1;

    private static Texture2D mapMask;
    private static GameObject mapRoot;

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
        // Let the old prototype/organic map instantiate first, then retire it.
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
        BuildCollisionGrid(mapRoot.transform);
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

        // The visible fins/tank should not make every tight cave opening impossible.
        // Keep the hitbox around the diver's actual torso rather than the full silhouette.
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

    private static void BuildCollisionGrid(Transform parent)
    {
        GameObject collisionRoot = new GameObject("Rock Collision");
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
                AddRunCollider(collisionRoot, row, runStart, runEnd, cellWidth, cellHeight, worldHeight);
                runStart = -1;
            }
        }
    }

    private static bool CellIsRock(int col, int row)
    {
        if (!RawCellIsRock(col, row))
            return false;

        // Erode the collision boundary inward by one grid cell. Any rock cell that is
        // adjacent to navigable water is omitted from collision, widening cramped necks
        // while leaving the rendered cave image exactly as the user drew it.
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
        // Outside the sampled map stays solid; the explicit outer boundary handles escape too.
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

    private static void AddRunCollider(
        GameObject root,
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

        BoxCollider2D collider = root.AddComponent<BoxCollider2D>();
        collider.offset = new Vector2(centerX, centerY);
        collider.size = new Vector2(count * cellWidth + 0.01f, cellHeight + 0.01f);
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

            // Source mask is 512 x 510.
            return WorldWidth * 510f / 512f;
        }
    }
}
