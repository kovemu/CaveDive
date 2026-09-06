using System.Collections;
using UnityEngine;

// Composition pass: keep the diver physically smaller relative to the cave while
// preserving the current movement/light systems. This runs after the map and diver
// visual bootstraps so their own startup configuration does not overwrite it.
[DefaultExecutionOrder(950)]
public sealed class DiverScaleRuntime : MonoBehaviour
{
    private const float VisualScale = 0.75f;
    private static readonly Vector2 ColliderSize = new Vector2(0.78f, 0.29f);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Install()
    {
        if (GameObject.Find("DiverScaleRuntime") != null)
            return;

        GameObject runtime = new GameObject("DiverScaleRuntime");
        runtime.AddComponent<DiverScaleRuntime>();
    }

    private IEnumerator Start()
    {
        // MvpMapRuntime and DiverVisualRigRuntime both build/configure after scene load.
        // Wait until those runtime objects exist before applying the final proportions.
        for (int i = 0; i < 6; i++)
            yield return null;

        ApplyScale();
    }

    private void ApplyScale()
    {
        GameObject diver = GameObject.Find("Diver");
        if (diver == null)
            return;

        Transform artRoot = diver.transform.Find("DiverArt");
        if (artRoot != null)
            artRoot.localScale = Vector3.one * VisualScale;

        CapsuleCollider2D capsule = diver.GetComponent<CapsuleCollider2D>();
        if (capsule != null)
        {
            capsule.size = ColliderSize;
            capsule.offset = Vector2.zero;
        }
    }
}
