using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public static class CaveDivePrototypeBootstrap
{
    private static Sprite solidSprite;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void BuildPrototype()
    {
        if (GameObject.Find("CaveDivePrototype") != null)
            return;

        var root = new GameObject("CaveDivePrototype");
        var game = root.AddComponent<CaveDivePrototypeGame>();

        ConfigureCamera();
        BuildCave(root.transform);

        var diver = BuildDiver(root.transform);
        var guideline = BuildGuideline(root.transform, diver.transform);

        BuildTrigger(root.transform, "Entrance", new Vector2(-12.5f, 0f), new Vector2(1.4f, 5.5f), CaveTrigger.Kind.Entrance, game);
        BuildTrigger(root.transform, "Target", new Vector2(20f, -1.8f), new Vector2(1.4f, 3.2f), CaveTrigger.Kind.Target, game);

        CreateSolid("Entrance Marker", root.transform, new Vector2(-13.1f, 0f), new Vector2(0.12f, 4.8f), new Color(0.72f, 0.78f, 0.78f), 1);
        CreateSolid("Target Marker", root.transform, new Vector2(20.5f, -1.8f), new Vector2(0.16f, 2.5f), new Color(0.82f, 0.68f, 0.38f), 1);

        var motor = diver.GetComponent<DiverMotor2D>();
        var oxygen = diver.GetComponent<OxygenTank>();
        game.Initialize(diver, motor, oxygen, guideline, new Vector2(-11.5f, 0f));

        diver.transform.position = new Vector3(-11.5f, 0f, 0f);
    }

    private static void ConfigureCamera()
    {
        var camera = Camera.main;
        if (camera == null)
            camera = Object.FindFirstObjectByType<Camera>();

        if (camera == null)
            return;

        camera.orthographic = true;
        camera.orthographicSize = 6.2f;
        camera.backgroundColor = new Color(0.055f, 0.12f, 0.16f);

        var follow = camera.GetComponent<SmoothCameraFollow>();
        if (follow == null)
            follow = camera.gameObject.AddComponent<SmoothCameraFollow>();
    }

    private static GameObject BuildDiver(Transform parent)
    {
        var diver = new GameObject("Diver");
        diver.transform.SetParent(parent);

        var rb = diver.AddComponent<Rigidbody2D>();
        rb.gravityScale = 0f;
        rb.linearDamping = 2.8f;
        rb.angularDamping = 4f;
        rb.collisionDetectionMode = CollisionDetectionMode2D.Continuous;
        rb.interpolation = RigidbodyInterpolation2D.Interpolate;

        var collider = diver.AddComponent<CapsuleCollider2D>();
        collider.size = new Vector2(1.25f, 0.5f);
        collider.direction = CapsuleDirection2D.Horizontal;

        diver.AddComponent<DiverMotor2D>();
        diver.AddComponent<OxygenTank>();

        CreateSolid("Body", diver.transform, Vector2.zero, new Vector2(1.15f, 0.42f), new Color(0.83f, 0.72f, 0.54f), 5);
        CreateSolid("Tank", diver.transform, new Vector2(-0.18f, 0.31f), new Vector2(0.66f, 0.20f), new Color(0.42f, 0.44f, 0.40f), 4);
        CreateSolid("Head", diver.transform, new Vector2(0.61f, 0.03f), new Vector2(0.30f, 0.30f), new Color(0.91f, 0.80f, 0.61f), 6);
        CreateSolid("FinTop", diver.transform, new Vector2(-0.70f, 0.20f), new Vector2(0.42f, 0.13f), new Color(0.66f, 0.59f, 0.47f), 4);
        CreateSolid("FinBottom", diver.transform, new Vector2(-0.70f, -0.20f), new Vector2(0.42f, 0.13f), new Color(0.66f, 0.59f, 0.47f), 4);

        var camera = Camera.main;
        if (camera != null)
        {
            var follow = camera.GetComponent<SmoothCameraFollow>();
            if (follow != null)
                follow.Target = diver.transform;
        }

        return diver;
    }

    private static GuidelineTrail BuildGuideline(Transform parent, Transform diver)
    {
        var lineObject = new GameObject("Guideline");
        lineObject.transform.SetParent(parent);

        var renderer = lineObject.AddComponent<LineRenderer>();
        renderer.useWorldSpace = true;
        renderer.startWidth = 0.055f;
        renderer.endWidth = 0.055f;
        renderer.startColor = new Color(0.88f, 0.78f, 0.34f);
        renderer.endColor = new Color(0.88f, 0.78f, 0.34f);
        renderer.numCapVertices = 2;
        renderer.sortingOrder = 2;

        var shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader != null)
            renderer.material = new Material(shader);

        var trail = lineObject.AddComponent<GuidelineTrail>();
        trail.Target = diver;
        return trail;
    }

    private static void BuildCave(Transform parent)
    {
        var rock = new Color(0.12f, 0.13f, 0.13f);
        var rock2 = new Color(0.16f, 0.17f, 0.16f);

        // Outer shell.
        CreateWall("Ceiling", parent, new Vector2(4f, 5.7f), new Vector2(40f, 3.4f), rock);
        CreateWall("Floor", parent, new Vector2(4f, -5.7f), new Vector2(40f, 3.4f), rock);
        CreateWall("LeftWall", parent, new Vector2(-16.3f, 0f), new Vector2(2.6f, 14f), rock);
        CreateWall("RightWall", parent, new Vector2(24.3f, 0f), new Vector2(2.6f, 14f), rock);

        // Alternating shelves force the player to actually read the cave on the way back.
        CreateWall("Shelf_A", parent, new Vector2(-5.8f, 2.3f), new Vector2(5.6f, 3.1f), rock2);
        CreateWall("Shelf_B", parent, new Vector2(1.2f, -2.25f), new Vector2(5.3f, 3.0f), rock2);
        CreateWall("Shelf_C", parent, new Vector2(8.3f, 2.3f), new Vector2(5.4f, 3.1f), rock2);
        CreateWall("Shelf_D", parent, new Vector2(14.8f, -2.15f), new Vector2(4.7f, 3.2f), rock2);

        // A narrow squeeze before the target chamber.
        CreateWall("PinchTop", parent, new Vector2(18.0f, 3.25f), new Vector2(3.0f, 1.65f), rock);
        CreateWall("PinchBottom", parent, new Vector2(18.0f, -3.55f), new Vector2(3.0f, 1.15f), rock);
    }

    private static void CreateWall(string name, Transform parent, Vector2 position, Vector2 size, Color color)
    {
        var wall = CreateSolid(name, parent, position, size, color, 0);
        wall.AddComponent<BoxCollider2D>();
    }

    private static GameObject CreateSolid(string name, Transform parent, Vector2 localPosition, Vector2 size, Color color, int sortingOrder)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = new Vector3(localPosition.x, localPosition.y, 0f);
        go.transform.localScale = new Vector3(size.x, size.y, 1f);

        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = GetSolidSprite();
        renderer.color = color;
        renderer.sortingOrder = sortingOrder;
        return go;
    }

    private static void BuildTrigger(Transform parent, string name, Vector2 position, Vector2 size, CaveTrigger.Kind kind, CaveDivePrototypeGame game)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent);
        go.transform.localPosition = position;

        var collider = go.AddComponent<BoxCollider2D>();
        collider.size = size;
        collider.isTrigger = true;

        var trigger = go.AddComponent<CaveTrigger>();
        trigger.TriggerKind = kind;
        trigger.Game = game;
    }

    private static Sprite GetSolidSprite()
    {
        if (solidSprite != null)
            return solidSprite;

        var texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.name = "RuntimeSolidPixel";
        texture.SetPixel(0, 0, Color.white);
        texture.Apply();
        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;

        solidSprite = Sprite.Create(texture, new Rect(0, 0, 1, 1), new Vector2(0.5f, 0.5f), 1f);
        solidSprite.name = "RuntimeSolidSprite";
        return solidSprite;
    }
}

internal sealed class DiverMotor2D : MonoBehaviour
{
    [SerializeField] private float acceleration = 12f;
    [SerializeField] private float maxSpeed = 4f;
    [SerializeField] private float turnSpeed = 420f;

    private Rigidbody2D rb;
    private Vector2 moveInput;

    public float InputAmount => moveInput.magnitude;
    public float Speed => rb == null ? 0f : rb.linearVelocity.magnitude;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        moveInput = ReadMoveInput();
    }

    private void FixedUpdate()
    {
        if (moveInput.sqrMagnitude > 0.001f)
        {
            rb.AddForce(moveInput * acceleration, ForceMode2D.Force);

            float targetAngle = Mathf.Atan2(moveInput.y, moveInput.x) * Mathf.Rad2Deg;
            float nextAngle = Mathf.MoveTowardsAngle(rb.rotation, targetAngle, turnSpeed * Time.fixedDeltaTime);
            rb.MoveRotation(nextAngle);
        }

        if (rb.linearVelocity.magnitude > maxSpeed)
            rb.linearVelocity = rb.linearVelocity.normalized * maxSpeed;
    }

    public void StopImmediately()
    {
        moveInput = Vector2.zero;
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private static Vector2 ReadMoveInput()
    {
        Vector2 input = Vector2.zero;

        var keyboard = Keyboard.current;
        if (keyboard != null)
        {
            if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
            if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
            if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
        }

        var gamepad = Gamepad.current;
        if (gamepad != null && input.sqrMagnitude < 0.01f)
            input = gamepad.leftStick.ReadValue();

        return Vector2.ClampMagnitude(input, 1f);
    }
}

internal sealed class OxygenTank : MonoBehaviour
{
    [SerializeField] private float maxOxygenSeconds = 90f;
    [SerializeField] private float baseConsumption = 1f;
    [SerializeField] private float movementConsumption = 0.55f;

    private DiverMotor2D motor;

    public float Current { get; private set; }
    public float Max => maxOxygenSeconds;
    public float Ratio => maxOxygenSeconds <= 0f ? 0f : Current / maxOxygenSeconds;
    public bool Empty => Current <= 0f;

    private void Awake()
    {
        motor = GetComponent<DiverMotor2D>();
        Current = maxOxygenSeconds;
    }

    private void Update()
    {
        if (Current <= 0f)
            return;

        float load = motor == null ? 0f : motor.InputAmount;
        Current = Mathf.Max(0f, Current - (baseConsumption + load * movementConsumption) * Time.deltaTime);
    }

    public void Refill()
    {
        Current = maxOxygenSeconds;
    }
}

internal sealed class GuidelineTrail : MonoBehaviour
{
    public Transform Target { get; set; }

    [SerializeField] private float pointSpacing = 0.45f;

    private readonly List<Vector3> points = new List<Vector3>();
    private LineRenderer line;

    private void Awake()
    {
        line = GetComponent<LineRenderer>();
    }

    private void Start()
    {
        ResetLine(Target != null ? Target.position : Vector3.zero);
    }

    private void LateUpdate()
    {
        if (Target == null || points.Count == 0)
            return;

        Vector3 current = Target.position;
        current.z = 0f;
        if (Vector3.Distance(points[points.Count - 1], current) >= pointSpacing)
        {
            points.Add(current);
            line.positionCount = points.Count;
            line.SetPosition(points.Count - 1, current);
        }
    }

    public void ResetLine(Vector3 start)
    {
        start.z = 0f;
        points.Clear();
        points.Add(start);
        if (line == null)
            line = GetComponent<LineRenderer>();
        line.positionCount = 1;
        line.SetPosition(0, start);
    }
}

internal sealed class CaveTrigger : MonoBehaviour
{
    internal enum Kind
    {
        Entrance,
        Target
    }

    public Kind TriggerKind { get; set; }
    public CaveDivePrototypeGame Game { get; set; }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.GetComponent<DiverMotor2D>() == null || Game == null)
            return;

        if (TriggerKind == Kind.Target)
            Game.ReachTarget();
        else
            Game.ReturnToEntrance();
    }
}

internal sealed class SmoothCameraFollow : MonoBehaviour
{
    public Transform Target { get; set; }

    [SerializeField] private float smoothTime = 0.22f;
    private Vector3 velocity;

    private void LateUpdate()
    {
        if (Target == null)
            return;

        Vector3 desired = new Vector3(Target.position.x, Target.position.y, -10f);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime);
    }
}

internal sealed class CaveDivePrototypeGame : MonoBehaviour
{
    private GameObject diver;
    private DiverMotor2D motor;
    private OxygenTank oxygen;
    private GuidelineTrail guideline;
    private Vector2 startPosition;

    private bool reachedTarget;
    private bool finished;
    private string message = "FIND THE MARKER AND RETURN ALIVE";

    public void Initialize(GameObject diverObject, DiverMotor2D diverMotor, OxygenTank oxygenTank, GuidelineTrail trail, Vector2 spawn)
    {
        diver = diverObject;
        motor = diverMotor;
        oxygen = oxygenTank;
        guideline = trail;
        startPosition = spawn;
    }

    private void Update()
    {
        if (!finished && oxygen != null && oxygen.Empty)
        {
            finished = true;
            message = "OUT OF AIR";
            motor.enabled = false;
            motor.StopImmediately();
        }

        var keyboard = Keyboard.current;
        if (finished && keyboard != null && keyboard.rKey.wasPressedThisFrame)
            ResetRun();
    }

    public void ReachTarget()
    {
        if (finished || reachedTarget)
            return;

        reachedTarget = true;
        message = "TARGET REACHED - FOLLOW YOUR LINE BACK";
    }

    public void ReturnToEntrance()
    {
        if (finished || !reachedTarget)
            return;

        finished = true;
        message = "DIVE COMPLETE";
        motor.enabled = false;
        motor.StopImmediately();
    }

    private void ResetRun()
    {
        finished = false;
        reachedTarget = false;
        message = "FIND THE MARKER AND RETURN ALIVE";

        diver.transform.position = startPosition;
        diver.transform.rotation = Quaternion.identity;
        motor.enabled = true;
        motor.StopImmediately();
        oxygen.Refill();
        guideline.ResetLine(startPosition);
    }

    private void OnGUI()
    {
        if (oxygen == null)
            return;

        const float x = 22f;
        const float y = 20f;
        const float width = 280f;
        const float height = 24f;

        GUI.Box(new Rect(x, y, width, height), string.Empty);

        Color previous = GUI.color;
        float ratio = Mathf.Clamp01(oxygen.Ratio);
        GUI.color = ratio > 0.35f ? new Color(0.72f, 0.82f, 0.80f) : new Color(0.85f, 0.52f, 0.42f);
        GUI.DrawTexture(new Rect(x + 3f, y + 3f, (width - 6f) * ratio, height - 6f), Texture2D.whiteTexture);
        GUI.color = previous;

        var labelStyle = new GUIStyle(GUI.skin.label)
        {
            fontSize = 16,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white }
        };

        GUI.Label(new Rect(x, y + 28f, 360f, 26f), $"OXYGEN  {Mathf.CeilToInt(oxygen.Current)}s", labelStyle);
        GUI.Label(new Rect(x, y + 56f, 520f, 26f), message, labelStyle);
        GUI.Label(new Rect(x, Screen.height - 48f, 560f, 28f), finished ? "R  RESTART" : "WASD / ARROWS  SWIM", labelStyle);
    }
}
