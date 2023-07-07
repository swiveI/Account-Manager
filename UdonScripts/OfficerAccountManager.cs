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

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;
using VRC.SDK3.StringLoading;

namespace LoliPoliceDepartment.Utilities.AccountManager
{
    public enum DataSource
    {
        Local = 0,
        Network = 1
    }
    public class OfficerAccountManager : UdonSharpBehaviour
    {
        //NOTE:
        //For the most efficient runtime performance call Officerdata[int OfficerID][int RoleID]
        //or call the _Get functions using int OfficerID and int RoleID. String lookups are slow
        //but extremely convenient when you only need to do it a few times. You can speed up
        //string lookups by caching the result of _IDLookup(string name) or _RoleLookup(string name)
        //but this is not necessary unless you are doing it a lot.

        //To look up data for the local officer, use _GetString(role)
        //To look up someone else, use _GetString(officer, role)
        //_GetBool(), _GetInt(), and _GetFloat() work the same way

        //Default return values for _Get functions if the officer or role is not found:
        //string: ""
        //bool: false
        //int: 0
        //float: 0
        
        //Are we ready to perform lookups? (i.e. Are we waiting for a web server to respond with our data?)
        public bool isInitialized { get; private set; } = false;
        public bool initializedSuccessfully { get; private set; } = true; //Set to false if initialization fails
        public DataSource dataSource = DataSource.Local;
        [SerializeField] public VRCUrl RemoteDataURL = VRCUrl.Empty;
        [SerializeField] public string[] RoleNames;
        [SerializeField] public string[][] OfficerData;
        [SerializeField] public int[][] CustomLists;
        [SerializeField] public UdonEvent OnInitializedListeners;
       
        //LocalOfficerID (resolves on-demand rather than on start to avoid race conditions, can be called immediately)
        private int _LocalOfficerID = -2; //Uninitialized
        public int LocalOfficerID
        {
            get
            {
                if (_LocalOfficerID == -2) //Initialize
                {
                    _LocalOfficerID = -1; //Default to -1 "not found"
                    if (OfficerData.Length == 0)
                    {
                        Debug.LogError("Officer Account Manager: OfficerData is empty, please update the " +
                            "officer list using the LPD -> AccountManager window", this);
                        return -1;
                    }
                    _LocalOfficerID = _IDLookup(Networking.LocalPlayer.displayName);
                }
                return _LocalOfficerID;
            }

            private set
            {
                _LocalOfficerID = value;
            }
        }

        private void Awake() {
            //Setup if using local data
            if (dataSource == DataSource.Local)
            {
                isInitialized = true;
                OnInitializedListeners.SendCustomEvent("OnInitialized");
            }
        }

        private void Start() {
            //Setup if using remote data
            if (dataSource == DataSource.Network)
            {
                //Request data from the web server
                VRCStringDownloader.LoadUrl(RemoteDataURL, (VRC.Udon.Common.Interfaces.IUdonEventReceiver) this);
            }
        }

        //Allows UdonBehaviors to be notified when the data is ready
        public void NotifyWhenInitialized(UdonBehaviour receiver, string eventName)
        {
            if (isInitialized)
            {
                //Wait a frame so people can count on this not occuring instantly
                receiver.SendCustomEventDelayedFrames(eventName, 1);
            }
            else
            {
                //Add the receiver to the list of listeners
                OnInitializedListeners.AddReceiver(receiver, eventName);
            }
        }

        //Callback for when the web server responds with our data
        public override void OnStringLoadSuccess(IVRCStringDownload result) => ParseCSV(result.Result);
        public override void OnStringLoadError(IVRCStringDownload result) {
            isInitialized = true;
            initializedSuccessfully = false;
            //Let everybody know we tried
            OnInitializedListeners.Trigger();
            Debug.Log("Officer Account Manager: Failed to load data from " + RemoteDataURL.Get());
        }

        //Parse the CSV file
        public void ParseCSV(string csv)
        {
            // List<string> dataLines = new List<string>(text.Split('\n'));
            // List<string> dataColumns = new List<string>(dataLines[0].Split('\u002C'));
            string dataLinesString = csv;
            string dataColumnsString = dataLinesString.Substring(0, dataLinesString.IndexOf('\n'));

            // dataColumns.Insert(0, "ID");
            dataColumnsString = "ID" + '\u002C' + dataColumnsString;

            //remove the first line
            // dataLines.RemoveAt(0);
            dataLinesString = dataLinesString.Substring(dataLinesString.IndexOf('\n') + 1);
            
            //remove empty lines
            // dataLines.RemoveAll(string.IsNullOrEmpty);
            string[] dataLines = dataLinesString.Split('\n');
            bool[] emptyLines = new bool[dataLines.Length];
            int emptyLineCount = 0;
            for(int i = 0; i < dataLines.Length; i++)
            {
                if (string.IsNullOrEmpty(dataLines[i]))
                {
                    emptyLines[i] = true;
                    emptyLineCount++;
                }
            }
            //Copy only non-empty lines
            string[] newDataLines = new string[dataLines.Length - emptyLineCount];
            int newDataLinesIndex = 0;
            for (int i = 0; i < dataLines.Length; i++)
            {
                if (!emptyLines[i])
                {
                    newDataLines[newDataLinesIndex] = dataLines[i];
                    newDataLinesIndex++;
                }
            }
            dataLines = newDataLines;
            string[] dataColumns = dataColumnsString.Split('\u002C');
            
            // dataLines.Sort();
            Chanoler.Utility.ChanArray.Sort(dataLines);

            //create dataset
            string[][] dataset = new string[dataLines.Length][];

            for (int index = 0; index < dataLines.Length; index++)
            {
                string[] splitData = dataLines[index].Split('\u002C');
                if (splitData.Length != dataColumns.Length)
                {
                    int columnCount = dataColumns.Length;
                    int dataCount = splitData.Length;
                    
                    dataCount = Mathf.Clamp(dataCount, dataCount, columnCount);
                    
                    string[] correctedData = new string[columnCount];
                    for (int i = 0; i < dataCount; i++)
                    {
                        correctedData[i] = splitData[i];
                    }
                    for (int i = splitData.Length; i < columnCount; i++)
                    {
                        correctedData[i] = "";
                    }
                    splitData = correctedData;
                }
                
                string[] accountData = splitData;
                // accountData.Insert(0, index.ToString());
                string[] newAccountData = new string[accountData.Length + 1];
                newAccountData[0] = index.ToString();
                for (int i = 0; i < accountData.Length; i++)
                {
                    newAccountData[i + 1] = accountData[i];
                }
                accountData = newAccountData;
                
                //accountData.Last().Trim();

                dataset[index] = accountData; 
            }
            this.OfficerData = dataset;
            this.RoleNames = dataColumns;
            // officerdata.DataTitles = dataColumns; //Idk what this does so I'm commenting it out for now
            Debug.Log("<color=navy><b>Account Manager:</b></color> parsed " + dataSource + " data for " + OfficerData.Length + " users");

            isInitialized = true;
            initializedSuccessfully = true;
            OnInitializedListeners.Trigger();
        }

        //Look up an officer's ID by name
        //return -1 if not found
        public int _IDLookup(string name)
        {
            int low = 0;
            int high = OfficerData.Length - 1;

            while(low <= high)
            {
                int mid = (low + high) / 2;
                int compare = name.CompareTo(OfficerData[mid][1]);
                //Debug.Log("Officer Account Manager: comparing name " + name + " to officerList " + mid + " " + OfficerList[mid][0] + " returns " + compare);

                if (compare == 0)
                {
                    Debug.Log("Account Manager: Officer ID " + OfficerData[mid][0] + " for user " + name + " was found");
                    return Int32.Parse(OfficerData[mid][0]);
                }

                if (compare > 0)
                {
                    low = mid + 1;
                }
                else
                {
                    high = mid - 1;
                }
            }
            Debug.LogWarning("Account Manager: User \"" + name + "\" is not in the officer database, they will be treated as ID=-1, name=\"null\"", this);
            return -1;
        }

        //Look up a role's index by its name
        //return -1 if not found
        //Note: Roles are not sorted, so this is a linear search
        public int _RoleLookup(string roleName)
        {
            //Linear search, too lazy, not sorted
            for (int i = 0; i < RoleNames.Length; i++)
            {
                if (RoleNames[i] == roleName)
                {
                    return i;
                }
            }
            Debug.LogWarning("Account Manager: Role " + roleName + " was not found", this);
            return -1;
        }

        //Return whether the specified player is an officer
        public bool _IsOfficer(string name)
        {
            return _IDLookup(name) != -1;
        }
        public bool _IsOfficer(VRCPlayerApi player)
        {
            return _IsOfficer(player.displayName);
        }
        public bool _IsLocalPlayerOfficer()
        {
            return LocalOfficerID != -1;
        }


        //Main function for getting values
        public string _GetString(int officerID, int roleIndex)
        {
            if (officerID < 0 || officerID >= OfficerData.Length)
            {
                Debug.LogWarning("Account Manager: Officer ID " + officerID + " is out of range", this);
                return "";
            }
            if (roleIndex < 0 || roleIndex >= OfficerData[0].Length)
            {
                Debug.LogWarning("Account Manager: Role index " + roleIndex + " is out of range", this);
                return "";
            }
            return OfficerData[officerID][roleIndex];
        }

        //Different flavors of the _Get function
        /* Default values if the officerID or roleIndex is out of range
         *   string: ""
         *   bool: false
         *   int: 0
         *   float: 0
         */
        //String
        public string _GetString(string officerName, string roleName) { return _GetString(_IDLookup(officerName), _RoleLookup(roleName)); }
        public string _GetString(string officerName, int roleIndex  ) { return _GetString(_IDLookup(officerName), roleIndex); }
        public string _GetString(int officerID     , string roleName) { return _GetString(officerID, _RoleLookup(roleName)); }
        //String, local
        public string _GetString(int roleIndex  ) { return _GetString(LocalOfficerID, roleIndex); }
        public string _GetString(string roleName) { return _GetString(LocalOfficerID, _RoleLookup(roleName)); }
        //Bool
        public bool _GetBool(int officerID, int roleIndex)
        {
            string value = _GetString(officerID, roleIndex);
            bool success = bool.TryParse(value, out bool result);
            if (!success)
            {
                string officerName = officerID == -1 ? "null" : OfficerData[officerID][1];
                string roleName = roleIndex == -1 ? "null" : RoleNames[roleIndex];
                Debug.LogWarning("Account Manager: Officer \"" + officerName + "\" role \"" + roleName + "\" could not parse \"" + value + "\" as a bool", this);
                return false;
            }
            return result;
        }
        public bool _GetBool(string officerName, string roleName) { return _GetBool(_IDLookup(officerName), _RoleLookup(roleName)); }
        public bool _GetBool(string officerName, int roleIndex  ) { return _GetBool(_IDLookup(officerName), roleIndex); }
        public bool _GetBool(int officerID     , string roleName) { return _GetBool(officerID, _RoleLookup(roleName)); }
        //Bool, local
        public bool _GetBool(int roleIndex  ) { return _GetBool(LocalOfficerID, roleIndex); }
        public bool _GetBool(string roleName) { return _GetBool(LocalOfficerID, _RoleLookup(roleName)); }
        //Int
        public int _GetInt(int officerID, int roleIndex)
        {
            string value = _GetString(officerID, roleIndex);
            bool success = int.TryParse(value, out int result);
            if (!success)
            {
                string officerName = officerID == -1 ? "null" : OfficerData[officerID][1];
                string roleName = roleIndex == -1 ? "null" : RoleNames[roleIndex];
                Debug.LogWarning("Account Manager: Officer \"" + officerName + "\" role \"" + roleName + "\" could not parse \"" + value + "\" as an int", this);
                return 0;
            }
            return result;
        }
        public int _GetInt(string officerName, string roleName) { return _GetInt(_IDLookup(officerName), _RoleLookup(roleName)); }
        public int _GetInt(string officerName, int roleIndex  ) { return _GetInt(_IDLookup(officerName), roleIndex); }
        public int _GetInt(int officerID     , string roleName) { return _GetInt(officerID, _RoleLookup(roleName)); }
        //Int, local
        public int _GetInt(int roleIndex  ) { return _GetInt(LocalOfficerID, roleIndex); }
        public int _GetInt(string roleName) { return _GetInt(LocalOfficerID, _RoleLookup(roleName)); }
        //Float
        public float _GetFloat(int officerID, int roleIndex)
        {
            string value = _GetString(officerID, roleIndex);
            bool success = float.TryParse(value, out float result);
            if (!success)
            {
                string officerName = officerID == -1 ? "null" : OfficerData[officerID][1];
                string roleName = roleIndex == -1 ? "null" : RoleNames[roleIndex];
                Debug.LogWarning("Account Manager: Officer \"" + officerName + "\" role \"" + roleName + "\" could not parse \"" + value + "\" as a float", this);
                return 0;
            }
            return result;
        }
        public float _GetFloat(string officerName, string roleName) { return _GetFloat(_IDLookup(officerName), _RoleLookup(roleName)); }
        public float _GetFloat(string officerName, int roleIndex  ) { return _GetFloat(_IDLookup(officerName), roleIndex); }
        public float _GetFloat(int officerID     , string roleName) { return _GetFloat(officerID, _RoleLookup(roleName)); }
        //Float, local
        public float _GetFloat(int roleIndex  ) { return _GetFloat(LocalOfficerID, roleIndex); }
        public float _GetFloat(string roleName) { return _GetFloat(LocalOfficerID, _RoleLookup(roleName)); }
    }
}
