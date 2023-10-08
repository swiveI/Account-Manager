
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using LoliPoliceDepartment.Utilities.AccountManager;

namespace LoliPoliceDepartment.Examples
{
    public class RestrictionIndicator : UdonSharpBehaviour
    {
        //For notes on the Account Manager, see OfficerAccountManager.cs line 25
        public OfficerAccountManager accountManager; //Our reference to the account manager
        [Space]
        public string roleName = "Staff"; //Role to look up
        [Space]
        public UnityEngine.UI.Text text; //The text object to display the allowed state
        public string allowedText = "Allowed";
        public string deniedText = "Denied";
        [Space]
        public MeshRenderer meshRenderer; //The mesh renderer to display the allowed state
        public Color allowedColor = Color.green;
        public Color deniedColor = Color.red;

        //Automatically find the account manager in the scene if it's not assigned
        #if !COMPILER_UDONSHARP && UNITY_EDITOR
            private void OnValidate() {
                if (accountManager == null) accountManager = (OfficerAccountManager) FindObjectOfType(typeof(OfficerAccountManager));
            }
        #endif
        
        private void Start() {
            //Display true or false based on the player's role
            bool allowed = accountManager._GetBool(roleName); //Note: The default value is "false" if the officer or role doesn't exist
            if (text != null) text.text = allowed ? allowedText : deniedText;
            if (meshRenderer != null) 
            {
                MaterialPropertyBlock block = new MaterialPropertyBlock();
                meshRenderer.GetPropertyBlock(block);
                block.SetColor("_Color", allowed ? allowedColor : deniedColor);
                meshRenderer.SetPropertyBlock(block);
            }
        }
    }
}