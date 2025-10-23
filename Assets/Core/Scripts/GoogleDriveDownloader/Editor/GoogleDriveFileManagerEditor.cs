using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

[CustomEditor(typeof(GoogleDriveFileManager))]
public class GoogleDriveFileManagerEditor : Editor
{
    private GoogleDriveFileManager manager;
    private bool showFileList = true;
    private bool showDownloadSettings = false;
    
    private void OnEnable()
    {
        manager = (GoogleDriveFileManager)target;
    }
    
    public override void OnInspectorGUI()
    {
        serializedObject.Update();
        
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Google Drive File Manager", EditorStyles.boldLabel);
        EditorGUILayout.Space();
        
        // Auto Download Settings
        EditorGUILayout.PropertyField(serializedObject.FindProperty("autoDownloadOnPlay"));
        
        EditorGUILayout.Space();
        
        // Download Settings
        showDownloadSettings = EditorGUILayout.Foldout(showDownloadSettings, "Download Settings");
        if (showDownloadSettings)
        {
            EditorGUI.indentLevel++;
            EditorGUILayout.PropertyField(serializedObject.FindProperty("maxConcurrentDownloads"));
            EditorGUILayout.PropertyField(serializedObject.FindProperty("timeoutSeconds"));
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.Space();
        
        // File List
        var fileListProperty = serializedObject.FindProperty("fileList");
        showFileList = EditorGUILayout.Foldout(showFileList, $"File List ({fileListProperty.arraySize} files)");
        
        if (showFileList)
        {
            EditorGUI.indentLevel++;
            
            // ファイル追加ボタン
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add New File", GUILayout.Height(25)))
            {
                fileListProperty.arraySize++;
                serializedObject.ApplyModifiedProperties();
            }
            if (GUILayout.Button("Clear All", GUILayout.Height(25)))
            {
                if (EditorUtility.DisplayDialog("確認", "すべてのファイルを削除しますか？", "はい", "いいえ"))
                {
                    fileListProperty.arraySize = 0;
                    serializedObject.ApplyModifiedProperties();
                }
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.Space();
            
            // ファイルリスト表示
            for (int i = 0; i < fileListProperty.arraySize; i++)
            {
                var fileElement = fileListProperty.GetArrayElementAtIndex(i);
                DrawFileElement(fileElement, i);
                EditorGUILayout.Space();
            }
            
            EditorGUI.indentLevel--;
        }
        
        EditorGUILayout.Space();
        
        // 実行時でない場合のみ、実行ボタンを表示
        if (!Application.isPlaying)
        {
            EditorGUILayout.LabelField("Editor Mode Actions", EditorStyles.boldLabel);
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Download All Files", GUILayout.Height(30)))
            {
                manager.DownloadAllFiles();
            }
            if (GUILayout.Button("Download Missing Only", GUILayout.Height(30)))
            {
                manager.DownloadMissingFiles();
            }
            EditorGUILayout.EndHorizontal();
            
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Reset Download Status", GUILayout.Height(25)))
            {
                manager.ResetDownloadStatus();
            }
            if (GUILayout.Button("Stop All Downloads", GUILayout.Height(25)))
            {
                manager.StopAllDownloads();
            }
            EditorGUILayout.EndHorizontal();
        }
        else
        {
            // 実行時の状態表示
            EditorGUILayout.LabelField("Runtime Mode", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Download All Files", GUILayout.Height(30)))
            {
                manager.DownloadAllFiles();
            }
            if (GUILayout.Button("Stop All Downloads", GUILayout.Height(30)))
            {
                manager.StopAllDownloads();
            }
            EditorGUILayout.EndHorizontal();
        }
        
        serializedObject.ApplyModifiedProperties();
    }
    
    private void DrawFileElement(SerializedProperty fileElement, int index)
    {
        var fileName = fileElement.FindPropertyRelative("fileName");
        var googleDriveUrl = fileElement.FindPropertyRelative("googleDriveUrl");
        var targetFolderPath = fileElement.FindPropertyRelative("targetFolderPath");
        var isDownloaded = fileElement.FindPropertyRelative("isDownloaded");
        var lastDownloadTime = fileElement.FindPropertyRelative("lastDownloadTime");
        
        // ファイル情報のヘッダー
        EditorGUILayout.BeginVertical("box");
        
        // ファイル名とダウンロード状態
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField($"File {index + 1}", EditorStyles.boldLabel, GUILayout.Width(60));
        
        // ダウンロード状態表示
        string statusText = isDownloaded.boolValue ? "Downloaded" : "Not Downloaded";
        Color originalColor = GUI.color;
        GUI.color = isDownloaded.boolValue ? Color.green : Color.red;
        EditorGUILayout.LabelField($"[{statusText}]", GUILayout.Width(100));
        GUI.color = originalColor;
        
        // 個別ダウンロードボタン
        if (GUILayout.Button("Download", GUILayout.Width(80)))
        {
            manager.DownloadFile(index);
        }
        
        // 削除ボタン
        if (GUILayout.Button("×", GUILayout.Width(25)))
        {
            if (EditorUtility.DisplayDialog("確認", $"ファイル \"{fileName.stringValue}\" を削除しますか？", "はい", "いいえ"))
            {
                var fileListProperty = serializedObject.FindProperty("fileList");
                fileListProperty.DeleteArrayElementAtIndex(index);
                serializedObject.ApplyModifiedProperties();
                return;
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // ファイル名
        EditorGUILayout.PropertyField(fileName, new GUIContent("File Name"));
        
        // Google Drive URL
        EditorGUILayout.PropertyField(googleDriveUrl, new GUIContent("Google Drive URL"));
        
        // ターゲットフォルダパス
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PropertyField(targetFolderPath, new GUIContent("Target Folder"));
        if (GUILayout.Button("Browse", GUILayout.Width(60)))
        {
            string selectedPath = EditorUtility.OpenFolderPanel("Select Target Folder", "Assets", "");
            if (!string.IsNullOrEmpty(selectedPath))
            {
                if (selectedPath.Contains(Application.dataPath))
                {
                    targetFolderPath.stringValue = "Assets" + selectedPath.Substring(Application.dataPath.Length);
                }
            }
        }
        EditorGUILayout.EndHorizontal();
        
        // ダウンロード時刻表示
        if (isDownloaded.boolValue && !string.IsNullOrEmpty(lastDownloadTime.stringValue))
        {
            EditorGUILayout.LabelField($"Last Downloaded: {lastDownloadTime.stringValue}", EditorStyles.miniLabel);
        }
        
        EditorGUILayout.EndVertical();
    }
}