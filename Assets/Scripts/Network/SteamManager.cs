using UnityEngine;

#if STEAMWORKS_NET
using Steamworks;
#endif

/// <summary>
/// SteamManager stub for Steamworks.NET initialization.
/// When STEAMWORKS_NET is not defined, this provides a stub implementation.
/// </summary>
public class SteamManager : MonoBehaviour
{
    public static bool Initialized { get; private set; }

#if STEAMWORKS_NET
    private static bool s_EverInitialized = false;

    void Awake()
    {
        if (s_EverInitialized) return;
        s_EverInitialized = true;

        try
        {
            if (!SteamAPI.Init())
            {
                Debug.LogError("[SteamManager] SteamAPI.Init() failed");
                return;
            }
            Initialized = true;
            Debug.Log("[SteamManager] Steam initialized successfully");
            DontDestroyOnLoad(gameObject);
        }
        catch (System.DllNotFoundException ex)
        {
            Debug.LogError("[SteamManager] Steamworks DLL not found: " + ex);
        }
    }

    void OnDestroy()
    {
        if (Initialized)
        {
            SteamAPI.Shutdown();
            Initialized = false;
        }
    }
#else
    void Awake()
    {
        Debug.Log("[SteamManager] STEAMWORKS_NET not defined – Steam disabled");
        Initialized = false;
    }
#endif
}
