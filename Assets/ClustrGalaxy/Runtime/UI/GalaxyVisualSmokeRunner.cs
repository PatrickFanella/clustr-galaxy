using System;
using System.Collections;
using System.IO;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.World;
using UnityEngine;
namespace ClustrGalaxy.UI
{
    public sealed class GalaxyVisualSmokeRunner : MonoBehaviour
    {
        [Serializable] private sealed class Result
        {public bool free_assets_loaded,planet_near_detail,engine_audio_played,interface_audio_played,reduced_audio_stopped,muted_audio_stopped,scanner_audio_played,scanner_audio_stopped,passed,origin_preserved,orbit_started,orbit_captured,orbit_released_bound,approach_started,approach_scanned,approach_clear,collision_recovered,recovery_resumed,recovery_progress_unchanged,moon_orbit_started,moon_orbit_captured,moon_coast_clear,stellar_recovered,trajectory_available;public string source,build;public int orbit_guides,minimum_orbit_vertices,width,height;public float planet_diameter,sun_diameter,orbit_minimum_radius,orbit_maximum_radius;}
        private static void SetPreferences(GalaxySessionBehaviour owner, ClustrGalaxy.Core.Activities.ActivityPreferences preferences)
        {
            var activities = owner.Session.Activities;
            var outcome = activities.Apply(new ClustrGalaxy.Core.Activities.ActivityIntent(Guid.NewGuid().ToString("N"), activities.State.Identity,
                ClustrGalaxy.Core.Activities.ActivityOperation.SetPreferences, DateTime.UtcNow, preferences: preferences));
            if (!outcome.Committed) throw new InvalidOperationException(outcome.Error);
        }
        private RunnerFailureWatchdog watchdog;
        private IEnumerator Start()
        {
            watchdog = new RunnerFailureWatchdog("ClustrGalaxy.VisualSmoke");
            yield return new WaitForSecondsRealtime(3);
            var owner=GetComponent<GalaxySessionBehaviour>();var hud=GetComponent<ProbeHud>();
            var folder=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","visual-smoke");Directory.CreateDirectory(folder);
            var planet=owner.Registry.FindEntity("post:open-source-ai");var sun=owner.Registry.FindEntity("subreddit:technology");
            var result=new Result{source=Application.version,build=Application.buildGUID,width=Screen.width,height=Screen.height,minimum_orbit_vertices=int.MaxValue};
            var catalog = owner.PresentationAssets;
            result.free_assets_loaded = catalog != null && catalog.starSurface != null && catalog.cockpitConsole != null && catalog.confirmPrompt != null && GetComponent<ShipAudioFeedback>() != null;
            foreach(var line in FindObjectsByType<LineRenderer>())if(line.name.StartsWith("Orbit ",StringComparison.Ordinal)){result.orbit_guides++;result.minimum_orbit_vertices=Math.Min(result.minimum_orbit_vertices,line.positionCount);}
            result.planet_diameter=planet.GetComponent<Renderer>().bounds.size.x;result.sun_diameter=sun.GetComponent<Renderer>().bounds.size.x;
            result.passed=result.free_assets_loaded&&result.orbit_guides>=5&&result.minimum_orbit_vertices>=512&&result.planet_diameter>=240&&result.sun_diameter>=4000&&GetComponent<CockpitFlightDeck>()!=null&&planet.GetComponent<Renderer>().sharedMaterial.shader.isSupported;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"flight.png"));yield return new WaitForSecondsRealtime(.6f);
            hud.SetMenuOpen(true);yield return new WaitForSecondsRealtime(.5f);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"console.png"));yield return new WaitForSecondsRealtime(.6f);
            hud.SetMenuOpen(false);
            var position=planet.transform.position+new Vector3(660,225,-780);transform.SetPositionAndRotation(position,Quaternion.LookRotation(planet.transform.position-position));
            yield return new WaitForSecondsRealtime(.5f);
            var detail = planet.GetComponent<LODGroup>();
            Debug.Log("[ClustrGalaxy.VisualSmoke] Detail bias="+QualitySettings.lodBias+" size="+detail.size+" localRef="+detail.localReferencePoint+" scale="+planet.transform.lossyScale+" mesh="+planet.GetComponent<MeshFilter>().sharedMesh.vertexCount);
            foreach(var renderer in planet.GetComponentsInChildren<Renderer>()) Debug.Log("[ClustrGalaxy.VisualSmoke] Renderer "+renderer.name+" enabled="+renderer.enabled+" visible="+renderer.isVisible+" vertices="+renderer.GetComponent<MeshFilter>()?.sharedMesh.vertexCount);
            result.planet_near_detail = planet.GetComponent<MeshRenderer>().isVisible && planet.GetComponent<MeshFilter>().sharedMesh.vertexCount >= 8000;
            result.passed &= result.planet_near_detail;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"planet.png"));yield return new WaitForSecondsRealtime(.6f);
            var body=GetComponent<Rigidbody>();
            var relative=planet.transform.position-body.position;
            GetComponent<FloatingOrigin>().Rebase(-transform.position);
            result.origin_preserved=Vector3.Distance(relative,planet.transform.position-body.position)<.02f;
            result.passed &= result.origin_preserved;
            yield return new WaitForSecondsRealtime(.5f);ScreenCapture.CaptureScreenshot(Path.Combine(folder,"origin.png"));yield return new WaitForSecondsRealtime(.6f);
            var audio = GetComponent<ShipAudioFeedback>();
            var engineVoice = transform.Find("Engine feedback").GetComponent<AudioSource>();
            var interfaceVoice = transform.Find("Interface feedback").GetComponent<AudioSource>();
            var scannerVoice = transform.Find("Scanner feedback").GetComponent<AudioSource>();
            var preferences = owner.Session.Activities.State.Preferences;
            SetPreferences(owner, preferences.With(musicMuted: false, reducedAudio: false, musicVolume: .5f));
            var gamepad = UnityEngine.InputSystem.InputSystem.AddDevice<UnityEngine.InputSystem.Gamepad>();
            UnityEngine.InputSystem.InputSystem.QueueStateEvent(gamepad, new UnityEngine.InputSystem.LowLevel.GamepadState { rightTrigger = 1 });
            yield return new WaitForSecondsRealtime(.12f);
            result.engine_audio_played = engineVoice.isPlaying && engineVoice.volume > 0;
            audio.PlayInterface(); result.interface_audio_played = interfaceVoice.isPlaying;
            SetPreferences(owner, preferences.With(musicMuted: false, reducedAudio: true));
            yield return null; result.reduced_audio_stopped = !engineVoice.isPlaying && !scannerVoice.isPlaying;
            SetPreferences(owner, preferences.With(musicMuted: true, reducedAudio: false));
            audio.PlayInterface(); yield return null;
            result.muted_audio_stopped = !engineVoice.isPlaying && !interfaceVoice.isPlaying && !scannerVoice.isPlaying;
            UnityEngine.InputSystem.InputSystem.RemoveDevice(gamepad);
            SetPreferences(owner, preferences);
            result.passed &= result.engine_audio_played && result.interface_audio_played && result.reduced_audio_stopped && result.muted_audio_stopped;
            var navigation=GetComponent<ClustrGalaxy.Flight.GalaxyNavigationController>();
            var targeting=GetComponent<ClustrGalaxy.Interaction.ProbeTargeting>();
            targeting.SelectTarget(planet);
            // Start outside scan range, then cover the approach using the real navigation/physics adapters.
            body.position = planet.transform.position + Vector3.right * (planet.SurfaceRadius + 3500f);
            body.linearVelocity = planet.GetComponent<ClustrGalaxy.Simulation.KeplerOrbit>().WorldVelocity;
            body.angularVelocity = Vector3.zero;
            Physics.SyncTransforms();
            result.approach_started = navigation.PlotAutopilot(planet);
            result.approach_clear = true;
            Time.timeScale = 4;
            var approachDeadline = Time.timeAsDouble + 90;
            while (planet.SurfaceDistance(body.position) > 2400f && Time.timeAsDouble < approachDeadline)
            {
                result.approach_clear &= planet.SurfaceDistance(body.position) > 250f;
                yield return null;
            }
            var scanner = GetComponent<ClustrGalaxy.Scanning.SignalScanner>();
            scanner.BeginScan();
            yield return new WaitForSecondsRealtime(.05f);
            result.scanner_audio_played = scannerVoice.isPlaying;
            var scanDeadline = Time.timeAsDouble + 2;
            while (Time.timeAsDouble < scanDeadline) yield return null;
            result.approach_scanned = scanner.State == ClustrGalaxy.Scanning.SignalScanState.Complete;
            result.scanner_audio_stopped = !scannerVoice.isPlaying;
            result.passed &= result.scanner_audio_played && result.scanner_audio_stopped;
            result.passed &= result.approach_started && result.approach_clear && result.approach_scanned;
            Time.timeScale = 1;
            var radius=planet.GetComponent<SphereCollider>().bounds.extents.x;
            body.position=planet.transform.position+Vector3.right*radius*2.2f;
            body.linearVelocity=planet.GetComponent<ClustrGalaxy.Simulation.KeplerOrbit>().WorldVelocity;
            body.angularVelocity=Vector3.zero;
            transform.SetPositionAndRotation(body.position,Quaternion.LookRotation(planet.transform.position-body.position));
            result.orbit_started=navigation.StartOrbit(planet);
            result.passed &= result.orbit_started;
            Debug.Log("[ClustrGalaxy.VisualSmoke] Orbit start="+result.orbit_started+" status="+navigation.TravelStatus);
            Time.timeScale=8;
            var until=Time.timeAsDouble+100;
            while(Time.timeAsDouble<until)
            {result.orbit_captured |= navigation.OrbitCaptured;yield return null;}
            result.orbit_minimum_radius=result.orbit_maximum_radius=Vector3.Distance(body.position,planet.transform.position);
            navigation.ReleaseOrbit();until=Time.timeAsDouble+120;
            while(Time.timeAsDouble<until)
            {
                var current=Vector3.Distance(body.position,planet.transform.position);
                result.orbit_minimum_radius=Mathf.Min(result.orbit_minimum_radius,current);
                result.orbit_maximum_radius=Mathf.Max(result.orbit_maximum_radius,current);
                yield return null;
            }
            Time.timeScale=1;
            result.orbit_released_bound=result.orbit_minimum_radius>radius*1.1f&&result.orbit_maximum_radius<radius*3.5f;
            result.passed &= result.orbit_captured && result.orbit_released_bound;
            var trajectory = owner.Session.Flight.Telemetry;
            result.trajectory_available = trajectory.Available && trajectory.Periapsis > planet.SurfaceRadius;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"orbit.png")); yield return new WaitForSecondsRealtime(.6f);
            var moon = owner.Registry.FindEntity("comment:open-source-ai-1");
            targeting.SelectTarget(moon);
            var moonOrbit = moon.GetComponent<ClustrGalaxy.Simulation.KeplerOrbit>();
            body.position = moon.transform.position + Vector3.right * moon.SurfaceRadius * 2.2f;
            body.linearVelocity = moonOrbit.WorldVelocity;
            Physics.SyncTransforms();
            result.moon_orbit_started = navigation.StartOrbit(moon);
            Time.timeScale = 8; var moonDeadline = Time.timeAsDouble + 50;
            while (Time.timeAsDouble < moonDeadline) { result.moon_orbit_captured |= navigation.OrbitCaptured; yield return null; }
            navigation.ReleaseOrbit(); moonDeadline = Time.timeAsDouble + 80;
            result.moon_coast_clear = true;
            while (Time.timeAsDouble < moonDeadline)
            {
                var moonDistance = Vector3.Distance(body.position, moon.transform.position);
                result.moon_coast_clear &= moonDistance > moon.SurfaceRadius + ClustrGalaxy.Flight.ShipSurfaceSafety.HullRadius && moonDistance < moon.SurfaceRadius * 3.5f;
                yield return null;
            }
            Time.timeScale = 1;
            result.passed &= result.trajectory_available && result.moon_orbit_started && result.moon_orbit_captured && result.moon_coast_clear;
            var safety = GetComponent<ClustrGalaxy.Flight.ShipSurfaceSafety>();
            var beforeRecovery = safety.RecoveryCount;
            var beforeSurvey = owner.Session.Survey?.State;
            var beforeActivities = owner.Session.Activities.State;
            body.position = planet.transform.position + Vector3.right * (planet.SurfaceRadius + 1000);
            body.linearVelocity = planet.GetComponent<ClustrGalaxy.Simulation.KeplerOrbit>().WorldVelocity - Vector3.right * 50000;
            Physics.SyncTransforms();
            yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            result.collision_recovered = safety.RecoveryCount == beforeRecovery + 1 && body.isKinematic && planet.SurfaceDistance(body.position) > 250;
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"recovery.png")); yield return new WaitForSecondsRealtime(.6f);
            foreach (var button in hud.GetComponentsInChildren<UnityEngine.UI.Button>())
                if (button.GetComponentInChildren<UnityEngine.UI.Text>()?.text == "RESUME FLIGHT") { button.onClick.Invoke(); break; }
            yield return new WaitForFixedUpdate();
            result.recovery_resumed = !owner.Session.Flight.Recovering && !body.isKinematic &&
                (body.linearVelocity - planet.GetComponent<ClustrGalaxy.Simulation.KeplerOrbit>().WorldVelocity).magnitude < 1f;
            result.recovery_progress_unchanged = ReferenceEquals(beforeSurvey, owner.Session.Survey?.State) && ReferenceEquals(beforeActivities, owner.Session.Activities.State);
            result.passed &= result.collision_recovered && result.recovery_resumed && result.recovery_progress_unchanged;
            var starGravity = sun.GetComponent<ClustrGalaxy.Simulation.GravitySource>();
            body.position = sun.transform.position + Vector3.right * ((float)starGravity.Definition.ExclusionRadius + ClustrGalaxy.Flight.ShipSurfaceSafety.HullRadius + .1f);
            body.linearVelocity = -Vector3.right * 10;
            Physics.SyncTransforms(); yield return new WaitForFixedUpdate(); yield return new WaitForFixedUpdate();
            result.stellar_recovered = owner.Session.Flight.Recovering && sun.SurfaceDistance(body.position) > 500;
            result.passed &= result.stellar_recovered;
            File.WriteAllText(Path.Combine(folder,"summary.json"),JsonUtility.ToJson(result,true));
            Debug.Log("[ClustrGalaxy.VisualSmoke] passed="+result.passed);Application.Quit(result.passed?0:2);
        }
        private void OnDestroy()
        {
            watchdog?.Dispose();
            Time.timeScale = 1;
        }
    }
}
