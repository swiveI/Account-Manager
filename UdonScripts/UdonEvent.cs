
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace LoliPoliceDepartment.Utilities
{
    //Store a stack of udonbehaviours and function names to call when an event is triggered.
    public class UdonEvent : UdonSharpBehaviour
    {
        private int count = 0;
        private const int minSize = 4;
        private UdonBehaviour[] udonBehaviours = new UdonBehaviour[minSize];
        private string[] functionNames = new string[minSize];

        //Notify all subscribed udonbehaviours that the event has been triggered.
        public void Trigger()
        {
            for (int i = 0; i < count; i++)
            {
                if (udonBehaviours[i] != null && functionNames[i] != null)
                {
                    udonBehaviours[i].SendCustomEvent(functionNames[i]);
                }
            }
        }

        // udonEvent.Add(this, "OnTrigger")
        public void AddReceiver(UdonBehaviour udonBehaviour, string functionName)
        {
            if (udonBehaviour == null) return;
            if (count == udonBehaviours.Length) AllocateArrays(count * 2);
            udonBehaviours[count] = udonBehaviour;
            functionNames[count] = functionName;
            count++;
        }

        public void RemoveReceiver(UdonBehaviour udonBehaviour, string functionName)
        {
            if (udonBehaviour == null) return;
            for (int i = 0; i < count; i++)
            {
                if (udonBehaviours[i] == udonBehaviour && functionNames[i] == functionName)
                {
                    RemoveReceiverAt(i);
                    return;
                }
            }
        }

        private void RemoveReceiverAt(int index)
        {
            if (index < 0 || index >= count) return;
            udonBehaviours[index] = null;
            functionNames[index] = null;
            for (int i = index; i < count - 1; i++)
            {
                udonBehaviours[i] = udonBehaviours[i + 1];
                functionNames[i] = functionNames[i + 1];
            }
            count--;
            if (count < udonBehaviours.Length / 2) AllocateArrays(count);
        }

        private void AllocateArrays(int elements, bool keepData = true)
        {
            if (elements < minSize) elements = minSize;
            if (elements == count) return; //Nothing to do
            UdonBehaviour[] newUdonBehaviours = new UdonBehaviour[elements];
            string[] newFunctionNames = new string[elements];
            if (keepData)
            {
                for (int i = 0; i < count; i++)
                {
                    newUdonBehaviours[i] = udonBehaviours[i];
                    newFunctionNames[i] = functionNames[i];
                }
            }
            udonBehaviours = newUdonBehaviours;
            functionNames = newFunctionNames;
        }
    }
}