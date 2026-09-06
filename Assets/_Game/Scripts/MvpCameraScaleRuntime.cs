using System.Collections;
using UnityEngine;

// MVP framing: zoom the camera a little closer so cave walls and passages occupy
// more of the screen. The diver visual itself is scaled down separately, which
// reduces the diver-to-cave ratio while keeping the cave feeling large and immediate.
[DefaultExecutionOrder(900)]
public sealed class MvpCameraScaleRuntime : MonoBehaviour
{
    private const float MvpOrthographicSize = 3.8f;

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
