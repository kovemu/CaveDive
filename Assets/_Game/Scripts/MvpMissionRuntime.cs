using System.Collections;
using UnityEngine;

// MVP mission rule: start at S, touch either T1 OR T2, then return to S.
// The two targets are alternatives; the player never needs to visit both in one run.
[DefaultExecutionOrder(50)]
public sealed class MvpMissionRuntime : MonoBehaviour
{
    public static readonly Vector2 StartPosition = new Vector2(-9.58f, 8.60f);
    public static readonly Vector2 Target1Position = new Vector2(10.97f, -2.89f);
    public static readonly Vector2 Target2Position = new Vector2(7.76f, -10.08f);

    public static bool TargetReached { get; private set; }
    public static int ReachedTargetId { get; private set; }

    private CaveDivePrototypeGame game;
    private GameObject diver;
    private DiverMotor2D motor;
    private OxygenTank oxygen;
    private GuidelineTrail guideline;
    private bool wasMotorEnabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("MvpMissionRuntime") != null)
            return;

        GameObject runtime = new GameObject("MvpMissionRuntime");
        runtime.AddComponent<MvpMissionRuntime>();
    }

    private IEnumerator Start()
    {
        // Prototype objects and the MVP map are runtime-created, so allow them to settle.
        yield return null;
        yield return null;
        yield return null;

        ConfigureMission();
    }

    private void ConfigureMission()
    {
        diver = GameObject.Find("Diver");
        game = Object.FindFirstObjectByType<CaveDivePrototypeGame>();
        guideline = Object.FindFirstObjectByType<GuidelineTrail>();

        if (diver == null || game == null || guideline == null)
        {
            Debug.LogError("CaveDive: MVP mission could not find prototype objects.");
            return;
        }

        motor = diver.GetComponent<DiverMotor2D>();
        oxygen = diver.GetComponent<OxygenTank>();

        DisableOldMissionObjects();
        BuildMissionTriggers();
        BuildMarker("T1 Marker", Target1Position);
        BuildMarker("T2 Marker", Target2Position);
        BuildStartMarker();

        TargetReached = false;
        ReachedTargetId = 0;

        diver.transform.position = StartPosition;
        diver.transform.rotation = Quaternion.identity;
        if (motor != null)
        {
            motor.enabled = true;
            motor.StopImmediately();
        }
        if (oxygen != null)
            oxygen.Refill();

        guideline.ResetLine(StartPosition);
        game.Initialize(diver, motor, oxygen, guideline, StartPosition);
        wasMotorEnabled = motor != null && motor.enabled;
    }

    private void Update()
    {
        if (diver == null || motor == null)
            return;

        bool enabledNow = motor.enabled;

        // R restart re-enables the motor and teleports the diver to S through the
        // prototype game manager. Reset the alternative-target mission state here.
        if (!wasMotorEnabled && enabledNow && Vector2.Distance(diver.transform.position, StartPosition) < 1.2f)
        {
            TargetReached = false;
            ReachedTargetId = 0;
        }

        wasMotorEnabled = enabledNow;
    }

    public void ReachTarget(int targetId)
    {
        if (TargetReached || game == null)
            return;

        TargetReached = true;
        ReachedTargetId = targetId;
        game.ReachTarget();
    }

    public void ReturnToStart()
    {
        if (game != null)
            game.ReturnToEntrance();
    }

    private static void DisableOldMissionObjects()
    {
        string[] names = { "Entrance", "Target", "Entrance Marker", "Target Marker" };
        for (int i = 0; i < names.Length; i++)
        {
            GameObject oldObject = GameObject.Find(names[i]);
            if (oldObject != null)
                oldObject.SetActive(false);
        }
    }

    private void BuildMissionTriggers()
    {
        GameObject start = new GameObject("MVP Entrance S");
        start.transform.position = StartPosition;
        BoxCollider2D startCollider = start.AddComponent<BoxCollider2D>();
        startCollider.size = new Vector2(1.4f, 1.4f);
        startCollider.isTrigger = true;
        MvpEntranceTrigger entrance = start.AddComponent<MvpEntranceTrigger>();
        entrance.Mission = this;

        BuildTargetTrigger("MVP Target T1", Target1Position, 1);
        BuildTargetTrigger("MVP Target T2", Target2Position, 2);
    }

    private void BuildTargetTrigger(string name, Vector2 position, int id)
    {
        GameObject target = new GameObject(name);
        target.transform.position = position;
        CircleCollider2D collider = target.AddComponent<CircleCollider2D>();
        collider.radius = 0.60f;
        collider.isTrigger = true;

        MvpTargetTrigger trigger = target.AddComponent<MvpTargetTrigger>();
        trigger.Mission = this;
        trigger.TargetId = id;
    }

    private static void BuildMarker(string name, Vector2 position)
    {
        GameObject root = new GameObject(name);
        root.transform.position = position;

        BuildMarkerBar(root.transform, Vector2.zero, new Vector2(0.10f, 0.70f));
        BuildMarkerBar(root.transform, Vector2.zero, new Vector2(0.46f, 0.10f));
    }

    private static void BuildStartMarker()
    {
        GameObject root = new GameObject("S Marker");
        root.transform.position = StartPosition;
        BuildMarkerBar(root.transform, Vector2.zero, new Vector2(0.08f, 0.78f), new Color(0.55f, 0.67f, 0.66f, 0.92f));
    }

    private static void BuildMarkerBar(Transform parent, Vector2 localPosition, Vector2 size)
    {
        BuildMarkerBar(parent, localPosition, size, new Color(0.64f, 0.58f, 0.25f, 0.94f));
    }

    private static void BuildMarkerBar(Transform parent, Vector2 localPosition, Vector2 size, Color color)
    {
        Texture2D pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        pixel.SetPixel(0, 0, Color.white);
        pixel.Apply(false, true);

        Sprite sprite = Sprite.Create(pixel, new Rect(0f, 0f, 1f, 1f), new Vector2(0.5f, 0.5f), 1f);

        GameObject bar = new GameObject("Marker Bar");
        bar.transform.SetParent(parent, false);
        bar.transform.localPosition = localPosition;
        bar.transform.localScale = new Vector3(size.x, size.y, 1f);

        SpriteRenderer renderer = bar.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.color = color;
        renderer.sortingOrder = 4;

        Shader litShader = Shader.Find("Universal Render Pipeline/2D/Sprite-Lit-Default");
        if (litShader != null)
            renderer.material = new Material(litShader) { name = "MVP Marker Lit" };
    }
}

internal sealed class MvpTargetTrigger : MonoBehaviour
{
    public MvpMissionRuntime Mission { get; set; }
    public int TargetId { get; set; }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Mission == null || other.GetComponent<DiverMotor2D>() == null)
            return;

        Mission.ReachTarget(TargetId);
    }
}

internal sealed class MvpEntranceTrigger : MonoBehaviour
{
    public MvpMissionRuntime Mission { get; set; }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (Mission == null || other.GetComponent<DiverMotor2D>() == null)
            return;

        Mission.ReturnToStart();
    }
}
