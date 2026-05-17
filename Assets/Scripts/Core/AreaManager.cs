using UnityEngine;

public enum AreaID { Floor1F, Floor2F, Floor3F, Basement }

[System.Serializable]
public class AreaData
{
    public AreaID id;
    public string displayName;
    // How much hallucination rises per second while in this area (on top of base rate)
    public float hallucinationBonus;
}

public class AreaManager : MonoBehaviour
{
    public static AreaManager Instance { get; private set; }

    public AreaData[] areas;
    public AreaID CurrentArea { get; private set; } = AreaID.Floor1F;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // Default area data if not set in inspector
        if (areas == null || areas.Length == 0)
        {
            areas = new[]
            {
                new AreaData { id = AreaID.Floor1F,   displayName = "1F 外来・受付",   hallucinationBonus = 0f   },
                new AreaData { id = AreaID.Floor2F,   displayName = "2F 一般病室",      hallucinationBonus = 0.5f },
                new AreaData { id = AreaID.Floor3F,   displayName = "3F 隔離病棟",      hallucinationBonus = 1.0f },
                new AreaData { id = AreaID.Basement,  displayName = "地下 記録保管室",  hallucinationBonus = 1.5f },
            };
        }
    }

    public void EnterArea(AreaID area)
    {
        CurrentArea = area;
        Debug.Log($"[AreaManager] Entered {GetAreaData(area)?.displayName}");
    }

    public AreaData GetAreaData(AreaID id)
    {
        foreach (var a in areas)
            if (a.id == id) return a;
        return null;
    }

    public float GetCurrentHallucinationBonus()
        => GetAreaData(CurrentArea)?.hallucinationBonus ?? 0f;
}
