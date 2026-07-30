# 消灯 — 作業引継ぎ書

> このファイルは作業の節目ごとに更新される。**セッションが切れた場合、まずこのファイルを読むこと。**
> 次に `CLAUDE.md`（ゲーム設計書）を読む。

---

## 現在のゴール

**「完成状態でプレイできる」ところまで持っていく。**
タイトル → 病院1F → 各フロア探索 → 手がかり収集 → 90分後にエンド確定、が通しで動く状態。

---

## 最終更新

- 日時: 2026-07-27
- フェーズ: **M1・M2 完了 / M3（見た目）に着手。ゲームは通しで動作する**
- コミット済み（未 push）: `46f2a0a` → `dcc8f89` → `44e2db6`

### ✅ 解決済み：タイル壁の白い矩形

**原因はアルベドが 1.0 を超えていたこと。** `Mat_Walllime01_C` の `_BaseColor` が
`(2.5, 2.4, 2.2)` に設定されていた。アルベドは「入射光のうち何割を反射するか」なので
1.0 を超えることはあり得ず、2.5倍された壁は照明をいくら落としても明るいまま残る。
`Mat_Walllime02`(1.2) と `Mat_Tile02`(1.1) も同様。2026-05 に「暗すぎる」対処として
入れられたものと思われる。

`M3AtmospherePass.ClampOverbrightAlbedo` で上限 0.78 に補正して解消。
実測: 輝度0.48超のピクセルが 16250 → **0**。矩形部分 0.198 ≒ 周囲のタイル壁 0.194。

#### 特定に使った手法（今後も使える）

通常描画と、全レンダラに固有色を割り当てた「IDパス」を各1枚撮り、
明るいピクセルの位置からオブジェクトを逆引きする
（`VisualDiagnostics.IdentifyFromCamera`）。明るいピクセルの **100%** が
`PackArch_1F/P_Wall_02/Wall_02` と出て確定した。

**この手法を使うときの注意点（2回ハマった）:**
1. IDパス中は**ポストプロセスを切る**こと。ビネットやトーンマッピングで色が変わり逆引きできない
2. **閾値を実測に合わせる**こと。当初 0.72 にして0件になり「ツールが壊れている」と誤判断したが、
   矩形の実測は最大 0.665 だった。白く見えていたのは周囲の壁が 0.26 と暗かったから

#### 外れた仮説（同じ道を辿らないための記録）

| 疑ったもの | 結果 |
|-----------|------|
| テクスチャ未設定 | ✗ 該当は蛍光灯カバー・NPC・時計盤のみ |
| デカールのアルファが不透明 | ✗ 透明率 67〜100%、正常 |
| デカールの RGB が明るすぎる | ✗ 血・カビ・傷は輝度22〜31/255 と十分暗い |
| 漆喰とタイルの Zファイティング | ✗ 2cm 逃がしても変化なし（revert 済み） |
| マテリアルの自己発光 | △ 発光は実在し解除したが（右の壁は暗くなった）、矩形は残った |

### 🎉 このプロジェクトで初めて「実際に動いた」

`run_playtest.ps1`（バッチで Play モードに入る）で以下を機械的に確認:

```
[PASS] GameManager 常駐 / ゲーム開始済み / タイマー稼働 / タイマー減少
[PASS] Player タグ / AudioListener / MainCamera / タイマー表示
[PASS] 幻覚レベル上昇 / FlagManager 常駐 / エンドUI 結線
[PASS] エンド発火: 「ENDING: 日常」/「気がつくと、自分の部屋にいた。」
[JapaneseFont] OS フォント 'Yu Gothic UI' を使用します
```

**起動 → システム常駐 → ゲーム開始 → タイマー進行 → エンド確定** が通しで動作。
NullReferenceException も InvalidOperationException も出ていない。

### M1 の結果

`M1Validator` が 46/46 PASS。以下が動く状態になった。

- Build Settings に5シーン登録済み
- `Resources/__Systems.prefab` 生成済み（マネージャ11種 + UI + EventSystem + 幻覚 Volume）
- 4病院シーンすべてで Player タグ / AudioListener / カメラ結線 / ポストプロセス有効化
- 4病院シーンすべてで NavMesh をベイク（`Assets/NavMeshes/*.asset`）
- 3F → 地下 の遷移トリガーを追加

### 解決済みの詰まりポイント（同じ罠を踏まないための記録）

1. **Unity ライセンス**: 一時的に失効していたが Unity Hub のサインインで復旧済み。
   ライセンスの実体は `%LOCALAPPDATA%\Unity\licenses\UnityEntitlementLicense.xml`。
   `C:\ProgramData\Unity\Unity_lic.ulf` は Unity 6 では**使われない**ので、
   そこを見て「ライセンスが無い」と判断しないこと。
2. **`.ps1` の文字コード**: Windows PowerShell 5.1 は BOM 無し `.ps1` を ANSI として読む。
   日本語の**文字列リテラル**を書くと文字化けしてパースエラーになる（コメントは無害）。
   → このリポジトリの `.ps1` は **ASCII のみ**で書くこと。
3. **`HospitalPackArchFix.cs` が未完成のまま放置されていた**（`CombinedBounds` が未実装）。
   Editor アセンブリ全体のコンパイルが通らず、あらゆるバッチ実行が失敗していた。補完済み。

---

## 見た目（M4/M5）の到達点と残件

**目標**: 市販レベルのホラーゲーム。

### 入っているもの

| 領域 | 内容 |
|------|------|
| 描画 | HDR / 4x MSAA / 追加ライトの影(2048) / SSAO / 影距離32m |
| ポストプロセス | ACES / 彩度-20・コントラスト+16 / 色温度-16 / 影を青・ハイライトを暖色 / ブルーム / フィルムグレイン |
| 空間 | フロアごとに深くなる環境光とフォグ、切れた蛍光灯68本による明暗リズム |
| 小道具 | パックの `P_Lamp` を16基に適用（自作の直方体から差し替え） |
| キャラクター | Blender 製の一体成型モデル4体、幻覚レベルでモデルごと差し替え |
| 汚し | デカール約1,600枚。種類ごとに付く高さを変えている |

### ✅ 解決した見た目の課題

- **汚しが見えない** → テクスチャを `tools/gen_decals.py` で暗い染みとして生成し直して解決。
  マテリアル側で暗く着色しても解決しなかった。テクスチャが「白背景＋薄い模様」である
  限り限界があり、可視部の輝度 83/255 → 22〜43/255 にして初めて汚れとして見えた
- **壁の高さ不揃い** → `M3WallHeightFix` を適用（76枚を各シーンの多数決高さに揃えた）

- **廊下が空の箱** → `tools/blender/make_corridor_props.py` で配管・換気口・案内表示・
  ラジエーターを作り、`M6CorridorDetailPass` で305個配置して解決
- **地下に照明器具が0基** → パックの `P_Lamp` を28基追加（半分は切れた状態）

### 🔴 残っている見た目の課題

1. **天井が床プレハブの流用**。未使用の `P_Ceiling_01` に差し替える余地あり
2. **3F 右壁の近景が明るすぎる**。ID 描画で追跡した結果、ジオメトリは正常で
   （腰壁の上が漆喰＝設計通り）、単にカメラ近傍の右壁が他より明るいだけだった。
   不具合ではなく**照明バランスの調整項目**。左右で明るさが揃っていない
3. **配管が4m単位で途切れて見える**。壁パネルの中央に置いているため間隔が空く。
   長さを可変にするか、壁を跨いで連続させる配置に変えると良い
4. **設備の配置が機械的**。`wallIndex % N` で選んでいるので、意味のある場所
   （ドア横に表示、窓下にラジエーター）には置かれていない
5. 地下の床に置かれた既存プロップが斜めに散っている箇所がある（要目視確認）

### ⚠️ 見た目の不具合を追うときの注意（実際に何度も外した記録）

**症状が同じでも原因は毎回別だった。** 「壁に白い矩形」で3回追って、原因は
アルベドが1.0超 → デカールのブレンド係数の不整合 → 白背景テクスチャ、と全て別物。
推測で当てにいくと必ず外れるので、以下の順で**実測**すること。

1. `run_playtest.ps1` で画を撮り、Python で該当座標の輝度を測る
2. `VisualDiagnostics.DumpShowcaseIds` で ID 画像とパレットCSVを出し、座標から逆引き
3. マテリアルの値は `.mat` ファイルを直接読んで確認する（API 呼び出しの成功を信じない）

**道具自体も疑うこと。** 実際に2回やられた。
- 閾値を 0.72 に置いて「0件」→ ツールが壊れたと誤判断。実際は矩形の輝度が最大0.665だった
- ID パスが透明メッシュを不透明として描画 → 車椅子の部品だと誤特定した（修正済み）

**Unity API の落とし穴**
- `Material.SetInt` は URP の `_SrcBlend` 等（Float プロパティ）に**書き込めない**。`SetFloat` を使う
- FBX モデルの `localRotation` / `localScale` を上書きしてはいけない。
  インポーターが Blender(Z-up・m) と Unity(Y-up・cm) の変換をそこに入れている

---

## 反映コマンド

**これ1本でよい。** 全パスが冪等なので、何度実行しても安全。

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\hvnes\YomawariByoin\run_all.ps1
```

4パス（各2〜4分）を順に実行する:
1. `GameBootstrapBuilder` — `__Systems.prefab` 生成 + Build Settings 登録
2. `SceneWiringFixer` — 各シーンの結線修復 + NavMesh ベイク
3. `M2ContentFixer` — 不足コンテンツ（手がかり・NPC）の配置
4. `M1Validator` — 検証。全 PASS で exit 0、1件でも FAIL で exit 1

ログは `unity_logs\` 配下。個別に走らせたい場合は `run_m1_setup.ps1` / `run_m2_content.ps1`。

---

## セッション開始時の再開手順

```bash
cd /c/Users/hvnes/YomawariByoin
git status --short          # 未コミットの変更を確認（現在まだコミットしていない）
git log --oneline -3
```

このファイルの「次にやること」の先頭から再開する。

---

## 環境

```
プロジェクト: C:\Users\hvnes\YomawariByoin
Unity:        6000.4.7f1  (C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe)
レンダリング:  URP
Git:          main で作業。origin あり。main はローカルが origin より5コミット先行（未push）
```

### Unity をバッチモードで動かす方法（この環境での唯一の実行手段）

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe' `
  -batchmode -projectPath 'C:\Users\hvnes\YomawariByoin' `
  -executeMethod ClassName.RunBatch `
  -logFile 'C:\Users\hvnes\YomawariByoin\unity_logs\xxx.log' -quit
```

- 実行前に `Temp\UnityLockfile` と `Temp\ArtifactDB-lock` を消す（`run_m1_setup.ps1` がやっている）
- 1起動に数分。`run_in_background` で回してログを見るのが良い
- 成功時はログ末尾に `Exiting batchmode successfully now!`

---

## この作業で分かった「動かなかった理由」と対応状況

| # | 問題 | 対応 | 状態 |
|---|------|------|------|
| 1 | `activeInputHandler: 1`（新Inputのみ）なのに実装は旧 Input API → 実行時例外 | `2`(Both) に変更 | ✅ コード済 / 未検証 |
| 2 | マネージャ系がどのシーンにも未配置（＝タイマーもUIも音も動かない） | `Resources/__Systems.prefab` + `SystemsBootstrap` で常駐化 | ✅ コード済 / 未生成 |
| 3 | "Player" タグが付いたオブジェクトが皆無（敵の追跡・手がかり調査が全滅） | `SceneWiringFixer` で付与 | ✅ コード済 / 未実行 |
| 4 | AudioListener が無く音が一切鳴らない | 同上で追加 | ✅ コード済 / 未実行 |
| 5 | Build Settings に TitleScreen しか無い → フロア移動が実行時エラー | `GameBootstrapBuilder` で5シーン登録 | ✅ コード済 / 未実行 |
| 6 | 世代違いの重複システム（旧 EndingManager / HallucinationManager） | 削除。FlagType の Phase1 メンバーも削除 | ✅ 完了 |
| 7 | 3F→地下 の遷移が存在せず最終エリアに行けない | `SceneWiringFixer` で追加（位置は2Fの階段から流用、**要目視確認**） | ✅ コード済 / 未実行 |
| 8 | `UNITY_URP` が未定義で幻覚のポストプロセスが丸ごと死んでいた | define に `UNITY_URP` を追加 + Volume/Profile をプレハブに同梱 | ✅ コード済 / 未検証 |
| 9 | 敵の捕捉が CharacterController に打ち消されて転送が効かない | 転送中だけ CC を無効化。幻覚+20 と捕捉カウントも追加 | ✅ 完了 |
| 10 | FlagManager が PlayerPrefs から前回フラグを復元し、2周目が壊れる | `GameManager.TryAutoStart` で開始時にリセット | ✅ 完了 |
| 11 | **NavMesh がどのシーンにも焼かれていない**（NavMeshSurface も NavMeshData も存在せず、敵の `SetDestination` が一度も成功していない） | `SceneWiringFixer.BakeNavMesh` で NavMeshSurface 追加＋ベイク＋アセット化 | ✅ コード済 / 未実行 |
| 12 | 覚醒エンドが到達不能（OwnRoom 手がかりが無く、NPC が全シーンに1体も居ないため `listenedToNPC` が永久に立たない） | `M2ContentFixer` で 3F に私物手がかり、1F に会話 NPC を配置 | ✅ コード済 / 未実行 |
| 13 | `NPCManager.UpdateAppearance` が未設定マテリアルを毎フレーム代入し NPC が不可視／マゼンタになる | null ガード＋band 変化時のみ代入に修正 | ✅ 完了 |

---

## エンド6種の到達可能性

`SetFlag` の呼び出し元を全て追跡した結果、**6エンド中3つに到達手段が存在しなかった**。
以下は対応後の状態。

| エンド | 条件フラグ | 立てる場所 | 状態 |
|--------|-----------|-----------|------|
| 暴走 | `attackedNPC` | `PlayerAttack`（F キー）→ `ParanoiaSystem.RecordAction` | 新規実装 |
| 孤立 | 他が覚醒 | `ParanoiaSystem.IsLocalPlayerIsolated` | **マルチプレイ専用・未検証** |
| 覚醒 | 全手がかり + 残り30分↑ + 鏡 + NPC | 手がかり4種 + NPC_Nurse | M2 で配置 |
| 脱出 | `triedToEscape` | `EscapeAttemptTrigger`（1F玄関） | M2 で配置 |
| 救出 | `followedHallucination` | `ClueType.FollowHallucination`（地下） | 新規実装 |
| 日常 | 時間切れ | `TimeManager` → `OnTimerExpired` | 元から動く |

**元々の欠落:**
- `ParanoiaSystem.RecordAction` の呼び出し元がコードベースのどこにも無かった → 暴走に到達不能
- `followedHallucination` を立てるコードが1行も無かった → 救出に到達不能
- `EscapeAttemptTrigger` は実装済みだがどのシーンにも配置されていなかった → 脱出に到達不能

---

## 今セッションで変更したファイル（すべて未コミット）

**新規**
```
HANDOFF.md                                  ← このファイル
run_m1_setup.ps1                            ← M1 反映の実行スクリプト
run_m2_content.ps1                          ← M2 コンテンツ配置の実行スクリプト
Assets/Scripts/Core/SystemsBootstrap.cs     ← システム常駐 + 日本語フォント供給
Assets/Scripts/Debug/PlayModeSelfCheck.cs   ← プレイ中の自動点検 + デバッグキー
Assets/Editor/GameBootstrapBuilder.cs       ← __Systems.prefab 生成 + BuildSettings 登録
Assets/Editor/SceneWiringFixer.cs           ← 各シーンの結線修復 + NavMesh ベイク
Assets/Editor/M1Validator.cs                ← セットアップ検証（batch で exit code を返す）
Assets/Editor/M2ContentFixer.cs             ← 不足手がかり・NPC の配置
```

**変更**
```
ProjectSettings/ProjectSettings.asset       ← activeInputHandler 1→2、define に UNITY_URP 追加
Assets/Scripts/Core/GameManager.cs          ← 病院シーンで自動開始、フラグリセット、timeScale 復帰
Assets/Scripts/Core/TimeManager.cs          ← DebugAdvance() 追加
Assets/Scripts/EnemyController.cs           ← 捕捉処理の修正
Assets/Scripts/FlagManager.cs               ← FlagType の Phase1 メンバー削除
Assets/Scripts/NPC/NPCManager.cs            ← マテリアル未設定時に NPC が壊れる問題を修正
```

**削除**
```
Assets/Scripts/EndingManager.cs        (+ .meta)
Assets/Scripts/HallucinationManager.cs (+ .meta)
```

コミットする場合の推奨単位:
```bash
git add -A && git commit -m "M1: システム常駐化・シーン結線修復・Input/URP設定の修正"
```

---

## 設計上の決定事項（この作業で決めたこと）

- **システムの常駐方式**: 各シーンに配置せず、`Resources/__Systems.prefab` を
  `[RuntimeInitializeOnLoadMethod]` で生成し `DontDestroyOnLoad` する。
  理由: TimeManager 等は DontDestroyOnLoad を持たないため、シーンごとに置くと
  フロア移動のたびに90分タイマーがリセットされる。ブートストラップ方式なら
  「どのシーンから Play しても動く」も同時に満たせる。
- **マネージャは全てプレハブのルートに載せる**（子に置くと `DontDestroyOnLoad` が
  「ルートでないと効かない」警告を出すため）。
- **Input**: 新 Input System への全面移行はせず `Both` にして旧 API を生かす。
  理由: 移行はプレイ可能化と無関係な差分を大量に生む。M1 の目的は最短でプレイ可能にすること。
  （新 Input System への移行は `claude/wonderful-nash-9538e4` ブランチに実装がある）
- **日本語フォント**: OS フォント（Yu Gothic UI 等）を実行時に動的取得する。
  理由: Unity 内蔵の LegacyRuntime.ttf に日本語グリフが無く、放送やエンド文が全て豆腐になる。
  **TODO(リリース前)**: 再配布可能なフォント（Noto Sans JP / M PLUS 等）を同梱に切り替える。
- **NetworkManager は M1 のプレハブに載せない**（Steam 未起動時に落ちるため）。

---

## 次にやること

### ここから先は人間の目が要る（バッチでの自動修正は打ち止め）

機能面は自動検証で通っている。残るのは**配置と見た目の判断**で、
4分かかるバッチ実行を繰り返すより、エディタで直接見て直すほうが速い。

1. **エディタで `Hospital.unity` を開いて Play し、実際に操作する**
   - Console に `===== プレイ時セルフチェック =====` が出る
   - WASD 移動 / マウス視点 / `C` しゃがみ / `E` 調べる / `F` 攻撃
   - `F9` = 10分スキップ、`F10` = エンド強制発火
2. **スポーン位置の調整**（最重要）
   自動で「一番開けている方向」を向かせたが、プレイヤー自体が壁際の角に置かれている。
   起動直後の画面が壁で埋まる。CLAUDE.md の想定は「薄暗い病室で目が覚める」なので、
   病室の中に置き直すのが本筋。
3. **推定で置いた座標の確認**（いずれも床の上にはスナップ済み）
   - 1F NPC_Nurse `(-1.33, -0.08, -3.00)`
   - 1F 脱出トリガー `(-1.47, 0.00, -15.75)` ← 階段の反対側を玄関と仮定
   - 地下 幻覚の分岐点 `(-0.24, 0.95, 6.82)` ← 仮に点光源で表現。人影モデルに要差し替え
   - 3F→地下 の遷移トリガー ← 2F の階段位置を流用
4. **エンド6種の到達確認**（孤立はマルチプレイ専用なので単体では5種）

### M3: ホラーゲームとして見られる
5. ✅ 照明・フォグ・自己発光・過剰アルベド → `M3AtmospherePass` で対応済み
6. ✅ 白い矩形 → アルベド補正で解消
7. **明るさのバランス確認（要・実プレイ判断）**
   現状の実測は 平均輝度 0.253 / 最大 0.353 / 暗部(<0.15) 13%。
   「一様に薄暗い」寄りで、光の溜まりと闇のコントラストは弱い。
   調整値は `M3AtmospherePass.MoodFor()` に集約してあるので、そこだけ触ればよい:
   - `ambient` … 全体の底上げ（暗さの下限を決める）
   - `fogDensity` … 奥行きの潰れ具合
   - `lightScale` … 光源の強さ（コントラストを決める）
8. 病室天井の誤マテリアル（青いテクスチャ）— 未着手
9. **壁の高さ不揃い**: `P_Wall_02` が 7.62m、`P_Wall_01` が 4.59m。
   `M3WallHeightFix`（多数決で揃える）を実装済みだが **適用していない**。
   4シーン計76枚を動かす変更で、1視点からは改善が確認できなかったため保留。
   エディタで全体を見られるなら適用を判断してよい（`run_wallfix.ps1`）
10. 放置されている `Assets/Editor/HospitalPackFixV2.cs` の扱い

### M4: 整理
11. ✅ `git push` 済み（origin/main と一致）、ローカルの merged ブランチ24本を削除。
    固有コミットを持つ7本（新Input System移行 / Phase5 Discord等）は残してある
12. マルチプレイ（Steam P2P）の実動作確認。`NetworkManager` は一度も実行されていない

---

## 検証コマンド早見

| やりたいこと | コマンド |
|-------------|---------|
| セットアップ一式を反映＋検証 | `run_all.ps1` |
| 実際に動くか（プレイモード＋エンド発火） | `run_playtest.ps1` |
| 見た目の診断 → 雰囲気パス適用 | `run_m3.ps1` |
| 画づくり設定の適用＋各フロア撮影 | `run_look.ps1` |
| キャラクター造形の作り直し＋撮影 | `run_characters.ps1` |
| 壁パネルの配置と重なりを調べる | `run_walls.ps1` |

`run_playtest.ps1` はプレイ画面を `Screenshots/PlayMode_1F.png` に保存する。
見た目を変えたら必ずこれで撮って確認すること。

### Blender

キャラクターとモデルの生成は Blender をヘッドレスで使う。
プレビュー描画込みで1分程度なので、4分かかる Unity 往復より速く形を詰められる。

```bash
"/c/Program Files/Blender Foundation/Blender 5.1/blender.exe" --background --python tools/blender/make_characters.py
```

出力は `Assets/Models/Characters/*.fbx` と `Screenshots/blender_*.png`。
体型は `lean`（前傾）/ `arm_drop`（腕の垂れ）/ `hunch`（猫背）/ `thin`（痩せ）で振れる。

**Blender MCP** も導入済み（`tools/blender-mcp-main/`、アドオン配置と
`claude mcp add blender uvx blender-mcp` 実行済み）。ただし MCP は
Claude Code の再起動後でないと使えない。使う場合は Blender を起動して
N パネルの BlenderMCP タブで「Connect to Claude」を押すこと。
ヘッドレス実行で足りるならそちらのほうが速い。

### Steamworks SDK

`C:\Users\hvnes\Downloads\steamworks_sdk_164.zip` に未展開で置いてある。
マルチプレイ着手時に使う。プロジェクトは既に Steamworks.NET パッケージを参照している。

---

## 作業ログ

### 2026-07-27
- 現状調査。「コードは Phase 1〜5 まで実装済みだが、マネージャがシーンに1つも無く
  ゲームとして成立していない」ことを特定。他にも致命的な欠落を計10点発見（上表）。
- M1〜M4 の計画を策定。
- コード側の修正を一通り実施（上記「変更したファイル」参照）。
- Unity バッチ実行を試みたが **ライセンス失効で exit 198**。ここで停止。
- Unity 不要な範囲（検証スクリプト `M1Validator`、プレイ時点検 `PlayModeSelfCheck`、
  実行スクリプト `run_m1_setup.ps1`）を先に整備して、ライセンス復旧後は1コマンドで
  進められる状態にした。
- さらに調査を進め、追加で3点の致命的欠落を発見（NavMesh 未ベイク／覚醒エンド到達不能／
  NPC マテリアルのバグ）。対応コードと `run_m2_content.ps1` まで用意した。
- ライセンス復旧後、`run_all.ps1` で反映。`M1Validator` 46/46 PASS。
- `run_playtest.ps1`（バッチで Play モードに入る仕組みを新設）で実行時検証。
  セルフチェック 11/11 PASS ＋ エンド発火まで確認。**このプロジェクトで初めて実際に動いた。**
- プレイ画面をキャプチャして判明した追加の問題:
  - 全シーンでプレイヤーが壁を向いてスポーンしていた → NavMesh で開けている方向を
    検出して自動修正（ただしスポーン位置自体が壁際の角なのは未解決）
  - M2 で置いた NPC・幻覚が「プレイヤー前方 N m」の素朴な計算で壁に埋まる恐れ
    → NavMesh 上の歩ける床にスナップする方式に変更
  - キャプチャが全面マゼンタになった件は RenderTexture のフォーマット未指定が原因
    （`RenderTextureFormat.ARGB32` を明示して解決）。ゲーム側の問題ではない
