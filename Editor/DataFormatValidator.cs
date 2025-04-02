using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text;
using System.IO;
using VRC.SDK3.Data;
using LocalPoliceDepartment.Utilities.AccountManager;

namespace LocalPoliceDepartment.Utilities.AccountManager.Editor
{
    public class DataFormatValidator : EditorWindow
    {
        private string inputData = "";
        private TextAsset dataFile;
        private DataFormat dataFormat = DataFormat.CSV;
        private Vector2 scrollPosition;
        private Vector2 resultScrollPosition;
        private bool showResults = false;
        private string validationResults = "";
        private bool isValid = false;
        private bool useTextInput = false;
        private Color validColor = new Color(0.3f, 0.8f, 0.3f);
        private Color invalidColor = new Color(0.8f, 0.3f, 0.3f);
        private Color warningColor = new Color(0.9f, 0.7f, 0.2f);

        [MenuItem("LPD/Account Manager/Data Format Validator")]
        public static void ShowWindow()
        {
            DataFormatValidator window = GetWindow<DataFormatValidator>("Data Format Validator");
            window.minSize = new Vector2(450, 400);
            window.Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Officer Account Manager - Data Validator", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Use this tool to verify if your CSV or JSON data will parse correctly in the OfficerAccountManager.", MessageType.Info);
            
            EditorGUILayout.Space();

            // Format selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Data Format", GUILayout.Width(100));
            dataFormat = (DataFormat)EditorGUILayout.EnumPopup(dataFormat);
            EditorGUILayout.EndHorizontal();
            
            // Input method selection
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Input Method", GUILayout.Width(100));
            bool newUseTextInput = EditorGUILayout.ToggleLeft("Text Input", useTextInput);
            EditorGUILayout.Space();
            bool fileInputSelected = EditorGUILayout.ToggleLeft("File Input", !useTextInput);
            
            if (newUseTextInput != useTextInput)
            {
                useTextInput = newUseTextInput;
            }
            else if (fileInputSelected != !useTextInput)
            {
                useTextInput = !fileInputSelected;
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space();
            
            if (useTextInput)
            {
                // Text input
                EditorGUILayout.LabelField("Input Data:");
                EditorStyles.textArea.wordWrap = true;
                scrollPosition = EditorGUILayout.BeginScrollView(scrollPosition, GUILayout.Height(200));
                inputData = EditorGUILayout.TextArea(inputData, EditorStyles.textArea, GUILayout.ExpandHeight(true));
                EditorGUILayout.EndScrollView();
            }
            else
            {
                // File input
                EditorGUILayout.BeginHorizontal();
                EditorGUI.BeginChangeCheck();
                dataFile = EditorGUILayout.ObjectField("Data File", dataFile, typeof(TextAsset), false) as TextAsset;
                if (EditorGUI.EndChangeCheck() && dataFile != null)
                {
                    inputData = dataFile.text;
                }
                EditorGUILayout.EndHorizontal();
                
                if (dataFile != null)
                {
                    EditorGUILayout.LabelField($"File Size: {dataFile.text.Length} characters");
                }
            }

            EditorGUILayout.Space();

            GUI.enabled = !string.IsNullOrEmpty(inputData);
            if (GUILayout.Button("Validate Data", GUILayout.Height(30)))
            {
                ValidateData();
                showResults = true;
            }
            GUI.enabled = true;

            if (showResults)
            {
                EditorGUILayout.Space();
                EditorGUILayout.LabelField("Validation Results:", EditorStyles.boldLabel);
                
                // Show validation status with color
                GUIStyle statusStyle = new GUIStyle(EditorStyles.boldLabel);
                statusStyle.alignment = TextAnchor.MiddleCenter;
                EditorGUILayout.BeginVertical(EditorStyles.helpBox);
                statusStyle.normal.textColor = isValid ? validColor : invalidColor;
                EditorGUILayout.LabelField(isValid ? "VALID" : "INVALID", statusStyle, GUILayout.Height(25));
                EditorGUILayout.EndVertical();
                
                // Show detailed results
                GUIStyle resultStyle = new GUIStyle(EditorStyles.textArea);
                resultStyle.wordWrap = true;
                resultStyle.richText = true;
                resultScrollPosition = EditorGUILayout.BeginScrollView(resultScrollPosition, GUILayout.Height(200));
                EditorGUILayout.TextArea(validationResults, resultStyle);
                EditorGUILayout.EndScrollView();
            }
        }

        private void ValidateData()
        {
            if (string.IsNullOrEmpty(inputData))
            {
                validationResults = "No data to validate.";
                isValid = false;
                return;
            }
            
            StringBuilder resultBuilder = new StringBuilder();
            
            if (dataFormat == DataFormat.CSV)
            {
                ValidateCSV(inputData, resultBuilder);
            }
            else
            {
                ValidateJSON(inputData, resultBuilder);
            }
            
            validationResults = resultBuilder.ToString();
        }

        private void ValidateCSV(string csvData, StringBuilder resultBuilder)
        {
            try
            {
                // Handle both Unix (\n) and Windows (\r\n) line endings
                string[] lines = csvData.Replace("\r\n", "\n").Split(new char[] { '\n' }, System.StringSplitOptions.RemoveEmptyEntries);
                
                if (lines.Length == 0)
                {
                    resultBuilder.AppendLine("<color=#FF4500>CSV data is empty or invalid.</color>");
                    isValid = false;
                    return;
                }
                
                string[] headers = lines[0].Split(',');
                
                // Trim the headers to remove any whitespace
                for (int i = 0; i < headers.Length; i++)
                {
                    headers[i] = headers[i].Trim();
                }
                
                resultBuilder.AppendLine($"<color=#00A5FF>Found {headers.Length} columns in header:</color>");
                resultBuilder.AppendLine(string.Join(", ", headers));
                resultBuilder.AppendLine();
                
                if (headers.Length < 2)
                {
                    resultBuilder.AppendLine("<color=#FF4500>Warning: CSV should have at least 2 columns (name + at least one role).</color>");
                }
                
                // Analyze data rows
                int validRows = 0;
                int invalidRows = 0;
                int emptyRoleOfficers = 0;
                List<int> problemRows = new List<int>();
                
                // This simulates the actual parsing behavior of OfficerAccountManager
                DataDictionary nameToRankDictionary = new DataDictionary();
                
                for (int i = 1; i < lines.Length; i++)
                {
                    string[] values = lines[i].Split(',');
                    
                    if (values.Length == 0 || string.IsNullOrEmpty(values[0].Trim()))
                    {
                        invalidRows++;
                        problemRows.Add(i);
                        continue;
                    }
                    
                    string name = values[0].Trim();
                    DataDictionary officerRoles = new DataDictionary();
                    for (int j = 1; j < values.Length && j < headers.Length; j++)
                    {
                        string value = values[j].Trim();
                        bool skipEntry = false;
                        
                        // Match OfficerAccountManager's behavior of skipping false boolean entries
                        if (bool.TryParse(value, out bool boolResult) && boolResult == false)
                        {
                            skipEntry = true;
                        }
                        
                        // Add non-false values to the officer's roles
                        if (!skipEntry && !string.IsNullOrEmpty(value))
                        {
                            officerRoles.Add(headers[j], value);
                        }
                    }
                    
                    // Check if officer has any roles (same as OfficerAccountManager)
                    if (officerRoles.Count > 0)
                    {
                        nameToRankDictionary.Add(name, officerRoles);
                        validRows++;
                    }
                    else
                    {
                        emptyRoleOfficers++;
                        resultBuilder.AppendLine($"<color=#FF9900>Warning: Officer '{name}' has no roles, would be skipped.</color>");
                    }
                    
                    if (values.Length < headers.Length)
                    {
                        resultBuilder.AppendLine($"<color=#FF9900>Warning: Row {i} has fewer columns ({values.Length}) than the header ({headers.Length}).</color>");
                    }
                }
                
                // Count actual officers that would be loaded (match OfficerAccountManager behavior)
                resultBuilder.AppendLine($"<color=#00A5FF>Found {lines.Length-1} total rows of data</color>");
                resultBuilder.AppendLine($"<color=#00A5FF>After processing: {validRows} valid officers, {invalidRows} invalid rows, {emptyRoleOfficers} officers with no roles</color>");
                
                if (invalidRows > 0)
                {
                    resultBuilder.AppendLine($"<color=#FF9900>Problem rows: {string.Join(", ", problemRows)}</color>");
                }
                
                // Test other value types for common parsing issues
                bool foundParsingWarnings = TestValueParsing(nameToRankDictionary, resultBuilder);
                
                // Changed to not consider parsing warnings as validation failures
                isValid = invalidRows == 0 && validRows > 0;
                
                // Show a note about warnings if any were found
                if (foundParsingWarnings && isValid)
                {
                    resultBuilder.AppendLine("\n<color=#FF9900>Note: Parsing warnings were found but don't affect validation status</color>");
                }
            }
            catch (System.Exception e)
            {
                resultBuilder.AppendLine($"<color=#FF4500>Error validating CSV: {e.Message}</color>");
                isValid = false;
            }
        }

        private void ValidateJSON(string jsonData, StringBuilder resultBuilder)
        {
            try
            {
                // Try to parse using VRCJson
                if (VRCJson.TryDeserializeFromJson(jsonData, out DataToken result))
                {
                    DataDictionary dict = result.DataDictionary;
                    int officerCount = dict.Count;
                    
                    resultBuilder.AppendLine($"<color=#00A5FF>Successfully parsed JSON with {officerCount} officers</color>");
                    
                    // Validate structure - each entry should be a dictionary of roles
                    DataList keys = dict.GetKeys();
                    int validEntries = 0;
                    int emptyRoleOfficers = 0;
                    List<string> invalidEntries = new List<string>();
                    HashSet<string> allRoles = new HashSet<string>();
                    
                    for (int i = 0; i < keys.Count; i++)
                    {
                        string officerName = keys[i].String;
                        DataToken officerData = dict[keys[i]];
                        
                        if (officerData.TokenType != TokenType.DataDictionary)
                        {
                            invalidEntries.Add(officerName);
                            continue;
                        }
                        
                        DataDictionary roleDict = officerData.DataDictionary;
                        if (roleDict.Count == 0)
                        {
                            emptyRoleOfficers++;
                            resultBuilder.AppendLine($"<color=#FF9900>Warning: Officer '{officerName}' has no roles, would be skipped</color>");
                            continue;
                        }
                        
                        DataList roleKeys = roleDict.GetKeys();
                        for (int j = 0; j < roleKeys.Count; j++)
                        {
                            allRoles.Add(roleKeys[j].String);
                        }
                        
                        validEntries++;
                    }
                    
                    resultBuilder.AppendLine($"<color=#00A5FF>Found {validEntries} valid officer entries, {emptyRoleOfficers} officers with no roles</color>");
                    resultBuilder.AppendLine($"<color=#00A5FF>Found {allRoles.Count} unique roles:</color> {string.Join(", ", allRoles)}");
                    
                    if (invalidEntries.Count > 0)
                    {
                        resultBuilder.AppendLine($"<color=#FF9900>{invalidEntries.Count} invalid entries: {string.Join(", ", invalidEntries)}</color>");
                    }
                    
                    // Test other value types for common parsing issues
                    bool foundParsingWarnings = TestValueParsing(dict, resultBuilder);
                    
                    // Changed to not consider parsing warnings as validation failures
                    isValid = invalidEntries.Count == 0 && validEntries > 0;
                    
                    // Show a note about warnings if any were found
                    if (foundParsingWarnings && isValid)
                    {
                        resultBuilder.AppendLine("\n<color=#FF9900>Note: Parsing warnings were found but don't affect validation status</color>");
                    }
                }
                else
                {
                    resultBuilder.AppendLine("<color=#FF4500>Failed to parse JSON. The data is not valid JSON or does not match expected format.</color>");
                    isValid = false;
                }
            }
            catch (System.Exception e)
            {
                resultBuilder.AppendLine($"<color=#FF4500>Error validating JSON: {e.Message}</color>");
                isValid = false;
            }
        }

        // New method to test value parsing - matches OfficerAccountManager's GetString, GetBool, etc. checks
        private bool TestValueParsing(DataDictionary officerDict, StringBuilder resultBuilder)
        {
            bool foundWarnings = false;
            DataList keys = officerDict.GetKeys();
            
            resultBuilder.AppendLine("\n<color=#00A5FF>Testing value parsing:</color>");
            
            // Common role names to check
            List<string> parsingWarnings = new List<string>();
            
            for (int i = 0; i < keys.Count; i++)
            {
                string officerName = keys[i].String;
                DataToken officerData = officerDict[keys[i]];
                
                if (officerData.TokenType != TokenType.DataDictionary)
                    continue;
                    
                DataDictionary roleDict = officerData.DataDictionary;
                DataList roleKeys = roleDict.GetKeys();
                
                // Test boolean parsing (for first few officers only, to avoid spam)
                if (i < 5) {
                    for (int j = 0; j < roleKeys.Count; j++) {
                        string roleName = roleKeys[j].String;
                        string valueStr = roleDict[roleName].ToString();
                        
                        // Test boolean parsing
                        if (valueStr.ToLower() == "true" || valueStr.ToLower() == "false") {
                            // Correctly formatted boolean, skip
                        } else if (valueStr == "1" || valueStr == "0") {
                            parsingWarnings.Add($"Officer '{officerName}', Role '{roleName}': Value '{valueStr}' might be intended as boolean but uses numbers");
                            foundWarnings = true;
                        } else if (valueStr.ToLower() == "yes" || valueStr.ToLower() == "no") {
                            parsingWarnings.Add($"Officer '{officerName}', Role '{roleName}': Value '{valueStr}' might be intended as boolean but uses yes/no");
                            foundWarnings = true;
                        }
                        
                        // Test number parsing
                        if (roleKeys[j].String.ToLower().Contains("level") || 
                            roleKeys[j].String.ToLower().Contains("score") || 
                            roleKeys[j].String.ToLower().Contains("rank"))
                        {
                            bool isNumber = int.TryParse(valueStr, out _) || float.TryParse(valueStr, out _);
                            if (!isNumber) {
                                parsingWarnings.Add($"Officer '{officerName}', Role '{roleName}': Value '{valueStr}' might be intended as number but isn't parseable");
                                foundWarnings = true;
                            }
                        }
                    }
                }
            }
            
            if (parsingWarnings.Count > 0) {
                resultBuilder.AppendLine("<color=#FF9900>Value parsing warnings found:</color>");
                for (int i = 0; i < Mathf.Min(10, parsingWarnings.Count); i++) {
                    resultBuilder.AppendLine($"<color=#FF9900>• {parsingWarnings[i]}</color>");
                }
                
                if (parsingWarnings.Count > 10) {
                    resultBuilder.AppendLine($"<color=#FF9900>...and {parsingWarnings.Count - 10} more warnings</color>");
                }
            } else {
                resultBuilder.AppendLine("<color=#00A5FF>No value parsing issues detected</color>");
            }
            
            return foundWarnings;
        }
    }
}
