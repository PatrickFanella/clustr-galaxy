using ClustrGalaxy.Bootstrap;
using ClustrGalaxy.Flight;
using ClustrGalaxy.Scanning;
using ClustrGalaxy.World;
using UnityEngine;

namespace ClustrGalaxy.UI
{
    /// <summary>Three bounded, local cockpit voices, independent of the eight musical voices.</summary>
    public sealed class ShipAudioFeedback : MonoBehaviour
    {
        private GalaxySessionBehaviour session;
        private ObserverProbeController flight;
        private SignalScanner scanner;
        private AudioSource engine, scan, ui;
        public void Initialize(GalaxySessionBehaviour owner, ObserverProbeController controller, SignalScanner signalScanner, GalaxyVisualCatalog catalog)
        {
            session = owner; flight = controller; scanner = signalScanner;
            engine = Voice("Engine feedback", catalog.engineSound, true);
            scan = Voice("Scanner feedback", catalog.scannerSound, true);
            ui = Voice("Interface feedback", catalog.interfaceSound, false);
        }
        private AudioSource Voice(string name, AudioClip clip, bool loop)
        {
            var child = new GameObject(name); child.transform.SetParent(transform, false);
            var source = child.AddComponent<AudioSource>(); source.clip = clip; source.loop = loop;
            source.playOnAwake = false; source.spatialBlend = 0; source.volume = 0;
            return source;
        }
        private float Volume()
        {
            var preferences = session?.Session?.Activities?.State.Preferences;
            return preferences == null || preferences.MusicMuted ? 0 : (float)preferences.MusicVolume;
        }
        private void Update()
        {
            if (engine == null) return;
            var preferences = session?.Session?.Activities?.State.Preferences;
            var ambient = preferences == null || preferences.ReducedAudio || Time.timeScale == 0 ? 0 : Volume();
            var effort = flight == null ? 0 : Mathf.Clamp01(flight.PilotTranslation.magnitude + flight.PilotRotation.magnitude * .15f);
            SetLoop(engine, ambient * effort * .22f, .8f + effort * .35f);
            SetLoop(scan, ambient * (scanner != null && scanner.State == SignalScanState.Charging ? .14f : 0), 1);
            if (Volume() == 0 && ui.isPlaying) ui.Stop();
        }
        private static void SetLoop(AudioSource source, float target, float pitch)
        {
            source.volume = Mathf.MoveTowards(source.volume, target, Time.unscaledDeltaTime * 2);
            source.pitch = pitch;
            if (target <= 0) { if (source.isPlaying) source.Stop(); return; }
            if (source.clip != null && !source.isPlaying) source.Play();
        }
        public void PlayInterface()
        {
            if (ui == null || ui.clip == null || Volume() <= 0) return;
            ui.volume = Volume() * .18f; ui.Play(); // Restart one voice rather than stack one-shots.
        }
        private void OnDisable()
        { if (engine != null) engine.Stop(); if (scan != null) scan.Stop(); if (ui != null) ui.Stop(); }
        private void OnDestroy()
        { if (engine != null) Destroy(engine.gameObject); if (scan != null) Destroy(scan.gameObject); if (ui != null) Destroy(ui.gameObject); }
    }
}
