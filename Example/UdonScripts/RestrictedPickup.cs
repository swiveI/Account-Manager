﻿
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;
using LocalPoliceDepartment.Utilities.AccountManager;

namespace LocalPoliceDepartment.Examples
{
    public class RestrictedPickup : UdonSharpBehaviour
    {
        //For notes on the Account Manager, see OfficerAccountManager.cs line 25
        public OfficerAccountManager accountManager; //Our reference to the account manager
        [Space]
        public string allowedRole = "Staff"; //Only allow players with this role set to "true" to see this object
        
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
            //Disallow the current player from picking up this object if they don't have the required role
            bool allowed = accountManager._GetBool(allowedRole);
            if (!allowed)
            {
                VRC_Pickup pickup = (VRC_Pickup) GetComponent(typeof(VRC_Pickup));
                Debug.Assert(pickup != null, "RestrictedPickup must be attached to a VRC_Pickup object!");
                pickup.pickupable = false;
            }
        }
    }
}
