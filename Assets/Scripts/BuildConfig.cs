using UnityEngine;

[CreateAssetMenu(fileName = "BuildConfig", menuName = "YomawariByoin/BuildConfig")]
public class BuildConfig : ScriptableObject
{
    [Header("Build Info")]
    public string version = "0.1.0-alpha";
    public string buildDate = "";
    public bool isSteamBuild = true;
    public bool isDebugBuild = false;

    [Header("Steam")]
    public uint steamAppId = 480; // SpaceWar (placeholder for development)
}
