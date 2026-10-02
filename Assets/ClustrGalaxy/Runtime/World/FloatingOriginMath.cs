using System;
using UnityEngine;

namespace ClustrGalaxy.World
{
    [Serializable]
    public readonly struct DoubleVector3
    {
        public DoubleVector3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static DoubleVector3 Zero => new(0d, 0d, 0d);
        public double X { get; }
        public double Y { get; }
        public double Z { get; }

        public DoubleVector3 Add(DoubleVector3 value) => new(X + value.X, Y + value.Y, Z + value.Z);
        public Vector3 RelativeTo(DoubleVector3 origin) => new((float)(X - origin.X), (float)(Y - origin.Y), (float)(Z - origin.Z));
        public DoubleVector3 Add(Vector3 value) =>
            new(X + value.x, Y + value.y, Z + value.z);
    }

    public static class FloatingOriginMath
    {
        public static Vector3 RequiredShift(Vector3 observerPosition, float threshold)
        {
            if (threshold <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(threshold));
            }
            return observerPosition.sqrMagnitude > threshold * threshold
                ? -observerPosition
                : Vector3.zero;
        }
    }
}
