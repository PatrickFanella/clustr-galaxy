using System;
using System.Collections;
using System.IO;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Navigation;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Simulation;
using ClustrGalaxy.World;
using UnityEngine;
namespace ClustrGalaxy.UI
{
    public sealed class GalaxySimulationJourneyRunner : MonoBehaviour
    {
        [Serializable] private sealed class Result
        {
            public double jump_clearance, jump_relative_speed;
            public bool manual_drop_safe, approach_cancelled, unavailable_cancelled, chart_live, chart_camera_stable, jump_arrived, jump_cancelled, jump_safe, passed, action_pool_stable, modal_paused, modal_restored, route_selected, departed, charge_cancelled, cruise_cancelled, arrived, safe, no_recovery, no_progress;
            public double journey_seconds, departure_seconds, final_clearance, final_closing, minimum_clearance;
            public string source, build, departure, destination; public float execution_time_scale; public int samples;
        }
        [Serializable] private struct Pose
        { public double seconds, x, y, z, clearance; public float speed; public int phase; }
        [Serializable] private sealed class Trajectory { public Pose[] poses; }
        private static SimulationPosition Logical(Vector3 position)
        {
            var origin = FloatingOrigin.Active != null ? FloatingOrigin.Active.LogicalOrigin : DoubleVector3.Zero;
            return new SimulationPosition(origin.X + position.x, origin.Y + position.y, origin.Z + position.z);
        }
        private static double Clearance(Vector3 position)
        {
            var minimum = double.MaxValue; var sources = GravitySource.ActiveSources;
            for (var i = 0; i < sources.Count; i++) if (sources[i]?.Definition != null)
                minimum = Math.Min(minimum, Vector3.Distance(position, sources[i].transform.position) - sources[i].Definition.ExclusionRadius - ShipSurfaceSafety.HullRadius);
            return minimum;
        }
        private RunnerFailureWatchdog watchdog;
        private IEnumerator Start()
        {
            watchdog = new RunnerFailureWatchdog("ClustrGalaxy.SimulationJourney");
            yield return new WaitForSecondsRealtime(3);
            var owner = GetComponent<GalaxySessionBehaviour>(); var navigation = GetComponent<GalaxyNavigationController>();
            var controller = GetComponent<GalaxySystemCruiseController>(); var body = GetComponent<Rigidbody>();
            var flight = GetComponent<ObserverProbeController>(); var safety = GetComponent<ShipSurfaceSafety>();
            var result = new Result { source = Application.version, build = Application.buildGUID, execution_time_scale = 4, minimum_clearance = double.MaxValue };
            var folder = Path.Combine(Application.persistentDataPath, "ClustrGalaxy", "simulation-journey"); Directory.CreateDirectory(folder);
            var hud = GetComponent<ProbeHud>(); var observedPlanet = owner.Registry.FindEntity("post:open-source-ai");
            var clock = Time.timeAsDouble; var planetPosition = observedPlanet.transform.position; var velocityBefore = body.linearVelocity;
            hud.SetModalInputCaptured(true); yield return new WaitForSecondsRealtime(.2f);
            result.modal_paused = Time.timeScale == 0 && Time.timeAsDouble == clock && observedPlanet.transform.position == planetPosition && body.isKinematic;
            hud.SetModalInputCaptured(false);
            result.modal_restored = Time.timeScale == 1 && flight.enabled && navigation.enabled && !body.isKinematic && body.linearVelocity == velocityBefore;
            var cockpitCamera = GetComponentInChildren<Camera>();
            var cameraPosition = cockpitCamera.transform.localPosition;
            var cameraRotation = cockpitCamera.transform.localRotation;
            clock = Time.timeAsDouble;
            navigation.SetMapOpen(true); yield return new WaitForSecondsRealtime(.3f);
            result.chart_live = Time.timeScale == 1 && Time.timeAsDouble > clock && !body.isKinematic &&
                !owner.Session.CockpitInput.PilotEnabled && !owner.Session.CockpitInput.Paused;
            result.chart_camera_stable = cockpitCamera.targetTexture == null &&
                Vector3.Distance(cockpitCamera.transform.localPosition, cameraPosition) < .001f &&
                Quaternion.Angle(cockpitCamera.transform.localRotation, cameraRotation) < .01f;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "chart.png")); yield return new WaitForSecondsRealtime(.3f);
            navigation.SetMapOpen(false);
            result.chart_live &= owner.Session.CockpitInput.PilotEnabled;
            GetComponent<ClustrGalaxy.Interaction.ProbeTargeting>().SelectTarget(observedPlanet);
            hud.SetMenuOpen(true); yield return null; yield return null;
            var camera = GetComponentInChildren<Camera>();
            var actionCount = camera.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length;
            for (var i = 0; i < 30; i++) yield return null;
            result.action_pool_stable = camera.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length == actionCount;
            hud.SetMenuOpen(false);
            for (var i = 0; i < 30; i++) yield return null;
            result.action_pool_stable &= camera.GetComponentsInChildren<UnityEngine.UI.Button>(true).Length == actionCount;
            var ids = new[] { "post:open-source-ai", "post:linux-handhelds", "post:right-to-repair", "post:privacy-tools", "post:retro-computing" };
            GalaxyEntity departure = null, destination = null; var start = Vector3.zero;
            for (var a = 0; a < ids.Length && departure == null; a++) for (var b = 0; b < ids.Length && departure == null; b++)
            {
                if (a == b) continue; var from = owner.Registry.FindEntity(ids[a]); var to = owner.Registry.FindEntity(ids[b]);
                var offset = to.transform.position - from.transform.position;
                if (offset.magnitude < 10000 || offset.magnitude > 50000) continue;
                var direction = offset.normalized; var candidate = from.transform.position + direction * (from.SurfaceRadius + 500);
                var end = to.transform.position - direction * ((float)to.GetComponent<GravitySource>().Definition.ExclusionRadius + 300);
                if (controller.Sweep(Logical(candidate), Logical(end), 0, out _)) continue;
                departure = from; destination = to; start = candidate;
            }
            result.route_selected = departure != null;
            if (departure != null)
            {
                result.departure = departure.StableId; result.destination = destination.StableId;
                body.position = start; body.linearVelocity = departure.GetComponent<GravitySource>().CenterVelocity; body.angularVelocity = Vector3.zero;
                transform.SetPositionAndRotation(start, Quaternion.LookRotation(destination.transform.position - start)); Physics.SyncTransforms();
                GetComponent<ClustrGalaxy.Interaction.ProbeTargeting>().SelectTarget(destination);
                var beforeActivities = owner.Session.Activities.State; var recoveries = safety.RecoveryCount;
                Time.timeScale = 4; var began = Time.timeAsDouble;
                navigation.PlotAutopilot(destination);
                while (departure.SurfaceDistance(body.position) < 1500 && Time.timeAsDouble - began < 40) yield return null;
                result.departure_seconds = Time.timeAsDouble - began; result.departed = departure.SurfaceDistance(body.position) >= 1500;
                controller.Begin(destination, true); yield return new WaitForSeconds(.5f); controller.Cancel();
                result.charge_cancelled = !controller.Active && !body.isKinematic;
                controller.Begin(destination, true); var deadline = Time.timeAsDouble + 8;
                while (owner.Session.Travel.State.Phase == TravelPhase.Charging && Time.timeAsDouble < deadline) yield return null;
                var entered = body.isKinematic; controller.Cancel();
                result.cruise_cancelled = entered && !controller.Active && !body.isKinematic && !flight.DriveVelocity.HasValue;
                began = Time.timeAsDouble; var launched = controller.Begin(destination, true);
                var poses = new Pose[3000]; var count = 0; var next = began;
                while (controller.Active && Time.timeAsDouble - began < 180)
                {
                    var clearance = Clearance(body.position); result.minimum_clearance = Math.Min(result.minimum_clearance, clearance);
                    if (Time.timeAsDouble >= next && count < poses.Length)
                    {
                        var position = Logical(body.position);
                        poses[count++] = new Pose { seconds = Time.timeAsDouble - began, x = position.X, y = position.Y, z = position.Z,
                            clearance = clearance, speed = flight.Speed, phase = (int)owner.Session.Travel.State.Phase };
                        next = Time.timeAsDouble + .1;
                    }
                    yield return null;
                }
                result.samples = count; result.journey_seconds = result.departure_seconds + Time.timeAsDouble - began;
                var relative = body.position - destination.transform.position;
                result.final_clearance = relative.magnitude - destination.GetComponent<GravitySource>().Definition.ExclusionRadius;
                result.final_closing = -Vector3.Dot(body.linearVelocity - destination.GetComponent<GravitySource>().CenterVelocity, relative.normalized);
                result.arrived = launched && !controller.Active && owner.Session.Travel.Status == "ARRIVED · LOCAL FLIGHT" && !body.isKinematic;
                result.safe = result.minimum_clearance > 0 && result.final_clearance > 0 && Math.Abs(result.final_closing) < 20;
                result.no_recovery = recoveries == safety.RecoveryCount;
                result.no_progress = ReferenceEquals(beforeActivities, owner.Session.Activities.State) && owner.Session.Survey == null;
                Array.Resize(ref poses, count); File.WriteAllText(Path.Combine(folder, "trajectory.json"), JsonUtility.ToJson(new Trajectory { poses = poses }));
            }
            Time.timeScale = 1;
            var jump = GetComponent<GalaxyJumpController>();
            var remote = owner.Registry.FindEntity("subreddit:signal-horizon");
            var activitiesBeforeJump = owner.Session.Activities.State;
            var jumpStarted = jump.Begin(remote); yield return new WaitForSeconds(.5f); jump.Cancel();
            result.jump_cancelled = jumpStarted && !jump.Active && owner.Session.CurrentSystemId.Value == "subreddit:technology";
            jumpStarted = jump.Begin(remote); var jumpDeadline = Time.realtimeSinceStartup + 10;
            while (jump.Active && Time.realtimeSinceStartup < jumpDeadline) yield return null;
            // Observe settled local physics and allow the bounded-rate instruments to refresh.
            yield return new WaitForSeconds(.35f);
            var remoteSource = remote.GetComponent<GravitySource>();
            result.jump_clearance = Vector3.Distance(body.position, remote.transform.position) - remoteSource.Definition.ExclusionRadius;
            result.jump_relative_speed = (body.linearVelocity - remoteSource.CenterVelocity).magnitude;
            result.jump_arrived = jumpStarted && !jump.Active && owner.Session.CurrentSystemId.Value == remote.StableId &&
                result.jump_clearance > 450 && result.jump_clearance < 550 && result.jump_relative_speed < 20 &&
                !navigation.IsMapOpen && !owner.Session.Navigation.State.IsMapOpen && !body.isKinematic && flight.enabled;
            result.jump_safe = Clearance(body.position) > 0 && !flight.Simulation.Recovering &&
                ReferenceEquals(activitiesBeforeJump, owner.Session.Activities.State);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder, "arrival.png")); yield return new WaitForSecondsRealtime(.6f);
            // Explicit safety fixture: approach a stellar exclusion with excessive incoming momentum.
            // The charge and swept drive run normally; fixture placement itself awards nothing.
            var beforeSafety = owner.Session.Activities.State;
            var recoveryCount = safety.RecoveryCount;
            body.position = remote.transform.position + Vector3.up * ((float)remoteSource.Definition.ExclusionRadius + 4500);
            transform.position = body.position; body.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            transform.rotation = body.rotation; body.linearVelocity = remoteSource.CenterVelocity + Vector3.down * 1000;
            body.angularVelocity = Vector3.zero; Physics.SyncTransforms();
            var manualStarted = controller.Begin(remote, false); var safetyDeadline = Time.timeAsDouble + 12;
            while (controller.Active && Time.timeAsDouble < safetyDeadline) yield return null;
            result.manual_drop_safe = manualStarted && !controller.Active &&
                owner.Session.Travel.Status.Contains("CONTROLLED DRIVE DROP") && !body.isKinematic &&
                Clearance(body.position) > 0 && (body.linearVelocity - remoteSource.CenterVelocity).magnitude < 20 &&
                safety.RecoveryCount == recoveryCount && ReferenceEquals(beforeSafety, owner.Session.Activities.State);
            controller.Cancel();
            body.position = remote.transform.position + Vector3.up * ((float)remoteSource.Definition.ExclusionRadius + 1800);
            transform.position = body.position; body.linearVelocity = remoteSource.CenterVelocity; Physics.SyncTransforms();
            var approachStarted = controller.Begin(remote, true); safetyDeadline = Time.timeAsDouble + 10;
            while (controller.Active && owner.Session.Travel.State.Phase != TravelPhase.Approach && Time.timeAsDouble < safetyDeadline) yield return null;
            var reachedApproach = owner.Session.Travel.State.Phase == TravelPhase.Approach; controller.Cancel();
            result.approach_cancelled = approachStarted && reachedApproach && !controller.Active && !body.isKinematic && Clearance(body.position) > 0;
            var unavailableStarted = controller.Begin(remote, true);
            remote.gameObject.SetActive(false); yield return new WaitForFixedUpdate(); yield return null;
            result.unavailable_cancelled = unavailableStarted && !controller.Active && !body.isKinematic &&
                owner.Session.Travel.Status.Contains("UNAVAILABLE") && ReferenceEquals(beforeSafety, owner.Session.Activities.State);
            remote.gameObject.SetActive(true);
            result.passed = result.manual_drop_safe && result.approach_cancelled && result.unavailable_cancelled && result.chart_live && result.chart_camera_stable && result.jump_arrived && result.jump_cancelled && result.jump_safe && result.action_pool_stable && result.modal_paused && result.modal_restored && result.route_selected && result.departed && result.charge_cancelled && result.cruise_cancelled && result.arrived && result.safe &&
                result.no_recovery && result.no_progress && result.journey_seconds >= 60 && result.journey_seconds <= 120 && result.samples >= 100;
            File.WriteAllText(Path.Combine(folder, "summary.json"), JsonUtility.ToJson(result, true));
            Debug.Log("[ClustrGalaxy.SimulationJourney] passed=" + result.passed); Application.Quit(result.passed ? 0 : 2);
        }
        private void OnDestroy()
        {
            watchdog?.Dispose();
            Time.timeScale = 1;
        }
    }
}
