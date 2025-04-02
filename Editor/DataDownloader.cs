using UnityEditor;
using UnityEngine;
using System.Net;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine.Networking;

namespace LocalPoliceDepartment.Utilities.AccountManager.Editor
{
    public class DataDownloader : EditorWindow
    {
        private string urlString = "";
        private bool isDownloading = false;
        private OfficerAccountManager targetAccountManager = null;
        private string downloadedData = "";
        private bool downloadComplete = false;
        private bool downloadError = false;
        private string errorMessage = "";
        
        // Editable offline data (for review before saving)
        private Vector2 dataScrollPosition;
        private string dataPreview = "";
        
        // Status message
        private string statusMessage = "";
        private float statusDisplayTime = 0f;
        private bool showingStatus = false;

        [MenuItem("LPD/Account Manager/Data Downloader")]
        public static void ShowWindow()
        {
            DataDownloader window = GetWindow<DataDownloader>("Account Data Downloader");
            window.minSize = new Vector2(450, 500);
            
            // Find account manager in scene if possible
            if (window.targetAccountManager == null)
            {
                window.targetAccountManager = FindObjectOfType<OfficerAccountManager>();
            }
            
            window.Show();
        }
        
        public static void ShowWindowWithTarget(OfficerAccountManager target)
        {
            DataDownloader window = GetWindow<DataDownloader>("Account Data Downloader");
            window.minSize = new Vector2(450, 500);
            
            window.targetAccountManager = target;
            
            // If target has a URL set, use that as default
            if (target != null)
            {
                SerializedObject serializedObject = new SerializedObject(target);
                SerializedProperty remoteURLProperty = serializedObject.FindProperty("_remoteDataURL");
                if (remoteURLProperty != null && remoteURLProperty.objectReferenceValue != null)
                {
                    // Get the value as a TextAsset or string property instead of trying to cast to VRCUrl
                    // This handles VRChat URLs in a more editor-friendly way
                    var urlObj = remoteURLProperty.objectReferenceValue;
                    if (urlObj is TextAsset textAsset)
                    {
                        window.urlString = textAsset.text;
                    }
                    else
                    {
                        // Try to get URL through the serialized property
                        SerializedObject urlSerializedObj = new SerializedObject(urlObj);
                        SerializedProperty urlProperty = urlSerializedObj.FindProperty("url");
                        if (urlProperty != null)
                        {
                            window.urlString = urlProperty.stringValue;
                        }
                    }
                }
            }
            
            window.Show();
        }

        void OnGUI()
        {
            GUILayout.Label("Account Data Downloader", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            EditorGUI.BeginDisabledGroup(isDownloading);
            
            EditorGUILayout.LabelField("Target Account Manager");
            targetAccountManager = (OfficerAccountManager)EditorGUILayout.ObjectField(
                targetAccountManager, typeof(OfficerAccountManager), true);
                
            EditorGUILayout.Space();
            
            EditorGUILayout.LabelField("URL to download from:");
            urlString = EditorGUILayout.TextField(urlString);
            
            EditorGUILayout.Space();
            
            bool canDownload = !string.IsNullOrEmpty(urlString) && !isDownloading && targetAccountManager != null;
            
            if (targetAccountManager == null)
            {
                EditorGUILayout.HelpBox("Please select a target Account Manager.", MessageType.Warning);
            }
            
            using (new EditorGUI.DisabledScope(!canDownload))
            {
                if (GUILayout.Button("Download Data"))
                {
                    DownloadData();
                }
            }
            
            EditorGUI.EndDisabledGroup();
            
            if (isDownloading)
            {
                EditorGUILayout.HelpBox("Downloading...", MessageType.Info);
                Repaint();
            }
            
            // Show preview if data is downloaded
            if (downloadComplete)
            {
                EditorGUILayout.Space();
                
                GUILayout.Label("Downloaded Data Preview:", EditorStyles.boldLabel);
                
                // Create a text area for the downloaded data
                GUIStyle textAreaStyle = new GUIStyle(EditorStyles.textArea);
                textAreaStyle.wordWrap = true;
                
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                dataScrollPosition = EditorGUILayout.BeginScrollView(dataScrollPosition, 
                    GUILayout.Height(200));
                
                // Allow editing the data before saving
                dataPreview = EditorGUILayout.TextArea(dataPreview, textAreaStyle, 
                    GUILayout.ExpandHeight(true));
                
                EditorGUILayout.EndScrollView();
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.Space();
                
                // Save data button
                if (GUILayout.Button("Save Data to Account Manager"))
                {
                    if (targetAccountManager != null)
                    {
                        SaveDataToAccountManager();
                    }
                    else
                    {
                        ShowStatus("No Account Manager selected!", true);
                    }
                }
            }
            
            // Display error message
            if (downloadError)
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox("Error: " + errorMessage, MessageType.Error);
            }
            
            // Display status message if active
            if (showingStatus && (EditorApplication.timeSinceStartup - statusDisplayTime < 5.0f))
            {
                EditorGUILayout.Space();
                EditorGUILayout.HelpBox(statusMessage, 
                    statusMessage.StartsWith("Error") ? MessageType.Error : MessageType.Info);
                Repaint();
            }
            else if (showingStatus)
            {
                showingStatus = false;
                Repaint();
            }
        }
        
        private void DownloadData()
        {
            if (string.IsNullOrEmpty(urlString) || targetAccountManager == null)
                return;
                
            isDownloading = true;
            downloadComplete = false;
            downloadError = false;
            downloadedData = "";
            dataPreview = "";
            
            EditorCoroutine.Start(PerformDownload());
        }
        
        private IEnumerator PerformDownload()
        {
            UnityWebRequest request = UnityWebRequest.Get(urlString);
            
            request.SendWebRequest();
            
            while (!request.isDone)
            {
                yield return null;
            }
            
            isDownloading = false;
            
            if (request.result == UnityWebRequest.Result.Success)
            {
                downloadedData = request.downloadHandler.text;
                dataPreview = downloadedData;
                downloadComplete = true;
                ShowStatus("Download successful!");
            }
            else
            {
                errorMessage = request.error;
                downloadError = true;
                ShowStatus("Error: " + errorMessage, true);
            }
            
            Repaint();
        }
        
        private void SaveDataToAccountManager()
        {
            if (targetAccountManager == null || string.IsNullOrEmpty(dataPreview))
                return;
                
            SerializedObject serializedObject = new SerializedObject(targetAccountManager);
            SerializedProperty rawDataProperty = serializedObject.FindProperty("rawOfficerData");
            
            if (rawDataProperty != null)
            {
                rawDataProperty.stringValue = dataPreview;
                serializedObject.ApplyModifiedProperties();
                ShowStatus("Data saved to Account Manager!");
                
                // Also create a text asset
                if (EditorUtility.DisplayDialog("Create Text Asset?", 
                    "Do you want to save this data as a Text Asset file?", "Yes", "No"))
                {
                    SaveAsTextAsset();
                }
            }
            else
            {
                ShowStatus("Error: Could not find raw data property", true);
            }
        }
        
        private void SaveAsTextAsset()
        {
            string filePath = EditorUtility.SaveFilePanelInProject(
                "Save Data As TextAsset", 
                "OfficerData", 
                "txt", 
                "Save officer data as a text asset");
                
            if (!string.IsNullOrEmpty(filePath))
            {
                File.WriteAllText(filePath, dataPreview);
                AssetDatabase.ImportAsset(filePath);
                
                // Set the text asset to the account manager
                TextAsset textAsset = AssetDatabase.LoadAssetAtPath<TextAsset>(filePath);
                if (textAsset != null && targetAccountManager != null)
                {
                    SerializedObject serializedObject = new SerializedObject(targetAccountManager);
                    SerializedProperty fileProperty = serializedObject.FindProperty("officerDataFile");
                    
                    if (fileProperty != null)
                    {
                        fileProperty.objectReferenceValue = textAsset;
                        serializedObject.ApplyModifiedProperties();
                        ShowStatus("Text asset created and assigned!");
                    }
                }
            }
        }
        
        private void ShowStatus(string message, bool isError = false)
        {
            statusMessage = message;
            showingStatus = true;
            statusDisplayTime = (float)EditorApplication.timeSinceStartup;
            if (isError)
            {
                Debug.LogError(message);
            }
            else
            {
                Debug.Log(message);
            }
            Repaint();
        }
    }

    // Simple EditorCoroutine implementation
    public class EditorCoroutine
    {
        public static EditorCoroutine Start(IEnumerator routine)
        {
            EditorCoroutine coroutine = new EditorCoroutine(routine);
            coroutine.Start();
            return coroutine;
        }

        private readonly IEnumerator routine;
        private bool isRunning;

        private EditorCoroutine(IEnumerator routine)
        {
            this.routine = routine;
        }

        private void Start()
        {
            isRunning = true;
            EditorApplication.update += Update;
        }

        public void Stop()
        {
            isRunning = false;
            EditorApplication.update -= Update;
        }

        private void Update()
        {
            if (!isRunning || !routine.MoveNext())
            {
                Stop();
            }
        }
    }
}
