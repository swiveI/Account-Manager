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

using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro;

namespace LocalPoliceDepartment.Utilities
{
    public class OfficerData : MonoBehaviour
    {
        public string[] DataTitles = new string[0];
        public List<CustomDataList> CustomLists = new List<CustomDataList>(0);
    }
    [System.Serializable]
    public class CustomDataList
    {
        public int Title;
        public string Value;
        public List<int> Ids;
        public TextMeshProUGUI nameDisplay;
    }
}