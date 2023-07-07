// Account manager is a tool for converting spreadsheet data, via csv, into arrays for easy access by udonbehavours within VRChat.
// Copyright (C) 2022 Adam Thomas
// This program is free software: you can redistribute it and/or modify
// it under the terms of the GNU General Public License as published by
// the Free Software Foundation, either version 3 of the License, or
// (at your option) any later version.
//
// This program is distributed in the hope that it will be useful,
// but WITHOUT ANY WARRANTY; without even the implied warranty of
// MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
// GNU General Public License for more details.
//
// You should have received a copy of the GNU General Public License
// along with this program.  If not, see <https://www.gnu.org/licenses/>.

using UnityEngine;
using UnityEditor;
using UdonSharp;
using UdonSharpEditor;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.CompilerServices;
using TMPro;
using UnityEngine.SceneManagement;

namespace LoliPoliceDepartment.Utilities.AccountManager
{
    public class AccountUpdaterUtility : EditorWindow   
    {
        [SerializeField] TextAsset DataSheet;
        [SerializeField] OfficerAccountManager accountManager;
        [SerializeField] Texture2D HeaderTexture;
        [SerializeField] string customListValue;
        [SerializeField] int customListSelected = 0;
        [SerializeField] OfficerData officerdata;
        bool initilized =  false;
        private Vector2 scroll;

        private Texture2D twitterLogo;
        private Texture2D discordLogo;
        private Texture2D youtubeLogo;
        private Texture2D kofiLogo;
        
        
        private const string DatasheetKeyPath = "LPD/AccountManager/DatasheetAsstetPath";
        enum dataType {csv};
        enum mode {Replace, Add}
        [SerializeField] dataType format;
        [SerializeField] mode datamode;

        [MenuItem("LPD/AccountManager")]
        public static void ShowWindow()
        {
            AccountUpdaterUtility window = (AccountUpdaterUtility)GetWindow<AccountUpdaterUtility>("Account Manager");
            window.maxSize = new Vector2(1024f, 4000);
            window.minSize = new Vector2(256, 512);
            window.Show();
        }
        private void OnEnable()
        {
            HeaderTexture = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/com.lolipolicedepartment.accountmanager/Resources/TITLEBAR.png", typeof(Texture2D));
            twitterLogo = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/com.lolipolicedepartment.accountmanager/Resources/SocialLogos/TwitterLogo.png", typeof(Texture2D));
            discordLogo = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/com.lolipolicedepartment.accountmanager/Resources/SocialLogos/DiscordLogo.png", typeof(Texture2D));
            youtubeLogo = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/com.lolipolicedepartment.accountmanager/Resources/SocialLogos/YoutubeLogo.png", typeof(Texture2D));
            kofiLogo = (Texture2D)AssetDatabase.LoadAssetAtPath("Packages/com.lolipolicedepartment.accountmanager/Resources/SocialLogos/KofiLogo.png", typeof(Texture2D));

            accountManager = FindBehaviourOfType<OfficerAccountManager>();
            if (EditorPrefs.HasKey(DatasheetKeyPath))
            {
                DataSheet = AssetDatabase.LoadAssetAtPath<TextAsset>(EditorPrefs.GetString(DatasheetKeyPath));
            }
            if (accountManager == null)
            {
                Debug.LogError("<color=navy><b>Account Manager:</b></color> There is no UdonSharpBehaviour with the OfficerAccountManager script in the scene!");
                return; 
            }
            else
            {
                Debug.Log("<color=navy><b>Account Manager:</b></color> found AccountManager in the scene.");
                officerdata = FindObjectOfType<OfficerData>();
                if (officerdata == null)
                {
                    officerdata = new GameObject("Officerdata").AddComponent<OfficerData>();
                    officerdata.gameObject.hideFlags = HideFlags.HideInHierarchy;
                    officerdata.gameObject.tag = "EditorOnly";
                }
                initilized = true;
            }
        }
        private void OnGUI()
        {
            float drawarea = Screen.width / 4;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, drawarea), HeaderTexture, ScaleMode.ScaleToFit);
            if (initilized)
            {   
                GUILayout.BeginArea(new Rect(0, drawarea, Screen.width, Screen.height));
                scroll = EditorGUILayout.BeginScrollView(scroll, GUILayout.MaxHeight((Screen.height - drawarea) - 45));
                GUILayout.Space(5f);
                GUILayout.Label("Officer DataSheet", EditorStyles.boldLabel);

                EditorGUI.BeginChangeCheck();
                DataSheet = (TextAsset)EditorGUILayout.ObjectField(DataSheet, typeof(TextAsset), true);
                if (EditorGUI.EndChangeCheck())
                {
                    EditorPrefs.SetString(DatasheetKeyPath, AssetDatabase.GetAssetPath(DataSheet));
                }

                GUILayout.Space(5f);
                GUILayout.BeginHorizontal();
                GUILayout.BeginVertical();
                GUILayout.Label("Format", EditorStyles.boldLabel);
                format = (dataType)EditorGUILayout.EnumPopup(format);
                GUILayout.EndVertical();
                
                GUILayout.BeginVertical();
                GUILayout.Label("Mode", EditorStyles.boldLabel);
                datamode = (mode)EditorGUILayout.EnumPopup(datamode);
                GUILayout.EndVertical();
                
                GUILayout.BeginVertical();
                GUILayout.Label("", EditorStyles.boldLabel);
                if (GUILayout.Button("Update Officer Data", GUILayout.MinWidth(Screen.width/3)))
                {
                    switch (format)
                    {
                        case dataType.csv:
                            parseCSV();
                            break;
                            /*
                        case dataType.json:
                            parseJSON();
                            break;*/
                    }
                    UpdateCustomLists();
                }
                if (GUILayout.Button("Clear Data"))
                {
                    ClearData();
                }
                GUILayout.EndVertical();
                GUILayout.EndHorizontal();

                GUILayout.Space(5f);
                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Groups", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                GUILayout.Label("Here you can specify a value and create a list of user ID's that have that value", EditorStyles.wordWrappedLabel);

                GUILayout.BeginHorizontal();
                customListValue = GUILayout.TextField(customListValue, GUILayout.Width(Screen.width / 2));
                customListSelected = EditorGUILayout.Popup(customListSelected, officerdata.DataTitles);

                if (GUILayout.Button("Create List"))
                {
                    CreateCustomList(customListSelected, customListValue);
                }
                GUILayout.EndHorizontal();

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("Custom Lists", EditorStyles.boldLabel);
                if (GUILayout.Button("Update All"))
                {
                    UpdateCustomLists();
                }

                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();

                //draw custom lists

                for (int i = 0; i < officerdata.CustomLists.Count; i++)
                {
                    using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
                    {
                        GUILayout.BeginVertical();
                        GUILayout.BeginHorizontal();
                        GUILayout.Label("List " + i + " of " + officerdata.DataTitles[officerdata.CustomLists[i].Title] + " for " + officerdata.CustomLists[i].Ids.Count + " users with value " + officerdata.CustomLists[i].Value, EditorStyles.wordWrappedLabel);
                        if (GUILayout.Button("Delete", GUILayout.Width(Screen.width / 3)))
                        {
                            officerdata.CustomLists.RemoveAt(i);
                            UpdateCustomLists();
                            GUILayout.EndHorizontal();
                            continue;   
                        }
                        GUILayout.EndHorizontal();
                        
                        GUILayout.BeginHorizontal();
                        officerdata.CustomLists[i].nameDisplay = (TextMeshProUGUI)EditorGUILayout.ObjectField(officerdata.CustomLists[i].nameDisplay, typeof(TextMeshProUGUI), true);
                        if (GUILayout.Button("Update names", GUILayout.Width(Screen.width / 3)))
                        {
                            if (officerdata.CustomLists[i].nameDisplay == null)
                            {
                                Debug.LogError("<color=navy><b>Account Manager:</b></color> Assign a TextMeshPro Text object first!");
                            }
                            else
                            {
                                string listname = "";
                                for (int index = 0; index < officerdata.CustomLists[i].Ids.Count; index++)
                                {
                                    string name = accountManager.OfficerData[officerdata.CustomLists[i].Ids[index]][1];
                                    name = name.Replace('\u2024', '\u002e');
                                    listname += name + '\n';
                                }
                                officerdata.CustomLists[i].nameDisplay.text = listname;
                                EditorUtility.SetDirty(officerdata.CustomLists[i].nameDisplay);
                            }
                        }
                        GUILayout.EndHorizontal();
                        GUILayout.EndVertical();
                    }
                    GUILayout.Space(3f);
                }
                EditorGUILayout.EndScrollView();
                GUILayout.EndArea();
            }
            else
            {
                GUILayout.BeginArea(new Rect(0, drawarea, Screen.width, Screen.height));
                GUILayout.Space(5f);

                GUILayout.BeginHorizontal();
                GUILayout.FlexibleSpace();
                GUILayout.Label("No Officer Account manager in the scene", EditorStyles.boldLabel);
                GUILayout.FlexibleSpace();
                GUILayout.EndHorizontal();
                if(GUILayout.Button("Add to scene"))
                {
                    GameObject am = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath("Packages/com.lolipolicedepartment.accountmanager/Officer Account Manager.prefab", typeof(GameObject)));
                    PrefabUtility.UnpackPrefabInstance(am, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
                    if (officerdata == null)
                    {
                        Debug.Log("<color=navy><b>Account Manager:</b></color> Added officer data");
                        officerdata = new GameObject("Officerdata").AddComponent<OfficerData>();
                        officerdata.gameObject.hideFlags = HideFlags.HideInHierarchy;
                        officerdata.gameObject.tag = "EditorOnly";
                    }
                }
                GUILayout.EndArea();

                accountManager = FindBehaviourOfType<OfficerAccountManager>();
                officerdata = FindObjectOfType<OfficerData>();
                if (accountManager != null && officerdata != null)
                {
                    initilized = true;
                    return;
                }
            }
            GUILayout.BeginArea(new Rect(0, Screen.height - 43f, Screen.width, 25f));
            using (new GUILayout.HorizontalScope())
            {
                GUI.color = Color.white;
                GUI.backgroundColor = Color.white;
                
                GUI.backgroundColor = new Color(0.4509804f, 0.5411765f, 0.8588236f, 1f);
                if (GUILayout.Button(new GUIContent(discordLogo, "Discord"), EditorStyles.miniButtonMid, GUILayout.Width(Screen.width / 4), GUILayout.Height(60))) Application.OpenURL("https://discord.gg/lpd");
                GUI.backgroundColor = new Color(0.1137255f, .6313726f, 0.9490196f, 1f);
                if (GUILayout.Button(new GUIContent(twitterLogo, "Twitter"), EditorStyles.miniButtonMid, GUILayout.Width(Screen.width / 4), GUILayout.Height(60))) Application.OpenURL("https://twitter.com/LPD_vrchat");
                GUI.backgroundColor = new Color(0.8039216f, 0.1254902f, 0.1215686f, 1f);
                if (GUILayout.Button(new GUIContent(youtubeLogo, "Youtube"), EditorStyles.miniButtonMid, GUILayout.Width(Screen.width / 4), GUILayout.Height(60))) Application.OpenURL("https://www.youtube.com/c/LoliPoliceDepartment");
                GUI.backgroundColor = new Color(1f, 0.3137255f, 0.3137255f, 1f);
                if (GUILayout.Button(new GUIContent(kofiLogo, "Ko-fi"), EditorStyles.miniButtonMid, GUILayout.Width(Screen.width / 4), GUILayout.Height(60))) Application.OpenURL("https://ko-fi.com/lolipolicedepartment");
            }
            GUILayout.EndArea();
            GUI.backgroundColor = Color.white;
        }

        public void CreateCustomList(int listName, string listValue)
        {
            if (listValue == null)
            {
                Debug.LogWarning("<color=navy><b>Account Manager:</b></color> Insufficient data to create list from.");
                return;
            }
            //create new list and sate the data to it
            CustomDataList newList = new CustomDataList();
            newList.Title = listName;
            newList.Value = listValue;
            newList.Ids = new List<int>();
            officerdata.CustomLists.Add(newList);
            //update all lists to refresh ids
            UpdateCustomLists();
        }
        public void UpdateCustomLists()
        {
            List<int[]> TempLists = new List<int[]>();
            for (int index = 0; index < officerdata.CustomLists.Count; index++)
            {
                officerdata.CustomLists[index].Ids.Clear();
                for (int j = 0; j < accountManager.OfficerData.Length; j++)
                {
                    if (accountManager.OfficerData[j][officerdata.CustomLists[index].Title] == officerdata.CustomLists[index].Value)
                    {
                        officerdata.CustomLists[index].Ids.Add(j);
                    }
                }
                TempLists.Add(officerdata.CustomLists[index].Ids.ToArray());
            }
            accountManager.CustomLists = TempLists.ToArray();
        }
        public static T FindBehaviourOfType<T>() where T : UdonSharpBehaviour
        {
            foreach (GameObject obj in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                T foundBehaviours = obj.GetComponentInChildren<T>();
                if (foundBehaviours != null)
                {
                    return foundBehaviours;
                }
            }
            return null;
        }

        private void ClearData()
        {
            //clear udon behaviour
            accountManager.RoleNames = new string[0];
            accountManager.OfficerData = new string[0][];
            accountManager.CustomLists = new int[0][];
            //clear storage object
            officerdata.DataTitles = new string[0];
            officerdata.CustomLists =  new List<CustomDataList>(0);
        }
        private void parseCSV()
        {
            if (DataSheet != null)
            {
                string text = DataSheet.text;
                List<string> dataLines = new List<string>(text.Split('\n'));
                List<string> dataColumns = new List<string>(dataLines[0].Split('\u002C'));
                dataColumns.Insert(0, "ID");

                //remove the first line
                dataLines.RemoveAt(0);
                //remove the last line because csv
                dataLines.RemoveAt(dataLines.Count - 1);
                dataLines.Sort();

                //create dataset
                string[][] dataset = new string[dataLines.Count][];

                for (int index = 0; index < dataLines.Count; index++)
                {
                    List<string> accountData = new List<string>(dataLines[index].Split('\u002C'));
                    accountData.Insert(0, index.ToString());
                    accountData.Last().Trim();
                    dataset[index] = accountData.ToArray(); 
                }
                if (datamode == mode.Replace)
                {
                    accountManager.OfficerData = dataset;
                    accountManager.RoleNames = dataColumns.ToArray();
                    officerdata.DataTitles = dataColumns.ToArray();
                }
                if (datamode == mode.Add)
                {
                    string[][] combinedDataset = accountManager.OfficerData.Concat(dataset).ToArray();
                    accountManager.OfficerData = combinedDataset;
                }
                Debug.Log("<color=navy><b>Account Manager:</b></color> parsed " + format + " data for " + accountManager.OfficerData.Length + " users");
            }
            else
            {
                Debug.LogError("<color=navy><b>Account Manager:</b></color> No data sheet provided.");
            }
        }
        private void parseJSON()
        {
            Debug.Log("<color=navy><b>Account Manager:</b></color> JSON is not supported yet :c");
        }
    }
}