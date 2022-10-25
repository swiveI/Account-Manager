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
using System;


namespace LoliPoliceDepartment.Utilities.AccountManager
{
    public class OfficerAccountManager : UdonSharpBehaviour
    {
        [VRC.Udon.Serialization.OdinSerializer.OdinSerialize] /* UdonSharp auto-upgrade: serialization */ public string[][] OfficerData;
        [VRC.Udon.Serialization.OdinSerializer.OdinSerialize] /* UdonSharp auto-upgrade: serialization */ public int[][] CustomLists;
       
        public int LocalOfficerID;

        private void Start()
        {
            _LocalOfficerCheck();
        }

        private void _LocalOfficerCheck()
        {
            LocalOfficerID = _IDLookup(Networking.LocalPlayer.displayName);
            if (LocalOfficerID == -1)
            {
                return;
            }
        }

        public int _IDLookup(string name)
        {
            int low = 0;
            int high = OfficerData.Length;

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
            Debug.Log("Account Manager: No Officer ID was found for user: " + name);
            return -1;
        }
    }
}
