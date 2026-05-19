using UnityEngine;

/// NPC visual swap driven by the player's hallucination level.
///
///   level   0 - 60 : normal (doctor / nurse / patient outfit visible)
///   level  60 -100 : ghost (white transparent with emission, blurred edges)
public class NPCAppearanceController : MonoBehaviour
{
    public enum NPCKind { Doctor, Nurse, Patient }

    [Header("Identity")]
    public NPCKind kind = NPCKind.Doctor;

    [Header("Materials")]
    public Material normalMaterial;
    public Material ghostMaterial;

    [Header("Renderers to swap")]
    public Renderer[] bodyRenderers;

    [Header("Hallucination link")]
    public string playerID = "local";
    [Range(0f, 100f)] public float ghostThreshold = 60f;

    [Header("Debug override - used by Editor for screenshots")]
    public bool overrideForScreenshot = false;
    public bool forceGhost = false;

    void Awake()
    {
        if (bodyRenderers == null || bodyRenderers.Length == 0)
            bodyRenderers = GetComponentsInChildren<Renderer>();
    }

    void Update()
    {
        float level;
        if (overrideForScreenshot)
            level = forceGhost ? 100f : 0f;
        else if (HallucinationSystem.Instance != null)
            level = HallucinationSystem.Instance.GetLevel(playerID);
        else
            return;

        bool ghost = level >= ghostThreshold;
        Material target = ghost ? ghostMaterial : normalMaterial;
        if (target == null) return;

        foreach (var r in bodyRenderers)
        {
            if (r == null) continue;
            if (r.sharedMaterial != target) r.sharedMaterial = target;
        }
    }
}
