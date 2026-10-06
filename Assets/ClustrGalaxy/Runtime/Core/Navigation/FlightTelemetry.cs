using System;
using System.Numerics;
using ClustrGalaxy.Core.Domain;
namespace ClustrGalaxy.Core.Navigation
{
    public readonly struct FlightTelemetry
    {
        private FlightTelemetry(GalaxyEntityId body, double radius, double altitude, double radial, double speed, double stopping,
            double periapsis, double apoapsis, double period, bool impact, bool escaping, Vector3 prograde)
        { BodyId=body; BodyRadius=radius; Available=true; Altitude=altitude; RadialSpeed=radial; RelativeSpeed=speed; StoppingDistance=stopping;
            Periapsis=periapsis; Apoapsis=apoapsis; Period=period; Impact=impact; Escaping=escaping; Prograde=prograde; }
        public bool Available { get; } public GalaxyEntityId BodyId { get; } public double BodyRadius { get; }
        public double Altitude { get; } public double RadialSpeed { get; } public double RelativeSpeed { get; }
        public double ClosingSpeed => Math.Max(0,-RadialSpeed);
        public double StoppingDistance { get; } public double Periapsis { get; } public double Apoapsis { get; } public double Period { get; }
        public bool Impact { get; } public bool Escaping { get; } public Vector3 Prograde { get; }
        public static FlightTelemetry Calculate(CelestialBodyDefinition body, Vector3 position, Vector3 velocity, double braking)
        {
            if(body==null||!ShipDefinition.Finite(position)||!ShipDefinition.Finite(velocity)||!double.IsFinite(braking)||braking<=0) return default;
            var r=Math.Sqrt(Dot(position,position)); if(r<1e-9) return default;
            var v2=Dot(velocity,velocity);var radial=Dot(position,velocity)/r;
            double hx=(double)position.Y*velocity.Z-(double)position.Z*velocity.Y;
            double hy=(double)position.Z*velocity.X-(double)position.X*velocity.Z;
            double hz=(double)position.X*velocity.Y-(double)position.Y*velocity.X;
            var h2=hx*hx+hy*hy+hz*hz;var energy=v2*.5-body.Mu/r;
            var e=Math.Sqrt(Math.Max(0,1+2*energy*h2/(body.Mu*body.Mu)));
            var periapsis=h2/(body.Mu*(1+e));var bound=energy<0;
            var axis=bound?-body.Mu/(2*energy):double.PositiveInfinity;
            var apoapsis=bound?axis*(1+e):double.PositiveInfinity;
            var period=bound?2*Math.PI*Math.Sqrt(axis*axis*axis/body.Mu):double.PositiveInfinity;
            var closing=Math.Max(0,-radial);
            return new FlightTelemetry(body.Id,body.Radius,Math.Max(0,r-body.Radius),radial,Math.Sqrt(v2),closing*closing/(2*braking),
                periapsis,apoapsis,period,r<=body.ExclusionRadius+2 || (periapsis<=body.ExclusionRadius+2 && (bound || radial<0)),!bound,v2>1e-12?velocity/(float)Math.Sqrt(v2):Vector3.Zero);
        }
        private static double Dot(Vector3 a,Vector3 b)=>(double)a.X*b.X+(double)a.Y*b.Y+(double)a.Z*b.Z;
    }
}
