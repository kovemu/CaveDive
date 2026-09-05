using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[DefaultExecutionOrder(-400)]
public sealed class CaveOrganicGeometryRuntime : MonoBehaviour
{
    private const float MinX = -16f;
    private const float MaxX = 24f;
    private const float MinY = -7f;
    private const float MaxY = 7f;
    private const float PixelsPerUnit = 32f;

    private static readonly Vector2[] CeilingBoundary =
    {
        new Vector2(-16.0f, 3.6f),
        new Vector2(-14.6f, 3.5f),
        new Vector2(-13.4f, 3.1f),
        new Vector2(-12.2f, 2.7f),
        new Vector2(-10.8f, 2.9f),
        new Vector2(-9.5f, 3.2f),
        new Vector2(-8.2f, 2.7f),
        new Vector2(-7.0f, 2.0f),
        new Vector2(-5.8f, 1.55f),
        new Vector2(-4.8f, 1.9f),
        new Vector2(-3.7f, 2.65f),
        new Vector2(-2.4f, 2.85f),
        new Vector2(-1.0f, 2.45f),
        new Vector2(0.1f, 1.65f),
        new Vector2(1.0f, 0.95f),
        new Vector2(2.0f, 0.72f),
        new Vector2(3.0f, 1.2f),
        new Vector2(4.0f, 1.95f),
        new Vector2(5.2f, 2.28f),
        new Vector2(6.4f, 2.05f),
        new Vector2(7.4f, 1.35f),
        new Vector2(8.2f, 0.52f),
        new Vector2(9.0f, 0.25f),
        new Vector2(9.8f, 0.55f),
        new Vector2(10.8f, 1.35f),
        new Vector2(12.0f, 1.95f),
        new Vector2(13.2f, 2.15f),
        new Vector2(14.3f, 1.85f),
        new Vector2(15.2f, 1.15f),
        new Vector2(15.9f, 0.42f),
        new Vector2(16.6f, 0.12f),
        new Vector2(17.3f, 0.28f),
        new Vector2(18.0f, 0.82f),
        new Vector2(18.8f, 1.45f),
        new Vector2(19.8f, 1.85f),
        new Vector2(20.9f, 2.15f),
        new Vector2(22.1f, 2.55f),
        new Vector2(24.0f, 2.95f)
    };

    private static readonly Vector2[] FloorBoundary =
    {
        new Vector2(-16.0f, -3.55f),
        new Vector2(-14.8f, -3.35f),
        new Vector2(-13.5f, -2.95f),
        new Vector2(-12.1f, -2.55f),
        new Vector2(-10.9f, -2.45f),
        new Vector2(-9.8f, -2.75f),
        new Vector2(-8.7f, -3.15f),
        new Vector2(-7.7f, -3.25f),
        new Vector2(-6.5f, -2.78f),
        new Vector2(-5.3f, -2.12f),
        new Vector2(-4.1f, -1.68f),
        new Vector2(-3.0f, -1.55f),
        new Vector2(-1.9f, -1.85f),
        new Vector2(-0.8f, -2.55f),
        new Vector2(0.2f, -3.25f),
        new Vector2(1.2f, -3.65f),
        new Vector2(2.3f, -3.72f),
        new Vector2(3.2f, -3.25f),
        new Vector2(4.0f, -2.45f),
        new Vector2(4.7f, -1.55f),
        new Vector2(5.4f, -0.82f),
        new Vector2(6.2f, -0.45f),
        new Vector2(7.0f, -0.62f),
        new Vector2(7.8f, -1.15f),
        new Vector2(8.7f, -1.82f),
        new Vector2(9.6f, -2.35f),
        new Vector2(10.8f, -2.62f),
        new Vector2(12.0f, -2.42f),
        new Vector2(13.1f, -1.88f),
        new Vector2(14.0f, -1.12f),
        new Vector2(14.8f, -0.72f),
        new Vector2(15.6f, -0.88f),
        new Vector2(16.3f, -1.32f),
        new Vector2(17.0f, -1.92f),
        new Vector2(17.8f, -2.38f),
        new Vector2(18.7f, -2.62f),
        new Vector2(19.7f, -2.52f),
        new Vector2(20.8f, -2.28f),
        new Vector2(22.0f, -2.55f),
        new Vector2(24.0f, -3.05f)
    };

    private static readonly string[] LegacyWallNames =
    {
        "Ceiling", "Floor", "LeftWall", "RightWall",
        "Shelf_A", "Shelf_B", "Shelf_C", "Shelf_D",
        "PinchTop", "PinchBottom"
    };

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("CaveOrganicGeometryRuntime") != null)
            return;

        var runtime = new GameObject("CaveOrganicGeometryRuntime");
        runtime.AddComponent<CaveOrganicGeometryRuntime>();
    }

    private IEnumerator Start()
    {
        // The prototype creates its objects after the scene loads too, so wait a frame.
        yield return null;

        DisableLegacyGeometry();
        BuildOrganicCave();
    }

    private static void DisableLegacyGeometry()
    {
        for (int i = 0; i < LegacyWallNames.Length; i++)
        {
            GameObject wall = GameObject.Find(LegacyWallNames[i]);
            if (wall == null)
                continue;

            SpriteRenderer renderer = wall.GetComponent<SpriteRenderer>();
            if (renderer != null)
                renderer.enabled = false;

            Collider2D collider = wall.GetComponent<Collider2D>();
            if (collider != null)
                collider.enabled = false;
        }
    }

    private static void BuildOrganicCave()
    {
        if (GameObject.Find("OrganicCave") != null)
            return;

        GameObject root = new GameObject("OrganicCave");

        int width = Mathf.RoundToInt((MaxX - MinX) * PixelsPerUnit);
        int height = Mathf.RoundToInt((MaxY - MinY) * PixelsPerUnit);

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "RuntimeOrganicCaveTexture",
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp
        };

        Color[] pixels = new Color[width * height];
        Color baseRock = new Color(0.095f, 0.105f, 0.10f, 1f);

        for (int py = 0; py < height; py++)
        {
            float worldY = MinY + (py + 0.5f) / PixelsPerUnit;

            for (int px = 0; px < width; px++)
            {
                float worldX = MinX + (px + 0.5f) / PixelsPerUnit;
                float ceilingY = SampleBoundary(CeilingBoundary, worldX);
                float floorY = SampleBoundary(FloorBoundary, worldX);
                bool rock = worldY >= ceilingY || worldY <= floorY;

                if (!rock)
                {
                    pixels[py * width + px] = Color.clear;
                    continue;
                }

                // Barely-visible monochrome mottling: enough to keep the wall from looking vector-perfect,
                // but still in the simple accident-diagram visual language.
                float n = Mathf.PerlinNoise((worldX + 31.7f) * 0.72f, (worldY + 18.2f) * 0.72f);
                float detail = Mathf.Lerp(0.82f, 1.14f, n);
                pixels[py * width + px] = new Color(
                    baseRock.r * detail,
                    baseRock.g * detail,
                    baseRock.b * detail,
                    1f);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(false, true);

        Vector2 pivot = new Vector2(
            -MinX / (MaxX - MinX),
            -MinY / (MaxY - MinY));

        Sprite sprite = Sprite.Create(
            texture,
            new Rect(0f, 0f, width, height),
            pivot,
            PixelsPerUnit,
            0,
            SpriteMeshType.FullRect);
        sprite.name = "RuntimeOrganicCaveSprite";

        SpriteRenderer spriteRenderer = root.AddComponent<SpriteRenderer>();
        spriteRenderer.sprite = sprite;
        spriteRenderer.sortingOrder = 0;

        Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (litShader != null)
            spriteRenderer.material = new Material(litShader);

        BuildColliders(root.transform);
        BuildEdgeLine(root.transform, "CeilingEdge", CeilingBoundary);
        BuildEdgeLine(root.transform, "FloorEdge", FloorBoundary);
    }

    private static void BuildColliders(Transform parent)
    {
        GameObject ceiling = new GameObject("OrganicCeilingCollider");
        ceiling.transform.SetParent(parent);
        PolygonCollider2D ceilingCollider = ceiling.AddComponent<PolygonCollider2D>();

        List<Vector2> ceilingPoints = new List<Vector2>();
        ceilingPoints.Add(new Vector2(MinX, MaxY));
        ceilingPoints.Add(new Vector2(MaxX, MaxY));
        for (int i = CeilingBoundary.Length - 1; i >= 0; i--)
            ceilingPoints.Add(CeilingBoundary[i]);
        ceilingCollider.points = ceilingPoints.ToArray();

        GameObject floor = new GameObject("OrganicFloorCollider");
        floor.transform.SetParent(parent);
        PolygonCollider2D floorCollider = floor.AddComponent<PolygonCollider2D>();

        List<Vector2> floorPoints = new List<Vector2>();
        floorPoints.Add(new Vector2(MinX, MinY));
        for (int i = 0; i < FloorBoundary.Length; i++)
            floorPoints.Add(FloorBoundary[i]);
        floorPoints.Add(new Vector2(MaxX, MinY));
        floorCollider.points = floorPoints.ToArray();
    }

    private static void BuildEdgeLine(Transform parent, string name, Vector2[] points)
    {
        GameObject lineObject = new GameObject(name);
        lineObject.transform.SetParent(parent);

        LineRenderer line = lineObject.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.positionCount = points.Length;
        line.startWidth = 0.035f;
        line.endWidth = 0.035f;
        line.startColor = new Color(0.26f, 0.29f, 0.28f, 0.72f);
        line.endColor = line.startColor;
        line.numCapVertices = 2;
        line.numCornerVertices = 2;
        line.sortingOrder = 1;

        Vector3[] positions = new Vector3[points.Length];
        for (int i = 0; i < points.Length; i++)
            positions[i] = new Vector3(points[i].x, points[i].y, 0f);
        line.SetPositions(positions);

        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader != null)
            line.material = new Material(shader);
    }

    private static float SampleBoundary(Vector2[] points, float x)
    {
        if (x <= points[0].x)
            return points[0].y;
        if (x >= points[points.Length - 1].x)
            return points[points.Length - 1].y;

        for (int i = 0; i < points.Length - 1; i++)
        {
            Vector2 a = points[i];
            Vector2 b = points[i + 1];
            if (x < a.x || x > b.x)
                continue;

            float t = Mathf.InverseLerp(a.x, b.x, x);
            return Mathf.Lerp(a.y, b.y, t);
        }

        return 0f;
    }
}
