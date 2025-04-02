using UnityEngine;
using UnityEditor;
using System;
using System.Text;
using System.Security.Cryptography;
using System.IO;

namespace LocalPoliceDepartment.Utilities.AccountManager
{
    public class DataSecurityUtility : EditorWindow
    {
        private TextAsset sourceFile;
        private string outputPath = "";
        private string securityKey = "";
        private bool useRandomizedSalt = true;
        private string customSalt = "";
        
        [MenuItem("LPD/Account Manager/Data Security Tool")]
        static void ShowWindow()
        {
            GetWindow<DataSecurityUtility>("Data Security");
        }

        private void OnGUI()
        {
            GUILayout.Label("Account Manager Data Security", EditorStyles.boldLabel);
            
            sourceFile = (TextAsset)EditorGUILayout.ObjectField("Source Data File", sourceFile, typeof(TextAsset), false);
            outputPath = EditorGUILayout.TextField("Output Path", outputPath);
            
            EditorGUILayout.Space();
            
            securityKey = EditorGUILayout.PasswordField("Security Key", securityKey);
            useRandomizedSalt = EditorGUILayout.Toggle("Use Random Salt", useRandomizedSalt);
            
            if (!useRandomizedSalt)
            {
                customSalt = EditorGUILayout.TextField("Custom Salt", customSalt);
            }

            EditorGUILayout.Space();

            if (GUILayout.Button("Encrypt Data"))
            {
                if (string.IsNullOrEmpty(securityKey))
                {
                    EditorUtility.DisplayDialog("Error", "Please enter a security key", "OK");
                    return;
                }

                if (sourceFile == null)
                {
                    EditorUtility.DisplayDialog("Error", "Please select a source file", "OK");
                    return;
                }

                string salt = useRandomizedSalt ? GenerateRandomSalt() : customSalt;
                string encrypted = EncryptData(sourceFile.text, securityKey, salt);
                
                string finalPath = string.IsNullOrEmpty(outputPath) ? 
                    AssetDatabase.GetAssetPath(sourceFile).Replace(".txt", "_encrypted.txt").Replace(".json", "_encrypted.json") : 
                    outputPath;
                
                File.WriteAllText(finalPath, encrypted);
                AssetDatabase.Refresh();
                
                EditorUtility.DisplayDialog("Success", "Data encrypted and saved to: " + finalPath, "OK");
            }

            EditorGUILayout.Space();
            EditorGUILayout.HelpBox(
                "This tool helps encrypt your account data for additional security.\n\n" +
                "1. Select your source data file (CSV or JSON)\n" +
                "2. Enter a security key (save this somewhere safe!)\n" +
                "3. Choose whether to use a random salt or specify your own\n" +
                "4. Click 'Encrypt Data' to create the encrypted file\n\n" +
                "Note: Make sure to update the Account Manager to use the encrypted file and security key.",
                MessageType.Info);
        }

        private string GenerateRandomSalt(int length = 32)
        {
            byte[] salt = new byte[length];
            using (var rng = new RNGCryptoServiceProvider())
            {
                rng.GetBytes(salt);
            }
            return Convert.ToBase64String(salt);
        }

        private string EncryptData(string data, string key, string salt)
        {
            try
            {
                byte[] saltBytes = Encoding.UTF8.GetBytes(salt);
                byte[] keyBytes = new Rfc2898DeriveBytes(key, saltBytes, 1000).GetBytes(32);
                
                using (var aes = Aes.Create())
                {
                    aes.KeySize = 256;
                    aes.BlockSize = 128;
                    aes.Mode = CipherMode.CBC;
                    aes.Key = keyBytes;
                    aes.GenerateIV();

                    using (var msEncrypt = new MemoryStream())
                    {
                        // Write the salt and IV first
                        msEncrypt.Write(saltBytes, 0, saltBytes.Length);
                        msEncrypt.Write(aes.IV, 0, aes.IV.Length);

                        using (var encryptor = aes.CreateEncryptor())
                        using (var csEncrypt = new CryptoStream(msEncrypt, encryptor, CryptoStreamMode.Write))
                        using (var swEncrypt = new StreamWriter(csEncrypt))
                        {
                            swEncrypt.Write(data);
                        }

                        byte[] encryptedData = msEncrypt.ToArray();
                        return Convert.ToBase64String(encryptedData);
                    }
                }
            }
            catch (Exception ex)
            {
                EditorUtility.DisplayDialog("Encryption Error", "Failed to encrypt data: " + ex.Message, "OK");
                return null;
            }
        }
    }
}