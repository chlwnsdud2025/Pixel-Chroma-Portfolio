using Mirror;
using UnityEngine;

namespace PixelChroma
{
    public struct RevealedPixelMessage : NetworkMessage
    {
        public uint netId;
        public Color32 color;
    }
}