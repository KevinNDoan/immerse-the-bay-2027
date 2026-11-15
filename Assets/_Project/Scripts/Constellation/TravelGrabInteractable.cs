using System;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace Zap.Constellation
{
    // Read the final smoothed throw before the next physics step can hit an environment mesh.
    public sealed class TravelGrabInteractable : XRGrabInteractable
    {
        public event Action<Vector3> Detached;
        protected override void Detach()
        {
            base.Detach();
            Detached?.Invoke(GetComponent<Rigidbody>().linearVelocity);
        }
    }
}
