using System.Collections;
using UnityEngine;

// MVP framing: keep the diver and nearby cave geometry large enough on screen
// that narrow passages feel immediate rather than like a distant map overview.
[DefaultExecutionOrder(900)]
public sealed class MvpCameraScaleRuntime : MonoBehaviour
{
    private const float MvpOrthographicSize = 4.3f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("MvpCameraScaleRuntime") != null)
            return;

        GameObject runtime = new GameObject("MvpCameraScaleRuntime");
        runtime.AddComponent<MvpCameraScaleRuntime>();
    }

    private IEnumerator Start()
    {
        // The prototype bootstrap also configures the camera at runtime.
        // Apply the MVP framing after those startup objects have finished initializing.
        yield return null;
        yield return null;
        ApplyFraming();
    }

    private void ApplyFraming()
    {
        Camera camera = Camera.main;
        if (camera == null)
            camera = Object.FindFirstObjectByType<Camera>();

        if (camera == null)
            return;

        camera.orthographic = true;
        camera.orthographicSize = MvpOrthographicSize;
    }
}
