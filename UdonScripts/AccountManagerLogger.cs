//Disable unused variable warning
#pragma warning disable CS0414

using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace LoliPoliceDepartment.Utilities.AccountManager
{
    public class AccountManagerLogger : UdonSharpBehaviour
    {
        [Tooltip("The instance of account manager you want to dump the data from")]
        [SerializeField] OfficerAccountManager accountManager;

        [Tooltip("If true, the data will only be printed to the log file of specific players to protect the data")]
        [SerializeField] bool limitToSpecificUsers = true;
        [Tooltip("ID#s of the players to dump the data to when they leave the room")]
        [SerializeField] int[] IDNumbers;
        void Start()
        {
            if (accountManager == null)
            {
                Debug.Log("Account Manager Logger: No Officer Account Manager assigned.");
            }
        }
        public override void OnPlayerLeft(VRCPlayerApi player)
        {
            if (player == Networking.LocalPlayer)
            {
                foreach (int id in IDNumbers)
                {
                    if (id == accountManager.LocalOfficerID) _DumpDataToLog();
                }                
            } 
        }
        private void _DumpDataToLog()
        {
            string dataDump = "Account Manager Data Dump Begin" + '\n';
            string[] dataLines = new string[accountManager.OfficerData.Length];
            for (int i = 0; i < accountManager.OfficerData.Length; i++)
            {
                dataLines[i] = string.Join(",", accountManager.OfficerData[i]);
            }
            dataDump += string.Join('\n'.ToString(), dataLines);
            dataDump += '\n' + "Account Manager Data Dump End";
            Debug.Log(dataDump);    
        }
    }
}