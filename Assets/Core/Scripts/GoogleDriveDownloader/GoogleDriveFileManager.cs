using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Collections;
using UnityEngine.Networking;
using System;

[System.Serializable]
public class GoogleDriveFileData
{
    [Header("File Settings")]
    public string fileName = "";
    public string googleDriveUrl = "";
    public string targetFolderPath = "Assets/DownloadedFiles/";
    
    [Header("Status")]
    [SerializeField] private bool isDownloaded = false;
    [SerializeField] private string lastDownloadTime = "";
    
    public bool IsDownloaded => isDownloaded;
    public string LastDownloadTime => lastDownloadTime;
    
    public void SetDownloadStatus(bool status, string time = "")
    {
        isDownloaded = status;
        lastDownloadTime = string.IsNullOrEmpty(time) ? DateTime.Now.ToString("yyyy/MM/dd HH:mm:ss") : time;
    }
}

public class GoogleDriveFileManager : MonoBehaviour
{
    [Header("Auto Download Settings")]
    [SerializeField] private bool autoDownloadOnPlay = false;
    
    [Header("File List")]
    [SerializeField] private List<GoogleDriveFileData> fileList = new List<GoogleDriveFileData>();
    
    [Header("Download Settings")]
    [SerializeField] private int maxConcurrentDownloads = 3;
    [SerializeField] private float timeoutSeconds = 300f; // 5分
    
    private List<Coroutine> activeDownloads = new List<Coroutine>();
    
    private void Start()
    {
        if (autoDownloadOnPlay)
        {
            DownloadAllFiles();
        }
    }
    
    /// <summary>
    /// 全ファイルのダウンロードを開始
    /// </summary>
    public void DownloadAllFiles()
    {
        StartCoroutine(DownloadAllFilesCoroutine());
    }
    
    /// <summary>
    /// 指定したインデックスのファイルのみダウンロード
    /// </summary>
    public void DownloadFile(int index)
    {
        if (index >= 0 && index < fileList.Count)
        {
            StartCoroutine(DownloadSingleFileCoroutine(fileList[index]));
        }
    }
    
    /// <summary>
    /// 未ダウンロードのファイルのみダウンロード
    /// </summary>
    public void DownloadMissingFiles()
    {
        StartCoroutine(DownloadMissingFilesCoroutine());
    }
    
    /// <summary>
    /// 全ダウンロードを停止
    /// </summary>
    public void StopAllDownloads()
    {
        foreach (var download in activeDownloads)
        {
            if (download != null)
            {
                StopCoroutine(download);
            }
        }
        activeDownloads.Clear();
        Debug.Log("すべてのダウンロードを停止しました");
    }
    
    private IEnumerator DownloadAllFilesCoroutine()
    {
        Debug.Log($"全ファイルのダウンロードを開始します ({fileList.Count}ファイル)");
        
        int downloadedCount = 0;
        int failedCount = 0;
        
        for (int i = 0; i < fileList.Count; i++)
        {
            // 同時ダウンロード数制限
            while (activeDownloads.Count >= maxConcurrentDownloads)
            {
                yield return new WaitForSeconds(0.1f);
                // 完了したダウンロードを削除
                activeDownloads.RemoveAll(d => d == null);
            }
            
            var downloadCoroutine = StartCoroutine(DownloadSingleFileCoroutine(fileList[i]));
            activeDownloads.Add(downloadCoroutine);
        }
        
        // すべてのダウンロードが完了するまで待機
        while (activeDownloads.Count > 0)
        {
            yield return new WaitForSeconds(0.1f);
            activeDownloads.RemoveAll(d => d == null);
        }
        
        // 結果をカウント
        foreach (var file in fileList)
        {
            if (file.IsDownloaded)
                downloadedCount++;
            else
                failedCount++;
        }
        
        Debug.Log($"全ファイルダウンロード完了 - 成功: {downloadedCount}, 失敗: {failedCount}");
    }
    
    private IEnumerator DownloadMissingFilesCoroutine()
    {
        var missingFiles = fileList.FindAll(f => !f.IsDownloaded);
        Debug.Log($"未ダウンロードファイルのダウンロードを開始します ({missingFiles.Count}ファイル)");
        
        foreach (var file in missingFiles)
        {
            // 同時ダウンロード数制限
            while (activeDownloads.Count >= maxConcurrentDownloads)
            {
                yield return new WaitForSeconds(0.1f);
                activeDownloads.RemoveAll(d => d == null);
            }
            
            var downloadCoroutine = StartCoroutine(DownloadSingleFileCoroutine(file));
            activeDownloads.Add(downloadCoroutine);
        }
        
        // すべてのダウンロードが完了するまで待機
        while (activeDownloads.Count > 0)
        {
            yield return new WaitForSeconds(0.1f);
            activeDownloads.RemoveAll(d => d == null);
        }
        
        Debug.Log("未ダウンロードファイルの処理完了");
    }
    
    private IEnumerator DownloadSingleFileCoroutine(GoogleDriveFileData fileData)
    {
        if (string.IsNullOrEmpty(fileData.googleDriveUrl) || string.IsNullOrEmpty(fileData.fileName))
        {
            Debug.LogError($"ファイル情報が不完全です: {fileData.fileName}");
            yield break;
        }
        
        string directUrl = ConvertToDirectDownloadUrl(fileData.googleDriveUrl);
        if (string.IsNullOrEmpty(directUrl))
        {
            Debug.LogError($"無効なGoogle Drive URL: {fileData.googleDriveUrl}");
            yield break;
        }
        
        // フォルダ作成
        if (!Directory.Exists(fileData.targetFolderPath))
        {
            Directory.CreateDirectory(fileData.targetFolderPath);
        }
        
        string fullPath = Path.Combine(fileData.targetFolderPath, fileData.fileName);
        
        Debug.Log($"ダウンロード開始: {fileData.fileName}");
        
        using (UnityWebRequest request = UnityWebRequest.Get(directUrl))
        {
            request.timeout = (int)timeoutSeconds;
            
            var operation = request.SendWebRequest();
            
            while (!operation.isDone)
            {
                yield return null;
            }
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                try
                {
                    File.WriteAllBytes(fullPath, request.downloadHandler.data);
                    fileData.SetDownloadStatus(true);
                    
                    Debug.Log($"ダウンロード成功: {fullPath}");
                }
                catch (Exception e)
                {
                    fileData.SetDownloadStatus(false);
                    Debug.LogError($"ファイル保存エラー ({fileData.fileName}): {e.Message}");
                }
            }
            else
            {
                fileData.SetDownloadStatus(false);
                Debug.LogError($"ダウンロードエラー ({fileData.fileName}): {request.error}");
            }
        }
        
        // AssetDatabaseの更新はメインスレッドで実行
        if (Application.isEditor)
        {
#if UNITY_EDITOR
            UnityEditor.AssetDatabase.Refresh();
#endif
        }
    }
    
    private string ConvertToDirectDownloadUrl(string shareUrl)
    {
        try
        {
            if (shareUrl.Contains("drive.google.com/file/d/"))
            {
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
                return $"https://drive.google.com/uc?export=download&id={fileId}";
            }
            else if (shareUrl.Contains("drive.google.com/open?id="))
            {
                int startIndex = shareUrl.IndexOf("id=") + 3;
                string fileId = shareUrl.Substring(startIndex);
                return $"https://drive.google.com/uc?export=download&id={fileId}";
            }
            else if (shareUrl.Contains("drive.google.com/uc?"))
            {
                return shareUrl;
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"URL変換エラー: {e.Message}");
        }
        
        return null;
    }
    
    /// <summary>
    /// ファイルリストに新しいファイルを追加
    /// </summary>
    public void AddFile(string fileName, string googleDriveUrl, string targetFolderPath = "Assets/DownloadedFiles/")
    {
        var newFile = new GoogleDriveFileData
        {
            fileName = fileName,
            googleDriveUrl = googleDriveUrl,
            targetFolderPath = targetFolderPath
        };
        fileList.Add(newFile);
    }
    
    /// <summary>
    /// 指定インデックスのファイルを削除
    /// </summary>
    public void RemoveFile(int index)
    {
        if (index >= 0 && index < fileList.Count)
        {
            fileList.RemoveAt(index);
        }
    }
    
    /// <summary>
    /// ダウンロード状況をリセット
    /// </summary>
    public void ResetDownloadStatus()
    {
        foreach (var file in fileList)
        {
            file.SetDownloadStatus(false, "");
        }
        Debug.Log("ダウンロード状況をリセットしました");
    }
}