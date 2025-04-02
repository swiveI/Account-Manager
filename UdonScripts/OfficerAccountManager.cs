using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using System;
using System.Text;
using System.Security.Cryptography;
using System.IO;
using VRC.SDK3.StringLoading;
using VRC.SDK3.Data;

#if UNITY_EDITOR && !COMPILER_UDONSHARP
using UnityEditor;
#endif


namespace LocalPoliceDepartment.Utilities.AccountManager
{
    //-------------------------Custom Enums-------------------------//
    /// <summary>
    /// The source of data for the account manager.
    /// </summary>
    public enum DataSource
    {
        Editor = 0,
        Internet = 1
    }
    
    /// <summary>
    /// The expcted data format for parsing.
    /// </summary>
    public enum DataFormat
    {
        CSV = 0,
        JSON = 1
    }

    /// <summary>
    /// Represents the comparison operators that can be used for filtering generated role lists.
    /// </summary>
    public enum Comparator
    {
        EqualTo = 0,
        NotEqualTo = 1,
        GreaterThan = 2,
        LessThan = 3,
        GreaterThanOrEqualTo = 4,
        LessThanOrEqualTo = 5
    }


    /// <summary>
    /// Manages officer account data, including loading and parsing data from a remote source.
    /// </summary>
    [UdonBehaviourSyncMode(BehaviourSyncMode.None)]
    public class OfficerAccountManager : UdonSharpBehaviour
    {

        //-------------------------Notes-------------------------//
        #region Notes
        //PLEASE NOTE ALL OFFICERS HAVE ALL ROLES IF THIS IS A CSV FILE
        //If you want a list of staff but are using CSV, please run _CreateFilteredRoleList("Staff", Comparator.EqualTo, "true")

        //Default return values for _Get functions if the officer or role is not found:
        //string: ""
        //bool: false
        //int: 0
        //float: 0
        #endregion


        //-------------------------Variables-------------------------//
        #region Variables
        
        /// <summary>
        /// Log the performance of raw data parsing, _CreateRoleList, and _CreateFilteredRoleList 
        /// </summary>
        [Tooltip("Log the performance of raw data parsing, _CreateRoleList, and _CreateFilteredRoleList")]
        [SerializeField] private bool performanceLogging = true;
        /// <inheritdoc cref="OfficerAccountManager.performanceLogging"/>
        private System.Diagnostics.Stopwatch stopwatch = new System.Diagnostics.Stopwatch();

        /// <summary>
        /// The raw data representing all of the officers.
        /// Editor/Offline data is stored here and is overwritten by Internet data if any is downloaded at runtime.
        /// </summary>
        [HideInInspector] // Uncomment this when not debugging
        [SerializeField, Multiline] public string rawOfficerData = "";

        /// <summary>
        /// For use by the custom inspector
        /// </summary>
        [SerializeField, HideInInspector] internal TextAsset officerDataFile = null;
        
        /// <summary>
        /// The URL for online officer data.
        /// If users have Untrusted URLs disabled then only the following sources are valid:
        /// <list type="bullet">
        ///     <item>GitHub (*.github.io)</item>
        ///     <item>Pastebin (pastebin.com)</item>
        ///     <item>Github Gist (gist.githubusercontent.com)</item>
        /// </list>
        /// See <see href="https://creators.vrchat.com/worlds/udon/string-loading/">the official VRChat documentation</see> for more information.
        /// </summary>
        [SerializeField] private VRCUrl _remoteDataURL;

        /// <summary>
        /// The preferred location for fetching account data.
        /// </summary>
        [SerializeField] public DataSource desiredDataSource = DataSource.Editor;

        /// <summary>
        /// The locaton of the data that is currently loaded.
        /// </summary>
        [NonSerialized, HideInInspector] public DataSource currentDataSource = DataSource.Editor;

        /// <summary>
        /// The expected format of the data for parsing.
        /// </summary>
        [SerializeField] public DataFormat dataFormat = DataFormat.CSV;
        
        /// <summary>
        /// A value indicating whether the <see cref="OfficerAccountManager"/> is ready to be used.
        /// </summary>
        public bool isReady { get; private set; } = false;

        /// <summary>
        /// A value for tracking whether the <see cref="OfficerAccountManager"/> is currently busy initializing.
        /// A false value does not necessarily mean that the <see cref="OfficerAccountManager"/> is ready to be used.
        /// </summary>
        public bool isInitializing { get; private set; } = false;

        /// <summary>
        /// Indicates whether the <see cref="OfficerAccountManager"/> was initialized successfully.
        /// </summary>
        public bool initializedSuccessfully { get; private set; } = false;

        /// <summary>
        /// A dictionary of UdonBehaviors who wish to be notified when the <see cref="OfficerAccountManager"/> has finished initializing.
        /// Keys are typ <see cref="UdonBehavior"/>, values are the string function names to call on each <see cref="UdonBehavior"/> when the data is ready.
        /// </summary>
        [NonSerialized] private DataDictionary onInitializedListeners = new DataDictionary(); //Dictionary of UdonBehaviors and their String function names to be called when the data is ready

        /// <summary>
        /// A dictionary mapping officer names to their role dictionaries.
        /// </summary>
        [NonSerialized] public DataDictionary nameToRankDictionary = new DataDictionary();

        /// <summary>
        /// An unsorted list of all role names.
        /// </summary>
        [NonSerialized] public DataList roleList = new DataList();

        [SerializeField] private bool _enableSecureLoading = false;
        [SerializeField] private string _dataSignature = "";
        [SerializeField] private bool _securityDebugLogging = false;
        [NonSerialized] private readonly string[] _obf = new string[] { "trn", "dec", "val", "chk", "prs" };
        [NonSerialized] private byte[] _secureBuffer;
        [NonSerialized] private int[] _sBox;
        private const int N = 256;

        private void InitSBox(byte[] key)
        {
            if (_sBox == null)
            {
                _sBox = new int[N];
            }
            
            for (int i = 0; i < N; i++)
            {
                _sBox[i] = i;
            }
            
            int j = 0;
            for (int i = 0; i < N; i++)
            {
                j = (j + _sBox[i] + key[i % key.Length]) % N;
                int temp = _sBox[i];
                _sBox[i] = _sBox[j];
                _sBox[j] = temp;
            }
        }

        private byte[] GenerateKeystream(int length)
        {
            byte[] keystream = new byte[length];
            int i = 0, j = 0;
            
            for (int k = 0; k < length; k++)
            {
                i = (i + 1) % N;
                j = (j + _sBox[i]) % N;
                int temp = _sBox[i];
                _sBox[i] = _sBox[j];
                _sBox[j] = temp;
                int t = (_sBox[i] + _sBox[j]) % N;
                keystream[k] = (byte)_sBox[t];
            }
            
            return keystream;
        }

        private byte[] FeistelTransform(byte[] data, byte[] key, bool encrypt)
        {
            int rounds = 16;
            int blockSize = 8;
            int length = data.Length - (data.Length % blockSize);
            byte[] result = new byte[length];
            for (int block = 0; block < length; block += blockSize)
            {
                int leftStart = block;
                int rightStart = block + (blockSize / 2);
                for (int round = 0; round < rounds; round++)
                {
                    int keyRotation = encrypt ? round : (rounds - 1 - round);
                    for (int i = 0; i < blockSize / 2; i++)
                    {
                        byte temp = data[rightStart + i];
                        byte keyByte = key[(i + keyRotation) % key.Length];
                        byte substituted = (byte)_sBox[temp];
                        result[leftStart + i] = (byte)(data[leftStart + i] ^ (substituted + keyByte));
                    }
                    if (round < rounds - 1)
                    {
                        for (int i = 0; i < blockSize / 2; i++)
                        {
                            byte temp = data[leftStart + i];
                            data[leftStart + i] = data[rightStart + i];
                            data[rightStart + i] = temp;
                        }
                    }
                }
            }
            
            return result;
        }

        private byte[] CustomHash(byte[] data)
        {
            byte[] hash = new byte[32];
            if (data.Length == 0) return hash;
            int seed = data.Length;
            for (int i = 0; i < 32; i++)
            {
                hash[i] = (byte)(seed % 256);
                seed = (seed * 1103515245 + 12345) & 0x7fffffff;
            }
            for (int i = 0; i < data.Length; i++)
            {
                byte b = data[i];
                for (int j = 0; j < 32; j++)
                {
                    hash[j] = (byte)(hash[j] ^ 
                        ((hash[(j + 1) % 32] + b) * (i + 1)) ^
                        (hash[(j + 7) % 32] >> 1) ^
                        (hash[(j + 13) % 32] << 1));
                }
            }
            return hash;
        }

        private string _t(string input) => _decryptData(input);
        private string _decryptData(string input)
        {
            if (!_enableSecureLoading || string.IsNullOrEmpty(input)) 
            {
                if (_securityDebugLogging)
                    _LogSecurityStatus("Security disabled or empty input, skipping decryption");
                return input;
            }
            byte[] data;
            if (!_v(input, out data)) 
            {
                if (_securityDebugLogging)
                    _LogSecurityStatus("Validation failed: Data format invalid");
                return input;
            }
            if (_securityDebugLogging)
            {
                _LogSecurityStatus("Beginning decryption of " + data.Length + " bytes");
            }
            byte[] salt = new byte[32];
            byte[] encryptedData = new byte[data.Length - 64];
            byte[] storedHash = new byte[32];
            System.Array.Copy(data, 0, salt, 0, 32);
            System.Array.Copy(data, 32, storedHash, 0, 32);
            System.Array.Copy(data, 64, encryptedData, 0, data.Length - 64);
            if (_securityDebugLogging)
            {
                _LogSecurityStatus("Salt extracted: " + BytesToHex(salt, 8) + "...");
                _LogSecurityStatus("Stored hash: " + BytesToHex(storedHash, 8) + "...");
                _LogSecurityStatus("Encrypted data length: " + encryptedData.Length + " bytes");
            }
            byte[] keyBase = Encoding.UTF8.GetBytes(_dataSignature);
            byte[] derivedKey = new byte[32];
            for (int i = 0; i < 32; i++)
                derivedKey[i] = (byte)(keyBase[i % keyBase.Length] ^ salt[i]);
            if (_securityDebugLogging) _LogSecurityStatus("Derived key: " + BytesToHex(derivedKey, 8) + "...");
            InitSBox(derivedKey);
            byte[] keystream = GenerateKeystream(encryptedData.Length);
            if (_securityDebugLogging) _LogSecurityStatus("Keystream: " + BytesToHex(keystream, 8) + "...");
            byte[] intermediate = new byte[encryptedData.Length];
            for (int i = 0; i < encryptedData.Length; i++)
                intermediate[i] = (byte)(encryptedData[i] ^ keystream[i]);
            if (_securityDebugLogging) _LogSecurityStatus("First layer XOR complete");
            byte[] decrypted = FeistelTransform(intermediate, derivedKey, false);
            if (_securityDebugLogging) _LogSecurityStatus("Feistel transform complete");
            byte[] computedHash = CustomHash(decrypted);
            bool hashMatch = true;
            for (int i = 0; i < 32; i++)
            {
                if (computedHash[i] != storedHash[i])
                {
                    hashMatch = false;
                    break;
                }
            }
            if (!hashMatch)
            {
                _LogError("Data integrity check failed - possible tampering detected");
                if (_securityDebugLogging)
                {
                    _LogSecurityStatus("Hash verification failed");
                    _LogSecurityStatus("Expected: " + BytesToHex(storedHash, 8) + "...");
                    _LogSecurityStatus("Computed: " + BytesToHex(computedHash, 8) + "...");
                }
                return "";
            }
            if (_securityDebugLogging)
            {
                _LogSecurityStatus("Hash verification successful");
                _LogSecurityStatus("Decryption complete, returning " + decrypted.Length + " bytes of plaintext");
            }
            return Encoding.UTF8.GetString(decrypted);
        }

        // Helper method to convert bytes to hex string for debugging
        private string BytesToHex(byte[] bytes, int maxLength = 0)
        {
            if (bytes == null || bytes.Length == 0)
                return "";
                
            int length = maxLength > 0 && maxLength < bytes.Length ? maxLength : bytes.Length;
            string hex = "";
            
            for (int i = 0; i < length; i++)
            {
                hex += bytes[i].ToString("X2");
            }
            
            return hex;
        }

        private bool _v(string data, out byte[] decoded)
        {
            decoded = null;
            if (!_enableSecureLoading) return true;
            
            if (string.IsNullOrEmpty(data)) return false;
            
            decoded = Convert.FromBase64String(data);
            return decoded.Length >= 96;
        }

        private bool _v(string data)
        {
            byte[] decoded;
            return _v(data, out decoded);
        }
        #endregion       



        //-------------------------Initialization Functions-------------------------//
        #region Initialization
        /// <summary>
        /// Allow <see cref="UdonSharpBehaviour"/> instances to be notified when the <see cref="OfficerAccountManager"/> has finished initializing.
        /// </summary>
        /// <param name="behaviour">The <see cref="UdonSharpBehaviour"/> to notify.</param>
        /// <param name="functionName">The name of the function to call when the <see cref="OfficerAccountManager"/> has finished initializing.</param>
        public void NotifyWhenInitialized(UdonSharpBehaviour behaviour, string functionName)
        {
            //If we are already initialized, send the event immediately
            if (isReady)
            {
                //Send it immediately
                behaviour.SendCustomEvent(functionName);
                return;
            }

            //Subscribe
            if (!onInitializedListeners.ContainsKey(behaviour))
            {
                onInitializedListeners.Add(behaviour, functionName);
            }
            //No duplicates pls thx
            else
            {
                _LogWarning(behaviour.name + " attempted to subscribe to the initialized event, but it was already subscribed", behaviour);
                return;
            }
        }

        /// <summary>
        /// Removes a <see cref="UdonSharpBehaviour"/> from the list of listeners that will be notified when the OfficerAccountManager is initialized.
        /// </summary>
        /// <param name="behaviour">The <see cref="UdonSharpBehaviour"/> to remove from the listener list.</param>
        public void RemoveListener(UdonSharpBehaviour behaviour)
        {
            if (onInitializedListeners.ContainsKey(behaviour))
            {
                onInitializedListeners.Remove(behaviour);
            }
            else
            {
                _LogWarning("Attempted to remove " + behaviour.name + " from the listener list, but it was not in the list", behaviour);
            }
        }

        /// <summary>
        /// Calls Initialize() on start. Initialize is basically <inheritdoc cref="OfficerAccountManager.Initialize"/>
        /// </summary>
        private void Start() => Initialize();
        /// <summary>
        /// Start but public. Safe to call multiple times if initialization fails.
        /// </summary>
        public void Initialize() {
            if (isInitializing)
            {
                _LogWarning("Initialize was called while the account manager is already initializing", this);
                return;
            }
            isInitializing = true;
            isReady = false;
            initializedSuccessfully = false;
            //Setup
            switch(desiredDataSource)
            {
                case DataSource.Editor:
                    //We are using offline data, it is already ready
                    _Log("Using offline data", this);
                    
                    // Apply decryption to offline data if security is enabled
                    if (_enableSecureLoading)
                    {
                        if (_securityDebugLogging)
                        {
                            _LogSecurityStatus("Applying decryption to offline data");
                        }
                        rawOfficerData = _t(rawOfficerData);
                    }
                    
                    currentDataSource = DataSource.Editor;
                    initializedSuccessfully = true;
                    DataReady();
                    break;

                case DataSource.Internet:
                    //Request data from the web server
                    _Log("Fetching officer data from " + _remoteDataURL.Get(), this);
                    // currentDataSource = DataSource.Local; //Set later depending on whether the request succeeds
                    stopwatch.Restart();
                    VRCStringDownloader.LoadUrl(_remoteDataURL, (VRC.Udon.Common.Interfaces.IUdonEventReceiver) this);
                    break;
            }
        }

        /// <summary>
        /// Called by VRChat after attempting to download officer data from the internet. Please do not call this function manually.
        /// See <see href="https://creators.vrchat.com/worlds/udon/string-loading/">the official VRChat documentation</see> for more information.
        /// </summary>
        /// <param name="result">The raw account data downloaded from the web server.</param>
        public override void OnStringLoadSuccess(IVRCStringDownload result)
        {
            stopwatch.Stop();
            
            string loadedData = result.Result;
            
            // Verify data integrity
            if (_enableSecureLoading && !_v(loadedData))
            {
                _LogError("Downloaded data failed integrity check - possible MITM attack");
                OnStringLoadError(result);
                return;
            }

            // Transform/decrypt the data
            rawOfficerData = _t(loadedData);
            
            if (string.IsNullOrEmpty(rawOfficerData))
            {
                _LogError("Data decryption failed - check security key");
                OnStringLoadError(result);
                return;
            }

            _Log("Officer data downloaded and processed successfully in " + stopwatch.ElapsedMilliseconds + " ms", this);
            currentDataSource = DataSource.Internet;
            initializedSuccessfully = true;
            DataReady();
        }

        /// <inheritdoc cref="OfficerAccountManager.OnStringLoadSuccess"/>
        public override void OnStringLoadError(IVRCStringDownload result)
        {
            //Use offline data
            _LogWarning("Failed to download officer data, using offline data", this);
            currentDataSource = DataSource.Editor;
            initializedSuccessfully = false;
            DataReady();
        }

        /// <summary>
        /// Callback method that is called when the data has been successfully retrieved and is ready to be parsed.
        /// </summary>
        private void DataReady()
        {
            //Parse whatever data is available
            if (performanceLogging) stopwatch.Restart();
            if (dataFormat == DataFormat.CSV)
            {
                //Parse the CSV file
                ParseCSV(rawOfficerData);
            }
            else if (dataFormat == DataFormat.JSON)
            {
                //Parse the JSON file
                ParseJSON(rawOfficerData);
            }

            if (performanceLogging) {
                stopwatch.Stop();
                _Log("Parsing took " + stopwatch.ElapsedMilliseconds + "ms", this);
            }

            // Log statistics about loaded data
            int officerCount = nameToRankDictionary.Count;
            DataList allRolesList = new DataList();
            
            // Gather unique roles from all officers
            DataList officerNames = nameToRankDictionary.GetKeys();
            for (int i = 0; i < officerNames.Count; i++)
            {
                DataToken officerRoles = nameToRankDictionary[officerNames[i]];
                DataList roleKeys = officerRoles.DataDictionary.GetKeys();
                
                for (int j = 0; j < roleKeys.Count; j++)
                {
                    string roleName = roleKeys[j].String;
                    if (!allRolesList.Contains(roleName))
                    {
                        allRolesList.Add(roleName);
                    }
                }
            }
            
            int uniqueRoleCount = allRolesList.Count;
            _Log(string.Format("Loaded {0} officers with {1} unique roles", officerCount, uniqueRoleCount), this);
            
            // Store role list for future use
            roleList = allRolesList;
            
            //Success
            isInitializing = false;
            isReady = true;

            //Notify subscribers
            DataList keys = onInitializedListeners.GetKeys();
            for (int i = 0; i < keys.Count; i++)
            {
                UdonSharpBehaviour receiver = (UdonSharpBehaviour)keys[i].Reference;
                string eventName = onInitializedListeners[keys[i]].ToString();
                receiver.SendCustomEvent(eventName);
            }

            // //----------Performance test----------//
            // _Log("Testing List generation performance");
            // _CreateRoleDict("Rank");

            // _Log("Testing Filtered List generation performance (Staff)");
            // DataDictionary staffList = _CreateFilteredRoleDict("Staff", Comparator.EqualTo, "True");
            // _Log("Staff List has " + staffList.Count + " entries");

            // _Log("Testing Filtered List generation performance (Recruit)");
            // DataDictionary recruitList = _CreateFilteredRoleDict("Rank", Comparator.EqualTo, "LPD Recruit");
            // float percent = (float) recruitList.Count / (float) nameToRankDictionary.Count;
            // _Log(recruitList.Count + " of " + nameToRankDictionary.Count + " officers are recruits!. That's " + percent.ToString("P1") + "!");

            // _Log("Testing Filtered List generation performance (Dev)");
            // DataDictionary devDict = _CreateFilteredRoleDict("Dev", Comparator.EqualTo, "True");
            // DataList devNames = devDict.GetKeys();
            // devNames.Sort();
            // string devNameList = "";
            // for (int i = 0; i < devNames.Count; i++) { devNameList += '\n' + devNames[i].String; }
            // _Log("The LPD devs are:" + devNameList);
        }

        /// <summary>
        /// Parse the CSV file
        /// </summary>
        /// <param name="csv">The CSV file to parse.</param>
        public void ParseCSV(string csv)
        {
            if (_securityDebugLogging)
            {
                _LogSecurityStatus("Parsing CSV data: " + csv.Length + " characters");
            }
            
            // Handle both Unix (\n) and Windows (\r\n) line endings
            string[] lines = csv.Replace("\r\n", "\n").Split(new char[] { '\n' }, StringSplitOptions.RemoveEmptyEntries);
            if (lines.Length == 0)
            {
                _LogError("CSV data is empty or invalid", this);
                return;
            }
            
            string[] roleNames = lines[0].Split(',');
            // Trim the role names to remove any whitespace
            for (int i = 0; i < roleNames.Length; i++)
            {
                roleNames[i] = roleNames[i].Trim();
            }
            
            if (_securityDebugLogging)
            {
                _LogSecurityStatus("CSV header contains " + roleNames.Length + " columns");
            }

            nameToRankDictionary = new DataDictionary();
            for (int i = 1; i < lines.Length; i++)
            {
                string[] values = lines[i].Split(',');
                if (values.Length == 0) continue;
                
                // Skip rows with insufficient data
                if (values.Length < 2)
                {
                    _LogWarning("Row " + i + " has insufficient data, skipping", this);
                    continue;
                }
                
                string name = values[0].Trim();
                DataDictionary officerRoles = new DataDictionary();
                for (int j = 1; j < values.Length && j < roleNames.Length; j++)
                {
                    string value = values[j].Trim();
                    //Skip entries which parse to "false"
                    if (bool.TryParse(value, out bool result) && result == false) {
                        //Do not add false entries
                    } else {
                        officerRoles.Add(roleNames[j], value);
                    }
                }
                
                // Only add officers with at least one role
                if (officerRoles.Count > 0)
                {
                    nameToRankDictionary.Add(name, officerRoles);
                }
                else
                {
                    _LogWarning("Officer '" + name + "' has no roles, skipping", this);
                }
            }

            if (_securityDebugLogging)
            {
                _LogSecurityStatus("CSV parse successful: " + nameToRankDictionary.Count + " entries");
            }
        }

        /// <summary>
        /// Parse the JSON file
        /// </summary>
        /// <param name="json">The JSON file to parse.</param>
        public void ParseJSON(string json)
        {
            if (_securityDebugLogging)
            {
                _LogSecurityStatus("Parsing JSON data: " + json.Length + " characters");
            }
            
            if(VRCJson.TryDeserializeFromJson(json, out DataToken result))
            {
                nameToRankDictionary = result.DataDictionary;
                
                if (_securityDebugLogging)
                {
                    _LogSecurityStatus("JSON parse successful: " + nameToRankDictionary.Count + " entries");
                }
            }
            else
            {
                _LogError("Failed to parse JSON data. Accounts are not be loaded but the script did not crash. You may try again with a different set of data.", this);
                if (_securityDebugLogging)
                {
                    _LogSecurityStatus("JSON parse failed");
                }
                isInitializing = false;
                isReady = false;
                initializedSuccessfully = false;
                return; //Give up
            }
        }
        #endregion


        //-------------------------Getters-------------------------//
        #region Getters
        /// <summary>
        /// Determines if the given player is an officer based on their display name.
        /// </summary>
        /// <param name="player">The player to check.</param>
        /// <param name="name">The display name of the officer.</param>
        /// <returns>True if the player is an officer, false otherwise.</returns>
        public bool _IsOfficer(string name)
        {
            return nameToRankDictionary.TryGetValue(name, out DataToken dont_care);
        }

        /// <inheritdoc cref="OfficerAccountManager._IsOfficer"/>
        public bool _IsOfficer(VRCPlayerApi player)
        {
            return _IsOfficer(player.displayName);
        }

        /// <inheritdoc cref="OfficerAccountManager._IsOfficer"/>
        public bool _IsLocalPlayerOfficer()
        {
            return _IsOfficer(Networking.LocalPlayer.displayName);
        }



        //Main function for getting values. Only returns DataTokens which can be converted to strings, bools, ints, and floats.
        /// <summary>
        /// Gets the data token for the specified player and role with an optional default value.
        /// See <see href="https://creators.vrchat.com/worlds/udon/data-containers/data-tokens/">the official VRChat documentation</see>
        /// for more information on the <see cref="DataToken"/> class.
        /// </summary>
        /// <param name="player">The player to get the data token for.</param>
        /// <param name="displayName">The display name of the officer.</param>
        /// <param name="role">The role associated with the data token.</param>
        /// <param name="defaultValue">The default value to return if no data token is found.</param>
        /// <returns>The data token for the specified player and role, or the default value if no data token is found.</returns>
        /// <inheritdoc cref="OfficerAccountManager._GetLocalPlayerToken"/>
        public DataToken _GetToken(string displayName, string role, DataToken defaultValue = new DataToken())
        {
            bool found = nameToRankDictionary.TryGetValue(displayName, out DataToken officerRoles);
            if (!found)
            {
                _LogWarning("Officer \"" + displayName + "\" not found", this);
                return DataError.KeyDoesNotExist;
            }

            found = officerRoles.DataDictionary.TryGetValue(role, out DataToken value);
            if (!found)
            {
                _LogWarning("Role \"" + role + "\" not found for officer \"" + displayName + "\"", this);
                return DataError.KeyDoesNotExist;
            }

            return value;
        }
        /// <inheritdoc cref="OfficerAccountManager._GetToken"/>
        public DataToken _GetToken(VRCPlayerApi player, string role, DataToken defaultValue = new DataToken()) => _GetToken(player.displayName, role, defaultValue);
        /// <inheritdoc cref="OfficerAccountManager._GetToken"/>
        public DataToken _GetLocalPlayerToken(string role, DataToken defaultValue = new DataToken()) => _GetToken(Networking.LocalPlayer.displayName, role, defaultValue);
        

        
        /// <summary>
        /// Attempts to retrieve the data token associated with the specified officer and role.
        /// See <see href="https://creators.vrchat.com/worlds/udon/data-containers/data-tokens/">the official VRChat documentation</see>
        /// for more information on the <see cref="DataToken"/> class.
        /// </summary>
        /// <param name="player">The player to get the data token for.</param>
        /// <param name="displayName">The display name of the officer.</param>
        /// <param name="role">The role of the officer.</param>
        /// <param name="value">The data token for the officer's display name and role.</param>
        /// <returns>True if the data token was found, false otherwise.</returns>
        public bool _TryGetToken(string displayName, string role, out DataToken value)
        {
            bool found = nameToRankDictionary.TryGetValue(displayName, out DataToken officerRoles);
            if (!found)
            {
                value = DataError.KeyDoesNotExist;
                return false;
            }

            found = officerRoles.DataDictionary.TryGetValue(role, out value);
            if (!found)
            {
                value = DataError.KeyDoesNotExist;
                return false;
            }

            return true;
        }
        /// <inheritdoc cref="OfficerAccountManager._TryGetToken"/>
        public bool _TryGetLocalPlayerToken(string role, out DataToken value) => _TryGetToken(Networking.LocalPlayer.displayName, role, out value);
        /// <inheritdoc cref="OfficerAccountManager._TryGetToken"/>
        public bool _TryGetToken(VRCPlayerApi player, string role, out DataToken value) => _TryGetToken(player.displayName, role, out value);



        /// <summary>
        /// Creates a dictionary of officer names to values for a given role name. Please do not call this function every frame.
        /// </summary>
        /// <param name="roleName">The name of the role to filter by.</param>
        /// <returns>A dictionary of officer names and their corresponding role values for the given role name.</returns>
        public DataDictionary _CreateRoleDict(string roleName)
        {
            if (performanceLogging) stopwatch.Restart();

            DataDictionary roleList = new DataDictionary();
            DataList keys = nameToRankDictionary.GetKeys();
            for (int i = 0; i < keys.Count; i++)
            {
                DataToken officerRoles = nameToRankDictionary[keys[i]];
                if (officerRoles.DataDictionary.TryGetValue(roleName, out DataToken value))
                {
                    roleList.Add(keys[i], value);
                }
            }

            if (performanceLogging) {
                stopwatch.Stop();
                _Log("Creating role list took " + stopwatch.ElapsedMilliseconds + "ms", this);
            }

            return roleList;
        }
        
        /// <summary>
        /// Creates a filtered list of officers whose value for a given role passes the specified test. Please do not call this function every frame.
        /// </summary>
        /// <param name="roleName">The name of the role to filter by.</param>
        /// <param name="comparator">The comparator to use for filtering.</param>
        /// <param name="token">The token to compare against.</param>
        /// <returns>A DataDictionary containing the filtered list of officers.</returns>
        public DataDictionary _CreateFilteredRoleDict(string roleName, Comparator comparator, DataToken token)
        {
            if (performanceLogging) stopwatch.Restart();

            //Dictionary enumerator
            // var userEnumerator = nameToRankDictionary.GetEnumerator();
            //Pretend dictionary enumerator
            DataList names = nameToRankDictionary.GetKeys();
            DataList rankDicts = nameToRankDictionary.GetValues();
            //Rank index
            int rankIndex = roleList.IndexOf(roleName);

            //Create the dictionary
            DataDictionary filteredDictionary = new DataDictionary();

            //I am so sorry for this abomination but it runs faster than checking each iteration
            //because UdonSharp compiles switch statements as if/elseif/elseif/elseif/elseif/elseif/else
            switch (comparator)
            {
                case Comparator.EqualTo:
                    for (int i = 0; i < names.Count; i++)
                    {
                        string name = names[i].String;
                        DataToken value = rankDicts[i].DataDictionary[roleName];
                        if (!value.IsNull && value.Equals(token)) filteredDictionary.Add(name, value);
                    }
                    break;
                case Comparator.NotEqualTo:
                    for (int i = 0; i < names.Count; i++)
                    {
                        string name = names[i].String;
                        DataToken value = rankDicts[i].DataDictionary[roleName];
                        if (!value.IsNull && !value.Equals(token)) filteredDictionary.Add(name, value);
                    }
                    break;
                case Comparator.GreaterThan:
                    for (int i = 0; i < names.Count; i++)
                    {
                        string name = names[i].String;
                        DataToken value = rankDicts[i].DataDictionary[roleName];
                        if (!value.IsNull && value.CompareTo(token) > 0) filteredDictionary.Add(name, value);
                    }
                    break;
                case Comparator.LessThan:
                    for (int i = 0; i < names.Count; i++)
                    {
                        string name = names[i].String;
                        DataToken value = rankDicts[i].DataDictionary[roleName];
                        if (!value.IsNull && value.CompareTo(token) < 0) filteredDictionary.Add(name, value);
                    }
                    break;
                case Comparator.GreaterThanOrEqualTo:
                    for (int i = 0; i < names.Count; i++)
                    {
                        string name = names[i].String;
                        DataToken value = rankDicts[i].DataDictionary[roleName];
                        if (!value.IsNull && value.CompareTo(token) >= 0) filteredDictionary.Add(name, value);
                    }
                    break;
                case Comparator.LessThanOrEqualTo:
                    for (int i = 0; i < names.Count; i++)
                    {
                        string name = names[i].String;
                        DataToken value = rankDicts[i].DataDictionary[roleName];
                        if (!value.IsNull && value.CompareTo(token) <= 0) filteredDictionary.Add(name, value);
                    }
                    break;
            }
            
            if (performanceLogging) {
                stopwatch.Stop();
                _Log("Creating filtered role list took " + stopwatch.ElapsedMilliseconds + "ms", this);
            }

            return filteredDictionary;
        }
        #endregion



        //-------------------------Backwards Compatibility and Convenience Functions-------------------------//
        #region BackwardsCompatibility&Convenience
        //Different flavors of the _Get function
        /* Default values if the officerID or roleIndex is out of range
         *   string: ""
         *   bool: false
         *   int: 0
         *   float: 0
         */

        //String
        /// <summary>See <see cref="OfficerAccountManager._GetToken"/>. Default value is "" if the officer or role doesn't exist.</summary>
        public string _GetString(string officerName, string roleName)
        {
            //No parsing needed
            return _GetToken(officerName, roleName, "").String;
        }
        /// <summary>See <see cref="OfficerAccountManager._GetToken"/>. Default value is "" if the officer or role doesn't exist.</summary>
        public string _GetString(string roleName) => _GetString(Networking.LocalPlayer.displayName, roleName);

        //Bool
        /// <summary>See <see cref="OfficerAccountManager._GetToken"/>. Default value is false if the officer or role doesn't exist.</summary>
        public bool _GetBool(string officerName, string roleName)
        {
            string value = _GetToken(officerName, roleName, bool.FalseString).String;
            bool success = bool.TryParse(value, out bool result);
            if (!success)
            {
                _LogWarning("Failed to parse bool for officer \"" + officerName + "\" and role \"" + roleName + "\"", this);
                return false;
            }
            return result;
        }
        /// <summary>See <see cref="OfficerAccountManager._GetToken"/>. Default value is false if the officer or role doesn't exist.</summary>
        public bool _GetBool(string roleName) => _GetBool(Networking.LocalPlayer.displayName, roleName);
        //Int
        /// <summary>See <see cref="OfficerAccountManager._GetToken"/>. Default value is 0 if the officer or role doesn't exist.</summary>
        public int _GetInt(string officerName, string roleName)
        {
            string value = _GetToken(officerName, roleName, "0").String;
            bool success = int.TryParse(value, out int result);
            if (!success)
            {
                _LogWarning("Failed to parse int for officer \"" + officerName + "\" and role \"" + roleName + "\"", this);
                return 0;
            }
            return result;
        }
        /// <summary>See <see cref="OfficerAccountManager._GetToken"/>. Default value is 0 if the officer or role doesn't exist.</summary>
        public int _GetInt(string roleName) => _GetInt(Networking.LocalPlayer.displayName, roleName);
        //Float
        /// <summary>See <see cref="OfficerAccountManager._GetToken"/>. Default value is 0 if the officer or role doesn't exist.</summary>
        public float _GetFloat(string officerName, string roleName) 
        {
            string value = _GetToken(officerName, roleName, "0").String;
            bool success = float.TryParse(value, out float result);
            if (!success)
            {
                _LogWarning("Failed to parse float for officer \"" + officerName + "\" and role \"" + roleName + "\"", this);
                return 0f;
            }
            return result;
        }
        /// <summary>See <see cref="OfficerAccountManager._GetToken"/>. Default value is 0 if the officer or role doesn't exist.</summary>
        public float _GetFloat(string roleName) => _GetFloat(Networking.LocalPlayer.displayName, roleName);
        #endregion



        //-------------------------Debug-------------------------//
        #region Debug
        //Logging because I am dumb and stupid and suffer to make things pretty
        internal void _Log(string value, UnityEngine.Object context = null) => _LogInternal(LogType.Log, value, context);
        internal void _LogWarning(string value, UnityEngine.Object context = null) => _LogInternal(LogType.Warning, value, context);
        internal void _LogError(string value, UnityEngine.Object context = null) => _LogInternal(LogType.Error, value, context);
        internal void _LogInternal(LogType type = LogType.Log, string value = "", UnityEngine.Object context = null)
        {
            Color32 color;
            switch (type) {
                case LogType.Warning:
                    color = Color.yellow;
                    break;
                case LogType.Error:
                    color = Color.red;
                    break;
                default:
                    color = Color.white;
                    break;
            }
            string hexColor = color.r.ToString("X2") + color.g.ToString("X2") + color.b.ToString("X2");

            if (context == null)
                UnityEngine.Debug.Log("<color=navy><b>Account Manager:</b></color> <color=#" + hexColor + ">" + value + "</color>");
            else
                UnityEngine.Debug.Log("<color=navy><b>Account Manager:</b></color> <color=#" + hexColor + ">" + value + "</color>", context);
        }

        // Security-specific logging
        internal void _LogSecurityStatus(string message)
        {
            // Only log security messages if both secure loading and debug logging are enabled
            if (_enableSecureLoading && _securityDebugLogging)
            {
                string hexColor = "00A5FF"; // Cyan-blue color for security logs
                Debug.Log("<color=navy><b>Account Manager Security:</b></color> <color=#" + hexColor + ">" + message + "</color>");
            }
        }
        #endregion
    }






    #if UNITY_EDITOR && !COMPILER_UDONSHARP
    /// <summary>
    /// Custom inspector for the <see cref="OfficerAccountManager"/> class.
    /// </summary>
    [CustomEditor(typeof(OfficerAccountManager))]
    public class OfficerAccountManagerEditor : Editor {

        private Texture2D HeaderBG;
        private Texture2D HeaderLogo;
        private Texture2D HeaderText;
        private Texture2D HeaderTape;



        private Texture2D twitterLogo;
        private Texture2D discordLogo;
        private Texture2D youtubeLogo;
        private Texture2D kofiLogo;

        private SerializedProperty performanceLogging;
        private SerializedProperty rawOfficerData;
        private SerializedProperty officerDataFile;
        private SerializedProperty _RemoteDataURL;
        private SerializedProperty desiredDataSource;
        private SerializedProperty dataFormat;
        private SerializedProperty _enableSecureLoading;
        private SerializedProperty _dataSignature;
        private SerializedProperty _securityDebugLogging;

        private void OnEnable() {
            performanceLogging = serializedObject.FindProperty("performanceLogging");
            rawOfficerData = serializedObject.FindProperty("rawOfficerData");
            officerDataFile = serializedObject.FindProperty("officerDataFile");
            _RemoteDataURL = serializedObject.FindProperty("_remoteDataURL");
            desiredDataSource = serializedObject.FindProperty("desiredDataSource");
            dataFormat = serializedObject.FindProperty("dataFormat");
            _enableSecureLoading = serializedObject.FindProperty("_enableSecureLoading");
            _dataSignature = serializedObject.FindProperty("_dataSignature");
            _securityDebugLogging = serializedObject.FindProperty("_securityDebugLogging");
        }

        public override void OnInspectorGUI() {
            serializedObject.Update();
            OfficerAccountManager manager = (OfficerAccountManager)target;

            // Try to load textures from package path first, then try relative paths, then fallback to blank textures if needed
            string packagePath = "Packages/com.localpolicedepartment.accountmanager";
            string relativePath = "Assets";
            
            HeaderBG = HeaderBG ?? TryLoadTexture(packagePath + "/Resources/Layers/BG.png", relativePath + "/Resources/Layers/BG.png");
            HeaderLogo = HeaderLogo ?? TryLoadTexture(packagePath + "/Resources/Layers/Logo.png", relativePath + "/Resources/Layers/Logo.png");
            HeaderText = HeaderText ?? TryLoadTexture(packagePath + "/Resources/Layers/Words.png", relativePath + "/Resources/Layers/Words.png");
            HeaderTape = HeaderTape ?? TryLoadTexture(packagePath + "/Resources/Layers/TapeL1000.png", relativePath + "/Resources/Layers/TapeL1000.png");

            twitterLogo = twitterLogo ?? TryLoadTexture(packagePath + "/Resources/SocialLogos/TwitterLogo.png", relativePath + "/Resources/SocialLogos/TwitterLogo.png", "d_BuildSettings.Twitter");
            discordLogo = discordLogo ?? TryLoadTexture(packagePath + "/Resources/SocialLogos/DiscordLogo.png", relativePath + "/Resources/SocialLogos/DiscordLogo.png", "d_UnityLogo");
            youtubeLogo = youtubeLogo ?? TryLoadTexture(packagePath + "/Resources/SocialLogos/YoutubeLogo.png", relativePath + "/Resources/SocialLogos/YoutubeLogo.png", "d_BuildSettings.Standalone");
            kofiLogo = kofiLogo ?? TryLoadTexture(packagePath + "/Resources/SocialLogos/KofiLogo.png", relativePath + "/Resources/SocialLogos/KofiLogo.png", "d_FilterByType");
            
            // Create fallback texture if needed for the background
            if (HeaderBG == null)
            {
                HeaderBG = new Texture2D(1, 1);
                HeaderBG.SetPixel(0, 0, new Color(0.2f, 0.2f, 0.2f, 1.0f));
                HeaderBG.Apply();
            }
            
            float imageAspectRatio = HeaderBG != null ? ((float)HeaderBG.width) / ((float)HeaderBG.height) : 4f;
            float screenAspectRatio = ((float)Screen.width) / ((float)128);
            
            // Helper method to load textures with fallbacks
            Texture2D TryLoadTexture(string primaryPath, string fallbackPath, string builtinIcon = null)
            {
                Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(primaryPath);
                
                if (texture == null)
                {
                    texture = AssetDatabase.LoadAssetAtPath<Texture2D>(fallbackPath);
                }
                
                if (texture == null && !string.IsNullOrEmpty(builtinIcon))
                {
                    texture = EditorGUIUtility.FindTexture(builtinIcon);
                }
                
                return texture;
            }

            bool horizontalStretch = imageAspectRatio < screenAspectRatio;
            GUI.DrawTexture(new Rect(0, 0, Screen.width, 128), HeaderBG, horizontalStretch ? ScaleMode.ScaleAndCrop : ScaleMode.StretchToFill);
            GUI.DrawTexture(new Rect(0, 0, horizontalStretch ? 512 : Screen.width, 128), HeaderLogo, ScaleMode.ScaleToFit);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, 128), HeaderText, ScaleMode.ScaleToFit);
            //Offset this to the right of the screen
            GUI.DrawTexture(new Rect(Screen.width - 256, 0, 512, 128), HeaderTape, ScaleMode.ScaleAndCrop);

            
            GUILayoutUtility.GetRect(Screen.width, 128);

            // base.OnInspectorGUI(); //We do it manually

            //Reused variabled
            bool online = manager.desiredDataSource == DataSource.Internet;
            bool hasOfflineData = manager.officerDataFile != null;


            //List statistics
            string rawData = manager.rawOfficerData;
            using (new GUILayout.HorizontalScope(EditorStyles.helpBox))
            {
                GUIStyle statisticStyle = EditorStyles.boldLabel;
                statisticStyle.alignment = TextAnchor.MiddleCenter;

                string dataFormat = manager.dataFormat == DataFormat.CSV ? "CSV" : "JSON";
                if (online)
                {
                    if (hasOfflineData)
                    {
                        EditorGUILayout.LabelField("Using online "+dataFormat+" data with offline backup", statisticStyle);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("Using online "+dataFormat+" data without offline backup", statisticStyle);
                    }
                }
                else
                {
                    if (hasOfflineData)
                    {
                        EditorGUILayout.LabelField("Using offline "+dataFormat+" data", statisticStyle);
                    }
                    else
                    {
                        EditorGUILayout.LabelField("No data file selected", statisticStyle);
                    }
                }
            }
            // //OFFLINE CSV STATISTICS
            // int rows = rawData.Count(c => c == '\n') + 1;
            // int columns = rawData.Substring(0, rawData.IndexOf('\n')).Count(c => c == ',') + 1;
            // EditorGUILayout.LabelField((online ? "Offline " : "") + "Officers: " + (rows - 1), statisticStyle);
            // EditorGUILayout.LabelField((online ? "Offline " : "") + "Roles: " + (columns - 1), statisticStyle);

            EditorGUILayout.Space();

            EditorGUI.BeginChangeCheck();
                EditorGUILayout.PropertyField(desiredDataSource, new GUIContent("Preferred Data Source", "The preferred location for fetching account data."));

                if (online)
                {
                    EditorGUILayout.PropertyField(_RemoteDataURL, new GUIContent("Remote Data URL", "The URL for online officer data. If users have Untrusted URLs disabled then only the following sources are valid:\n" +
                                                                                            "GitHub (*.github.io)\n" +
                                                                                            "Pastebin (pastebin.com)\n" +
                                                                                            "Github Gist (gist.githubusercontent.com)\n" +
                                                                                            "See the official VRChat documentation for more information."));
                }
                
                EditorGUI.BeginChangeCheck();
                    //Officer data file picker
                    EditorGUILayout.PropertyField(officerDataFile, new GUIContent((online ? "Offline " : "") + "Officer Data File", "The CSV or JSON file containing officer data."));
                    if (!hasOfflineData)
                    {
                        if (online)
                        {
                            //Warning
                            EditorGUILayout.HelpBox("No backup data file is selected. If the remote data URL fails to load the account manager will not function.", MessageType.Warning);
                        }
                        else
                        {
                            //Error
                            EditorGUILayout.HelpBox("Please select a CSV or JSON file containing officer data.", MessageType.Error);
                        }
                    }
                if (EditorGUI.EndChangeCheck()) {
                    serializedObject.ApplyModifiedProperties();
                    //Update the raw data
                    if (manager.officerDataFile != null) {
                        manager.rawOfficerData = manager.officerDataFile.text;
                    }
                    else
                    {
                        manager.rawOfficerData = "";
                    }
                }

                EditorGUILayout.PropertyField(dataFormat, new GUIContent("Data Format", "The expected format of the data for parsing."));
                EditorGUILayout.PropertyField(performanceLogging, new GUIContent("Performance Logging", "Enables performance logging for the account manager. This will print the time it takes to parse the data and generate lists to the console."));
            
                if (GUILayout.Button("Open Data Validator"))
                {
                    EditorApplication.ExecuteMenuItem("LPD/Account Manager/Data Format Validator");
                }

                if (GUILayout.Button("Open Offline Data Downloader"))
                {
                    EditorApplication.ExecuteMenuItem("LPD/Account Manager/Data Downloader");
                }

                EditorGUILayout.LabelField("Security Settings", EditorStyles.boldLabel);
                using (new GUILayout.VerticalScope(EditorStyles.helpBox))
                {
                    EditorGUILayout.PropertyField(_enableSecureLoading, new GUIContent("Enable Secure Loading", "Enable encryption and MITM protection for downloaded data"));
                    
                    if (_enableSecureLoading.boolValue)
                    {
                        // Use PasswordField to hide the actual key text with asterisks
                        string securityKey = _dataSignature.stringValue;
                        string maskedKey = EditorGUILayout.PasswordField(
                            new GUIContent("Security Key", "The key used to decrypt the data. Must match the key used to encrypt the data."), 
                            securityKey);

                        // Only update if the value changed
                        if (securityKey != maskedKey)
                        {
                            _dataSignature.stringValue = maskedKey;
                        }
                        
                        EditorGUILayout.PropertyField(_securityDebugLogging, new GUIContent("Security Debug Logging", "Enable detailed logging of encryption/decryption process (Editor only)"));
                        
                        if (_securityDebugLogging.boolValue)
                        {
                            EditorGUILayout.HelpBox("Security debug logging is enabled. This will log detailed information about the encryption/decryption process in the Editor console.", MessageType.Info);
                        }
                        
                        if (string.IsNullOrEmpty(_dataSignature.stringValue))
                        {
                            EditorGUILayout.HelpBox("Please enter a security key to decrypt the data", MessageType.Warning);
                        }
                        
                        if (GUILayout.Button("Open Data Security Tool"))
                        {
                            EditorApplication.ExecuteMenuItem("LPD/Account Manager/Data Security Tool");
                        }
                    }
                }

            if (EditorGUI.EndChangeCheck()) {
                serializedObject.ApplyModifiedProperties();
            }

            EditorGUILayout.Space();

            // using (new GUILayout.HorizontalScope())
            // {
            //     GUI.color = Color.white;
            //     GUI.backgroundColor = Color.white;
            //     GUIStyle socialLinkStyle = new GUIStyle(EditorStyles.miniButtonMid);
            //     socialLinkStyle.fixedHeight = 20;
            //
            //     GUI.backgroundColor = new Color(0.4509804f, 0.5411765f, 0.8588236f, 1f);
            //     if (GUILayout.Button(new GUIContent(discordLogo, "Discord"), socialLinkStyle, GUILayout.Width(Screen.width / 4))) Application.OpenURL("https://discord.gg/lpd");
            //     GUI.backgroundColor = new Color(0.1137255f, 0.6313726f, 0.9490196f, 1f);
            //     if (GUILayout.Button(new GUIContent(twitterLogo, "Twitter"), socialLinkStyle, GUILayout.Width(Screen.width / 4))) Application.OpenURL("https://twitter.com/LPD_vrchat");
            //     GUI.backgroundColor = new Color(0.8039216f, 0.1254902f, 0.1215686f, 1f);
            //     if (GUILayout.Button(new GUIContent(youtubeLogo, "Youtube"), socialLinkStyle, GUILayout.Width(Screen.width / 4))) Application.OpenURL("https://www.youtube.com/c/LoliPoliceDepartment");
            //     GUI.backgroundColor = new Color(1f, 0.3137255f, 0.3137255f, 1f);
            //     if (GUILayout.Button(new GUIContent(kofiLogo, "Ko-fi")     , socialLinkStyle, GUILayout.Width(Screen.width / 4))) Application.OpenURL("https://ko-fi.com/lolipolicedepartment");
            // }
        }

        [MenuItem("LPD/Account Manager")]
        public static void AddToSceneMenu()
        {
            bool exists = Component.FindObjectOfType<OfficerAccountManager>();
            if (exists)
            {
                //Select it, ping it, and return
                OfficerAccountManager amInstance = Component.FindObjectOfType<OfficerAccountManager>();
                Selection.activeObject = amInstance;
                EditorGUIUtility.PingObject(amInstance);
                return;
            }
            else
            {
                GameObject am = (GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath("Packages/com.localpolicedepartment.accountmanager/Officer Account Manager.prefab", typeof(GameObject)));
                PrefabUtility.UnpackPrefabInstance(am, PrefabUnpackMode.Completely, InteractionMode.AutomatedAction);
            }
        }
        
        [MenuItem("LPD/Account Manager/Data Format Validator", false, 101)]
        public static void OpenDataFormatValidator()
        {
            EditorWindow.GetWindow(System.Type.GetType("LocalPoliceDepartment.Utilities.AccountManager.Editor.DataFormatValidator, Assembly-CSharp-Editor"));
        }
    }
    #endif
}