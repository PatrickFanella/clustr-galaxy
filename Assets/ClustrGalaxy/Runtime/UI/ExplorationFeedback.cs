using System;
using System.Collections.Generic;
using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Core.Activities;
using ClustrGalaxy.Core.Domain;
using ClustrGalaxy.Core.Survey;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.World;
using UnityEngine;
using UnityEngine.UI;

namespace ClustrGalaxy.UI
{
    /// <summary>Bounded audio and visual adapters. Progress remains in the authoritative core.</summary>
    public sealed class ExplorationFeedback : MonoBehaviour
    {
        private GalaxySessionBehaviour owner;
        private SignalScanner scanner;
        private GalaxyVisualCatalogProvider assets;
        private GalaxySurveyVisibilityController visibility;
        private readonly AudioSource[] voices = new AudioSource[8];
        private readonly GalaxyEntity[] voiceEntities = new GalaxyEntity[8];
        private readonly Transform[] voiceAnchors=new Transform[8];
        private RectTransform reticle;
        private RectTransform canvasRect;
        private Image reticleImage;
        private Text caption;
        private Camera view;
        private ActivityPreferences observedPreferences;
        private SurveyState observedSurvey;
        private ActivityState observedActivity;
        private float nextBeat, flourishUntil, retryAt, noteUntil;
        private int cursor, voiceCursor, phrasePreset = -1;
        private string observedCaption;
        private bool captionInStation;
        private readonly List<GalaxyEntityId> phrase = new();
        public bool MusicPlaying { get; private set; }
        public int ActiveVoiceCount
        {
            get { var count = 0; foreach (var voice in voices) if (voice != null && voice.isPlaying) count++; return count; }
        }
        public void Initialize(GalaxySessionBehaviour session, SignalScanner signalScanner, GalaxyVisualCatalogProvider catalog)
        {
            owner = session; scanner = signalScanner; assets = catalog;
            visibility = GetComponent<GalaxySurveyVisibilityController>(); view = Camera.main;
            var canvasObject = new GameObject("Signal tuning feedback", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            canvasObject.transform.SetParent(transform, false); canvasRect = canvasObject.GetComponent<RectTransform>();
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            canvasObject.GetComponent<Canvas>().sortingOrder = 5;
            var scaler = canvasObject.GetComponent<CanvasScaler>(); scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1280,720); scaler.matchWidthOrHeight = 1;
            var mark = new GameObject("Tuning alignment", typeof(RectTransform), typeof(Image));
            mark.transform.SetParent(canvasRect, false); reticle = mark.GetComponent<RectTransform>();
            reticle.sizeDelta = new Vector2(18,3); reticleImage = mark.GetComponent<Image>(); reticleImage.raycastTarget = false;
            var vertical = new GameObject("Vertical", typeof(RectTransform), typeof(Image)); vertical.transform.SetParent(reticle,false);
            vertical.GetComponent<RectTransform>().sizeDelta = new Vector2(3,18); vertical.GetComponent<Image>().raycastTarget = false;
            var text = new GameObject("Tuning status", typeof(RectTransform), typeof(Text)); text.transform.SetParent(canvasRect,false);
            caption = text.GetComponent<Text>(); caption.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            caption.fontSize = 12; caption.alignment = TextAnchor.MiddleCenter; caption.color = new Color(.7f,1f,.9f); caption.raycastTarget = false;
            caption.rectTransform.anchorMin = new Vector2(.5f,0); caption.rectTransform.anchorMax = new Vector2(.5f,0);
            caption.rectTransform.anchoredPosition = new Vector2(0,78); caption.rectTransform.sizeDelta = new Vector2(600,24);
            reticle.gameObject.SetActive(false);
            for (var i = 0; i < voices.Length; i++)
            {
                var child = new GameObject("Exploration voice " + i); child.transform.SetParent(transform,false);
                var voice = voices[i] = child.AddComponent<AudioSource>(); voice.playOnAwake = false;
                voice.spatialBlend = .6f; voice.minDistance = 50; voice.maxDistance = 4000; voice.dopplerLevel = 0;
            }
            scanner.ScanFinished += Finished;
        }
        public void ToggleMusic()
        {
            MusicPlaying = !MusicPlaying; nextBeat = Time.unscaledTime;
            if (!MusicPlaying) foreach (var voice in voices) voice.Stop();
        }
        public void PlayConstellation(IReadOnlyList<GalaxyEntityId> members, int preset = -1)
        {
            if (preset < -1 || preset > 2) throw new ArgumentOutOfRangeException(nameof(preset));
            phrasePreset = preset;
            phrase.Clear(); if (members != null) for (var i = 0; i < members.Count; i++) phrase.Add(members[i]);
            cursor = 0; MusicPlaying = true; nextBeat = Time.unscaledTime;
        }
        public void Audition(GalaxyEntityId id)
        {
            var entity = owner.Registry.FindEntity(id.Value);
            if (entity != null && visibility.CanLabel(entity)) PlayNote(entity.StableId, entity.transform.position);
        }
        private void Update()
        {
            if (owner == null || owner.Session == null || scanner == null) return;
            // Keep the visual audio equivalent below projected station controls.
            // Only move it when the input context changes, not on every pulse.
            var stationOpen = owner.Session.CockpitInput.Paused;
            if (captionInStation != stationOpen)
            {
                captionInStation = stationOpen;
                caption.rectTransform.anchoredPosition = new Vector2(0, stationOpen ? 14 : 78);
            }
            var activity = owner.Session.Activities;
            var preferences = activity?.State.Preferences;
            if (!ReferenceEquals(preferences, observedPreferences))
            {
                observedPreferences = preferences; scanner.AutomaticTuning = preferences?.AutomaticTuning == true;
                if (preferences?.MusicMuted == true || preferences?.ReducedAudio == true) foreach (var voice in voices) voice.Stop();
            }
            if (!ReferenceEquals(observedActivity, activity?.State))
            { if(observedActivity!=null&&activity!=null&&!observedActivity.Identity.Equals(activity.State.Identity))foreach(var voice in voices)voice.Stop();observedActivity = activity?.State; GetComponent<ProbeHud>()?.InvalidateExploration(); }
            var survey = owner.Session.Survey?.State;
            if (!ReferenceEquals(observedSurvey, survey) || (owner.Session.ActivityError.Length != 0 && Time.unscaledTime >= retryAt))
            {
                observedSurvey = survey; retryAt = Time.unscaledTime + 5;
                owner.Session.ReconcileSurveyActivityDiary();
            }
            for (var i = 0; i < voices.Length; i++)
            {
                if (!voices[i].isPlaying) continue;
                if(voiceAnchors[i]!=null)
                {if(!voiceAnchors[i].gameObject.activeInHierarchy||activity?.State.Identity.Snapshot.StorageNamespace!="demo")voices[i].Stop();else voices[i].transform.position=voiceAnchors[i].position;continue;}
                var entity = voiceEntities[i];
                if (entity == null || !visibility.CanLabel(entity)) voices[i].Stop();
                else voices[i].transform.position = entity.transform.position;
            }
            var charging = scanner.State == SignalScanState.Charging;
            if (reticle.gameObject.activeSelf != charging) reticle.gameObject.SetActive(charging);
            if (charging && view != null && scanner.TuningDirection.sqrMagnitude > 0)
            {
                var point = view.WorldToScreenPoint(view.transform.position + scanner.TuningDirection * 100);
                if (RectTransformUtility.ScreenPointToLocalPointInRectangle(canvasRect, point, null, out var local)) reticle.anchoredPosition = local;
                reticleImage.color = scanner.CleanSignal ? Color.cyan : Color.white;
            }
            var label = charging ? "TUNE WITH PROBE AIM · HOLD SCAN 1.5s" : Time.unscaledTime < flourishUntil ? observedCaption : MusicPlaying || Time.unscaledTime < noteUntil ? "♪ ARTISTIC SONIFICATION ♪" : "";
            if (caption.text != label) caption.text = label;
            caption.canvasRenderer.SetAlpha(!charging && Time.unscaledTime >= flourishUntil && MusicPlaying && Time.unscaledTime >= noteUntil ? .35f : 1f);
            if (MusicPlaying && Time.unscaledTime >= nextBeat)
            {
                nextBeat = Time.unscaledTime + 60f / (preferences?.Tempo ?? 80);
                if (phrase.Count > 0) Audition(phrase[cursor++ % phrase.Count]);
                else
                {
                    var entities = owner.Registry.Entities;
                    for (var tries = 0; tries < entities.Count; tries++)
                    {
                        var entity = entities[cursor++ % entities.Count];
                        if (visibility.CanLabel(entity) && (entity.transform.position - transform.position).sqrMagnitude <= 9000000)
                        { PlayNote(entity.StableId, entity.transform.position); break; }
                    }
                }
            }
        }
        private void Finished(GalaxyEntity entity, bool clean, bool practice)
        {
            observedCaption = practice ? (clean ? "CLEAN TUNING PRACTICE — NO SURVEY PROGRESS" : "TUNING PRACTICE — NO SURVEY PROGRESS") :
                clean ? "CLEAN SIGNAL — ARTISTIC TUNING" : "SIGNAL IDENTIFIED";
            flourishUntil = Time.unscaledTime + 2;
            if (clean && entity != null) PlayNote(entity.StableId, entity.transform.position);
        }
        public void PlayAuthoredAnomaly(string authoredId,Transform anchor)
        {
            if(owner?.Session?.Activities?.State.Identity.Snapshot.StorageNamespace!="demo"||anchor==null||!anchor.gameObject.activeInHierarchy)return;
            PlayNote(authoredId,anchor.position,anchor);
        }
        private void PlayNote(string id, Vector3 position,Transform authoredAnchor=null)
        {
            noteUntil = Time.unscaledTime + .35f;
            var preferences = owner.Session.Activities?.State.Preferences;
            if (preferences?.MusicMuted == true || assets.Catalog == null || assets.Catalog.explorationInstruments.Length == 0) return;
            var clip = assets.Catalog.explorationInstruments[(phrasePreset >= 0 && phrase.Count > 0 ? phrasePreset : preferences?.MusicPreset ?? 0) % assets.Catalog.explorationInstruments.Length]; if (clip == null) return;
            var index = voiceCursor++ % (preferences?.ReducedAudio == true ? 2 : voices.Length);
            voiceEntities[index] = owner.Registry.FindEntity(id);voiceAnchors[index]=authoredAnchor;
            var voice = voices[index]; voice.Stop(); voice.clip = clip;
            voice.transform.position = position; voice.pitch = Mathf.Pow(2, DiscoveryMusic.SemitoneFor(id) / 12f);
            voice.volume = (preferences?.MusicVolume ?? .35f) * (preferences?.ReducedAudio == true ? .4f : 1f); voice.Play();
        }
        private void OnDestroy()
        {
            if (scanner != null) scanner.ScanFinished -= Finished;
            foreach (var voice in voices) if (voice != null) voice.Stop();
        }
    }
}
