using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.IO;

namespace LocalPoliceDepartment.Utilities.AccountManager
{
    public class DataSecurityUtility : EditorWindow
    {
        private TextAsset sourceFile;
        private string outputPath = "";
        private string securityKey = "";
        private byte[] _sBox;
        private const int N = 256;
        private bool _enableDetailedLogging = false;
        private Vector2 _logScrollPosition;
        private System.Collections.Generic.List<LogEntry> _logEntries = new System.Collections.Generic.List<LogEntry>();
        
        private class LogEntry
        {
            public string Message;
            public LogType Type;
            
            public LogEntry(string message, LogType type = LogType.Log)
            {
                Message = message;
                Type = type;
            }
        }
        
        [MenuItem("LPD/Account Manager/Data Security Tool")]
        static void ShowWindow()
        {
            GetWindow<DataSecurityUtility>("Data Security");
        }

        private void OnGUI()
        {
            GUILayout.Label("Account Manager Data Security", EditorStyles.boldLabel);
            
            EditorGUILayout.Space();
            
            // Input Section
            using (new GUILayout.VerticalScope(EditorStyles.helpBox))
            {
                GUILayout.Label("Data Source", EditorStyles.boldLabel);
                
                sourceFile = (TextAsset)EditorGUILayout.ObjectField("Source Data File", sourceFile, typeof(TextAsset), false);
                outputPath = EditorGUILayout.TextField("Output Path", outputPath);
                
                EditorGUILayout.Space();
                
                securityKey = EditorGUILayout.PasswordField("Security Key", securityKey);
                
                if (string.IsNullOrEmpty(securityKey))
                {
                    EditorGUILayout.HelpBox("Please enter a security key for encryption.", MessageType.Warning);
                }
                
                _enableDetailedLogging = EditorGUILayout.Toggle("Detailed Logging", _enableDetailedLogging);
            }
            
            EditorGUILayout.Space();
            
            // Encryption Button
            using (new GUILayout.HorizontalScope())
            {
                GUI.enabled = !string.IsNullOrEmpty(securityKey) && sourceFile != null;
                
                if (GUILayout.Button("Encrypt Data"))
                {
                    _logEntries.Clear();
                    Log("Starting encryption process...");
                    
                    if (string.IsNullOrEmpty(securityKey))
                    {
                        LogWarning("No security key provided. Encryption aborted.");
                        return;
                    }

                    if (sourceFile == null)
                    {
                        LogWarning("No source file selected. Encryption aborted.");
                        return;
                    }
                    
                    Log("Source file: " + sourceFile.name);
                    Log("File size: " + sourceFile.text.Length + " characters");
                    
                    string encrypted = EncryptData(sourceFile.text, securityKey);
                    
                    string finalPath = string.IsNullOrEmpty(outputPath) ? 
                        AssetDatabase.GetAssetPath(sourceFile).Replace(".txt", "_encrypted.txt").Replace(".json", "_encrypted.json") : 
                        outputPath;
                    
                    Log("Writing encrypted data to: " + finalPath);
                    System.IO.File.WriteAllText(finalPath, encrypted);
                    AssetDatabase.Refresh();
                    
                    Log("Encryption complete!", LogType.Log);
                    EditorUtility.DisplayDialog("Success", "Data encrypted and saved to: " + finalPath, "OK");
                }
                
                GUI.enabled = true;
            }
            
            EditorGUILayout.Space();
            
            // Instructions
            EditorGUILayout.HelpBox(
                "This tool helps encrypt your account data for additional security.\n\n" +
                "1. Select your source data file\n" +
                "2. Enter a security key (save this somewhere safe!)\n" +
                "3. Click 'Encrypt Data' to create the encrypted file\n\n" +
                "Note: Make sure to update the Account Manager's security key to match.",
                MessageType.Info);
            
            // Log Display
            if (_logEntries.Count > 0)
            {
                EditorGUILayout.Space();
                
                GUILayout.Label("Encryption Log", EditorStyles.boldLabel);
                
                using (new GUILayout.VerticalScope(EditorStyles.helpBox, GUILayout.Height(150)))
                {
                    _logScrollPosition = EditorGUILayout.BeginScrollView(_logScrollPosition);
                    
                    foreach (var entry in _logEntries)
                    {
                        Color color = Color.white;
                        
                        switch (entry.Type)
                        {
                            case LogType.Warning:
                                color = Color.yellow;
                                break;
                            case LogType.Error:
                                color = Color.red;
                                break;
                            case LogType.Log:
                            default:
                                color = Color.white;
                                break;
                        }
                        
                        GUI.color = color;
                        EditorGUILayout.LabelField(entry.Message, EditorStyles.wordWrappedLabel);
                    }
                    
                    GUI.color = Color.white;
                    EditorGUILayout.EndScrollView();
                }
                
                if (GUILayout.Button("Clear Log"))
                {
                    _logEntries.Clear();
                }
            }
        }

        private void Log(string message, LogType type = LogType.Log)
        {
            _logEntries.Add(new LogEntry(message, type));
            
            if (_enableDetailedLogging)
            {
                switch (type)
                {
                    case LogType.Warning:
                        Debug.LogWarning("[Data Security] " + message);
                        break;
                    case LogType.Error:
                        Debug.LogError("[Data Security] " + message);
                        break;
                    default:
                        Debug.Log("[Data Security] " + message);
                        break;
                }
            }
            
            // Force repaint to show log entries immediately
            Repaint();
        }
        
        private void LogWarning(string message) => Log(message, LogType.Warning);
        private void LogError(string message) => Log(message, LogType.Error);

        private void InitSBox(byte[] key)
        {
            if (_sBox == null)
            {
                _sBox = new byte[N];
            }
            
            for (int i = 0; i < N; i++)
            {
                _sBox[i] = (byte)i;
            }
            
            int j = 0;
            for (int i = 0; i < N; i++)
            {
                j = (j + _sBox[i] + key[i % key.Length]) % N;
                byte temp = _sBox[i];
                _sBox[i] = _sBox[j];
                _sBox[j] = temp;
            }
            
            if (_enableDetailedLogging)
            {
                Log("S-Box initialized with key of length: " + key.Length);
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
                
                byte temp = _sBox[i];
                _sBox[i] = _sBox[j];
                _sBox[j] = temp;
                
                int t = (_sBox[i] + _sBox[j]) % N;
                keystream[k] = _sBox[t];
            }
            
            if (_enableDetailedLogging)
            {
                Log("Generated keystream of length: " + length);
                if (length > 0)
                {
                    Log("Keystream preview: " + BytesToHex(keystream, 8) + "...");
                }
            }
            
            return keystream;
        }

        private byte[] FeistelTransform(byte[] data, byte[] key, bool encrypt)
        {
            int rounds = 16;
            int blockSize = 8;
            int length = data.Length - (data.Length % blockSize);
            byte[] result = new byte[length];
            
            if (_enableDetailedLogging)
            {
                Log("Starting Feistel network transform (" + (encrypt ? "encrypt" : "decrypt") + ")");
                Log("Data length: " + data.Length + ", Aligned length: " + length);
                Log("Block size: " + blockSize + ", Rounds: " + rounds);
            }
            
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
                        byte substituted = _sBox[temp];
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
            
            if (_enableDetailedLogging)
            {
                Log("Feistel transform complete");
            }
            
            return result;
        }

        private byte[] CustomHash(byte[] data)
        {
            if (_enableDetailedLogging)
            {
                Log("Generating hash for data of length: " + data.Length);
            }
            
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
            
            if (_enableDetailedLogging)
            {
                Log("Hash generated: " + BytesToHex(hash, 8) + "...");
            }
            
            return hash;
        }

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

        private string EncryptData(string data, string key)
        {
            if (_enableDetailedLogging)
            {
                Log("Starting encryption with key: " + new string('*', key.Length));
                Log("Data length: " + data.Length + " characters");
            }
            
            // Generate a random salt
            byte[] salt = new byte[32];
            for(int i = 0; i < salt.Length; i++)
            {
                salt[i] = (byte)UnityEngine.Random.Range(0, 256);
            }
            
            if (_enableDetailedLogging)
            {
                Log("Generated random salt: " + BytesToHex(salt, 8) + "...");
            }
            
            byte[] dataBytes = Encoding.UTF8.GetBytes(data);
            byte[] keyBase = Encoding.UTF8.GetBytes(key);
            byte[] derivedKey = new byte[32];
            
            for (int i = 0; i < 32; i++)
            {
                derivedKey[i] = (byte)(keyBase[i % keyBase.Length] ^ salt[i]);
            }
            
            if (_enableDetailedLogging)
            {
                Log("Derived key from salt: " + BytesToHex(derivedKey, 8) + "...");
            }
            
            InitSBox(derivedKey);
            byte[] keystream = GenerateKeystream(dataBytes.Length);
            
            // First layer: XOR with keystream
            byte[] intermediate = new byte[dataBytes.Length];
            for (int i = 0; i < dataBytes.Length; i++)
            {
                intermediate[i] = (byte)(dataBytes[i] ^ keystream[i]);
            }
            
            if (_enableDetailedLogging)
            {
                Log("First layer XOR complete");
            }
            
            // Second layer: Feistel network
            byte[] encrypted = FeistelTransform(intermediate, derivedKey, true);
            
            // Generate hash for integrity verification
            byte[] hash = CustomHash(dataBytes);
            
            if (_enableDetailedLogging)
            {
                Log("Combining salt, hash, and encrypted data");
                Log("Salt length: " + salt.Length);
                Log("Hash length: " + hash.Length);
                Log("Encrypted data length: " + encrypted.Length);
            }
            
            // Combine salt + hash + encrypted data
            byte[] result = new byte[32 + 32 + encrypted.Length];
            Array.Copy(salt, 0, result, 0, 32);
            Array.Copy(hash, 0, result, 32, 32);
            Array.Copy(encrypted, 0, result, 64, encrypted.Length);
            
            string base64 = Convert.ToBase64String(result);
            
            if (_enableDetailedLogging)
            {
                Log("Final encrypted payload size: " + result.Length + " bytes");
                Log("Base64 encoded length: " + base64.Length + " characters");
            }
            
            return base64;
        }
    }
}