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
- フェーズ: **M1 完了 / M2 の主要部分まで完了。実際にゲームが動くことを確認済み**

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
5. 病室天井の誤マテリアル（青いテクスチャ）
6. **壁に浮いている白い矩形デカール**（プレイ画面でもはっきり見える。汚しデカールの
   配置ズレか z-fighting）
7. 明るすぎてフラットな照明。1990年代の閉鎖病院に見えない
8. 放置されている `Assets/Editor/HospitalPackFixV2.cs` とステージ済みマテリアル変更の扱い

### M4: 整理
9. Git 整理（未push 5件、`claude/*` 約30本）
10. マルチプレイ（Steam P2P）の実動作確認。`NetworkManager` は一度も実行されていない

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
