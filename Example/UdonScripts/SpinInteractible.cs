
using UdonSharp;
using UnityEngine;
using VRC.SDKBase;
using VRC.Udon;

namespace LoliPoliceDepartment.Examples
{
    public class SpinInteractible : UdonSharpBehaviour
    {
        [UdonSynced] public bool spinning = false;
        
        //Toggle the spinning state and sync it to all players
        public override void Interact()
        {
            Networking.SetOwner(Networking.LocalPlayer, gameObject);
            spinning = !spinning;
            RequestSerialization();
        }

        //Spin the object if the "spinning" is true
        private void Update()
        {
            if (spinning)
            {
                transform.Rotate(0, 90 * Time.deltaTime, 0);
            }
        }
    }
}