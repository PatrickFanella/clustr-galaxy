using System;
using System.Numerics;
namespace ClustrGalaxy.Core.Navigation
{
    public readonly struct ShipControlInput
    {
        public ShipControlInput(Vector3 translation,Vector3 rotation){Translation=translation;Rotation=rotation;}
        public Vector3 Translation {get;} public Vector3 Rotation {get;}
        private static float Curve(float value)
        {if(!float.IsFinite(value))throw new ArgumentException("Invalid flight input.");value=Math.Clamp(value,-1,1);return value*Math.Abs(value);}
        public static ShipControlInput FromGamepad(Vector2 attitude,Vector2 yawLift,float forward,float reverse,float right,float left)
        {
            if(!float.IsFinite(forward)||!float.IsFinite(reverse)||!float.IsFinite(right)||!float.IsFinite(left)||!float.IsFinite(yawLift.Y))throw new ArgumentException("Invalid thrust input.");
            var translation=new Vector3(Math.Clamp(right-left,-1,1),Math.Clamp(yawLift.Y,-1,1),Math.Clamp(forward-reverse,-1,1));
            if(translation.LengthSquared()>1)translation=Vector3.Normalize(translation);
            return new ShipControlInput(translation,new Vector3(-Curve(attitude.Y),Curve(yawLift.X),-Curve(attitude.X)));
        }
    }
}
