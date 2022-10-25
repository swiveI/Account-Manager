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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using LoliPoliceDepartment.Utilities;
using UnityEditor.Experimental.GraphView;
using UdonSharpEditor;

namespace LoliPoliceDepartment.Utilities.AccountManager
{
    [CustomEditor(typeof(OfficerAccountManager))]
    public class AccountManagerInspector : Editor, ISearchWindowProvider
    {
        private OfficerAccountManager AccountManager;
        private OfficerData officerData;
        private string displayedAccount;
        private string searchname;
        private int displayedID;

        private void OnEnable()
        {
            if(target == null)
            {
                return; 
            }
            AccountManager = target as OfficerAccountManager;
            officerData = FindObjectOfType<OfficerData>();
        }
        public override void OnInspectorGUI()
        {
            //update our data
            serializedObject.Update();
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("Total Accounts: " + AccountManager.OfficerData.Length, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label(AccountManager.CustomLists.Length + " Custom Lists. Check the Account Manager Utility for info.");
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            GUILayout.Space(5f);
            GUILayout.BeginHorizontal();
            GUILayout.FlexibleSpace();
            GUILayout.Label("User Data for ID# " + displayedID, EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            GUILayout.EndHorizontal();

            if (GUILayout.Button("Search"))
            {
                SearchWindow.Open(new SearchWindowContext(GUIUtility.GUIToScreenPoint(Event.current.mousePosition)), this);
            }
            if(displayedAccount != searchname)
            {
                //make sure data actually exists
                if (AccountManager.OfficerData.Length <= 0)
                {
                    return;
                }

                //find user data
                displayedID = AccountManager._IDLookup(searchname);
                if(displayedID == -1)
                {
                    //broke
                    return;
                }
                displayedAccount = searchname;
            }
            if (displayedAccount != null)
            {
                //display next to titles
                for (int i = 1; i < officerData.DataTitles.Length; i++)
                {
                    GUILayout.BeginHorizontal();
                    GUILayout.Label( i + " " +officerData.DataTitles[i], GUILayout.Width(Screen.width / 2));
                    EditorGUI.BeginChangeCheck();
                    string text = GUILayout.TextField(AccountManager.OfficerData[displayedID][i], GUILayout.Width(Screen.width / 2));
                    if (EditorGUI.EndChangeCheck())
                    {
                        Undo.RecordObject(AccountManager, "Changed officer data");
                        AccountManager.OfficerData[displayedID][i] = text;
                    }
                    GUILayout.EndHorizontal();
                }
            }
        }

        public List<SearchTreeEntry> CreateSearchTree(SearchWindowContext context)
        {
            List<SearchTreeEntry> searchTreeEntries = new List<SearchTreeEntry>();
            searchTreeEntries.Add(new SearchTreeGroupEntry(new GUIContent("PlayerName"), 0));

            for (int i = 0; i < AccountManager.OfficerData.Length; i++)
            {
                SearchTreeEntry entry = new SearchTreeEntry(new GUIContent(AccountManager.OfficerData[i][1]));
                entry.level = 1;
                entry.userData = AccountManager.OfficerData[i][1];
                searchTreeEntries.Add(entry);
            }
            return searchTreeEntries;
        }

        public bool OnSelectEntry(SearchTreeEntry SearchTreeEntry, SearchWindowContext context)
        {
            searchname = (string)SearchTreeEntry.userData;
            return true;
        }
    }

}
