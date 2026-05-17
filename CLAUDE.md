# 消灯 (Shōtō) - CLAUDE.md

## プロジェクト概要

1990年代の日本を舞台にした一人称視点の心理ホラーゲーム。
1〜4人のオンラインマルチプレイ対応。
プレイヤーは「廃病院を調査している」と思っているが、実は全員が患者である。

---

## 開発環境

```
エンジン：Unity 6（6000.4.7f1）
レンダリング：Universal Render Pipeline（URP）
言語：C#
マルチプレイ：Steam P2P（Steamworks.NET）
開発ツール：Claude Code + Unity MCP
バージョン管理：Git / GitHub
```

---

## ゲーム基本情報

```
タイトル：消灯
読み：しょうとう
ジャンル：一人称視点 心理ホラー
プレイ人数：1〜4人
制限時間：90分
配信プラットフォーム：Steam（Windows）
```

---

## 真相（開発者のみ）

```
表向き：廃病院に迷い込んだ調査者が脱出を目指す
実際：プレイヤー全員が病院の患者
　　　幽霊→医師・看護師・他の患者
　　　黒い人影→夜間スタッフ
　　　脱出できない→病院の構造のため
　　　消灯時間→鎮静剤で眠らされる
```

---

## 舞台

```
旧桐島病院（1990年代・閉鎖精神病院）

1F：外来・受付・院長室（チュートリアル）
2F：一般病室・処置室（幻覚が増え始める）
3F：隔離病棟（幻覚ピーク・自分の病室がある）
地下：記録保管室（最終エリア・エンド分岐）
```

---

## シナリオ概要

### 冒頭
薄暗い病室で目が覚める。手元に自分の筆跡のメモ「ここから出てはいけない」。
院内放送「消灯まで90分です。病室にお戻りください。」

### 3幕構成

**第1幕（0〜30分）**
- 他プレイヤーと合流
- 脱出を試みるが同じ廊下に戻る
- 幽霊らしきものを目撃

**第2幕（30〜60分）**
- 院内放送「残り30分です」
- 3Fの自分の病室を発見
- 幻覚が不安定になる
- 疑心暗鬼システム発動

**第3幕（60〜90分）**
- 院内放送「消灯10分前です」
- 地下の記録保管室へ
- 全手がかりが揃う
- エンド分岐

---

## エンドシステム

### 6エンド構成
```
ハッピーエンド（1個）
　覚醒：全てが幻覚と気づき治療を受け入れる
　　条件：全手がかり収集＋残り30分以上＋鏡直視＋NPCの言葉を聞いた

バッドエンド・悟られる系（2個）
　暴走：NPCを攻撃して隔離室へ
　　条件：NPCまたは他プレイヤーを攻撃
　孤立：他プレイヤーだけ覚醒（複数プレイ限定）
　　条件：他が覚醒したのに自分だけ気づけなかった

バッドエンド・悟られない系（3個）
　脱出：脱出成功したように見える（実はまだ病院）
　　条件：手がかりをほぼ無視して外に出ようとした
　救出：家族が迎えに来る（家族は幻覚）
　　条件：幻覚に従った
　日常：自分の家にいる（窓の外に鉄格子）
　　条件：時間切れ or 何も解決しないまま
```

### エンド判定優先度
暴走 > 孤立 > 覚醒 > 脱出 > 救出 > 日常

### 悟られないエンドの隠し演出
```
脱出：エンドロール後に入院記録の更新日表示＋画面一瞬切り替え
救出：エンドロール中盤に病室の写真カット
日常：エンドロール最後に窓の外の鉄格子
```

---

## システム設計

### GameManager
```csharp
enum GameState { Lobby, Playing, Ending, Result }
// タイマー管理：90分カウントダウン
// イベントトリガー：60分・30分・10分・5分
// 院内放送の自動再生
// 時間切れ時の強制エンド処理
```

### PlayerManager
```csharp
class PlayerData {
    string playerID;
    Vector3 position;
    float hallucinationLevel; // 0〜100
    FlagData flags;
    ParanoiaAction paranoiaResult;
    EndingType endingResult;
    bool isAwakened;
}
// 同期対象：位置・アニメーション・インタラクション・エンド確定状態
// 非同期（個別）：幻覚レベル・フラグ・見えているもの
```

### HallucinationSystem
```
幻覚レベル上昇要因：
　時間経過（毎分+1）
　特定エリア進入（+5〜10）
　NPCを攻撃（+15）
　鏡を避け続ける（+5）
　捕捉された（+20）

幻覚レベル下降要因：
　鏡を直視（-10）
　NPCの言葉を聞く（-5）
　手がかり発見（-5）

レベル帯：
　0〜30：じわじわ系のみ・NPC普通に見える
　30〜60：心理系追加・NPC幽霊っぽく見える
　60〜80：全演出・NPC幽霊に見える
　80〜100：常時発生・バッドエンド加速
```

### ParanoiaSystem（疑心暗鬼）
```
発動条件：幻覚レベル30以上
他プレイヤーがNPCに見える確率：(幻覚レベル - 60) × 1%
エリアの見え方が個人ごとに異なる
行動記録：Trusted / Doubted / Attacked
エンド影響：
　Trusted多い → 覚醒・孤立エンド寄り
　Doubted多い → 脱出・日常エンド寄り
　Attacked発生 → 暴走エンド確定
```

### EnemyManager（夜間警備員）
```
配置：各エリアに1体・計4体
行動：Patrolling / Chasing / Returning

発見条件：
　視野角：前方90度・距離10m（標準）
　暗所：前方60度・距離6m
　幻覚高：前方120度・距離15m
　走り足音：半径8m
　インタラクション音：半径5m

捕捉処理：
　暗転→「病室にお戻りください」→最寄り病室に転送
　幻覚レベル+20・捕捉カウント+1
　3回以上：覚醒エンド到達不可

隠れ場所：
　クローゼット・ベッド下・物陰
　60秒以上隠れると幻覚レベル+5/分

幻覚レベル連動：
　0〜30：普通の警備員・標準速度
　30〜60：顔のない人影・やや速い
　60〜100：黒く歪んだ人影・速い・視野広い
```

### FlagManager
```csharp
class FlagData {
    bool checkedOwnRoom;        // 自分の病室を調べた
    bool facedMirror;           // 鏡を直視した
    bool readMedicalRecord;     // カルテを読んだ
    bool listenedToNPC;         // NPCの言葉に耳を傾けた
    bool attackedNPC;           // NPCを攻撃しようとした
    bool collectedAllClues;     // 全手がかりを集めた
    bool followedHallucination; // 幻覚に従った
    bool triedToEscape;         // 脱出しようとした
    int captureCount;           // 捕捉回数
    ParanoiaAction paranoiaResult;
}
```

### HorrorEventSystem
```
じわじわ系：
　HumanShadow：廊下の人影（5〜15分間隔）
　Footsteps：3D足音（3〜8分間隔）
　NameCall：名前を呼ぶ声（10〜20分間隔）
　PhotoChange：写真の人物位置変化
　WindowFigure：窓の外の人影（3Fのみ）
　PhoneEvent：電話→無音→後ろにNPC

心理系：
　CorridorChange：廊下の微細な変化
　DiaryReflection：日記にプレイヤー情報を反映
　MirrorChange：鏡の顔変化

びっくり系：
　DarkRoomAppear：暗室で目の前にNPC
　MirrorDelay：鏡の0.5秒遅延（レベル60以上）
　DoorEvent：ドアを開けると暗闇に目
　TapeScream：録音テープの最後に叫び声
　BackVoice：真後ろから「ねえ」と声
　SuddenNPC：安全な場所に突然NPC出現
　SuddenLoudNoise：長い無音の後に急音
```

### AudioSystem
```
BGM：ambient_normal / ambient_tense / ambient_peak / ending_*
環境音：換気扇・時計・雨・廊下
院内放送：90分・60分・30分・10分・5分・0分
3Dサウンド：位置情報連動・距離減衰・壁遮蔽
幻覚連動：レベル上昇で環境音が歪む
```

### EndingSystem
```
フロー：
1. 各プレイヤーのエンド確定
2. 確定済みプレイヤーは現実視点で待機
3. 全員確定 or 時間切れ
4. 一斉にエンド画面へ
5. エンドロール
6. 隠し演出
```

### Discord連携（Phase 5で実装）
```
AudioInputManager（抽象化）
　├── UnityAudioInput（Discordなし）
　└── DiscordAudioInput（Discordあり）

音量大（叫び）：敵の視野拡大・幻覚+5
音量中（会話）：敵の検知範囲拡大・幻覚+2
全員無音5分：幻覚-10
全員同時発言：現実が一瞬見える
1人だけ発言（Discordあり）：他プレイヤー幻覚+3
```

---

## 開発フェーズ

```
Phase 1：プロトタイプ
　1部屋・1プレイヤー・基本移動・インタラクション
　敵の基本巡回・捕捉・フラグ管理・簡易エンド判定

Phase 2：コア実装
　4エリアのマップ・幻覚システム・NPC基本動作
　敵の完全実装・エンドシステム

Phase 3：マルチプレイ
　Steam P2P実装・プレイヤー同期
　敵のマルチプレイ対応・疑心暗鬼システム

Phase 4：ホラー演出
　全ホラーイベント・音響システム
　画面エフェクト・幻覚レベル連動演出

Phase 5：仕上げ
　Discord連携・Steam実績
　バランス調整・テストプレイ
```

---

## Unityバージョン・設定

```
Unity：6000.4.7f1
レンダリング：URP
入力システム：New Input System
マルチプレイ：Steamworks.NET
NavMesh：AI Navigation（敵の経路探索）
```

---

## 重要なルール

```
1. コードを書く前に必ずこのファイルを参照する
2. 幻覚レベルは常にプレイヤーごとに独立して管理する
3. エンド判定は優先度順に処理する
4. マルチプレイの同期対象と非同期対象を混同しない
5. ホラー演出は幻覚レベルに連動させる
6. 悟られないバッドエンドの真実はゲーム内で明示しない
7. Discordなしでも全機能でプレイできる設計を維持する
```

---

## ファイル構成（予定）

```
Assets/
　├── Scripts/
　│　　├── Core/
　│　　│　　├── GameManager.cs
　│　　│　　├── PlayerManager.cs
　│　　│　　└── TimeManager.cs
　│　　├── Systems/
　│　　│　　├── HallucinationSystem.cs
　│　　│　　├── ParanoiaSystem.cs
　│　　│　　├── HorrorEventSystem.cs
　│　　│　　├── FlagManager.cs
　│　　│　　└── EndingSystem.cs
　│　　├── Enemy/
　│　　│　　└── EnemyManager.cs
　│　　├── NPC/
　│　　│　　└── NPCManager.cs
　│　　├── Audio/
　│　　│　　└── AudioSystem.cs
　│　　├── UI/
　│　　│　　└── UIManager.cs
　│　　├── Network/
　│　　│　　└── NetworkManager.cs
　│　　└── Discord/
　│　　　　　├── AudioInputManager.cs
　│　　　　　├── UnityAudioInput.cs
　│　　　　　└── DiscordAudioInput.cs
　├── Scenes/
　│　　├── MainMenu
　│　　├── Lobby
　│　　└── Hospital（1F・2F・3F・地下）
　├── Prefabs/
　├── Audio/
　└── Plugins/
　　　└── steam_appid.txt
```

---

<!-- team-dev-rules-v1 -->
## チーム開発ルール

### エージェント役割分担
ディレクター：タスク分解・進捗管理
実装担当：C#コード作成・Unity MCP操作
テスト担当：動作確認・バグ報告
レビュー担当：コード品質・CLAUDE.md整合性確認

### トークン節約ルール
・CLAUDE.mdを必ず最初に読む
・不明点は実装せず質問する
・1タスクずつ完了してから次へ
・コードは最小限で動くものを先に作る
・テストはUnity Test Runnerを使う
