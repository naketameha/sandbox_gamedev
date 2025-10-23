# プロジェクト概要
Unity6 URP環境での小規模テキストゲーム試作プロジェクト。
ボタンクリック → セリフ表示レベルのシンプルなゲームを想定。
Visual Scriptingメイン、C#は補助・複雑な処理用。

## 技術スタック
- Unity 6 (最新版・保守考慮なしでアップデート)
- URP (Universal Render Pipeline)
- Visual Scripting (メインロジック)
- C# (ヘルパー、エディタ拡張、複雑な処理)
- Google Apps Script (データ管理・スプレッドシート連携)
- Python (テキスト解析・口パクデータ生成: pandas, numpy？ライブラリ選定中)

## プロジェクト構造
```
UnityProject/
├── Assets/
│   ├── Core/               # スクリプト・シェーダー（Git管理）
│   └── その他いろいろ　　　　#Git管理しないサードパーティ製のプラグインや、画像、モデル等大きなアセット。AIに触らせない。
├── ProjectSettings/
├── Packages/
├── Tools/                  # 外部ツール群
│   ├── GAS/               # スプレッドシート連携（手コピペ実行）
│   └── Python/            # pythonで作成したツール。口パク生成など用
└── Docs/
```

## パフォーマンス方針

### エディタスクリプト
- **正確性最優先、速度は度外視**
- 処理に時間かかってもOK
- データの整合性・正確性を重視
- エラーチェックは厳密に
```csharp
// エディタスクリプトの例
// 全ファイル走査してもOK、正確さ優先
[MenuItem("Tools/Validate All Dialogue Data")]
static void ValidateAllData()
{
    // 遅くても確実にチェック
    string[] guids = AssetDatabase.FindAssets("t:DialogueData");
    foreach (string guid in guids)
    {
        string path = AssetDatabase.GUIDToAssetPath(guid);
        ValidateDialogueFile(path);
    }
}
```

### ランタイムスクリプト・グラフィック
- **なるべくパフォーマンス節約**
- ハイクオリティなグラフィック目指さない
- 何がパフォーマンス食うか不明なので保守的に
- Update()での重い処理は避ける
- キャッシュ活用、不要なGetComponent()削減
```csharp
// ランタイムの例
private Text dialogueText;
private Image characterImage;

void Awake()
{
    // 初期化時に取得・キャッシュ
    dialogueText = GetComponent<Text>();
    characterImage = transform.Find("Character").GetComponent<Image>();
}

void Update()
{
    // 重い処理は書かない
    // 必要ならコルーチンや非同期処理で
}
```

## コーディング規約

### 命名規則
- 変数名・メソッド名: キャメルケース (`currentLine`, `showDialogue()`)
- クラス名: パスカルケース (`DialogueManager`, `TextParser`)
- private フィールド: キャメルケース (`dialogueSpeed`)

### フィールドの扱い
Inspectorで編集したい値は `[SerializeField] private` を使用。
他スクリプトからのアクセスが必要な場合は `public` プロパティで公開。
```csharp
[SerializeField] private float textSpeed = 0.05f;
[SerializeField] private AudioClip voiceClip;

// 読み取り専用で公開
public float TextSpeed => textSpeed;
```

### コメント
- **日本語で記述必須**
- コードの意図・目的を説明（「なぜ」を書く）
- 「変更しました」「拡張しました」等の履歴的コメント不要
- 複雑なロジック、注意点、制約条件は必ず記載
```csharp
// 良いコメント例
// カメラの急激な動きを防ぐため、補間係数は0.1以下に制限
private const float maxLerpSpeed = 0.1f;

// セリフ表示中にスキップ入力されたら即座に全文表示
// コルーチンを止めずにフラグで制御
private bool isSkipping = false;

// 悪いコメント例
// textSpeedを追加  ← こういう履歴的コメントは不要
```

### 必須事項

#### NullReferenceException防止（最重要）
```csharp
void Update()
{
    // 毎フレーム処理は必ずnullチェック
    if (targetTransform == null) return;
    
    // null条件演算子を活用
    audioSource?.Play();
}

void Start()
{
    // 参照取得時も必ずnullチェック
    dialogueText = GetComponent<Text>();
    if (dialogueText == null)
    {
        Debug.LogError($"{gameObject.name} に Text コンポーネントがありません");
    }
}
```

#### 数値比較はビタ合わせ
```csharp
// Mathf.Approximately()は使わない
if (currentValue == targetValue) { }
if (playbackPosition == 0.0f) { }
```

#### 既存コメント・スクリプト名の保持
- コード修正時も既存コメントは削除しない（不正確になった場合のみ更新）
- スクリプト名（ファイル名・クラス名）は変更しない

#### エディタスクリプトのフォルダ構造
```
Scripts/
└── [ScriptName]/           # スクリプト名でフォルダ作成
    ├── [ScriptName].cs     # 本体（必要なら）
    └── Editor/             # Editorフォルダ必須
        └── [ScriptName]Editor.cs
```

例:
```
Scripts/
└── DialogueValidator/
    └── Editor/
        └── DialogueValidatorEditor.cs
```

## ファイル分割方針

### 基本原則
- **1ファイル = 1責務** を徹底
- 1ファイルは200行以内を目安に（厳密ではない）
- 複数の機能が混在したら分割を検討

### ファイル冒頭に依存関係を記述
各ファイルの先頭付近に、依存関係と役割をコメントで明記する。
```csharp
// DialogueManager.cs
// セリフ表示を管理するクラス
//
// 依存:
//   - DialogueData.cs: セリフデータの読み込み
//   - DialogueUI.cs: UI表示処理
//   - AudioManager.cs: 音声再生
//
// 呼び出し元:
//   - GameController.cs: ゲーム進行から呼ばれる
//   - Visual Scripting: 会話イベントから呼ばれる

using UnityEngine;

public class DialogueManager : MonoBehaviour
{
    // ...
}
```

このコメントにより:
- 修正時に影響範囲が分かる
- AIが関連ファイルを理解しやすい
- 後から見た時に構造把握できる

記述する項目や形式は状況に応じて柔軟に調整してよい。

### 分割の目安

**分割すべきサイン:**
- ファイルが300行超えた
- 「〇〇Manager」に複数の責務が混在
- 「Utils」「Helper」に無関係な関数が増えた
- スクロールしないと全体が見えない

**良い分割例:**
```csharp
// 悪い例: 1ファイルに全部
GameManager.cs (1000行)
- セリフ管理
- UI管理
- セーブロード
- サウンド管理

// 良い例: 責務ごとに分割
DialogueManager.cs (150行)    // セリフ管理のみ
UIManager.cs (120行)           // UI管理のみ
SaveDataManager.cs (180行)     // セーブロードのみ
AudioManager.cs (100行)        // サウンド管理のみ
```

### クラス設計
- Managerクラスは単一責務に
- staticヘルパー関数は機能別にクラス分け
```csharp
  // 悪い例
  public static class Utils
  {
      public static string ParseText() { }
      public static float CalculateDistance() { }
      public static Color BlendColors() { }
  }
  
  // 良い例
  public static class TextHelper { }
  public static class MathHelper { }
  public static class ColorHelper { }
```

### Visual Scripting連携への配慮
ヘルパー関数は小さいstaticクラスに分割すると、Visual Scriptingのノードとして使いやすい。

### 分割時の注意
- 既存コメントを保持
- 依存関係コメントを追加・更新
- ファイル名とクラス名を一致させる

### リファクタリングのタイミング
- 機能追加の前
- ファイルが肥大化してきたと感じたら
- 定期的（ファイル数20個ごと、など）

新規機能を追加する際は、既存ファイルに詰め込まず、新しいファイルとして作成することを優先する。

## Visual Scripting連携

### ヘルパー関数の設計
Visual Scriptingから呼び出しやすいよう、シンプルなstatic関数で実装。
```csharp
public static class DialogueHelper
{
    // シンプルな引数、明確な戻り値
    public static string ParseText(string rawText)
    {
        // タグ除去などの処理
        return rawText.Replace("<br>", "\n");
    }
    
    // 口パクデータ取得
    public static float[] GetLipsyncData(string characterName, int lineIndex)
    {
        // Pythonツールで生成したデータを読み込み
        return LoadLipsyncData(characterName, lineIndex);
    }
}
```


## 避けること

### エディタスクリプト
- データ検証の手抜き
- エラーメッセージ不足
- ユーザー確認なしの破壊的操作

### ランタイムスクリプト
- Update()での `GetComponent()`、ファイルI/O
- string連結の乱用（`StringBuilder`推奨）
- 不要なGameObject検索（`Find`, `FindObjectOfType`）


## プロジェクトの用途
小規模テキストゲーム試作。ボタンクリック → セリフ表示、キャラクター口パク、音声再生、シンプルな演出。

## 試行錯誤前提
仕様変更に強い設計、柔軟な実装、こまめな動作確認。予定は未定。

## 開発フロー
- Visual Scriptingでメインロジック構築
- 複雑な処理・ヘルパー関数をC#で実装
- エディタ拡張で作業効率化
- 外部ツール（GAS/Python）でデータ生成

## Git運用
- commit頻度: 細かめ（機能追加・修正毎）
- ブランチ: AI編集実験用にブランチ切る運用を検討
  - `main`: 動作確認済みバージョン
  - `ai-issue-N`: Claude Code作業用