using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.Gameplay;
using UnityEngine;

namespace ClustrGalaxy.Flight
{
    [DefaultExecutionOrder(500)]
    public sealed class ShipSurfaceSafety : MonoBehaviour
    {
        public const float HullRadius = 2f;
        private Rigidbody body;
        private ObserverProbeController flight;
        private PhysicsMaterial material;
        private GravitySource recoveryBody;
        private Vector3 recoveryOffset;
        private static readonly Vector3[] Directions = { Vector3.up, Vector3.down, Vector3.right, Vector3.left, Vector3.forward, Vector3.back };
        public int RecoveryCount { get; private set; }
        private void Awake()
        {
            body = GetComponent<Rigidbody>(); flight = GetComponent<ObserverProbeController>();
            var collider = gameObject.AddComponent<BoxCollider>(); collider.size = new Vector3(1.8f, .9f, 3.2f);
            material = new PhysicsMaterial("Probe contact") { bounciness = 0, dynamicFriction = 0, staticFriction = 0,
                frictionCombine = PhysicsMaterialCombine.Minimum, bounceCombine = PhysicsMaterialCombine.Minimum };
            collider.sharedMaterial = material;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;
        }
        private void FixedUpdate()
        {
            if (flight.Simulation.Recovering)
            {
                if (recoveryBody != null) { body.position = recoveryBody.transform.position + recoveryOffset; transform.position = body.position; }
                return;
            }
            if (body.isKinematic || !flight.enabled) return;
            var dt = Time.fixedDeltaTime;
            var predictedVelocity = body.linearVelocity + flight.AppliedAcceleration * dt;
            var sources = GravitySource.ActiveSources;
            var shipSpeed = predictedVelocity.magnitude;
            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                if (source == null || source.Definition == null) continue;
                var start = body.position - source.transform.position;
                var radius = source.Definition.ExclusionRadius + HullRadius;
                var reach = radius + (shipSpeed + source.MaximumSpeed) * dt + 16 * dt * dt;
                if (start.sqrMagnitude > reach * reach) continue;
                var centerVelocity = source.CenterVelocity;
                var end = start + (predictedVelocity - centerVelocity) * dt;
                if (!SurfaceIntersection.Sweep(N(start), N(end), radius, out var fraction)) continue;
                var normal = Vector3.Lerp(start, end, fraction).normalized;
                if (normal.sqrMagnitude < .5f) normal = Vector3.up;
                var inward = -Vector3.Dot(predictedVelocity - centerVelocity, normal);
                if (source.Definition.Stellar || inward > 20f || start.magnitude < radius - HullRadius)
                { Recover(source, normal); return; }
                // Contact impulse removes inward momentum; PhysX resolves the contact position.
                if (inward > 0f) body.AddForce(normal * inward * body.mass, ForceMode.Impulse);
            }
        }
        private void Recover(GravitySource source, Vector3 normal)
        {
            var candidate = source.transform.position + normal * ((float)source.Definition.ExclusionRadius + 500f);
            var found = IsClear(candidate);
            for (var shell = 1; shell <= 8 && !found; shell++) foreach (var direction in Directions)
            {
                candidate = source.transform.position + direction * ((float)source.Definition.ExclusionRadius + 500f * shell);
                if (IsClear(candidate)) { found = true; break; }
            }
            if (!found) candidate = body.position; // Remain protected and require a later safe resume.
            recoveryBody = source; recoveryOffset = candidate - source.transform.position;
            flight.Simulation.BeginRecovery(found ? "RECOVERED · SELECT RESUME FLIGHT" : "RECOVERY HOLD · SAFE POSITION UNAVAILABLE", source.Definition.Id);
            GetComponent<GalaxyNavigationController>()?.InterruptForRecovery();
            flight.ClearAutopilotDesiredVelocity();
            body.isKinematic = true; body.position = candidate; transform.position = candidate;
            RecoveryCount++;
            GetComponent<ClustrGalaxy.UI.ProbeHud>()?.SetMenuOpen(true);
        }
        public bool Resume()
        {
            if (!flight.Simulation.Recovering) return true;
            if (!IsClear(body.position)) return false;
            var velocity = recoveryBody != null ? recoveryBody.CenterVelocity : Vector3.zero;
            body.isKinematic = false; body.linearVelocity = velocity; body.angularVelocity = Vector3.zero;
            flight.Simulation.Resume(); flight.ModeState.SetFlightMode(FlightControlMode.PilotMastery);
            recoveryBody = null; GetComponent<GalaxyNavigationController>()?.RefreshResidency(); return true;
        }
        public static bool IsClear(Vector3 point)
        {
            var sources = GravitySource.ActiveSources;
            for (var index = 0; index < sources.Count; index++)
            {
                var source = sources[index];
                if (source != null && source.Definition != null && Vector3.Distance(point, source.transform.position) <= source.Definition.ExclusionRadius + HullRadius + 1) return false;
            }
            return true;
        }
        private static System.Numerics.Vector3 N(Vector3 value) => new(value.x, value.y, value.z);
        private void OnDestroy() { if (material != null) Destroy(material); }
    }
}
