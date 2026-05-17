using UnityEngine;

/// <summary>
/// Discordなし版：Unity Microphoneで音量検知
/// CLAUDE.md: Discordなしでも全機能でプレイできる設計を維持する
/// </summary>
public class UnityAudioInput : AudioInputManager
{
    [Header("Microphone")]
    public int sampleRate = 16000;
    public int bufferLengthSec = 1;

    private AudioClip micClip;
    private string micDevice;
    private readonly float[] samples = new float[512];

    public override bool IsDiscordActive => false;

    protected override void Awake()
    {
        base.Awake();
        StartMicrophone();
    }

    void StartMicrophone()
    {
        if (Microphone.devices.Length == 0)
        {
            Debug.LogWarning("[UnityAudioInput] No microphone found — voice effects disabled.");
            return;
        }
        micDevice = Microphone.devices[0];
        micClip = Microphone.Start(micDevice, true, bufferLengthSec, sampleRate);
    }

    protected override void Update()
    {
        if (micClip != null)
        {
            micClip.GetData(samples, 0);
            float rms = ComputeRMS(samples);
            string localID = PlayerManager.Instance?.LocalPlayerID ?? "local";
            ReportVolume(localID, rms);
        }
        base.Update();
    }

    void OnDestroy()
    {
        if (!string.IsNullOrEmpty(micDevice) && Microphone.IsRecording(micDevice))
            Microphone.End(micDevice);
    }

    static float ComputeRMS(float[] buf)
    {
        float sum = 0f;
        foreach (var s in buf) sum += s * s;
        return Mathf.Sqrt(sum / buf.Length);
    }
}
