using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M14: 音を鳴らす。
///
/// **監査した時点で、このゲームは完全に無音だった。**
/// `AudioSystem` はクリップの入れ物として作られていたが、
/// どのフィールドにも何も入っておらず、`PlayBGM(bgmNormal)` は
/// null を受け取って即 return していた。既存の wav もサイン波のままで、
/// 3フロア分の BGM は**全て同一ファイル**だった。
///
/// ここでやること:
///   1. 生成した音を `Resources/Audio` に配置し、ループ設定を入れる
///   2. `__Systems.prefab` に環境音の再生役を載せる
///   3. プレイヤーに足音を付ける
///   4. 生きている蛍光灯に、その位置から鳴る音を付ける
///
/// 4 が効く。光源から音が出ると、廊下のどのあたりに居るのかが音だけで分かる。
/// 切れかけの管は、じりじりという別の音にしてある。
/// </summary>
public static class M14AudioPass
{
    const string SourceDir = "Assets/Audio";
    const string ResourceDir = "Assets/Resources/Audio";
    const string BuzzRoot = "FluorescentAudio";

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    [MenuItem("消灯/M14: 音を配置する")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[M14] 音\n");

        int copied = SyncToResources(log);
        ConfigureImporters(log);
        int scenes = PlaceFluorescentAudio(log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();

        log.AppendLine($"  Resources へ {copied} 本 / 蛍光灯の音を {scenes} シーンに配置");
        Debug.Log(log.ToString());
        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    /// <summary>
    /// `Resources` 配下に置く。実行時に名前で読めるようにするため。
    /// シーンごとにインスペクタで結線して回ると、必ずどこかで取りこぼす。
    /// </summary>
    static int SyncToResources(StringBuilder log)
    {
        int copied = 0;

        foreach (var folder in new[] { "Ambient", "SE" })
        {
            var from = $"{SourceDir}/{folder}";
            if (!AssetDatabase.IsValidFolder(from)) continue;

            foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { from }))
            {
                var src = AssetDatabase.GUIDToAssetPath(guid);
                var file = System.IO.Path.GetFileName(src);

                // 足音は数が多いので、まとめて読めるよう別フォルダにする
                var sub = file.StartsWith("Footstep_") ? "SE/Footsteps" : folder;
                var dstDir = $"{ResourceDir}/{sub}";
                EnsureFolder(dstDir);

                var dst = $"{dstDir}/{file}";
                if (AssetDatabase.LoadAssetAtPath<AudioClip>(dst) != null)
                    AssetDatabase.DeleteAsset(dst);

                if (AssetDatabase.CopyAsset(src, dst)) copied++;
                else log.AppendLine($"  ? {src} をコピーできない");
            }
        }

        // 旧世代のサイン波を残しておくと、名前で引いたときに掴む可能性がある
        foreach (var stale in new[]
        {
            $"{ResourceDir}/BGM/BGM_Hospital_1F.wav",
            $"{ResourceDir}/BGM/BGM_Hospital_2F.wav",
            $"{ResourceDir}/BGM/BGM_Hospital_Basement.wav",
            $"{ResourceDir}/Voice/Voice_Announcement.wav",
        })
        {
            if (AssetDatabase.LoadAssetAtPath<AudioClip>(stale) == null) continue;
            AssetDatabase.DeleteAsset(stale);
            log.AppendLine($"  旧プレースホルダを削除: {System.IO.Path.GetFileName(stale)}");
        }

        return copied;
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        var parts = path.Split('/');
        var current = parts[0];
        for (int i = 1; i < parts.Length; i++)
        {
            var next = $"{current}/{parts[i]}";
            if (!AssetDatabase.IsValidFolder(next)) AssetDatabase.CreateFolder(current, parts[i]);
            current = next;
        }
    }

    /// <summary>
    /// インポート設定。環境音は常時鳴るので圧縮して読み込み、
    /// 足音のような短いものは展開済みで持つ（再生の遅れを避ける）。
    /// </summary>
    static void ConfigureImporters(StringBuilder log)
    {
        int changed = 0;

        foreach (var guid in AssetDatabase.FindAssets("t:AudioClip", new[] { ResourceDir }))
        {
            var path = AssetDatabase.GUIDToAssetPath(guid);
            var importer = AssetImporter.GetAtPath(path) as AudioImporter;
            if (importer == null) continue;

            bool longLoop = path.Contains("/Ambient/");
            var settings = importer.defaultSampleSettings;
            settings.loadType = longLoop
                ? AudioClipLoadType.CompressedInMemory   // 24秒の環境音を生で持つと重い
                : AudioClipLoadType.DecompressOnLoad;    // 足音は遅れが出ないように
            settings.compressionFormat = longLoop ? AudioCompressionFormat.Vorbis
                                                  : AudioCompressionFormat.PCM;
            settings.quality = 0.7f;

            importer.defaultSampleSettings = settings;
            importer.forceToMono = true;
            importer.loadInBackground = longLoop;
            importer.SaveAndReimport();
            changed++;
        }

        log.AppendLine($"  インポート設定 {changed} 本");
    }

    /// <summary>
    /// 蛍光灯から音を出す。生きている管はハム、切れかけの管はじりじり。
    ///
    /// 距離減衰は短めにする。廊下中に響くと、どの光源が鳴っているのか分からなくなり、
    /// 「位置が分かる」という利点が消える。
    /// </summary>
    static int PlaceFluorescentAudio(StringBuilder log)
    {
        var hum = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ResourceDir}/Ambient/Fluorescent_Hum.wav");
        var dying = AssetDatabase.LoadAssetAtPath<AudioClip>($"{ResourceDir}/Ambient/Fluorescent_Dying.wav");
        if (hum == null || dying == null)
        {
            log.AppendLine("  ? 蛍光灯の音が読めないので配置しない");
            return 0;
        }

        int done = 0;

        foreach (var path in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(path, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(path);

            // 作り直し方式。前回の分が残ると二重に鳴る
            var old = GameObject.Find(BuzzRoot);
            if (old != null) Object.DestroyImmediate(old);
            var root = new GameObject(BuzzRoot).transform;

            var lights = Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                               .Where(l => l.type == LightType.Point || l.type == LightType.Spot)
                               .Where(l => l.transform.position.y > 1.9f)
                               .Where(l => l.GetComponentInParent<ClueInteractable>() == null)
                               .ToList();

            int alive = 0, dead = 0;

            // 全部に付けると音源が数十個になって飽和する。間引いて代表だけ鳴らす
            var placed = new System.Collections.Generic.List<Vector3>();

            foreach (var light in lights.OrderBy(l => l.transform.position.z)
                                        .ThenBy(l => l.transform.position.x))
            {
                var pos = light.transform.position;
                // 近すぎる音源は作らない。重なると音量だけが積み上がる
                if (placed.Any(p => Vector3.Distance(p, pos) < 6f)) continue;
                placed.Add(pos);

                bool isDying = !light.enabled;
                var go = new GameObject(isDying ? $"Buzz_Dying_{dead}" : $"Buzz_{alive}");
                go.transform.SetParent(root, false);
                go.transform.position = pos;

                var src = go.AddComponent<AudioSource>();
                src.clip = isDying ? dying : hum;
                src.loop = true;
                src.playOnAwake = true;
                src.spatialBlend = 1f;                       // 完全に3D
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 1.5f;
                src.maxDistance = isDying ? 9f : 7f;          // 切れかけは少し遠くまで
                src.volume = isDying ? 0.45f : 0.28f;
                src.dopplerLevel = 0f;                        // 光源は動かない

                // 全部が同時に同じ位相で鳴ると唸りが出る。開始位置をずらす
                src.time = (isDying ? dead : alive) * 0.37f % Mathf.Max(0.1f, src.clip.length);

                if (isDying) dead++; else alive++;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            log.AppendLine($"  {label}: 蛍光灯の音 {alive + dead} 個（うち切れかけ {dead}）");
            done++;
        }

        return done;
    }
}
