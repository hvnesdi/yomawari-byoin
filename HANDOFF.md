# 消灯 — 作業引継ぎ書

> このファイルは作業の節目ごとに更新される。**セッションが切れた場合、まずこのファイルを読むこと。**
> 次に `CLAUDE.md`（ゲーム設計書）を読む。

---

## 現在のゴール

**「完成状態でプレイできる」ところまで持っていく。**
タイトル → 病院1F → 各フロア探索 → 手がかり収集 → 90分後にエンド確定、が通しで動く状態。

---

## 最終更新

- 日時: 2026-07-30
- フェーズ: **M1〜M12 完了。ゲームは通しで動作し、見た目は一括で再現できる**
- 最新コミット: `1f9f12a`（`origin/main` と一致・未コミットの変更なし）
- 検証: **全フロアでプレイテスト 24/24 PASS**（`run_playtest_all.ps1`）
  プレイテストは鏡の前・写真の前でも撮る（置いた物は画で確かめる）
- **反映の順番**: ゲーム側を直したら `run_all.ps1`、見た目を直したら `run_visuals.ps1`。
  どちらを先に走らせても結果は同じになるようにしてある（以前は run_all が見た目を壊していた）
- 検証: プレイテスト 12/12 PASS、エンド発火まで確認

### ✅ この回で直した「効いていなかったもの」

見た目の作り込みそのものより、**作ったつもりで効いていなかったもの**が多かった。

| 何 | 症状 | 原因 |
|----|------|------|
| ポストプロセス一式 | 色調整・グレイン・ビネットが一度も効いていなかった | `profile.Add<T>()` はアセットの子として保存しない。参照が9個すべて null |
| 撮影ツール | 検証用の画にポストプロセスが乗っていなかった | LDR ターゲットに描くと URP がトーンマップを飛ばす |
| デカールの合成 | 壁に明るい矩形（3回目） | URP の Preserve Specular が乗算前提アルファを毎回入れ直していた |
| フロアごとの明るさ | 地下が暗くない | M3 と M5 が同じ元値から計算して書き戻し、後の M5 が勝っていた |
| 蛍光灯の設定変更 | 定数を変えても反映されない | マーカーがあると丸ごと飛ばしていた |
| 天井のタイリング | 実行のたびに 16→32→64 | M10 が既存値に毎回倍率を掛けていた |
| スキャン小物 | 単色の箱に見える | fbx からマテリアルが生成されず、テクスチャが1枚も当たっていなかった |
| 小物の配置数 | 目標に届かない | 「同種の間隔」が「全種類との間隔」として効いていた |
| 開始位置 | 起動直後の画面が壁 | 向きだけ直しても、角に立っていればどこを向いても壁 |
| ポストプロセス（2回目） | `run_all.ps1` を走らせるたびに画が白く戻る | `GameBootstrapBuilder` が同じアセットを全消ししてから3つだけ入れ直していた |
| 病室 | 1F の病室に入れない（歩ける点 0/429） | NavMesh の `minRegionArea`(既定2m²) が、細切れになった病室の床を捨てていた |
| **音** | **ゲームが完全に無音だった** | `AudioSystem` にクリップが1つも割り当てられていない。既存 wav はサイン波で、3フロア分の BGM は同一ファイル |
| **キャラクター** | 敵が姿勢を固めたまま滑って移動 | ボーンもモーションも無く、メッシュを動かしているだけだった |
| 恐怖演出 | 演出は発火するのに無音 | `HorrorEventSystem` のクリップが全て null |
| 緊張度 | 緊張が上がっても音が変わらない | `bgmNormal/Tense/Peak` が全て null |
| 敵の検知 | 見つかっても音がしない | 検知音を鳴らす処理そのものが無かった |
| 幻覚の最上位 | NPC の描画が崩れる | `NPCManager.ghostHighMat` が未設定（60+ でだけ null になる） |
| 鏡・写真の演出 | 発火するが何も起きない | 鏡も写真もシーンに存在せず、参照も全て空だった |

**実測値（1F のプレイ画面）**: 平均輝度 0.419 → **0.198**、暗部(<0.15) 0.6% → **53.7%**。
昼間の事務所から、夜の病院になった。

**フロアごとの明るさ**（`run_playtest_all.ps1` で実測。降りるほど暗くなる設計通り）:

| フロア | 平均輝度 | 暗部(<0.15) |
|--------|---------|------------|
| 1F | 0.184 | 53.1% |
| 2F | 0.175 | 53.5% |
| 3F | 0.095 | 84.0% |
| 地下 | 0.070 | 91.8% |

全フロアでプレイテスト 12/12 PASS。地下は蛍光灯1本が天井を照らすだけで、
床と手前の物は読めるが奥は闇に落ちる状態。狙い通り。
**3F と地下はかなり暗い**ので、遊んでみて見えなさすぎるようなら
`M5LookPass` の `postExposure`（現在 -0.55）を上げる。
- **ユーザー操作待ち: Mixamo のキャラクター取得**（Adobe ログインが必要。
  `docs/Mixamo導入手順.md` 参照。ファイルが置かれれば以降は自動化できる）

### ✅ 解決済み：タイル壁の白い矩形

**原因はアルベドが 1.0 を超えていたこと。** `Mat_Walllime01_C` の `_BaseColor` が
`(2.5, 2.4, 2.2)` に設定されていた。アルベドは「入射光のうち何割を反射するか」なので
1.0 を超えることはあり得ず、2.5倍された壁は照明をいくら落としても明るいまま残る。
`Mat_Walllime02`(1.2) と `Mat_Tile02`(1.1) も同様。2026-05 に「暗すぎる」対処として
入れられたものと思われる。

`M3AtmospherePass.ClampOverbrightAlbedo` で補正して解消。
実測: 輝度0.48超のピクセルが 16250 → **0**。矩形部分 0.198 ≒ 周囲のタイル壁 0.194。

**上限値は 1.0 にしてある（一度 0.78 にして失敗した）。** 0.78 だと不具合の無い
マテリアルまで 22% 暗くなり、画全体が沈んだ。直すべきは「物理的にあり得ない値」だけで、
正常な範囲に手を入れるのは補正ではなく改変になる。
補正パスを書くときは、**壊れているものだけに触れる条件**にすること。

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
| スキャン小物 | Poly Haven の CC0 実写スキャン10種を71個配置（椅子・スチール棚・段ボール・木箱・樽・清掃看板・ワゴン・時計） |
| 音 | フロアごとの環境音／蛍光灯の音（光源から3Dで鳴る）／足音6種／心音・扉・検知・チャイム・スティンガー。すべて `tools/gen_audio.py` で生成 |
| モーション | 全キャラに骨と待機・歩行・走り。速さでブレンド（`rig_characters.py` + `M15`） |
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
- **小物がすべて手続き生成でのっぺりしていた** → Poly Haven の実写スキャン小物を
  `M11PolyHavenPropsPass` で57個配置。段ボールの折れ目や棚の錆は手続き生成では作れない。
  **スキャン素材が小物では噛み合う理由**は、椅子や棚が「その物体の実寸」で作られており
  タイリングという概念が無いため（壁テクスチャで失敗した寸法不一致が起きない）

### 🔴 残っている見た目の課題

0. **キャラクター**。歩行モーションは付いた（`rig_characters.py` + `M15`）ので
   「滑って移動する」問題は解決済み。残るのは**造形**——顔が無く、
   皮膚も衣服のしわも無い。ここは手続き生成の限界の外なので Mixamo 待ち。
   Mixamo を入れるときは `M15CharacterAnimationPass` の
   `animationType` を Humanoid に変えること（今は骨名が独自なので Generic）
1. **天井が床プレハブの流用**（`P_Ceiling_01` は使用0）。ただしマテリアルは
   実写の有孔ボードに差し替わっているので、見た目の優先度は低い
1b. ✅ **1F の病室に入れない** → 解決（`minRegionArea` を 0.5 に）。
   ただし**病室は開始位置には向かない**。歩けるようにしても、
   一番良い場所で壁から 0.3m・見通し 0.7m しかなく、画面の半分が暗い壁になる。
   設計上は「病室で目が覚める」なので、病室を広げるかベッドの配置を変えれば
   `SceneWiringFixer` が自動でそちらを選ぶ（病室には点数の下駄を履かせてある）
2. **3F 右壁の近景が明るすぎる**。ID 描画で追跡した結果、ジオメトリは正常で
   （腰壁の上が漆喰＝設計通り）、単にカメラ近傍の右壁が他より明るいだけだった。
   不具合ではなく**照明バランスの調整項目**。左右で明るさが揃っていない
3. **配管が4m単位で途切れて見える**。壁パネルの中央に置いているため間隔が空く。
   長さを可変にするか、壁を跨いで連続させる配置に変えると良い
4. **設備の配置が機械的**。`wallIndex % N` で選んでいるので、意味のある場所
   （ドア横に表示、窓下にラジエーター）には置かれていない
5. 地下の床に置かれた既存プロップが斜めに散っている箇所がある（要目視確認）
6. **キャラクターが最大の弱点**。背景は実写素材と焼き込んだ間接光で持ち上がったが、
   人物は顔の無い一体成型モデルのまま。**背景と人物の落差が今いちばん目につく。**
   敵は歩行モーションも無く NavMesh 上を滑って移動している。→ Mixamo で解決する
7. ~~スキャン小物が目標数に届いていない~~ → **解決済み。間隔判定の不具合だった。**
   「同種を12m離す」という指定が「先に置いた椅子12脚すべてから13m離す」の意味に
   なっていて、幅4mの廊下では置ける場所が消えていた。
   同種の間隔（`minSpacing`）と別種との重なり防止（`footprint`）を分けて解決。
   **57個 → 71個**、全種類が目標数に到達（不足ログが1件も出なくなった）

### 外部アセットについて（調査済み・一部導入済み）

| 入手先 | ライセンス | 状態 |
|--------|-----------|------|
| [ambientCG](https://ambientcg.com) | CC0（商用可・帰属不要） | **導入済** `Assets/Textures/Ambient/`（217MB / 9素材） |
| [Mixamo](https://www.mixamo.com) | 無料・商用可・再配布のみ禁止 | **未導入（要ユーザー操作）** Adobe ログインが必要 |
| Hyper3D Rodin | 無料トライアル | blender-mcp 経由。MCP 有効化後に利用可 |
| [Poly Haven](https://polyhaven.com) | CC0（商用可・帰属不要） | **導入済** `Assets/Models/PolyHaven/`（87MB / 10モデル）。テクスチャではなく**小物**として使う |

**ambientCG は天井（OfficeCeiling003）だけ採用している。** 壁・床・金属も試したが
すべて画が悪化したため戻した。理由は品質ではなく**寸法の不一致**:
- 1枚が実寸 1〜2m の素材を長い廊下の壁に敷くと、繰り返しが「レンガ調の壁紙」に見える
- 実写の床は本物の光沢をそのまま持ち込むので、廃病院としては綺麗すぎる
- 細い配管では実写の情報量が潰れて汚れに見える

天井が唯一噛み合ったのは、有孔ボードが実際に 60cm 角前後で繰り返すため。
**「実写スキャンだから写実的になる」は成立しない。** 形状が先にある場合は、
手続き生成テクスチャ（周期を面に合わせられる）のほうが適合することがある。

**ただしこれはテクスチャに限った話で、小物には当てはまらない。** Poly Haven の
スキャンモデルは実寸で作られた完結した物体なので、タイリングを合わせる必要が無い。
壁で失敗した理由（1〜2m の素材を長い面に敷く）が構造的に発生しない。
**大きい面 → 手続き生成／独立した物体 → スキャン**、という切り分けになる。

Poly Haven の探し方（API・認証不要）:
```
https://api.polyhaven.com/assets?t=models     … 全モデル一覧（CC0 521件）
https://api.polyhaven.com/files/<id>          … ダウンロードURL
```
`files` の構造は `f['fbx']['2k']['fbx']['url']` と**1段深い**。
`f['fbx']['2k']['url']` を見て「全部 0.0 MB」と誤読した。
選定は `thumbnail_url` を並べて**目で見てから**落とすこと（ambientCG と同じ教訓）。
病院に合わないもの（アンティーク家具・弾薬箱・双眼鏡）は候補に上がっても捨てる。

**素材を追加するときの手順**（一度失敗している）:
1. API の `previewImage`（球体サムネイル）を**先に見る**。
   `https://ambientcg.com/api/v2/full_json?type=Material&q=<語>&include=imageData`
   これを飛ばして 145MB 落とし、適用してから「配管が赤い」と気づいた
2. タイリングは既存値をそのまま使わない。素材の実寸に合わせて倍率を掛ける

**Mixamo はキャラクターの解決策になる。** 顔の無い滑らかな人型は手続き生成の限界で、
写実には届かない。Mixamo はリグ済みキャラクターとアニメーション（歩行・待機）が
無料で使え、現在 NavMesh で滑るように動いている敵に歩行モーションが付く。
認証が必要なため取得はユーザー側で行い、組み込みは自動化できる。

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

**設定した値は、読み直して確かめる。** これで4件見つかった。
API の呼び出しが成功しても、値が残るとは限らない。
`M5GrimePass.FixDecalBlending` と `M5LookPass.ConfigureVolumeProfile` は
保存後にアセットを読み直し、**残った数**を報告するようにしてある。この形を真似ること。
「設定した」と書くログは、何も保証していない。

**Unity API の落とし穴**
- `Material.SetInt` は URP の `_SrcBlend` 等（Float プロパティ）に**書き込めない**。`SetFloat` を使う
- **URP の Preserve Specular（`_BlendModePreserveSpecular`）が有効だと、
  半透明マテリアルの合成が乗算前提アルファに強制される**
  （`_SrcBlend=One` + `_ALPHAPREMULTIPLY_ON`）。マテリアルのインポートごとに
  入れ直されるので、`_SrcBlend` を書いても勝てない。デカールでは切ること。
  「壁に明るい矩形」の3回目の原因はこれだった
- **`VolumeProfile.Add<T>()` だけでは保存されない。**
  `AssetDatabase.AddObjectToAsset` が必要。忘れると次の読み込みで全部 null になり、
  ポストプロセスが丸ごと無効になる（そして誰も気づかない）
- **カメラを LDR の RenderTexture に描くと URP はトーンマップを飛ばす。**
  検証用の撮影は HDR に描いてから LDR へ blit する
- `GameObject.Find` は**非アクティブなものを見つけられない**
- FBX モデルの `localRotation` / `localScale` を上書きしてはいけない。
  インポーターが Blender(Z-up・m) と Unity(Y-up・cm) の変換をそこに入れている
- `NavMeshSurface` の `useGeometry = RenderMeshes` は**装飾の描画メッシュまで
  障害物として焼く**。通行の邪魔をしない前提の飾りは
  `NavMeshModifier.ignoreFromBuild`（+ `applyToChildren`）で外す
- **Play モードを使うバッチ実行に `-quit` を付けてはいけない。**
  `-executeMethod` が返った時点で Unity が終了するので、Play モードが1回も
  ティックせずに「成功」で終わる。`run_playtest*.ps1` はどちらも付けていない

**未設定の参照は `M16ReferenceAudit` で数える。**

```powershell
& 'C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe' -batchmode `
  -projectPath 'C:\Users\hvnes\YomawariByoin' `
  -executeMethod M16ReferenceAudit.RunBatch -logFile unity_logs\m16.log -quit
```

シーンと `__Systems.prefab` を走査して、自作コンポーネントの未設定参照を
**場所付きで**並べる。この形の不具合（下記）を偶然に頼らず見つけるための道具。

**場所を出すのが要点。** 最初は型名だけ出していて、
`EnemyAppearanceController.guardVisual が 1F で null` を
「見た目を作り直したときに壊した」と読み違えた。
場所を出したら `CharacterShowcase_1F/Enemy_*_Preview`——撮影用の置物で、
実際の敵はずっと正しく繋がっていた。
**どこが悪いかを言わない検査は、こういう誤読を招く。**

出力は2つに分かれる:
- **要確認** … 判断が要るもの
- **実行時に入るもの** … アセット上は空で正しい（`GameAudioBinder` /
  `HorrorPropBinder` が入れる）。**隠さず出している**。ここが減っていたら
  結線側が壊れている合図。中身が本当に入るかはプレイテストのセルフチェックが見る

現在の「要確認」9件（いずれも実害なしと判断済み）:
- `AudioSystem.ambientVentilation` 他 … `FloorAmbience` が肩代わりしているので未使用
- `*MixerGroup` … AudioMixer を作っていない。作れば音量バランスを一括で扱える
- 撮影用の `CharacterShowcase_*` の参照 … 画作りにしか使わないので実害なし

**「入れ物がある」ことを「中身がある」と読み替えないこと。**
`AudioSystem` はクリップのフィールドを一通り備えていて、
セルフチェックも `AudioListener` の存在を PASS にしていた。
それで**完全に無音のまま長く気づけなかった**。
同じ形で `Animator` も、コントローラが未設定だと例外も出さずに静止する。
今はどちらも「実際に鳴っているか／再生中か」を見るようにしてある。

**鏡まわりで踏んだ落とし穴**（4件とも、結線の検査では絶対に出ない）:
- **Unity の Quad は -Z 側から見える。** `LookRotation(outward)` にしたら
  鏡が壁の内側を向き、廊下から見えなかった。
  法線は `MirrorReflection.SurfaceNormal`（= -forward）に一本化してある
- `Camera.CopyFrom(main)` は**クリア方法まで引き継ぐ**。
  窓の無い地下の鏡に既定の青空が映った。鏡のカメラは SolidColor にする
- 「鏡に鏡を映さない」つもりで自分のレイヤーを除外したら、
  鏡は Default に居るので**廊下が丸ごと消えた**。
  鏡面はカメラの真後ろなので、そもそも除外は要らない
- 鏡面を Lit にすると、暗い廊下の照明で反射像まで暗く落ちる。
  鏡が返すのは反射光なので **Unlit** が正しい

**確かめる向きを間違えると、直っているものを壊れていると読む。**
歩行モーションを正面から描いて確認していたとき、前後の脚の振りが遠近で潰れて
「脚が動いていない」ように見えた。横から描いたら普通に歩いていた。
**何を確かめたいのかで、見る角度・測る量を選ぶこと。**

**Unity の `??` は使えない。**
`GetComponent<T>() ?? AddComponent<T>()` は動かない。
UnityEngine.Object は `==` を独自定義して「破棄済み/未設定」を null に見せているが、
`??` はそれを見ないので、実体の無い参照をそのまま返す（実際に例外で落ちた）。
`if (x == null)` と書くこと。

**Blender で骨を組んだら roll を揃える。**
揃えないと骨ごとにローカル軸がばらばらで、「X 回りに振る」が骨ごとに別の意味になる。
歩かせたつもりが**体ごと横に倒れていく**動きになった。
ローカル X をワールド X に向けておけば、X=前後の振り / Y=捻り で統一できる。

**「失敗が無い」を成功と数えないこと。** `run_playtest_all.ps1` の初版が
まさにこれで、`-quit` のせいで検査が1件も走っていないのに
「全フロア合格」と表示していた。**PASS が0件なら失敗**として扱う。

**1つのアセットを2つのパスが持ってはいけない。**
`HallucinationProfile.asset` を `GameBootstrapBuilder`（幻覚の3効果）と
`M5LookPass`（色調整一式）の両方が触っていた。前者が「再実行で増殖しないよう」
全消ししてから作り直していたので、**`run_all.ps1` を走らせるたびに
画づくりが丸ごと消えていた**。ゲーム側を直しただけのつもりで画面が白く戻る。
今は「参照が死んだものだけ掃除して、足りないものを足す」方式にしてある。
既にある効果の値には触らない（どちらが後に走っても同じ結果になるように）。

検査方法: `run_all` → `run_visuals` → `run_all` と走らせ、
`HallucinationProfile.asset` の components が9個生きたままか見る。

**NavMesh が部屋ごと消えることがある。**
`minRegionArea`（既定 2m²）は、それ未満の孤立した歩行領域を捨てる。
病室はベッドで床が細切れになり、入口もエージェント半径で削られて
廊下から切り離されるため、断片ごとの面積が閾値を割っていた。
結果、1F の病室3室すべてで歩ける点が 0 だった。0.5 に下げて解決（`SceneWiringFixer`）。
**「床はあるのに歩けない」ときは、まず `minRegionArea` を 0 にして焼き直してみる。**
それで直るなら原因はこれで、ジオメトリは無関係。

（このとき「床が厚さ0の板だからだ」という見立てを立てて差し替えパスまで書いたが、
下見を走らせたら、既に歩けている 2F の床 80 枚まで対象になっていて誤りと分かった。
**変更する前に「何を変えるつもりか」を出す下見を挟むと、こういう思い違いが止まる。**）

**冪等でないパスを書かないこと（実際に混入させた）**

「既存値を読んで、それに掛けて、書き戻す」形は2回目の実行で結果が変わる。
`M10RealMaterialsPass` がこれで、天井のタイリングが実行ごとに 16 → 32 → 64 と
倍々になっていた。スクリプトに「全パスが冪等」と書いておきながら破れていた。

`M3AtmospherePass` と `M5SetDressingPass` は**元の値を `LightBaseIntensity` に
1度だけ記録し、以降は必ずそこから計算する**ことで冪等を保っている。
値を掛ける処理を書くときは必ずこの形にする。
M10 は「差し替え済みなら倍率を掛けない」判定を入れて直した。

**倍率を掛ける処理を追加したら、2回連続で走らせて `.mat` の値が変わらないことを確認する。**
`run_m10_check.ps1` がその形の検査になっている。

**同じ値を2つのパスが書くときは、後のパスが前の結果を含めて計算する。**
M3 が「フロアごとの倍率」、M5 が「生き残りの強調」を、**どちらも同じ元の値から**
計算して書き戻していた。後に走る M5 が勝つので、地下 0.75 倍・1F 1.1 倍という
フロアごとの明るさが消え、全フロアが一律 1.45 倍で焼かれていた。
「地下がちゃんと暗くない」の原因はこれ。
M5 は `M3AtmospherePass.LightScaleFor(scenePath)` を掛けるように直した。

**「適用済みなら飛ばす」は、値を変えたときに反映されない。**
M5SetDressingPass はマーカーを見て丸ごと飛ばしていたので、
明るさの定数を変えても2回目以降は何も起きなかった。
また対象を `FindObjectsInactive.Exclude` で集めていたため、
2回目は「前回切った分」が対象から外れ、走らせるたびに暗くなっていた。
どちらも修正済み（毎回、記録した元の値から決め直す）。

**「置けた」と「見えている」は別。** スキャン小物71個を配置して
「57個 → 71個」というログで満足していたが、実際には**テクスチャが1枚も
当たっていなかった**（Poly Haven の fbx は Unity 側でマテリアルを生成しない）。
段ボールの折れ目も棚の錆も出ておらず、ただのベージュの箱が置かれていた。
スキャン素材を使う理由そのものが失われていたのに、配置ログは正常だった。
`M12ScannedPropMaterialsPass` で解決。**モデルを置いたら必ず画で確かめる。**

**部分一致で判定するときは共通の接頭辞を先に落とす。** M12 で
スロット `CoffeeCart_01_props` が素材 `cart` に一致した。小物名自体が
"CoffeeCart" で "cart" を含んでいたため。`slot.Contains(setName)` は
一見正しく見えて、名前の作りによって静かに壊れる。

**合計値だけを見て判断しないこと。** `M6CorridorDetailPass` の設備数は
1F 256 / 2F 1085 で「1F の設備が抜けている」ように見えた。内訳を出したら
大半が巾木と回り縁（壁の長さに比例）で、壁1mあたりの設備数は
1F 0.29 / 2F 0.19 と**1F のほうが密**だった。結論が逆になった。

---

## 反映コマンド

スクリプトは2本ある。**動くようにする**方と**見えるようにする**方。
どちらも全パスが冪等なので、何度実行しても安全。

### 1. 動作（10分程度）

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\hvnes\YomawariByoin\run_all.ps1
```

1. `GameBootstrapBuilder` — `__Systems.prefab` 生成 + Build Settings 登録
2. `SceneWiringFixer` — 各シーンの結線修復 + NavMesh ベイク
3. `M2ContentFixer` — 不足コンテンツ（手がかり・NPC）の配置
4. `M1Validator` — 検証。全 PASS で exit 0、1件でも FAIL で exit 1

### 2. 見た目（30〜40分）

```powershell
powershell -ExecutionPolicy Bypass -File C:\Users\hvnes\YomawariByoin\run_visuals.ps1
```

M3 → M5 → M6 → M11 → M7 → M10 → M8 → M9ベイク → 撮影 を依存順に実行する。

**順序には理由がある**（2回踏んだ罠）:
- マテリアルを触るパス（M7/M8/M10）は、**そのマテリアルを作るパスより後**でなければならない。
  M8 を M6 より先に走らせると、後から置いた小物の色が直らない
- M8 は「_BaseColor の最終決定者」なので材質パスの最後。M10 を後に回すと天井の色が戻る
- M9（ベイク）は必ず最後。ジオメトリと光を焼き固めるので、
  後で物を動かすと「そこに無い物の影」が残る

**このスクリプトが無かった間、見た目の状態はシーンファイルの中だけに存在していた。**
シーンを1つ巻き戻したら復元手段が無い状態だったので、それを塞ぐために作った。

ログは `unity_logs\` 配下（`v_` 接頭辞が見た目パスのもの）。
個別に走らせたい場合は `run_m1_setup.ps1` / `run_m2_content.ps1` / `run_m3.ps1` /
`run_look.ps1` / `run_detail.ps1`。

---

## セッション開始時の再開手順

```bash
cd /c/Users/hvnes/YomawariByoin
git status --short          # 空であるべき（節目ごとにコミット・push している）
git log --oneline -5
```

このファイルの「次にやること」の先頭から再開する。

**シーンやマテリアルが壊れていると思ったら、まず巻き戻して作り直す。**
```bash
git checkout -- Assets/Scenes           # シーンを最後のコミットに戻す
powershell -ExecutionPolicy Bypass -File run_visuals.ps1   # 見た目を作り直す（30-40分）
powershell -ExecutionPolicy Bypass -File run_playtest.ps1  # 動作確認
```
配置と見た目はすべてスクリプトから再生成できるので、手で直そうとしなくてよい。

---

## 環境

```
プロジェクト: C:\Users\hvnes\YomawariByoin
Unity:        6000.4.7f1  (C:\Program Files\Unity\Hub\Editor\6000.4.7f1\Editor\Unity.exe)
レンダリング:  URP
Git:          main で作業。origin あり。push 済み（節目ごとにコミット + push している）
Blender:      C:\Program Files\Blender Foundation\Blender 5.1\blender.exe（headless で使用）
Python:       numpy 2.4 + Pillow 12（テクスチャ生成に使う。追加インストール不要）
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

## 今セッションで変更したファイル

**すべて `origin/main` にコミット済み**（最新 `2903697`）。以下は何がどこにあるかの索引。

**新規（M1 時点）**
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

**新規（見た目・M3〜M11）**
```
Assets/Editor/M3AtmospherePass.cs           ← フロアごとの環境光・フォグ・過剰アルベド補正
Assets/Editor/M5LookPass.cs                 ← URP 設定（HDR/MSAA/SSAO）+ ポストプロセス
Assets/Editor/M5SetDressingPass.cs          ← 蛍光灯を割合で切る（フロアが深いほど多く）
Assets/Editor/M5GrimePass.cs                ← デカール配置 + ブレンド係数の修正
Assets/Editor/M6CorridorDetailPass.cs       ← 廊下設備の配置（配管・案内表示・巾木等）
Assets/Editor/M7SurfacePass.cs              ← 生成テクスチャの割り当て
Assets/Editor/M8PalettePass.cs              ← マテリアル色の整理
Assets/Editor/M9BakedLightingPass.cs        ← 静的マーク + ライトマップベイク
Assets/Editor/M10RealMaterialsPass.cs       ← ambientCG 実写マテリアル（天井のみ採用）
Assets/Editor/M11PolyHavenPropsPass.cs      ← Poly Haven スキャン小物の配置
Assets/Editor/PlayModeBatchRunner.cs        ← batchmode で Play に入り撮影・検証する
Assets/Editor/VisualDiagnostics.cs          ← ID 描画で画面上の物体を逆引きする
tools/gen_decals.py                         ← 汚しテクスチャ生成（暗い染み）
tools/gen_surfaces.py                       ← 表面の凹凸・粗さ生成
tools/blender/make_characters.py            ← キャラクター4体の生成
tools/blender/make_corridor_props.py        ← 廊下設備の生成
docs/Mixamo導入手順.md                       ← キャラクター取得の手順（ユーザー操作待ち）
Assets/Models/PolyHaven/                    ← CC0 スキャン小物 10種（87MB）
Assets/Textures/Ambient/                    ← ambientCG 素材（217MB・天井のみ使用）
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

優先度順。上から順に取ればよい。

### 1. キャラクター（最優先・ユーザー操作待ち）

**背景と人物の落差が現時点で最も目につく弱点。** 背景は実写素材と焼き込んだ
間接光で持ち上がったが、人物は顔の無い一体成型モデルで、敵は歩行モーションも無い。

`docs/Mixamo導入手順.md` の通りにファイルを `Assets/Models/Mixamo/` に置いてもらえれば、
インポート設定・Animator 構築・既存の敵/NPC への差し替え・ベイクし直しまで自動化できる。
**Adobe ログインが必要なため取得だけはユーザー側で行う必要がある。**

### 2. 人間の目が要る判断（バッチでは決められない）

配置と見た目の「良し悪し」は自動判定できない。エディタで見て決めるほうが速い。

1. **エディタで `Hospital.unity` を開いて Play し、実際に操作する**
   - Console に `===== プレイ時セルフチェック =====` が出る
   - WASD 移動 / マウス視点 / `C` しゃがみ / `E` 調べる / `F` 攻撃
   - `F9` = 10分スキップ、`F10` = エンド強制発火
2. ✅ **スポーン位置** → 解決。廊下の長手方向を向いて始まるようになった。
   ただし設計上は「病室で目が覚める」なので、病室が歩ける空間になれば
   `SceneWiringFixer.StartSearchAreas` が自動でそちらを選ぶ（第一希望にしてある）
3. **明るさのバランス**。実測は 平均輝度 0.198 / 暗部(<0.15) 53.7%。
   光の溜まりと闇のコントラストは付いた。**これ以上暗くすると見えなくなる**ので、
   もし調整するなら明るくする方向。触る場所は2つ:
   - `M5LookPass` の `postExposure`（現在 -0.55）… 画全体の明るさ
   - `M3AtmospherePass.MoodFor()` … フロアごとの `ambient` / `fogDensity` / `lightScale`
4. **3F 右壁の明るさ**（左右で揃っていない。ジオメトリは正常と確認済み）
5. **推定で置いた座標の確認**（いずれも床の上にはスナップ済み）
   - 1F NPC_Nurse `(-1.33, -0.08, -3.00)`
   - 1F 脱出トリガー `(-1.47, 0.00, -15.75)` ← 階段の反対側を玄関と仮定
   - 地下 幻覚の分岐点 `(-0.24, 0.95, 6.82)` ← 仮に点光源。人影モデルに要差し替え
   - 3F→地下 の遷移トリガー ← 2F の階段位置を流用

### 3. まだ手を付けていない見た目の項目

6. 天井が床プレハブの流用（未使用の `P_Ceiling_01` がある）
7. 病室天井の誤マテリアル（青いテクスチャ）
8. 配管が4m単位で途切れる／設備の配置が機械的（`M6CorridorDetailPass`）
9. **壁の高さ不揃い**: `P_Wall_02` が 7.62m、`P_Wall_01` が 4.59m。
   `M3WallHeightFix`（多数決で揃える）を実装済みだが **適用していない**。
   4シーン計76枚を動かす変更で、1視点からは改善が確認できなかったため保留。
   エディタで全体を見られるなら適用を判断してよい（`run_wallfix.ps1`）

### 4. 後回しにしているもの

10. **マルチプレイ（Steam P2P）**。`NetworkManager` は一度も実行されていない。
    ユーザーの指示で見た目を優先したため着手していない
11. リリース前: 日本語フォントを再配布可能なもの（Noto Sans JP 等）に同梱切り替え
12. 放置されている `Assets/Editor/HospitalPackFixV2.cs` の扱い

---

## 検証コマンド早見

| やりたいこと | コマンド | 所要 |
|-------------|---------|------|
| セットアップ一式を反映＋検証 | `run_all.ps1` | 約10分 |
| **見た目を全部作り直す** | `run_visuals.ps1` | 30〜40分 |
| 実際に動くか（プレイモード＋エンド発火） | `run_playtest.ps1` | 約3分 |
| **全フロアをプレイして撮る** | `run_playtest_all.ps1` | 約12分 |
| 見た目の診断 → 雰囲気パス適用 | `run_m3.ps1` | 約5分 |
| 画づくり設定の適用＋各フロア撮影 | `run_look.ps1` | 約6分 |
| 廊下設備の配置＋撮影 | `run_detail.ps1` | 約6分 |
| キャラクター造形の作り直し＋撮影 | `run_characters.ps1` | 約8分 |
| 画面上の物体を ID 描画で逆引き | `run_identify.ps1` | 約4分 |
| 壁パネルの配置と重なりを調べる | `run_walls.ps1` | 約4分 |
| 小物の配置をやり直す（+ベイク+撮影） | `run_props_fix.ps1` | 約7分 |
| **M10 が冪等か確かめる** | `run_m10_check.ps1` | 約1分 |
| 音を作り直す | `python tools/gen_audio.py` → `M14AudioPass` | 約2分 |
| **未設定の参照を調べる** | `M16ReferenceAudit` | 約1分 |
| キャラの骨とモーションを作り直す | `blender --background --python tools/blender/rig_characters.py` → `M15CharacterAnimationPass` | 約3分 |

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

### 2026-07-28 〜 07-30（見た目の作り込み）

ユーザーの指示は「市販レベルのホラーゲームを想定、見た目にこだわる」「よりリアルに」
「現実のようなクオリティに」。マルチプレイは後回しと明示された。

やったこと（すべてバッチパスとして残してあるので再実行できる）:
- **画づくりの土台**（M5）: HDR / 4xMSAA / 追加ライトの影 / SSAO、
  ACES トーンマップ・彩度落とし・影を青く・フィルムグレイン
- **明暗のリズム**（M5）: 蛍光灯をフロアが深いほど多く切る（1F 20% → 地下 55%）
- **汚し**（M5）: デカール約1,600枚。種類ごとに付く高さを変える
  （水は天井から、カビは床際、擦り傷は手の高さ、血は3Fと地下のみ）
- **廊下のディテール**（M6）: Blender で配管・換気口・案内表示・ラジエーター・
  巾木・回り縁を作り305個配置。「空の箱」だった廊下を埋めた
- **表面の質感**（M7）: ノーマル・粗さマップを持たないマテリアルに生成テクスチャを割り当て
- **色の整理**（M8）: テクスチャ持ちは白、生成テクスチャ側は実際の材料色
- **間接光の焼き込み**（M9）: 4シーンをベイク。ライトは Mixed なので点滅は生きたまま
- **実写マテリアル**（M10）: ambientCG から9素材。**採用は天井1つだけ**（下記）
- **スキャン小物**（M11）: Poly Haven から10種57個
- **見た目の再現手段**（`run_visuals.ps1`）: 上記を依存順に1本でやり直せるようにした

**この期間で分かった一番大きいこと:**
「実写スキャンだから写実的になる」は成立しない。**素材の実寸と貼る面の大きさが
噛み合うかどうかで決まる。** 1〜2m の壁素材を長い廊下に敷くと繰り返しが模様に見える。
一方で小物は物体そのものの大きさで作られているので必ず噛み合う。
→ **大きい面は手続き生成、独立した物体はスキャン。**

**外し方の記録**（推測で当てにいって毎回外した）:
「壁に白い矩形」を**4回**追い、原因は毎回別だった（アルベド1.0超 → 白背景テクスチャ
→ ブレンド係数を `SetInt` で書いていた → URP の Preserve Specular が入れ直していた）。
道具自体も2回疑うべきだった。
以降は必ず実測（輝度計測・ID 描画・`.mat` の直読み）してから直している。

### 2026-07-30（効いていなかったものを直す）

見た目のパスを一括で走らせる `run_visuals.ps1` を作り、実際に通してみたところ、
**作ったはずの処理がいくつも効いていなかった**ことが判明した。詳細は冒頭の表。

一番大きかったのはポストプロセスで、`VolumeProfile.Add<T>()` の戻り値を
アセットの子として保存していなかったため、ACES・彩度・グレイン・ビネットが
**一度も適用されていなかった**。さらに検証用の撮影が LDR ターゲットに描いていて
トーンマップを飛ばしていたので、**そもそも画で気づけない状態だった**。
両方直したところ、平均輝度 0.419 → 0.198 になり、初めて夜の病院の画になった。

この期間の教訓は1つに集約できる:
**「設定した」ではなく「残った」を確認する。** ログが成功と言っていても、
書いた値がアセットに残っているかは別の問題。読み直して数えるコードを書くこと。
