using System;
namespace ClustrGalaxy.Core.Activities
{
    public sealed class ActivityPreferences
    {
        public ActivityPreferences(bool automaticTuning = false, bool musicMuted = false, bool reducedMotion = false,
            float musicVolume = .35f, int tempo = 80, float celebrationIntensity = .6f, int musicPreset = 0, bool reducedAudio = false, bool saveBestGhosts = false)
        {
            if (!Unit(musicVolume) || !Unit(celebrationIntensity) || tempo < 60 || tempo > 120 || musicPreset < 0 || musicPreset > 2)
                throw new ArgumentException("Invalid exploration preferences.");
            MusicPreset = musicPreset; ReducedAudio = reducedAudio; SaveBestGhosts=saveBestGhosts;
            AutomaticTuning = automaticTuning; MusicMuted = musicMuted; ReducedMotion = reducedMotion;
            MusicVolume = musicVolume; Tempo = tempo; CelebrationIntensity = celebrationIntensity;
        }
        public ActivityPreferences With(bool? automaticTuning = null, bool? musicMuted = null, bool? reducedMotion = null,
            float? musicVolume = null, int? tempo = null, float? celebrationIntensity = null, int? musicPreset = null, bool? reducedAudio = null, bool? saveBestGhosts = null) =>
            new(automaticTuning ?? AutomaticTuning, musicMuted ?? MusicMuted, reducedMotion ?? ReducedMotion,
                musicVolume ?? MusicVolume, tempo ?? Tempo, celebrationIntensity ?? CelebrationIntensity,
                musicPreset ?? MusicPreset, reducedAudio ?? ReducedAudio, saveBestGhosts ?? SaveBestGhosts);
        public bool SaveBestGhosts {get;}
        public int MusicPreset { get; }
        public bool ReducedAudio { get; }
        private static bool Unit(float value) => !float.IsNaN(value) && !float.IsInfinity(value) && value >= 0 && value <= 1;
        public bool AutomaticTuning { get; }
        public bool MusicMuted { get; }
        public bool ReducedMotion { get; }
        public float MusicVolume { get; }
        public int Tempo { get; }
        public float CelebrationIntensity { get; }
    }
}
