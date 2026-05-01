// AudioFeedbackManager.cs
// Singleton that provides procedurally-generated audio feedback sounds for
// clinical test interactions. No external audio files required.
//
// Public API
// ----------
//   static AudioFeedbackManager Instance
//   void PlayClick()           – short click for button presses
//   void PlayCorrect()         – positive confirmation tone (training mode)
//   void PlayStimulus()        – soft alert that a stimulus was presented
//   void PlaySessionStart()    – ascending chime
//   void PlaySessionEnd()      – descending chime
//   void PlayError()           – low error buzz

using UnityEngine;

namespace OphthalSuite.Core
{
    [RequireComponent(typeof(AudioSource))]
    public class AudioFeedbackManager : MonoBehaviour
    {
        // ── Singleton ────────────────────────────────────────────────────────────
        public static AudioFeedbackManager Instance { get; private set; }

        [Header("Volume")]
        [SerializeField] [Range(0f, 1f)] private float masterVolume = 0.35f;

        [Header("Enable/Disable")]
        [SerializeField] private bool enableSounds = true;

        private AudioSource _source;

        // Cached clips (generated once)
        private AudioClip _clickClip;
        private AudioClip _correctClip;
        private AudioClip _stimulusClip;
        private AudioClip _sessionStartClip;
        private AudioClip _sessionEndClip;
        private AudioClip _errorClip;

        private const int SampleRate = 44100;

        // ── Unity lifecycle ──────────────────────────────────────────────────────

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;

            GenerateClips();
            Debug.Log("AudioFeedbackManager: Procedural audio clips generated.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        // ── Public API ───────────────────────────────────────────────────────────

        /// <summary>Short click for button presses and UI interactions.</summary>
        public void PlayClick()
        {
            Play(_clickClip, 0.6f);
        }

        /// <summary>Positive confirmation tone (for training/demo mode).</summary>
        public void PlayCorrect()
        {
            Play(_correctClip, 0.5f);
        }

        /// <summary>Soft alert that a stimulus was presented (optional).</summary>
        public void PlayStimulus()
        {
            Play(_stimulusClip, 0.25f);
        }

        /// <summary>Ascending chime when session starts.</summary>
        public void PlaySessionStart()
        {
            Play(_sessionStartClip, 0.7f);
        }

        /// <summary>Descending chime when session ends.</summary>
        public void PlaySessionEnd()
        {
            Play(_sessionEndClip, 0.7f);
        }

        /// <summary>Low error buzz.</summary>
        public void PlayError()
        {
            Play(_errorClip, 0.5f);
        }

        /// <summary>Enable or disable all audio feedback.</summary>
        public void SetEnabled(bool enabled)
        {
            enableSounds = enabled;
        }

        // ── Playback ─────────────────────────────────────────────────────────────

        private void Play(AudioClip clip, float volumeScale)
        {
            if (!enableSounds || clip == null || _source == null) return;
            _source.PlayOneShot(clip, masterVolume * volumeScale);
        }

        // ── Procedural clip generation ───────────────────────────────────────────

        private void GenerateClips()
        {
            _clickClip = GenerateTone("click", 0.06f, 1200f, 0f, ToneShape.Decay);
            _correctClip = GenerateTwoTone("correct", 0.12f, 880f, 1100f);
            _stimulusClip = GenerateTone("stimulus", 0.08f, 600f, 0f, ToneShape.Soft);
            _sessionStartClip = GenerateChime("start", true);
            _sessionEndClip = GenerateChime("end", false);
            _errorClip = GenerateTone("error", 0.15f, 200f, 0f, ToneShape.Buzz);
        }

        private enum ToneShape { Decay, Soft, Buzz }

        private static AudioClip GenerateTone(string name, float duration, float freq,
            float freqEnd, ToneShape shape)
        {
            int samples = Mathf.CeilToInt(duration * SampleRate);
            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            var data = new float[samples];

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / samples;
                float f = freqEnd > 0f ? Mathf.Lerp(freq, freqEnd, progress) : freq;

                float sample = Mathf.Sin(2f * Mathf.PI * f * t);

                // Apply envelope based on shape
                switch (shape)
                {
                    case ToneShape.Decay:
                        sample *= Mathf.Exp(-progress * 8f);
                        break;
                    case ToneShape.Soft:
                        float attack = Mathf.Clamp01(progress * 10f);
                        float release = Mathf.Clamp01((1f - progress) * 5f);
                        sample *= attack * release;
                        break;
                    case ToneShape.Buzz:
                        sample = (sample > 0 ? 0.5f : -0.5f) * Mathf.Exp(-progress * 4f);
                        break;
                }

                data[i] = sample * 0.5f;
            }

            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateTwoTone(string name, float duration, float freq1, float freq2)
        {
            int samples = Mathf.CeilToInt(duration * SampleRate);
            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            var data = new float[samples];
            int half = samples / 2;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                float progress = (float)i / samples;
                float f = i < half ? freq1 : freq2;
                float sample = Mathf.Sin(2f * Mathf.PI * f * t);

                // Smooth envelope
                float attack = Mathf.Clamp01(progress * 15f);
                float release = Mathf.Clamp01((1f - progress) * 10f);
                data[i] = sample * attack * release * 0.4f;
            }

            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip GenerateChime(string name, bool ascending)
        {
            float duration = 0.4f;
            int samples = Mathf.CeilToInt(duration * SampleRate);
            var clip = AudioClip.Create(name, samples, 1, SampleRate, false);
            var data = new float[samples];

            // Three-note chime
            float[] notes = ascending
                ? new float[] { 523f, 659f, 784f }   // C5, E5, G5
                : new float[] { 784f, 659f, 523f };  // G5, E5, C5

            int noteLen = samples / 3;

            for (int i = 0; i < samples; i++)
            {
                float t = (float)i / SampleRate;
                int noteIdx = Mathf.Min(i / noteLen, 2);
                float freq = notes[noteIdx];

                float localProgress = (float)(i - noteIdx * noteLen) / noteLen;
                float sample = Mathf.Sin(2f * Mathf.PI * freq * t);

                // Per-note decay envelope
                float env = Mathf.Exp(-localProgress * 4f);
                // Global fade
                float globalFade = 1f - ((float)i / samples) * 0.3f;

                data[i] = sample * env * globalFade * 0.35f;
            }

            clip.SetData(data, 0);
            return clip;
        }
    }
}
