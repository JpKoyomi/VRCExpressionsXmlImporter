using System.Collections.Generic;
using UnityEngine;
using VRC.SDK3.Avatars.ScriptableObjects;

namespace KLabs.VRC.SDK3.Avatars.ScriptableObjects
{
    public class VRCExpressions : ScriptableObject
    {
        [field: SerializeField]
        public VRCExpressionParameters Parameters { get; set; }

        [field: SerializeField]
        public VRCExpressionsMenu[] Menu { get; set; }
    }
}
