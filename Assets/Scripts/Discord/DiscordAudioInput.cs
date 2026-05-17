using System;
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>
/// Discordあり版：discord.py BotからWebSocketで音量・発言者データを受信
/// CLAUDE.md: Discordあり時のみ有効、なしでも全機能でプレイできる
/// Bot protocol: JSON { "player_id": "...", "volume": 0.0-1.0 }
/// </summary>
public class DiscordAudioInput : AudioInputManager
{
    [Header("WebSocket settings")]
    public string wsUrl = "ws://localhost:8765";
    public float reconnectDelaySec = 5f;

    private bool connected;
    private bool running;

    public override bool IsDiscordActive => connected;

    protected override void Awake()
    {
        base.Awake();
        running = true;
        StartCoroutine(ConnectLoop());
    }

    void OnDestroy()
    {
        running = false;
    }

    // Unity doesn't ship with a native WebSocket client.
    // We poll an HTTP endpoint exposed by the bot as a simple alternative
    // that avoids third-party WS packages.
    IEnumerator ConnectLoop()
    {
        string httpUrl = wsUrl.Replace("ws://", "http://").Replace("wss://", "https://");
        string pollUrl = httpUrl + "/volumes";

        while (running)
        {
            using var req = UnityWebRequest.Get(pollUrl);
            req.timeout = 3;
            yield return req.SendWebRequest();

            if (req.result == UnityWebRequest.Result.Success)
            {
                connected = true;
                ParseVolumes(req.downloadHandler.text);
            }
            else
            {
                connected = false;
            }

            yield return new WaitForSeconds(0.1f); // 10 Hz poll
        }
    }

    // Expected JSON: [{"player_id":"xxx","volume":0.5}, ...]
    void ParseVolumes(string json)
    {
        try
        {
            var wrapper = JsonUtility.FromJson<VolumeList>("{\"items\":" + json + "}");
            if (wrapper?.items == null) return;
            foreach (var entry in wrapper.items)
                ReportVolume(entry.player_id, Mathf.Clamp01(entry.volume));
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[DiscordAudioInput] Parse error: {e.Message}");
        }
    }

    [Serializable] class VolumeEntry { public string player_id; public float volume; }
    [Serializable] class VolumeList  { public VolumeEntry[] items; }
}
