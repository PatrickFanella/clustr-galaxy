using System;
using System.Collections;
using System.IO;
using System.Linq;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Persistence;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Gameplay;
using ClustrGalaxy.Interaction;
using ClustrGalaxy.World;
using ClustrGalaxy.Scanning;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace ClustrGalaxy.UI
{
    /// <summary>Opt-in player integration journey. Range placement is fixture setup, not flight acceptance.</summary>
    public sealed class GalaxyDemoJourneyRunner : MonoBehaviour
    {
        private Gamepad controller;
        private InputSettings.BackgroundBehavior previousBackground;
        private ProbeHud hud;
        private GalaxySessionBehaviour session;
        private GalaxySurveyVisibilityController visibility;
        private ProbeTargeting targeting;
        private bool failed;
        private readonly string[] members = { "subreddit:technology", "post:retro-computing", "post:open-source-ai",
            "post:linux-handhelds", "post:right-to-repair", "post:privacy-tools" };

        private RunnerFailureWatchdog watchdog;
        private IEnumerator Start()
        {
            watchdog = new RunnerFailureWatchdog("ClustrGalaxy.DemoJourney");
            session = GetComponent<GalaxySessionBehaviour>();
            hud = GetComponent<ProbeHud>();
            visibility = GetComponent<GalaxySurveyVisibilityController>();
            targeting = GetComponent<ProbeTargeting>();
            previousBackground = InputSystem.settings.backgroundBehavior;
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            controller = InputSystem.AddDevice<Gamepad>();
            var deadline = Time.realtimeSinceStartup + 30f;
            while (session.Session?.Snapshots.Manifest == null && Time.realtimeSinceStartup < deadline) yield return null;
            if (!Require(session.Session?.Snapshots.Manifest != null, "Demo snapshot did not open")) yield break;
            yield return new WaitForSecondsRealtime(1);
            if(Array.Exists(Environment.GetCommandLineArgs(),value=>value=="--clustr-demo-journey=import-only"))
            {yield return ExerciseSeparateProfileImport();yield break;}
            yield return Press(GamepadButton.Start);
            yield return Click("ENTER SURVEY");
            yield return Click("START AT r/technology");
            if (failed || !Require(visibility.SurveySession != null, "Survey start did not commit")) yield break;
            if (!session.Session.Activities.State.Preferences.AutomaticTuning) yield return Click("AUTOMATIC TUNING: OFF");
            if (!Require(!session.Session.Navigation.State.RequiresSurveyStart, "Navigation still requires a starting system")) yield break;
            if(Array.Exists(Environment.GetCommandLineArgs(),value=>value=="--clustr-ui-performance"))
            {yield return ProfileUiInteractions();yield break;}
            var resume=Array.Exists(Environment.GetCommandLineArgs(),value=>value=="--clustr-demo-journey=resume");
            var restart = resume||Array.Exists(Environment.GetCommandLineArgs(), value => value == "--clustr-demo-journey=restart");
            var expeditionJourney=Array.Exists(Environment.GetCommandLineArgs(),value=>value=="--clustr-expedition-journey");
            if (!restart)
            {
                yield return Click("SECTION: CREATIONS & DIARY");
                if(!Require(hud.GetComponentsInChildren<Button>().Any(x=>x.GetComponentInChildren<Text>()?.text=="OPEN LOCAL CREATIONS")&&!hud.GetComponentsInChildren<Button>().Any(x=>x.GetComponentInChildren<Text>()?.text=="START RingRun"),"Creation section mixed unrelated flight activities"))yield break;
                yield return Click("SECTION: ROUTES & EXPEDITIONS");
                if(!Require(hud.GetComponentsInChildren<Button>().Any(x=>x.GetComponentInChildren<Text>()?.text=="START RingRun")&&!hud.GetComponentsInChildren<Button>().Any(x=>x.GetComponentInChildren<Text>()?.text=="CAPTURE SCREENSHOT"),"Activity section did not filter creation controls"))yield break;
                yield return Click("SECTION: ALL ACTIONS");
                yield return Press(GamepadButton.East);
                for (var i = 0; i < members.Length; i++)
                {
                    var entity = session.Registry.FindEntity(members[i]);
                    if (!Require(entity != null && entity.gameObject.activeInHierarchy, "Scan target unavailable: " + members[i])) yield break;
                    var body = GetComponent<Rigidbody>();
                    body.position = entity.transform.position + new Vector3(100f, 80f, -100f).normalized * (entity.SurfaceRadius + 350f);
                    body.rotation = Quaternion.LookRotation(entity.transform.position - body.position, Vector3.up);
                    body.linearVelocity = Vector3.zero;
                    Physics.SyncTransforms();
                    yield return new WaitForFixedUpdate();
                    yield return new WaitForFixedUpdate();
                    yield return null;
                    if (!Require(entity.SurfaceDistance(transform.position) < 3000f,
                        "Fixture placement did not reach scan range")) yield break;
                    yield return Press(GamepadButton.South);
                    if (!Require(targeting.Target == entity, "Controller reticle selection failed: " + members[i])) yield break;
                    // Test the actual held Input System action and elapsed player time.
                    InputSystem.QueueStateEvent(controller, new GamepadState().WithButton(GamepadButton.West));
                    yield return new WaitForSecondsRealtime(.6f);
                    if (!Require(!visibility.SurveySession.State.IsScanComplete((GalaxyEntityId)members[i]), "Scan committed before required hold")) yield break;
                    var scanDeadline = Time.realtimeSinceStartup + 5f;
                    while (!visibility.SurveySession.State.IsScanComplete((GalaxyEntityId)members[i]) && Time.realtimeSinceStartup < scanDeadline)
                        yield return null;
                    var scanner = GetComponent<SignalScanner>();
                    Debug.Log("[ClustrGalaxy.DemoJourney] scan=" + members[i] + " state=" + scanner.State +
                        " charge=" + scanner.Charge01 + " target=" + targeting.Target?.StableId +
                        " distance=" + Vector3.Distance(transform.position, entity.transform.position) +
                        " enabled=" + scanner.enabled);
                    InputSystem.QueueStateEvent(controller, new GamepadState());
                    yield return null;
                    if (!Require(visibility.SurveySession.State.IsScanComplete((GalaxyEntityId)members[i]), "Held scan did not commit: " + members[i])) yield break;
                    yield return null;
                    yield return null;
                    if (!Require(visibility.SurveySession.State.Discoveries.Contains("clean-signal:" + members[i]), "Clean tuning distinction did not commit")) yield break;
                    if (i == 0)
                    {
                        var remote = session.Registry.FindEntity("subreddit:signal-horizon");
                        if (!Require(visibility.IsKnown(remote) && !visibility.CanLabel(remote), "Strong route destination must remain unidentified")) yield break;
                        if (!Require(!visibility.IsRouteKnown("route:technology-weak-archive"), "Weak route revealed too early")) yield break;
                    }
                }
                yield return Press(GamepadButton.Start);
                for (var i = 0; i < 3; i++)
                {
                    targeting.SelectTarget(session.Registry.FindEntity(members[i]));
                    yield return null;
                    yield return Click("ADD CONSTELLATION MEMBER");
                    if (failed) yield break;
                }
                foreach (var field in hud.GetComponentsInChildren<InputField>())
                {
                    if (field.name == "Constellation name") field.text = "Player journey arc";
                    if (field.name == "Reason / note") field.text = "Local authored journey evidence";
                }
                yield return null;
                yield return Click("SAVE LOCALLY");
                yield return Click("QUEUE FOR REVIEW");
                if (failed || !Require(visibility.SurveySession.State.QueuedPersonalConstellationIds.Count == 0, "Queue request prematurely confirmed")) yield break;
                yield return Click("CANCEL LOCAL QUEUE");
                yield return Click("QUEUE FOR REVIEW");
                yield return Click("CONFIRM LOCAL QUEUE");
                if (failed) yield break;
                yield return Click("EDIT SAVED CONSTELLATION DESIGN");
                yield return ExerciseStudio();
                if (failed) yield break;
                yield return Press(GamepadButton.East);
                yield return new WaitForSecondsRealtime(.6f);
                yield return Press(GamepadButton.Start);
                var recorder = GetComponent<JourneyRecorder>();
                yield return Click("SAVE ROLLING REPLAY — " + recorder.Status);
                if (!Require(!string.IsNullOrEmpty(recorder.LastSavedPath) && File.Exists(recorder.LastSavedPath),"Explicit replay save failed")) yield break;
            }
            if(Array.Exists(Environment.GetCommandLineArgs(),value=>value=="--clustr-campaign-journey"))
            {
                if(!restart){yield return ExerciseCampaign();if(failed)yield break;}
                if(!Require(session.Session.Activities.State.Records.Count(x=>x.Completed)==11,"Campaign did not restore all eleven completed activities"))yield break;
                yield return Click("OPEN PROBE OBSERVATORY");
                if(!Require(GetComponent<ProbeObservatory>().IsOpen&&GetComponent<Rigidbody>().isKinematic,"Observatory did not capture local flight"))yield break;
                if(!Require((GetComponentInChildren<Camera>().cullingMask&(1<<31))==0,"Observatory models leaked into the pilot camera"))yield break;
                if(!restart)
                {
                    yield return ClickModal("PLACE SELECTED ITEM");
                    yield return ClickModal("CLEAR DISPLAY SLOT");
                    if(!Require(session.Session.Activities.State.Displays.Count==0,"Clear display did not commit"))yield break;
                    yield return ClickModal("PLACE SELECTED ITEM");
                    for(var displaySlot=1;displaySlot<6;displaySlot++)
                    {
                        yield return ClickModal("NEXT DISPLAY SLOT");yield return ClickModal("NEXT ITEM");yield return ClickModal("PLACE SELECTED ITEM");
                    }
                }
                if(!Require(session.Session.Activities.State.Displays.Count==6,"Observatory display did not save or restore"))yield break;
                var observatoryFolder=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","demo-journey");Directory.CreateDirectory(observatoryFolder);
                ScreenCapture.CaptureScreenshot(Path.Combine(observatoryFolder,"observatory.png"));yield return new WaitForSecondsRealtime(.5f);
                yield return Press(GamepadButton.East);
                if(!restart){yield return ExerciseLibrary();if(failed)yield break;yield return ExerciseImages();if(failed)yield break;yield return ExerciseDiary();if(failed)yield break;yield return ExerciseLiveNotebookFixture();if(failed)yield break;yield return ExerciseCelebrationPresentation();if(failed)yield break;}
            }
            if(Array.Exists(Environment.GetCommandLineArgs(),value=>value=="--clustr-flight-journey"))
            {
                if(!restart){yield return ExerciseFlights();if(failed)yield break;}
                if(!Require(session.Session.Activities.State.Flights.Count==3,"Flight personal bests did not restore"))yield break;
                if(Array.Exists(Environment.GetCommandLineArgs(),value=>value=="--clustr-campaign-journey"))
                {
                    yield return Click("OPEN PROBE OBSERVATORY");
                    if(!restart)
                    {
                        // Keep three keepsakes and the personal constellation; replace a journal slot with a flight record.
                        for(var n=0;n<4;n++)yield return ClickModal("NEXT DISPLAY SLOT");
                        var firstFlight=session.Session.Activities.State.Records.Count(x=>x.Completed&&CampaignContent.Keepsake(x.Id).Length>0)+session.Session.Activities.State.Designs.Count;
                        for(var n=0;n<firstFlight;n++)yield return ClickModal("NEXT ITEM");
                        yield return ClickModal("PLACE SELECTED ITEM");
                    }
                    else for(var n=0;n<4;n++)yield return ClickModal("NEXT DISPLAY SLOT");
                    yield return ClickModal("VIEW DISPLAY REPLAY");
                    if(!Require(GetComponent<JourneyReplayViewer>().IsOpen&&GetComponent<Rigidbody>().isKinematic,"Displayed flight replay did not open safely"))yield break;
                    yield return Press(GamepadButton.East);yield return null;
                    if(!Require(GetComponent<ProbeObservatory>().IsOpen&&GetComponent<Rigidbody>().isKinematic&&EventSystem.current.currentSelectedGameObject?.name=="VIEW DISPLAY REPLAY","Replay did not restore observatory focus and input"))yield break;
                    var displayFolder=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","demo-journey");Directory.CreateDirectory(displayFolder);
                    ScreenCapture.CaptureScreenshot(Path.Combine(displayFolder,"observatory-all-types.png"));yield return new WaitForSecondsRealtime(.5f);
                    yield return Press(GamepadButton.East);
                }
                if(restart)
                {
                    yield return Click("VIEW LATEST FLIGHT RECORDING");
                    yield return new WaitForSecondsRealtime(.3f);
                    if(!Require(GetComponent<JourneyReplayViewer>()?.IsOpen==true,"Flight replay did not reopen after restart"))yield break;
                    yield return Press(GamepadButton.East);
                    yield return ExerciseBestGhost();if(failed)yield break;
                }
            }
            if(expeditionJourney)
            {
                if(!restart){yield return Click("EXPEDITION: QUIET DRIFT");yield return Click("PAUSE EXPEDITION");}
                else if(resume){yield return ExerciseExpeditions();if(failed)yield break;}
                else
                {
                    if(!Require(session.Session.Activities.State.Expedition?.Status==ExpeditionStatus.Completed&&session.Session.Activities.State.Diary.Count(x=>x.Id.StartsWith("expedition:",StringComparison.Ordinal))==1,"Survey expedition card did not restore"))yield break;
                    yield return Click("RETURN TO EXPLORE");
                    if(!Require(session.Session.Activities.State.Expedition?.Status==ExpeditionStatus.Completed&&session.Session.Activities.State.Diary.Count(x=>x.Id.StartsWith("expedition:",StringComparison.Ordinal))==2,"Explore expedition cards did not restore"))yield break;
                    yield return Click("ENTER SURVEY");
                }
            }
            yield return null;
            var state = visibility.SurveySession.State;
            var starfield = UnityEngine.Object.FindAnyObjectByType<InstancedStarfield>();
            if (!Require(state.StarfieldAfterimageUnlocked && starfield != null && starfield.AuthoredAfterimageEnabled,
                "Authored cosmetic was not applied/restored")) yield break;
            if (!Require(state.CompletedContracts.Count == 4 && state.ScannerLevel == 3 && state.CompletedConstellations.Count == 1,
                "Contracts, upgrades or authored constellation incomplete")) yield break;
            if (!Require(state.Discoveries.Contains("easter-egg:signal-afterimage"), "Authored discovery missing")) yield break;
            if (!Require(session.Session.Activities.State.Preferences.AutomaticTuning, "Tuning preference did not restore")) yield break;
            if (!Require(session.Session.Activities.State.Diary.Count(entry => entry.Id.StartsWith("survey:clean-signal:", StringComparison.Ordinal)) == 6,
                "Committed clean tuning journal did not restore")) yield break;
            if (!Require(state.PersonalConstellations.Count == 1 && state.QueuedPersonalConstellationIds.Count == 1,
                "Saved constellation/local queue not restored exactly once")) yield break;
            if (!Require(state.KnownRoutes.Count == 2 && session.Registry.Routes.Count == 2,
                "Authored route progression or renderers missing")) yield break;
            var design = session.Session.Activities.State.Designs.SingleOrDefault();
            if (!Require(design != null && design.Edges.Count == 1 && design.Palette == 1 && design.MusicPreset == 1,
                "Saved constellation design did not restore")) yield break;
            var replayFolder = Path.Combine(Application.persistentDataPath,"ClustrGalaxy","replays");
            var replayFiles = Directory.Exists(replayFolder) ? Directory.GetFiles(replayFolder,"*.clustr-replay") : Array.Empty<string>();
            if (!Require(replayFiles.Length == 1,"Saved replay missing or duplicated after restart")) yield break;
            using (var stream = File.OpenRead(replayFiles[0]))
            {
                var replay = ReplayPackage.Read(stream);
                if(!Require(replay.Events.Any(x=>x.Kind==ReplayEventKind.CommittedDiscovery),"Committed scan annotations did not survive replay save/restart"))yield break;
                var midpoint = replay.At(replay.Duration*.5);
                if (!Require(replay.Poses.Count >= 5 && replay.Poses.Count <= 9000 && midpoint.Seconds >= 0,
                    "Recorded replay could not be restored/interpolated")) yield break;
            }
            if (!Require(ReferenceEquals(state,visibility.SurveySession.State),"Reading a replay mutated Survey progression")) yield break;
            var personal = state.PersonalConstellations.Single();
            if (!Require(personal.Name == "Player journey arc" && personal.Members.Count == 3 &&
                personal.Reason == "Local authored journey evidence", "Saved draft content changed")) yield break;
            var directory = Path.Combine(Application.persistentDataPath, "ClustrGalaxy", "demo-journey");
            Directory.CreateDirectory(directory);
            var phase = resume?"resume":restart ? "restart" : "create";
            ScreenCapture.CaptureScreenshot(Path.Combine(directory, phase + ".png"));
            yield return new WaitForSecondsRealtime(1);
            File.WriteAllText(Path.Combine(directory, phase + ".json"),
                "{\"passed\":true,\"phase\":\"" + phase + "\",\"real_controller\":false,\"range_placement_is_fixture_setup\":true}");
            Debug.Log("[ClustrGalaxy.DemoJourney] PASS " + phase);
            Application.Quit(0);
        }

        private IEnumerator ExerciseFlights()
        {
            var activities=GetComponent<FlightActivitiesController>();var body=GetComponent<Rigidbody>();var origin=GetComponent<FloatingOrigin>();
            var route=session.Session.Resident.LoadedRoutes.First(r=>visibility.SurveySession.State.KnownRoutes.Contains(r.Id));
            targeting.SelectTarget(session.Registry.FindEntity(route.SourceId.Value));yield return null;
            var destination=session.Session.Resident.LoadedEntities.First(e=>e.Id==route.TargetId);
            var destinationLabel=visibility.SurveySession.State.IsIdentified(route.TargetId)?destination.DisplayName:"Unidentified destination";
            var profileLabels=new[]{"BROAD CURRENT","WINDING PASSAGE","EDGE RIBBON"};
            for(var profile=0;profile<3;profile++)
            {
                yield return Click("SCENIC "+profileLabels[profile]+" · 1 · "+destinationLabel);yield return Press(GamepadButton.East);
                var scenic=session.Session.FlightActivity;if(!Require(scenic!=null,"Scenic course failed to start: "+activities.Status))yield break;var scenicDeadline=Time.realtimeSinceStartup+15;
                while(scenic.Active&&Time.realtimeSinceStartup<scenicDeadline)
                {
                    var point=scenic.Target;var offset=origin.LogicalOrigin;
                    body.position=new Vector3((float)(point.X-offset.X),(float)(point.Y-offset.Y),(float)(point.Z-offset.Z));
                    body.linearVelocity=Vector3.zero;Physics.SyncTransforms();yield return new WaitForFixedUpdate();yield return new WaitForSecondsRealtime(.12f);
                }
                if(!Require(scenic.Completed,"Scenic profile fixture did not complete: "+profile))yield break;
                yield return Press(GamepadButton.Start);
            }
            yield return Click("PREVIEW RingRun");yield return Press(GamepadButton.East);
            var deadline=Time.realtimeSinceStartup+70;
            while(session.Session.FlightActivity?.Active==true&&Time.realtimeSinceStartup<deadline)yield return null;
            if(!Require(session.Session.FlightActivity?.Completed==true,"Autopilot ring preview failed"))yield break;
            if(!Require(session.Session.Activities.State.Flights.Count==0,"Preview produced a scored record"))yield break;
            yield return Press(GamepadButton.Start);
            session.Modes.SetFlightMode(FlightControlMode.PilotAssist);
            if(!session.Session.Activities.State.Preferences.SaveBestGhosts)yield return Click("SAVE PERSONAL-BEST GHOSTS: OFF");
            foreach(var kind in new[]{FlightActivityKind.RingRun,FlightActivityKind.FigureEight,FlightActivityKind.StableOrbit})
            {
                yield return Click("START "+kind);yield return Press(GamepadButton.East);
                var run=session.Session.FlightActivity;deadline=Time.realtimeSinceStartup+30;var angle=0d;
                while(run.Active&&Time.realtimeSinceStartup<deadline)
                {
                    var p=run.Target;
                    if(kind==FlightActivityKind.StableOrbit)
                    {var first=run.Course.Points[0];p=new ReplayPosition(first.X+Math.Sin(angle)*240,first.Y,first.Z-240+Math.Cos(angle)*240);angle+=Time.fixedDeltaTime*.2;}
                    var logical=origin.LogicalOrigin;body.position=new Vector3((float)(p.X-logical.X),(float)(p.Y-logical.Y),(float)(p.Z-logical.Z));
                    body.linearVelocity=Vector3.zero;Physics.SyncTransforms();yield return new WaitForFixedUpdate();
                    if(kind!=FlightActivityKind.StableOrbit)yield return new WaitForSecondsRealtime(.12f);
                }
                if(!Require(run.Completed&&!run.Preview,"Playground fixture did not complete: "+kind))yield break;
                if(!Require(session.Session.FlightError.Length==0,"Flight record commit failed"))yield break;
                yield return Press(GamepadButton.Start);
            }
            yield return Click("SAVE COMPLETED FLIGHT RECORDING");
            if(!Require(!string.IsNullOrEmpty(activities.LastGhostPath)&&File.Exists(activities.LastGhostPath),"Flight recording export failed"))yield break;
            var state=visibility.SurveySession.State;
            yield return Click("VIEW LATEST FLIGHT RECORDING");yield return new WaitForSecondsRealtime(.3f);
            var viewer=GetComponent<JourneyReplayViewer>();
            if(!Require(viewer!=null&&viewer.IsOpen&&viewer.PlaybackSeconds>0&&body.isKinematic,"Replay viewer did not capture input or advance"))yield break;
            var folder=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","demo-journey");Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"flight-replay.png"));yield return new WaitForSecondsRealtime(.5f);
            yield return Press(GamepadButton.East);
            if(!Require(!viewer.IsOpen&&!body.isKinematic&&ReferenceEquals(state,visibility.SurveySession.State),"Replay close changed flight context or Survey"))yield break;
            yield return ExerciseBestGhost();yield return ExerciseImportedGhost();
        }
        private IEnumerator ExerciseImportedGhost()
        {
            var before=session.Session.Activities.State;var surveyBefore=visibility.SurveySession.State;
            var source=GetComponent<FlightActivitiesController>().LastGhostPath;
            yield return Click("OPEN LOCAL CREATIONS");
            foreach(var field in GetComponentsInChildren<InputField>())if(field.name=="Local replay import path")field.text=source;
            yield return ClickModal("IMPORT LOCAL REPLAY");
            var root=Path.Combine(Application.persistentDataPath,"ClustrGalaxy");var files=new LocalReplayLibrary(root).Files();
            var index=Array.FindIndex(files,x=>Path.GetDirectoryName(x)==Path.Combine(root,"imported"));
            if(!Require(index>=0,"Imported course package not listed"))yield break;
            for(var i=0;i<index;i++)yield return ClickModal("NEXT FILE");
            yield return ClickModal("USE SELECTED PRACTICE GHOST");
            if(!Require(session.Session.SelectedPracticeGhost!=null,"Compatible imported ghost was rejected"))yield break;
            yield return ClickModal("CLOSE LIBRARY");yield return Click("START StableOrbit");
            if(!Require(session.Session.FlightActivity.BestGhost==session.Session.SelectedPracticeGhost,"Imported ghost did not attach to matching practice"))yield break;
            yield return Click("END FLIGHT ACTIVITY");
            if(!Require(ReferenceEquals(before,session.Session.Activities.State)&&ReferenceEquals(surveyBefore,visibility.SurveySession.State),"Import/practice selection changed progression"))yield break;
            session.Session.ClearPracticeGhost();
        }
        private IEnumerator ProfileUiInteractions()
        {
            Application.targetFrameRate=120;
            var state=visibility.SurveySession.State;var activity=session.Session.Activities.State;
            if(!Require(state.PersonalConstellations.Count>0&&activity.Displays.Count==6,"UI performance requires the completed fixture profile"))yield break;
            var saved=state.PersonalConstellations[0];
            yield return Click("SAVED: "+saved.Name+(state.QueuedPersonalConstellationIds.Contains(saved.Id)?" — QUEUED LOCALLY":""));
            var monitor=GetComponent<GalaxyPerformanceMonitor>();var allPassed=true;
            foreach(var scenario in new[]{"constellation-editor-entry-interaction-exit","observatory-entry-interaction-exit"})
            {
                monitor.PrepareCapture(40);yield return new WaitForSecondsRealtime(5);
                monitor.BeginCapture();var started=Time.realtimeSinceStartup;
                yield return ClickOpenMenu(scenario.StartsWith("constellation",StringComparison.Ordinal)?"EDIT SAVED CONSTELLATION DESIGN":"OPEN PROBE OBSERVATORY");
                var studio=scenario.StartsWith("constellation",StringComparison.Ordinal);var step=0;var paletteMask=0;var instrumentMask=0;
                if(studio)yield return Press(GamepadButton.DpadRight);
                while((Time.realtimeSinceStartup-started<30||monitor.CapturedSeconds<30)&&!monitor.SampleOverflow)
                {
                    if(!Require(GetComponent<Rigidbody>().isKinematic,"UI performance leaked flight input"))yield break;
                    if(studio)
                    {
                        InputSystem.QueueStateEvent(controller,new GamepadState{rightStick=new Vector2(step%2==0?.5f:-.5f,0)});
                        yield return new WaitForSecondsRealtime(.4f);InputSystem.QueueStateEvent(controller,new GamepadState());
                        yield return ClickModal("NEXT COLOR PALETTE");yield return ClickModal("NEXT MUSIC PRESET");
                        var draft=GetComponent<ConstellationStudio>().Draft;paletteMask|=1<<draft.Palette;instrumentMask|=1<<draft.MusicPreset;
                    }
                    else {yield return ClickModal("NEXT DISPLAY SLOT");yield return ClickModal("NEXT ITEM");yield return new WaitForSecondsRealtime(.4f);}
                    step++;
                }
                yield return Press(GamepadButton.East);yield return null;
                monitor.EndCapture();var drain=Time.realtimeSinceStartup+2;
                while(!monitor.GpuDrainComplete&&Time.realtimeSinceStartup<drain)yield return null;
                monitor.FinishCapture();
                var unchanged=ReferenceEquals(state,visibility.SurveySession.State)&&ReferenceEquals(activity,session.Session.Activities.State);
                var report=InteractionPerformanceReport.Capture(scenario,monitor,!failed&&unchanged&&step>5&&(!studio||(paletteMask==63&&instrumentMask==7)));
                report.interaction_steps=step;report.palette_mask=paletteMask;report.instrument_mask=instrumentMask;
                report.failures=report.Evaluate();report.measurements_passed=report.failures.Length==0;allPassed&=report.measurements_passed;
                string writeError=null;
                try{ClustrGalaxy.Core.Diagnostics.QualificationReportStore.Write(Path.Combine(Application.persistentDataPath,"ClustrGalaxy","ui-performance",scenario),JsonUtility.ToJson(report,true));}
                catch(Exception error){writeError=error.Message;}
                if(writeError!=null){Debug.LogError("[ClustrGalaxy.UiPerformance] Report write failed: "+writeError);Application.Quit(3);yield break;}
                Debug.Log("[ClustrGalaxy.UiPerformance] "+scenario+" functional="+report.functional_passed+" measurements="+report.measurements_passed+" failures="+string.Join("; ",report.failures));
            }
            Application.Quit(allPassed?0:2);
        }

        private IEnumerator ExerciseSeparateProfileImport()
        {
            const string prefix="--clustr-import-fixture-path=";
            var argument=Array.Find(Environment.GetCommandLineArgs(),value=>value.StartsWith(prefix,StringComparison.Ordinal));
            if(!Require(argument!=null,"Import fixture source missing"))yield break;
            var source=argument.Substring(prefix.Length);var before=session.Session.Activities.State;var surveyBefore=session.Session.Survey;
            var residentCount=session.Session.Resident.LoadedEntities.Count;
            if(!Require(before.Identity.ProfileId=="replay-recipient"&&surveyBefore==null&&before.Diary.Count==0&&before.Records.Count==0,"Recipient profile was not fresh"))yield break;
            var root=Path.Combine(Application.persistentDataPath,"ClustrGalaxy");var bad=Path.Combine(root,"invalid-fixture.clustr-replay");Directory.CreateDirectory(root);File.WriteAllText(bad,"not a replay");
            yield return Click("OPEN LOCAL CREATIONS");
            foreach(var field in GetComponentsInChildren<InputField>())if(field.name=="Local replay import path")field.text=bad;
            yield return ClickModal("IMPORT LOCAL REPLAY");
            if(!Require(GetComponentsInChildren<Text>().Any(x=>x.text.Contains("Import rejected:"))&&!Directory.Exists(Path.Combine(root,"imported")),"Malformed import changed library or gave no explanation"))yield break;
            foreach(var field in GetComponentsInChildren<InputField>())if(field.name=="Local replay import path")field.text=bad+"-missing";
            yield return ClickModal("IMPORT LOCAL REPLAY");
            if(!Require(GetComponentsInChildren<Text>().Any(x=>x.text.Contains("Import rejected:")),"Missing import gave no explanation"))yield break;
            foreach(var field in GetComponentsInChildren<InputField>())if(field.name=="Local replay import path")field.text=source;
            yield return ClickModal("IMPORT LOCAL REPLAY");
            var imported=Directory.GetFiles(Path.Combine(root,"imported"),"*.clustr-replay");
            if(!Require(imported.Length==1,"Recipient import did not produce one declarative package"))yield break;
            yield return ClickModal("VIEW SELECTED ITEM");yield return new WaitForSecondsRealtime(.4f);
            var viewer=GetComponent<JourneyReplayViewer>();
            if(!Require(viewer.IsOpen&&viewer.PlaybackSeconds>0&&GetComponent<Rigidbody>().isKinematic,"Recipient standalone playback failed"))yield break;
            var folder=Path.Combine(root,"demo-journey");Directory.CreateDirectory(folder);ScreenCapture.CaptureScreenshot(Path.Combine(folder,"recipient-replay.png"));yield return new WaitForSecondsRealtime(.5f);
            yield return Press(GamepadButton.East);yield return null;
            if(!Require(GetComponent<LocalCreationLibrary>().IsOpen&&EventSystem.current.currentSelectedGameObject?.name=="VIEW SELECTED ITEM","Recipient playback lost library focus"))yield break;
            yield return ClickModal("USE SELECTED PRACTICE GHOST");
            if(!Require(session.Session.SelectedPracticeGhost==null,"Incompatible Survey recording became an Explore practice ghost"))yield break;
            yield return Press(GamepadButton.East);
            var exports=Path.Combine(root,"exports");Directory.CreateDirectory(exports);var brokenImage=Path.Combine(exports,"invalid-fixture.png");File.WriteAllText(brokenImage,"not a PNG");
            yield return Click("OPEN LOCAL CREATIONS");
            for(var i=0;i<4&&GetComponent<LocalCreationLibrary>().SelectedPath!=brokenImage;i++)yield return ClickModal("NEXT FILE");
            if(!Require(GetComponent<LocalCreationLibrary>().SelectedPath==brokenImage,"Corrupt-image fixture selection failed"))yield break;
            yield return ClickModal("VIEW SELECTED ITEM");
            if(!Require(GetComponentsInChildren<Text>().Any(x=>x.text.Contains("Cannot view this file:"))&&GetComponent<LocalImageViewer>()?.IsOpen!=true,"Corrupt image did not remain recoverable in library"))yield break;
            yield return Press(GamepadButton.East);File.Delete(brokenImage);File.Delete(bad);
            if(!Require(ReferenceEquals(before,session.Session.Activities.State)&&ReferenceEquals(surveyBefore,session.Session.Survey)&&residentCount==session.Session.Resident.LoadedEntities.Count,"Imported playback altered progression or residency"))yield break;
            File.WriteAllText(Path.Combine(folder,"import-only.json"),"{\"passed\":true,\"different_profile\":true,\"progression_unchanged\":true,\"media_failures_recoverable\":true}");
            Debug.Log("[ClustrGalaxy.DemoJourney] PASS import-only");Application.Quit(0);
        }

        private IEnumerator ExerciseCelebrationPresentation()
        {
            var effects=GetComponent<ExplorationCelebrations>();var body=GetComponent<Rigidbody>();var anchor=session.Registry.FindEntity(members[0]);var before=session.Session.Activities.State;var surveyBefore=visibility.SurveySession.State;
            hud.SetMenuOpen(true);
            for(var i=0;i<35;i++){var angle=i*.1f;body.position=anchor.transform.position+new Vector3(Mathf.Sin(angle)*300,anchor.SurfaceRadius+800,-450+Mathf.Cos(angle)*100);body.rotation=Quaternion.LookRotation(anchor.transform.position-body.position);body.linearVelocity=Vector3.zero;Physics.SyncTransforms();yield return new WaitForSecondsRealtime(.1f);}
            var folder=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","demo-journey");Directory.CreateDirectory(folder);
            var canvases=GetComponentsInChildren<Canvas>().Where(x=>x.enabled).ToArray();
            foreach(var canvas in canvases)canvas.enabled=false;
            foreach(var kind in new[]{CelebrationKind.Discovery,CelebrationKind.SystemRoutes,CelebrationKind.Constellation,CelebrationKind.FlightRibbon})
            {
                var bounds=new Bounds(anchor.transform.position,Vector3.one*600);
                if(kind==CelebrationKind.Constellation||kind==CelebrationKind.SystemRoutes)
                    foreach(var id in members.Take(3))bounds.Encapsulate(session.Registry.FindEntity(id).transform.position);
                var exclusion=(float)(anchor.GetComponent<ClustrGalaxy.Simulation.GravitySource>()?.Definition?.ExclusionRadius??anchor.SurfaceRadius);
                var distance=Mathf.Max(exclusion+800,bounds.extents.magnitude*2.5f);
                var capturePosition=bounds.center+new Vector3(0,distance*.6f,-distance);
                for(var attempt=0;attempt<32&&!ClustrGalaxy.Flight.ShipSurfaceSafety.IsClear(capturePosition);attempt++)capturePosition+=Vector3.up*1000;
                if(!Require(ClustrGalaxy.Flight.ShipSurfaceSafety.IsClear(capturePosition),"Celebration fixture has no safe camera placement"))yield break;
                body.position=capturePosition;
                body.rotation=Quaternion.LookRotation(bounds.center-body.position);Physics.SyncTransforms();
                yield return new WaitForFixedUpdate();yield return null;
                var ids=kind==CelebrationKind.Constellation?members.Take(3).Select(x=>(GalaxyEntityId)x):new[]{(GalaxyEntityId)members[0]};
                effects.Show(new CelebrationCue("presentation-fixture-"+kind,kind,ids));yield return null;yield return null;
                if(!Require(effects.ActiveCount>0&&effects.ActiveCount<=3,"Celebration presentation unavailable: "+kind))yield break;
                var positions=new Vector3[3];var active=new bool[3];for(var i=0;i<3;i++)active[i]=effects.TryGetActivePosition(i,out positions[i]);
                var shift=new Vector3(-1000,0,500);GetComponent<FloatingOrigin>().Rebase(shift);
                for(var i=0;i<3;i++)if(active[i]&&effects.TryGetActivePosition(i,out var next)&&!Require(Vector3.Distance(next,positions[i]+shift)<.01f,"Active celebration did not follow origin shift"))yield break;
                yield return new WaitForSecondsRealtime(.6f);
                ScreenCapture.CaptureScreenshot(Path.Combine(folder,"celebration-"+kind+".png"));yield return new WaitForSecondsRealtime(2.2f);
            }
            foreach(var canvas in canvases)canvas.enabled=true;
            if(!Require(ReferenceEquals(before,session.Session.Activities.State)&&ReferenceEquals(surveyBefore,visibility.SurveySession.State),"Presentation fixture awarded progression"))yield break;
        }
        private IEnumerator ExerciseLiveNotebookFixture()
        {
            var before=session.Session.Activities.State;var surveyBefore=visibility.SurveySession.State;
            var identity=new ActivityIdentity("notebook-fixture",new ClustrGalaxy.Core.Survey.SurveySnapshotId(9,3,"provider-fixture"),ActivityWorldMode.Explore);
            var notes=new ActivitySession(new MemoryActivityRepository(),identity,AuthoredActivityCatalog.Definitions);
            for(var i=0;i<16;i++)notes.Apply(new ActivityIntent("old"+i,identity,ActivityOperation.SaveBookmark,DateTime.UtcNow,value:"Personal fixture observation "+i));
            var notebook=GetComponent<CampaignNotebook>();notebook.Open(notes);yield return null;
            yield return ClickModal("PREVIOUS");yield return ClickModal("NEXT");
            foreach(var field in GetComponentsInChildren<InputField>())if(field.name=="Optional written explanation")field.text="My personal interpretation of a loaded connection.";
            yield return ClickModal("SAVE PERSONAL NOTE");yield return ClickModal("SAVE PERSONAL NOTE");
            if(!Require(notes.State.Diary.Count==17&&notes.State.Diary.Last().Retained,"Live notebook did not save exactly one retained note beyond demo page count"))yield break;
            var directory=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","demo-journey");Directory.CreateDirectory(directory);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"live-notebook-fixture.png"));yield return new WaitForSecondsRealtime(.5f);
            yield return ClickModal("BROWSE SAVED NOTES");yield return Press(GamepadButton.East);yield return null;
            if(!Require(notebook.IsOpen&&GetComponent<Rigidbody>().isKinematic,"Saved-note diary did not return to the live notebook"))yield break;
            yield return Press(GamepadButton.East);
            if(!Require(ReferenceEquals(before,session.Session.Activities.State)&&ReferenceEquals(surveyBefore,visibility.SurveySession.State),"Injected notebook mutated gameplay state"))yield break;
        }
        private IEnumerator ExerciseExpeditions()
        {
            var before=visibility.SurveySession.State;
            if(!Require(session.Session.Activities.State.Expedition?.Status==ExpeditionStatus.Paused,"Paused expedition did not survive process restart"))yield break;
            yield return Click("RESUME EXPEDITION");yield return VisitExpeditionStops();if(failed)yield break;
            var afterQuiet=session.Session.Activities.State;
            yield return Click("EXPEDITION: UNEXPECTED CONNECTION");
            if(!Require(ReferenceEquals(afterQuiet,session.Session.Activities.State),"Unavailable Survey connection changed saved activity state"))yield break;
            yield return Click("RETURN TO EXPLORE");yield return Click("EXPEDITION: UNEXPECTED CONNECTION");yield return VisitExpeditionStops();if(failed)yield break;
            yield return Click("EXPEDITION: PILOT PRACTICE");session.Modes.SetFlightMode(FlightControlMode.PilotAssist);
            var origin=GetComponent<FloatingOrigin>();var body=GetComponent<Rigidbody>();
            foreach(var kind in new[]{FlightActivityKind.RingRun,FlightActivityKind.FigureEight,FlightActivityKind.StableOrbit})
            {
                yield return Click("START NEXT PRACTICE CHALLENGE");hud.SetMenuOpen(false);var run=session.Session.FlightActivity;var deadline=Time.realtimeSinceStartup+35;var angle=0d;
                while(run.Active&&Time.realtimeSinceStartup<deadline)
                {
                    var point=run.Target;if(kind==FlightActivityKind.StableOrbit){var first=run.Course.Points[0];point=new ReplayPosition(first.X+Math.Sin(angle)*240,first.Y,first.Z-240+Math.Cos(angle)*240);angle+=Time.fixedDeltaTime*.2;}
                    var offset=origin.LogicalOrigin;body.position=new Vector3((float)(point.X-offset.X),(float)(point.Y-offset.Y),(float)(point.Z-offset.Z));body.linearVelocity=Vector3.zero;Physics.SyncTransforms();yield return new WaitForFixedUpdate();if(kind!=FlightActivityKind.StableOrbit)yield return new WaitForSecondsRealtime(.12f);
                }
                if(!Require(run.Completed&&session.Session.FlightError.Length==0,"Expedition practice did not commit: "+kind))yield break;
            }
            if(!Require(session.Session.Activities.State.Expedition.Status==ExpeditionStatus.ReadyToClose,"Pilot expedition did not await intentional closing"))yield break;
            yield return Click("CLOSE JOURNEY AND SAVE JOURNAL CARD");
            if(!Require(session.Session.Activities.State.Diary.Count(x=>x.Id.StartsWith("expedition:",StringComparison.Ordinal))==2&&ReferenceEquals(before,visibility.SurveySession.State),"Expedition cards or Survey isolation failed"))yield break;
            yield return Click("ENTER SURVEY");
        }
        private IEnumerator VisitExpeditionStops()
        {
            var trip=session.Session.Activities.State.Expedition;
            if(!Require(trip?.Status==ExpeditionStatus.Active,"Expedition did not start"))yield break;
            var body=GetComponent<Rigidbody>();
            while(trip.Status==ExpeditionStatus.Active)
            {
                var current=trip.Current;var target=session.Registry.FindEntity(trip.Plan.Stops[current].Value);
                if(!Require(target!=null&&target.gameObject.activeInHierarchy,"Expedition selected unavailable target"))yield break;
                hud.SetMenuOpen(false);var deadline=Time.realtimeSinceStartup+3;
                while(session.Session.Activities.State.Expedition.Current==current&&Time.realtimeSinceStartup<deadline)
                {body.position=target.transform.position+Vector3.up*((float)(target.GetComponent<ClustrGalaxy.Simulation.GravitySource>()?.Definition?.ExclusionRadius??target.SurfaceRadius)+100);body.linearVelocity=Vector3.zero;Physics.SyncTransforms();yield return new WaitForFixedUpdate();}
                trip=session.Session.Activities.State.Expedition;if(!Require(trip.Current>current,"Eligible expedition stop did not advance"))yield break;
            }
            if(!Require(trip.Status==ExpeditionStatus.ReadyToClose,"Expedition did not await closing"))yield break;
            yield return Click("CLOSE JOURNEY AND SAVE JOURNAL CARD");
        }
        private IEnumerator ExerciseDiary()
        {
            var surveyBefore=visibility.SurveySession.State;yield return Click("OPEN PROBE OBSERVATORY");yield return ClickModal("BROWSE JOURNEY DIARY");
            var diary=GetComponent<JourneyDiary>();if(!Require(diary.IsOpen&&GetComponent<Rigidbody>().isKinematic,"Diary did not suspend flight"))yield break;
            foreach(var field in GetComponentsInChildren<InputField>())if(field.name=="Bookmark text")field.text="A quiet lap around the observatory lights.";
            yield return ClickModal("SAVE BOOKMARK");
            if(!Require(session.Session.Activities.State.Diary.Any(x=>x.Retained&&x.Text=="A quiet lap around the observatory lights."),"Bookmark did not commit"))yield break;
            yield return ClickModal("REMOVE SAVED BOOKMARK");yield return ClickModal("CANCEL REMOVAL");
            if(!Require(session.Session.Activities.State.Diary.Any(x=>x.Retained&&x.Text=="A quiet lap around the observatory lights."),"Cancelled bookmark removal lost the saved moment"))yield break;
            yield return ClickModal("EXPORT DIARY POSTCARD");if(!Require(File.Exists(diary.LastExportPath),"Diary postcard export failed"))yield break;
            var directory=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","demo-journey");Directory.CreateDirectory(directory);ScreenCapture.CaptureScreenshot(Path.Combine(directory,"diary.png"));yield return new WaitForSecondsRealtime(.5f);
            yield return Press(GamepadButton.East);yield return null;
            if(!Require(GetComponent<ProbeObservatory>().IsOpen&&GetComponent<Rigidbody>().isKinematic&&EventSystem.current.currentSelectedGameObject?.name=="BROWSE JOURNEY DIARY","Diary did not return to observatory focus"))yield break;
            yield return Press(GamepadButton.East);
            if(!Require(ReferenceEquals(surveyBefore,visibility.SurveySession.State),"Personal diary changed Survey progression"))yield break;
        }
        private IEnumerator ExerciseImages()
        {
            var before=session.Session.Activities.State;var surveyBefore=visibility.SurveySession.State;
            var root=Path.Combine(Application.persistentDataPath,"ClustrGalaxy");
            yield return Click("CAPTURE SCREENSHOT");yield return new WaitForSecondsRealtime(.3f);
            yield return ClickModal("DISCARD IMAGE");
            if(!Require(!Directory.Exists(Path.Combine(root,"images")),"Discarded screenshot wrote a permanent image"))yield break;
            yield return Click("CAPTURE SCREENSHOT");yield return new WaitForSecondsRealtime(.3f);yield return ClickModal("SAVE IMAGE");
            var preview=GetComponent<LocalImageViewer>();var path=preview.LastSavedPath;
            if(!Require(path!=null&&File.Exists(path),"Screenshot save failed"))yield break;
            var size=LocalImageStore.Dimensions(File.ReadAllBytes(path));
            if(!Require(size.Width==Screen.width&&size.Height==Screen.height,"Screenshot dimensions do not match rendered view"))yield break;
            yield return ClickModal("CLOSE IMAGE");yield return Click("OPEN LOCAL CREATIONS");
            var imageIndex=Array.IndexOf(new LocalReplayLibrary(root).Files(),path);for(var i=0;i<imageIndex;i++)yield return ClickModal("NEXT FILE");yield return ClickModal("VIEW SELECTED ITEM");
            if(!Require(preview.IsOpen&&GetComponent<Rigidbody>().isKinematic,"Saved image did not open with flight suppressed"))yield break;
            yield return Press(GamepadButton.East);
            if(!Require(GetComponent<LocalCreationLibrary>().IsOpen&&GetComponent<Rigidbody>().isKinematic,"Image viewer lost the originating library"))yield break;
            yield return ClickModal("REQUEST DELETE SELECTED");yield return ClickModal("CANCEL DELETE");
            if(!Require(File.Exists(path),"Image deletion cancellation removed the file"))yield break;
            yield return ClickModal("CLOSE LIBRARY");
            if(!Require(ReferenceEquals(before,session.Session.Activities.State)&&ReferenceEquals(surveyBefore,visibility.SurveySession.State),"Image capture/view changed progression"))yield break;
        }
        private IEnumerator ExerciseLibrary()
        {
            var before=session.Session.Activities.State;var surveyBefore=visibility.SurveySession.State;
            yield return Click("OPEN LOCAL CREATIONS");
            var library=GetComponent<LocalCreationLibrary>();if(!Require(library.IsOpen&&GetComponent<Rigidbody>().isKinematic,"Library did not capture flight"))yield break;
            var root=Path.Combine(Application.persistentDataPath,"ClustrGalaxy");
            var source=Directory.GetFiles(Path.Combine(root,"replays"),"*.clustr-replay").Single();
            foreach(var field in GetComponentsInChildren<InputField>())if(field.name=="Local replay import path")field.text=source;
            yield return ClickModal("IMPORT LOCAL REPLAY");
            var imported=Directory.GetFiles(Path.Combine(root,"imported"),"*.clustr-replay");if(!Require(imported.Length==1,"Local replay import failed"))yield break;
            var importedIndex=Array.IndexOf(new LocalReplayLibrary(root).Files(),imported[0]);for(var i=0;i<importedIndex;i++)yield return ClickModal("NEXT FILE");yield return ClickModal("REQUEST DELETE SELECTED");yield return ClickModal("CANCEL DELETE");
            if(!Require(File.Exists(imported[0]),"Cancelled deletion removed the imported replay"))yield break;
            yield return ClickModal("VIEW SELECTED ITEM");
            if(!Require(GetComponent<JourneyReplayViewer>()?.IsOpen==true,"Imported replay did not open"))yield break;
            yield return ClickModal("PLAY / PAUSE");var replayViewer=GetComponent<JourneyReplayViewer>();
            var playbackBefore=replayViewer.PlaybackSeconds;yield return Press(GamepadButton.DpadUp);yield return Press(GamepadButton.DpadRight);
            if(!Require(replayViewer.PlaybackSeconds>playbackBefore,"Controller timeline seeking failed"))yield break;
            yield return Press(GamepadButton.DpadDown);yield return ClickModal("SPEED: 1×");
            if(!Require(replayViewer.PlaybackSpeed==2,"Replay speed control failed"))yield break;
            yield return ClickModal("EXPORT POSTCARD");if(!Require(File.Exists(replayViewer.LastExportPath),"Replay postcard export failed"))yield break;
            yield return Press(GamepadButton.East);
            if(!Require(library.IsOpen&&GetComponent<Rigidbody>().isKinematic&&EventSystem.current.currentSelectedGameObject.name=="VIEW SELECTED ITEM","Replay did not restore originating library focus"))yield break;
            yield return ClickModal("REQUEST DELETE SELECTED");yield return ClickModal("CONFIRM DELETE");
            if(!Require(!File.Exists(imported[0])&&File.Exists(source),"Explicit deletion affected the wrong replay"))yield break;
            yield return Press(GamepadButton.East);
            if(!Require(ReferenceEquals(before,session.Session.Activities.State)&&ReferenceEquals(surveyBefore,visibility.SurveySession.State),"Replay import/view/delete changed progression"))yield break;
        }
        private IEnumerator ExerciseCampaign()
        {
            var surveyBefore=visibility.SurveySession.State;
            hud.SetMenuOpen(false);
            foreach(var clue in CampaignContent.Observations)
            {
                if(!CampaignContent.Eligible(session.Session.Activities.State,clue))continue;
                var entity=session.Registry.FindEntity(clue.Anchor.Value);
                if(!Require(entity!=null&&entity.gameObject.activeInHierarchy,"Campaign anchor unavailable: "+clue.Anchor))yield break;
                var until=Time.realtimeSinceStartup+6;
                while(CampaignContent.Eligible(session.Session.Activities.State,clue)&&Time.realtimeSinceStartup<until)
                {
                    // Fixture placement tests real observation timing; not manual campaign travel duration.
                    var point = GetComponent<CampaignController>().ObservationPoint(clue);
                    var outward = point - entity.transform.position;
                    if (outward.sqrMagnitude < 1) outward = new Vector3(1, .8f, -1);
                    outward.Normalize();
                    var radius = (float)CampaignController.SafetyRadius(entity);
                    var rigid=GetComponent<Rigidbody>();
                    rigid.position = point == entity.transform.position ? point + outward * (radius + 100) : point + outward * 100;
                    if (!Require(Vector3.Distance(rigid.position, entity.transform.position) > radius + 2, "Campaign fixture intersects safety surface")) yield break;
                    rigid.rotation=Quaternion.LookRotation(point-rigid.position,Vector3.up);rigid.linearVelocity=Vector3.zero;
                    Physics.SyncTransforms();yield return new WaitForFixedUpdate();
                }
                if(!Require(!CampaignContent.Eligible(session.Session.Activities.State,clue),"Campaign observation did not commit: "+clue.Id))yield break;
            }
            hud.SetMenuOpen(true);yield return null;
            yield return Click("OPEN CAMPAIGN NOTEBOOK");
            if(!Require(GetComponent<CampaignNotebook>().IsOpen&&GetComponent<Rigidbody>().isKinematic,"Notebook did not suspend local flight"))yield break;
            for(var i=0;i<9;i++)yield return ClickModal("NEXT");
            var existingBoards=session.Session.Activities.State.EvidenceBoards.Count;
            yield return ClickModal("OPEN EVIDENCE BOARD");
            if(existingBoards==0)
            {
                var boardEditor=GetComponent<EvidenceBoardEditor>();var beforeX=boardEditor.Draft.Nodes[0].X;
                InputSystem.QueueStateEvent(controller,new GamepadState{rightStick=Vector2.right});yield return new WaitForSecondsRealtime(.25f);
                InputSystem.QueueStateEvent(controller,new GamepadState());yield return null;
                if(!Require(boardEditor.Draft.Nodes[0].X>beforeX&&GetComponent<Rigidbody>().isKinematic,"Evidence card movement failed or leaked flight input"))yield break;
                yield return ClickModal("CONNECT TO NEXT CARD");
            }
            yield return ClickModal("A shared greeting");
            var boardFolder=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","demo-journey");Directory.CreateDirectory(boardFolder);
            ScreenCapture.CaptureScreenshot(Path.Combine(boardFolder,"evidence-board.png"));yield return new WaitForSecondsRealtime(.5f);yield return ClickModal("CLOSE / DISCARD DRAFT");
            yield return ClickModal("NEXT");yield return ClickModal("OPEN EVIDENCE BOARD");if(existingBoards==0)yield return ClickModal("CONNECT TO NEXT CARD");yield return ClickModal("A welcome for wanderers");yield return ClickModal("CLOSE / DISCARD DRAFT");
            if(!Require(session.Session.Activities.State.EvidenceBoards.Count==2,"Evidence boards did not commit and restore"))yield break;
            var folder=Path.Combine(Application.persistentDataPath,"ClustrGalaxy","demo-journey");Directory.CreateDirectory(folder);
            ScreenCapture.CaptureScreenshot(Path.Combine(folder,"campaign.png"));yield return new WaitForSecondsRealtime(.5f);
            yield return Press(GamepadButton.East);
            if(!Require(!GetComponent<CampaignNotebook>().IsOpen&&ReferenceEquals(surveyBefore,visibility.SurveySession.State),"Campaign mutated Survey progression or failed to close"))yield break;
        }
        private IEnumerator ExerciseBestGhost()
        {
            var activities=GetComponent<FlightActivitiesController>();var before=session.Session.Activities.State;
            yield return Click("START RingRun");
            if(!Require(session.Session.FlightActivity.BestGhost!=null,"Compatible personal-best ghost did not restore"))yield break;
            yield return Press(GamepadButton.East);yield return new WaitForSecondsRealtime(.15f);
            if(!Require(activities.GhostDisplayed,"Best ghost was not rendered during practice"))yield break;
            yield return Press(GamepadButton.Start);yield return null;
            // Compare one replay instant in both frames. Command menus intentionally
            // keep physics running, so comparing successive moving poses is invalid.
            var stationLease=hud.AcquireStation();
            try
            {
                yield return new WaitForEndOfFrame();
                if(!Require(activities.GhostDisplayed,"Ghost expired before the rebase check"))yield break;
                var ghostBefore=activities.GhostPosition;var shift=-transform.position;
                GetComponent<FloatingOrigin>().Rebase(shift);yield return new WaitForEndOfFrame();
                if(!Require(activities.GhostDisplayed&&Vector3.Distance(activities.GhostPosition,ghostBefore+shift)<.5f,
                    "Best ghost did not follow the floating origin at the same replay time"))yield break;
            }
            finally { hud.ReleaseStation(stationLease); }
            yield return Click("HIDE PERSONAL-BEST GHOST");yield return null;
            if(!Require(!activities.GhostDisplayed,"Best ghost hide control failed"))yield break;
            yield return Click("SHOW PERSONAL-BEST GHOST");yield return null;
            var activePractice=session.Session.FlightActivity;
            if(!Require(activities.GhostVisible&&(activities.GhostDisplayed||activePractice.Elapsed>activePractice.BestGhost.Replay.Duration),
                "Best ghost show control failed before recording expiry"))yield break;
            // Menus leave flight running. A short fixture ghost may finish during focus traversal.
            // Start another practice to prove that the restored preference renders its available interval.
            yield return Click("START RingRun");yield return Press(GamepadButton.East);yield return null;
            if(!Require(activities.GhostDisplayed,"Shown ghost did not render in fresh practice"))yield break;
            yield return Click("END FLIGHT ACTIVITY");
            if(!Require(ReferenceEquals(before,session.Session.Activities.State),"Ghost practice/abandon changed saved progress"))yield break;
        }

        private IEnumerator ExerciseStudio()
        {
            var studio = GetComponent<ConstellationStudio>();
            var body = GetComponent<Rigidbody>();
            if (!Require(studio != null && body.isKinematic, "Studio did not capture flight")) yield break;
            var before = studio.Draft.Nodes[0].X;
            InputSystem.QueueStateEvent(controller,new GamepadState { rightStick = Vector2.right });
            yield return new WaitForSecondsRealtime(.25f);
            InputSystem.QueueStateEvent(controller,new GamepadState()); yield return null;
            if (!Require(Mathf.Abs(studio.Draft.Nodes[0].X-before) > .01f,"Controller did not move design node")) yield break;
            yield return Press(GamepadButton.DpadRight);
            yield return Press(GamepadButton.DpadDown);
            if (!Focused("LINK / UNLINK SELECTED")) yield break;
            yield return Press(GamepadButton.South);
            yield return Press(GamepadButton.DpadLeft);
            yield return Press(GamepadButton.RightShoulder);
            yield return Press(GamepadButton.South);
            if (!Require(studio.Draft.Edges.Count == 1,"Controller did not create personal connection")) yield break;
            yield return Press(GamepadButton.DpadRight);
            yield return Press(GamepadButton.DpadDown); yield return Press(GamepadButton.DpadDown);
            if (!Focused("NEXT COLOR PALETTE")) yield break;
            yield return Press(GamepadButton.South); yield return Press(GamepadButton.DpadDown);
            if (!Focused("NEXT MUSIC PRESET")) yield break;
            yield return Press(GamepadButton.South);
            for (var i = 0; i < 3; i++) yield return Press(GamepadButton.DpadUp);
            if (!Focused("SAVE DESIGN")) yield break;
            yield return Press(GamepadButton.South);
            if (!Require(session.Session.Activities.State.Designs.Count == 1,"Design save did not commit")) yield break;
            for (var i = 0; i < 4; i++) yield return Press(GamepadButton.DpadDown);
            if (!Focused("PLAY CONSTELLATION")) yield break;
            yield return Press(GamepadButton.South); yield return new WaitForSecondsRealtime(.2f);
            var music = GetComponent<ExplorationFeedback>();
            if (!Require(music.MusicPlaying && music.ActiveVoiceCount > 0 && music.ActiveVoiceCount <= 8,
                "Local constellation audio did not play within the voice limit")) yield break;
            yield return Press(GamepadButton.DpadDown);
            if (!Focused("EXPORT SAVED POSTCARD")) yield break;
            yield return Press(GamepadButton.South);
            if (!Require(!string.IsNullOrEmpty(studio.LastExportPath) && File.Exists(studio.LastExportPath),"Postcard export failed")) yield break;
            var directory = Path.Combine(Application.persistentDataPath,"ClustrGalaxy","demo-journey"); Directory.CreateDirectory(directory);
            ScreenCapture.CaptureScreenshot(Path.Combine(directory,"studio.png")); yield return new WaitForSecondsRealtime(1);
            yield return Press(GamepadButton.East);
            if (!Require(!body.isKinematic && hud.InputCaptured,"Studio did not restore previous input context")) yield break;
            music.ToggleMusic();
        }
        private bool Focused(string text) => Require(EventSystem.current?.currentSelectedGameObject?.GetComponentInChildren<Text>()?.text == text,
            "Studio controller focus did not reach " + text + "; actual=" + EventSystem.current?.currentSelectedGameObject?.GetComponentInChildren<Text>()?.text);

        private IEnumerator ClickOpenMenu(string label)
        {
            if(!Require(hud.InputCaptured,"Expected an already open Actions menu"))yield break;
            var count=hud.GetComponentsInChildren<Button>().Length;
            for(var i=0;i<=count;i++)
            {
                if(EventSystem.current.currentSelectedGameObject?.GetComponentInChildren<Text>()?.text==label)
                {yield return Press(GamepadButton.South);yield break;}
                yield return Press(GamepadButton.DpadDown);
            }
            Require(false,"Open Actions menu could not reach "+label);
        }

        private IEnumerator ClickModal(string label)
        {
            var found=false;
            for(var attempt=0;attempt<16;attempt++)
            {
                if(EventSystem.current.currentSelectedGameObject?.GetComponentInChildren<Text>()?.text==label){found=true;break;}
                yield return Press(GamepadButton.DpadDown);
            }
            if(!Require(found,"Modal controller focus cannot reach: "+label))yield break;
            yield return Press(GamepadButton.South);yield return null;
        }
        private IEnumerator Click(string label)
        {
            yield return null;
            // Reopen first: screenshot discard deliberately returns to flight with the menu closed.
            if (hud.InputCaptured) yield return Press(GamepadButton.East);
            yield return Press(GamepadButton.Start);
            var button = hud.GetComponentsInChildren<Button>().FirstOrDefault(value => (value.gameObject.name == label || value.GetComponentInChildren<Text>()?.text == label));
            if(button==null&&label!="SECTION: ALL ACTIONS")
            {yield return Click("SECTION: ALL ACTIONS");button=hud.GetComponentsInChildren<Button>().FirstOrDefault(value=>(value.gameObject.name==label||value.GetComponentInChildren<Text>()?.text==label));}
            if (!Require(button != null && button.interactable, "HUD action unavailable: " + label)) yield break;
            var found = false;
            // Text fields share the console's navigation chain with the buttons.
            var focusLimit=hud.GetComponentsInChildren<Selectable>().Length+1;
            for (var attempt = 0; attempt < focusLimit; attempt++)
            {
                var focused = EventSystem.current.currentSelectedGameObject;
                if (focused != null && focused.GetComponent<Button>() != null &&
                    (focused.name == label || focused.GetComponentInChildren<Text>()?.text == label)) { found = true; break; }
                yield return Press(GamepadButton.DpadDown);
            }
            if (!Require(found, "Controller focus cannot reach: " + label)) yield break;
            yield return Press(GamepadButton.South);
            yield return null;
        }
        private IEnumerator Press(GamepadButton button)
        {
            InputSystem.QueueStateEvent(controller, new GamepadState().WithButton(button));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(controller, new GamepadState());
            yield return null;
            yield return new WaitForSecondsRealtime(.08f);
        }
        private bool Require(bool condition, string reason)
        {
            if (condition) return true;
            failed = true;
            Debug.LogError("[ClustrGalaxy.DemoJourney] FAIL " + reason);
            Application.Quit(2);
            return false;
        }
        private void OnDestroy()
        {
            watchdog?.Dispose();
            Time.timeScale = 1;
            if (controller == null) return;
            InputSystem.RemoveDevice(controller);
            InputSystem.settings.backgroundBehavior = previousBackground;
        }
    }
}
