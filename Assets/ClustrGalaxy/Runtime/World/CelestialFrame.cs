using UnityEngine;

namespace ClustrGalaxy.World
{
    // Organizational parents stay at the render origin. Otherwise a near-camera body's
    // float localPosition would have to cancel its distant parent's large translation.
    public sealed class CelestialFrame : MonoBehaviour
    {
        public static void Shift(Transform frame, Vector3 shift)
        {
            for (var i = 0; i < frame.childCount; i++)
            {
                var child = frame.GetChild(i);
                if (child.TryGetComponent<CelestialFrame>(out _)) Shift(child, shift);
                else if (child.TryGetComponent<Rigidbody>(out var body)) FloatingOrigin.ShiftRigidbody(body, shift);
                else child.position += shift;
            }
        }
    }
}
