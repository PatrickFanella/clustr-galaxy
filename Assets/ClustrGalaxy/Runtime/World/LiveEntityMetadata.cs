using ClustrGalaxy.Data;
using UnityEngine;

namespace ClustrGalaxy.World
{
    public sealed class LiveEntityMetadata : MonoBehaviour
    {
        public SpatialNode Node { get; private set; } = null!;

        public void Initialize(SpatialNode node)
        {
            Node = node;
        }

        public void Clear() => Node = null!;
    }
}
