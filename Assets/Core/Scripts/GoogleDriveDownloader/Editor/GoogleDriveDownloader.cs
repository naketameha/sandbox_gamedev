using UnityEngine;
using UnityEditor;
using System.IO;
using System.Collections;
using UnityEngine.Networking;
using System;

public class GoogleDriveDownloader : EditorWindow
{
    private string googleDriveUrl = "";
    private string targetFolderPath = "Assets/DownloadedFiles/";
    private string fileName = "";
    private bool isDownloading = false;
    private float downloadProgress = 0f;
    private string statusMessage = "";

    [MenuItem("Tools/Google Drive Downloader")]
    public static void ShowWindow()
    {
        GetWindow<GoogleDriveDownloader>("Google Drive Downloader");
    }

    private void OnGUI()
    {
        GUILayout.Label("Google Drive File Downloader", EditorStyles.boldLabel);
        EditorGUILayout.Space();

        // Google DriveのURL入力
        EditorGUILayout.LabelField("Google Drive Share URL:");
        googleDriveUrl = EditorGUILayout.TextField(googleDriveUrl);
        
        EditorGUILayout.Space();

        // ファイル名入力
        EditorGUILayout.LabelField("File Name (with extension):");
        fileName = EditorGUILayout.TextField(fileName);

        EditorGUILayout.Space();

        // 保存先フォルダパス
        EditorGUILayout.LabelField("Target Folder Path:");
        EditorGUILayout.BeginHorizontal();
        targetFolderPath = EditorGUILayout.TextField(targetFolderPath);
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Target Folder", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                // Assetsフォルダからの相対パスに変換
                if (selectedPath.Contains(Application.dataPath))
                {
                    targetFolderPath = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.Space();

        // ダウンロードボタン
        GUI.enabled = !isDownloading && !string.IsNullOrEmpty(googleDriveUrl) && !string.IsNullOrEmpty(fileName);
        if (GUILayout.Button("Download File"))
        {
            StartDownload();
        }
        GUI.enabled = true;

        // 進行状況表示
        if (isDownloading)
        {
            EditorGUILayout.Space();
            EditorGUI.ProgressBar(EditorGUILayout.GetControlRect(), downloadProgress, "Downloading...");
            EditorGUILayout.LabelField("Status:", statusMessage);
        }

        // ステータスメッセージ表示
        if (!string.IsNullOrEmpty(statusMessage) && !isDownloading)
        {
            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(statusMessage, MessageType.Info);
        }
    }

    private void StartDownload()
    {
        // Google DriveのURLを直接ダウンロード用に変換
        string directDownloadUrl = ConvertToDirectDownloadUrl(googleDriveUrl);
        
        if (string.IsNullOrEmpty(directDownloadUrl))
        {
            statusMessage = "無効なGoogle Drive URLです。共有リンクを確認してください。";
            return;
        }

        // フォルダが存在しない場合は作成
        if (!Directory.Exists(targetFolderPath))
        {
            Directory.CreateDirectory(targetFolderPath);
        }

        string fullPath = Path.Combine(targetFolderPath, fileName);
        
        isDownloading = true;
        downloadProgress = 0f;
        statusMessage = "ダウンロード開始...";

        // EditorCoroutineを使用してダウンロード
        EditorCoroutines.StartCoroutine(DownloadFile(directDownloadUrl, fullPath), this);
    }

private string ConvertToDirectDownloadUrl(string shareUrl)
    {
        try
        {
            // Google Driveの共有URLから直接ダウンロードURLに変換
            if (shareUrl.Contains("drive.google.com/file/d/"))
            {
                // URLからファイルIDを抽出
                int startIndex = shareUrl.IndexOf("/d/") + 3;
                int endIndex = shareUrl.IndexOf("/", startIndex);
                if (endIndex == -1)
                {
                    endIndex = shareUrl.IndexOf("?", startIndex);
                }
                if (endIndex == -1)
                {
                    endIndex = shareUrl.Length;
                }
                
                string fileId = shareUrl.Substring(startIndex, endIndex - startIndex);
                // 確認をリクエストするパラメータを追加
                return $"https://drive.google.com/uc?export=download&id={fileId}&confirm=t";
            }
            else if (shareUrl.Contains("drive.google.com/open?id="))
            {
                // 古い形式のURL
                int startIndex = shareUrl.IndexOf("id=") + 3;
                string fileId = shareUrl.Substring(startIndex);
                return $"https://drive.google.com/uc?export=download&id={fileId}&confirm=t";
            }
            else if (shareUrl.Contains("drive.google.com/uc?"))
            {
                // 既に直接ダウンロードURL形式の場合
                if (!shareUrl.Contains("confirm="))
                {
                    return shareUrl + "&confirm=t";
                }
                return shareUrl;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"URL変換エラー: {e.Message}");
        }
        
        return null;
    }

private IEnumerator DownloadFile(string url, string savePath)
    {
        using (UnityWebRequest request = UnityWebRequest.Get(url))
        {
            // タイムアウトを設定
            request.timeout = 300; // 5分
            // User-Agentを設定（一部のサーバーで必要）
            request.SetRequestHeader("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");

            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                downloadProgress = operation.progress;
                statusMessage = $"ダウンロード中... {(downloadProgress * 100):F1}%";
                Repaint();
                yield return null;
            }

            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    byte[] data = request.downloadHandler.data;
                    
                    // HTMLが返ってきた場合の検出
                    string dataAsString = System.Text.Encoding.UTF8.GetString(data, 0, Mathf.Min(1024, data.Length));
                    if (dataAsString.Contains("<!doctype html>") || dataAsString.Contains("<html"))
                    {
                        statusMessage = "エラー: Google Driveの認証ページが返されました。\n" +
                                      "ファイルの共有設定を確認してください：\n" +
                                      "1. ファイルを右クリック → 共有\n" +
                                      "2. '制限付き'から'リンクを知っている全員'に変更\n" +
                                      "3. '閲覧者'権限を確認";
                        Debug.LogError("Google Drive認証エラー: HTMLページが返されました。共有設定を確認してください。");
                    }
                    else
                    {
                        // ファイルを保存
                        File.WriteAllBytes(savePath, data);
                        
                        // UnityのAssetDatabaseを更新
                        AssetDatabase.Refresh();
                        
                        statusMessage = $"ダウンロード完了: {savePath}\nファイルサイズ: {data.Length / 1024f:F1} KB";
                        Debug.Log($"ファイルのダウンロードが完了しました: {savePath} ({data.Length} bytes)");
                    }
                }
                catch (Exception e)
                {
                    statusMessage = $"ファイル保存エラー: {e.Message}";
                    Debug.LogError($"ファイル保存エラー: {e.Message}");
                }
            }
            else
            {
                statusMessage = $"ダウンロードエラー: {request.error}";
                Debug.LogError($"ダウンロードエラー: {request.error}");
            }
        }
        
        isDownloading = false;
        downloadProgress = 0f;
        Repaint();
    }
}

// EditorCoroutineのサポートクラス
public static class EditorCoroutines
{
    public static void StartCoroutine(IEnumerator coroutine, EditorWindow window)
    {
        window.StartCoroutine(coroutine);
    }
}

// EditorWindowの拡張メソッド
public static class EditorWindowExtensions
{
    public static void StartCoroutine(this EditorWindow window, IEnumerator coroutine)
    {
        EditorApplication.CallbackFunction callback = null;
        callback = () =>
        {
            try
            {
                if (!coroutine.MoveNext())
                {
                    EditorApplication.update -= callback;
                }
            }
            catch (Exception e)
            {
                Debug.LogError($"Coroutine エラー: {e}");
                EditorApplication.update -= callback;
            }
        };
        
        EditorApplication.update += callback;
    }
}