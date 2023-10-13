
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using LoliPoliceDepartment.Utilities.AccountManager;

namespace LoliPoliceDepartment.Examples
{
    public class RestrictedInteractible : UdonSharpBehaviour
    {
        //For notes on the Account Manager, see OfficerAccountManager.cs line 25
        public OfficerAccountManager accountManager; //Our reference to the account manager
        [Space]
        public string roleName = "Staff"; //Role to look up

        //Automatically find the account manager in the scene if it's not assigned
        #if !COMPILER_UDONSHARP && UNITY_EDITOR
            private void OnValidate() {
                if (accountManager == null) accountManager = (OfficerAccountManager) FindObjectOfType(typeof(OfficerAccountManager));
            }
        #endif
        
        private void Start() {
            //Wait for the account manager to be ready
            accountManager.NotifyWhenInitialized(this, nameof(AccountManagerReady));
        }

        public void AccountManagerReady()
        {
            //Disable the interactible if the current player doesn't have the required role
            bool allowed = accountManager._GetBool(roleName); //Note: The default value is "false" if the officer or role doesn't exist
            if (!allowed)
            {
                //Just disable all the colliders
                Collider[] colliders = GetComponentsInChildren<Collider>();
                foreach (Collider collider in colliders) collider.enabled = false;
            }
        }
    }
}