using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// M15: キャラクターを歩かせる。
///
/// **敵も NPC も、姿勢が固まったまま NavMesh の上を滑っていた。**
/// 背景は夜の病院として仕上がったのに、人物だけが動く置物のままで、
/// そこだけが作り物に見えていた。
///
/// Mixamo が使えれば人体も動きも一度に片付くが、取得に Adobe ログインが要る。
/// 待たずにできるところまでやる、というのがここの方針:
/// `tools/blender/rig_characters.py` で既存の関節定義から骨を組み、
/// 待機・歩行・走りを手で打って焼いた FBX を使う。
///
/// ここでやること:
///   1. リグ付き FBX のインポート設定（Generic + クリップをループ指定）
///   2. 速さで待機↔歩き↔走りを混ぜる Animator Controller を作る
///   3. シーン上の Visual をリグ付きモデルに差し替え、Animator を載せる
///   4. 速さを渡す CharacterAnimatorDriver を付ける
///
/// Humanoid ではなく Generic を選んでいる。骨の名前が Unity の想定と違うので
/// Humanoid にするとアバターの対応付けに失敗し、静止したまま無言で通る。
/// Mixamo を入れるときは Humanoid に切り替えることになる。
/// </summary>
public static class M15CharacterAnimationPass
{
    const string ModelDir = "Assets/Models/Characters";
    const string ControllerDir = "Assets/Animations";

    /// <summary>速さ（m/s）と動きの対応。NavMeshAgent の速度をそのまま使う。</summary>
    const float WalkSpeed = 1.1f;
    const float RunSpeed = 3.0f;

    static readonly string[] Scenes =
    {
        "Assets/Scenes/Hospital.unity",
        "Assets/Scenes/Hospital2F.unity",
        "Assets/Scenes/Hospital3F.unity",
        "Assets/Scenes/HospitalBasement.unity",
    };

    /// <summary>差し替える対象。Visual の名前 → リグ付きモデル名。</summary>
    static readonly (string visualName, string model)[] Targets =
    {
        ("Visual", null),           // model は元の見た目から推定する
        ("Visual_Shadow", "Shadow"),
    };

    [MenuItem("消灯/M15: キャラクターを歩かせる")]
    public static void RunBatch()
    {
        var log = new StringBuilder("[M15] キャラクターのモーション\n");

        int configured = ConfigureImporters(log);
        if (configured == 0)
        {
            Debug.LogError("[M15] リグ付きモデルが無い。先に rig_characters.py を実行すること");
            if (Application.isBatchMode) EditorApplication.Exit(1);
            return;
        }

        var controllers = BuildControllers(log);
        int swapped = SwapVisuals(controllers, log);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        log.AppendLine($"  モデル {configured} 体 / 差し替え {swapped} 体");
        Debug.Log(log.ToString());

        if (Application.isBatchMode) EditorApplication.Exit(0);
    }

    // ------------------------------------------------------------------
    static string[] ModelNames => new[] { "Patient", "Civilian", "Guard", "Shadow" };

    static string RiggedPath(string model) => $"{ModelDir}/{model}_Rigged.fbx";

    /// <summary>
    /// FBX のインポート設定。クリップをループ指定するのが要点で、
    /// これを忘れると歩行が1周して止まる（歩き出して固まる、という妙な動きになる）。
    /// </summary>
    static int ConfigureImporters(StringBuilder log)
    {
        int count = 0;

        foreach (var model in ModelNames)
        {
            var path = RiggedPath(model);
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null) { log.AppendLine($"  ? {path} が無い"); continue; }

            importer.animationType = ModelImporterAnimationType.Generic;
            importer.importAnimation = true;
            importer.importConstraints = false;
            importer.importCameras = false;
            importer.importLights = false;
            // 骨のスケールを触らない。触ると体格が崩れる
            importer.importBlendShapes = false;

            var clips = importer.defaultClipAnimations;
            if (clips.Length == 0)
            {
                log.AppendLine($"  ? {model}: アニメーションが入っていない");
                continue;
            }

            foreach (var clip in clips)
            {
                clip.loopTime = true;
                // ループの継ぎ目で姿勢が飛ばないよう、姿勢も繋ぐ
                clip.loopPose = true;
                clip.lockRootRotation = true;
                // 前進はコード側（NavMeshAgent）が担当するので、
                // モーションに含まれる移動は殺す。両方効くと足が滑る
                clip.lockRootHeightY = true;
                clip.lockRootPositionXZ = true;
            }
            importer.clipAnimations = clips;
            importer.SaveAndReimport();

            log.AppendLine($"  {model}: クリップ {clips.Length} 本 " +
                            $"({string.Join(", ", clips.Select(c => c.name))})");
            count++;
        }

        return count;
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// 速さで待機↔歩き↔走りを混ぜるコントローラ。
    /// 状態遷移ではなくブレンドツリーにしてある。歩きと走りの境目で
    /// 動きが切り替わるより、混ざるほうが滑らかに見える。
    /// </summary>
    static Dictionary<string, AnimatorController> BuildControllers(StringBuilder log)
    {
        if (!AssetDatabase.IsValidFolder(ControllerDir))
            AssetDatabase.CreateFolder("Assets", "Animations");

        var result = new Dictionary<string, AnimatorController>();

        foreach (var model in ModelNames)
        {
            var clips = AssetDatabase.LoadAllAssetsAtPath(RiggedPath(model))
                                     .OfType<AnimationClip>()
                                     .Where(c => !c.name.StartsWith("__"))
                                     .ToList();
            if (clips.Count == 0) { log.AppendLine($"  ? {model}: クリップを読めない"); continue; }

            AnimationClip Find(string name) =>
                clips.FirstOrDefault(c => c.name.Equals(name, System.StringComparison.OrdinalIgnoreCase))
                ?? clips[0];

            var path = $"{ControllerDir}/{model}_Locomotion.controller";
            AssetDatabase.DeleteAsset(path);
            var controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);

            var tree = new BlendTree
            {
                name = "Locomotion",
                blendType = BlendTreeType.Simple1D,
                blendParameter = "Speed",
                useAutomaticThresholds = false,
            };
            AssetDatabase.AddObjectToAsset(tree, controller);

            tree.AddChild(Find("Idle"), 0f);
            tree.AddChild(Find("Walk"), WalkSpeed);
            tree.AddChild(Find("Run"), RunSpeed);

            var state = controller.layers[0].stateMachine.AddState("Locomotion");
            state.motion = tree;
            controller.layers[0].stateMachine.defaultState = state;

            EditorUtility.SetDirty(controller);
            result[model] = controller;
            log.AppendLine($"  {model}: コントローラを作成（{clips.Count} クリップ）");
        }

        return result;
    }

    // ------------------------------------------------------------------
    /// <summary>
    /// シーン上の Visual をリグ付きモデルに差し替える。
    /// 見た目の下にあるマテリアルは引き継ぐ（M8 が決めた色を捨てないため）。
    /// </summary>
    static int SwapVisuals(Dictionary<string, AnimatorController> controllers, StringBuilder log)
    {
        int total = 0;

        foreach (var scenePath in Scenes)
        {
            var scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var label = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            int count = 0;

            var characters = Object.FindObjectsByType<Transform>(FindObjectsInactive.Include,
                                                                 FindObjectsSortMode.None)
                                   .Where(t => t.GetComponent<EnemyController>() != null ||
                                               t.name.StartsWith("NPC_"))
                                   .ToList();

            foreach (var character in characters)
            {
                foreach (var (visualName, forcedModel) in Targets)
                {
                    var visual = character.Find(visualName);
                    if (visual == null) continue;

                    var model = forcedModel ?? GuessModel(character.gameObject);
                    if (!controllers.TryGetValue(model, out var controller)) continue;

                    var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(RiggedPath(model));
                    if (prefab == null) continue;

                    // 既に差し替え済みなら触らない（Animator が付いているかで判る）
                    if (visual.GetComponentInChildren<Animator>() != null) continue;

                    var materials = visual.GetComponentsInChildren<Renderer>(true)
                                          .SelectMany(r => r.sharedMaterials)
                                          .Where(m => m != null)
                                          .Distinct().ToList();

                    bool wasActive = visual.gameObject.activeSelf;
                    var localPos = visual.localPosition;
                    var localRot = visual.localRotation;
                    Object.DestroyImmediate(visual.gameObject);

                    var inst = (GameObject)PrefabUtility.InstantiatePrefab(prefab, character);
                    inst.name = visualName;
                    inst.transform.localPosition = localPos;
                    inst.transform.localRotation = localRot;
                    // localScale は触らない（FBX インポーターの単位変換が入っている）

                    // マテリアルは順番に配る。全部に materials[0] を入れると、
                    // 患者の body と gown のように2枚使い分けている見た目が
                    // 1色に潰れる（病衣が肌と同じ色になる）
                    if (materials.Count > 0)
                    {
                        var renderers = inst.GetComponentsInChildren<Renderer>(true);
                        for (int ri = 0; ri < renderers.Length; ri++)
                        {
                            var mat = materials[Mathf.Min(ri, materials.Count - 1)];
                            var slots = new Material[Mathf.Max(1, renderers[ri].sharedMaterials.Length)];
                            for (int i = 0; i < slots.Length; i++) slots[i] = mat;
                            renderers[ri].sharedMaterials = slots;
                        }
                    }

                    // `??` を使ってはいけない。UnityEngine.Object は == を独自に定義していて、
                    // 「破棄済み/未設定」を null として見せているだけなので、
                    // C# の ?? はそれを素通しし、実体の無い Animator を掴む。
                    // 実際 MissingComponentException で落ちた。
                    var animator = inst.GetComponent<Animator>();
                    if (animator == null) animator = inst.AddComponent<Animator>();
                    animator.runtimeAnimatorController = controller;
                    animator.applyRootMotion = false;   // 移動は NavMeshAgent が担当する
                    animator.cullingMode = AnimatorCullingMode.CullUpdateTransforms;

                    inst.SetActive(wasActive);
                    count++;
                }

                if (character.GetComponent<CharacterAnimatorDriver>() == null &&
                    character.GetComponentInChildren<Animator>(true) != null)
                    character.gameObject.AddComponent<CharacterAnimatorDriver>();

                // 繋ぎ直しも「変更」に数える。数えないと、差し替えが済んでいる
                // シーンでは count が 0 のままで**保存されず**、直したつもりが
                // 何も残らない（実際に一度これで空振りした）
                if (Relink(character)) count++;
            }

            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(scene);
                EditorSceneManager.SaveScene(scene);
            }
            log.AppendLine($"  {label}: {count} 体を差し替え");
            total += count;
        }

        return total;
    }

    /// <summary>
    /// 見た目を作り直したあと、それを参照していたコンポーネントを繋ぎ直す。
    ///
    /// **繋ぎ直しを忘れて壊した。** 差し替えで元の Visual を消したので、
    /// `EnemyAppearanceController` が持っていた参照が空になり、
    /// 幻覚レベルが上がっても敵が黒い人影に変わらなくなっていた。
    /// 例外は出ないので、`M16ReferenceAudit` で数えるまで気づけなかった。
    ///
    /// オブジェクトを作り直すパスを書いたら、それを指していたものを探すこと。
    /// </summary>
    static bool Relink(Transform character)
    {
        bool touched = RelinkNpc(character);

        var appearance = character.GetComponent<EnemyAppearanceController>();
        if (appearance == null) return touched;

        var visual = character.Find("Visual");
        var shadow = character.Find("Visual_Shadow");
        bool changed = false;

        if (visual != null && appearance.guardVisual != visual.gameObject)
        { appearance.guardVisual = visual.gameObject; changed = true; }

        if (shadow != null && appearance.shadowVisual != shadow.gameObject)
        { appearance.shadowVisual = shadow.gameObject; changed = true; }

        // 体のレンダラも作り直しているので入れ直す
        var renderers = new List<Renderer>();
        if (visual != null) renderers.AddRange(visual.GetComponentsInChildren<Renderer>(true));
        if (shadow != null) renderers.AddRange(shadow.GetComponentsInChildren<Renderer>(true));
        if (renderers.Count > 0 &&
            (appearance.bodyRenderers == null ||
             appearance.bodyRenderers.Length != renderers.Count))
        { appearance.bodyRenderers = renderers.ToArray(); changed = true; }

        if (changed) EditorUtility.SetDirty(appearance);
        return changed || touched;
    }

    /// <summary>
    /// NPC の見た目まわりを繋ぎ直す。
    ///
    /// `ghostHighMat`（幻覚60+ で使うマテリアル）が未設定だった。
    /// 一番幻覚が強いときだけ NPC のマテリアルが null になるが、
    /// 例外は出ず描画が崩れるだけなので、`M16ReferenceAudit` で数えるまで
    /// 誰も気づいていなかった。
    ///
    /// 本来の割り当ては `HospitalPropsAndCharacters` が行うが、
    /// あちらを再実行すると**リグ付きモデルへの差し替えが巻き戻る**ので、
    /// キャラクターに最後に触るこのパスでも面倒を見る。
    /// </summary>
    static bool RelinkNpc(Transform character)
    {
        var npc = character.GetComponent<NPCManager>();
        if (npc == null || npc.ghostHighMat != null) return false;

        // 幻覚が極まった状態なので、人影と同じ黒い材質を使う
        var shadow = AssetDatabase.LoadAssetAtPath<Material>(
            "Assets/Materials/Props/Char_ShadowFigure.mat");
        var ghost = AssetDatabase.LoadAssetAtPath<Material>("Assets/Materials/Char_Ghost.mat");

        var chosen = shadow != null ? shadow : ghost;
        if (chosen == null) return false;

        npc.ghostHighMat = chosen;
        EditorUtility.SetDirty(npc);
        return true;
    }

    /// <summary>
    /// 元の見た目から、どのモデルを使うか決める。
    /// 名前ではなく既存の Visual が何だったかで判断する
    /// （NPC_Nurse / NPC_Doctor など名前の付き方が揃っていないため）。
    /// </summary>
    static string GuessModel(GameObject character)
    {
        if (character.GetComponent<EnemyController>() != null) return "Guard";

        var visual = character.transform.Find("Visual");
        if (visual != null)
        {
            var name = visual.GetComponentInChildren<Renderer>(true)?.gameObject.name ?? "";
            if (name.Contains("Patient")) return "Patient";
            if (name.Contains("Guard")) return "Guard";
        }
        return "Civilian";
    }
}
