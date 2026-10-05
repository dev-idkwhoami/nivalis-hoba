using NivalisMods.ModCompanion.Api;
using UnityEngine;
using UnityEngine.Audio;

namespace NivalisMods.Hoba;

internal sealed class BoardAudio
{
    private const string ClipName = "Driving Loop V1";
    private static Setting<float> _baseVolume = null!, _boost = null!;
    private readonly GameObject _root;
    private readonly HoverVolume _volume = new();
    private AudioSource? _source;
    private int _attempts;
    private float _retryAt;
    private bool _paused, _failed;

    internal static void Configure(SettingsCategory audio)
    {
        _baseVolume = audio.Slider("BaseVolume", "Hover volume", .2f, 0, 1, .001f,
            "Normal hover loop volume (0 to 1). Changes apply immediately.");
        _boost = audio.Slider("AccelerationVolumeBoost", "Acceleration volume boost", .075f, 0, 1, .001f,
            "Additional volume while gaining speed; smoothly returns to the normal volume afterward.");
    }
    internal BoardAudio(GameObject root) => _root = root;

    internal void Tick(bool accelerating, bool mounted)
    {
        if (_failed) return;
        try
        {
            var audible = _root.activeInHierarchy && Application.isFocused && Time.timeScale > 0;
            if (_source == null)
            {
                if (!audible || _attempts >= 3 || Time.unscaledTime < _retryAt) return;
                _attempts++; _retryAt = Time.unscaledTime + 5;
                // Resolve once per board (bounded retries if scene audio is still loading).
                // Borrow only the clip and mixer route; never modify the taxi's source.
                var donors = Resources.FindObjectsOfTypeAll<AudioSource>()
                    .Where(a => a != null && a.clip != null && a.clip.name == ClipName).ToArray();
                var donor = donors.FirstOrDefault(a => a.outputAudioMixerGroup != null) ?? donors.FirstOrDefault();
                var clip = donor?.clip ?? Resources.FindObjectsOfTypeAll<AudioClip>().FirstOrDefault(c => c != null && c.name == ClipName);
                if (clip == null)
                {
                    if (_attempts == 3) Plugin.Logger.LogWarning("HOBA hover loop unavailable: " + ClipName);
                    return;
                }
                _source = _root.AddComponent<AudioSource>();
                _source.playOnAwake = false;
                _source.clip = clip;
                // Player-owned equipment uses the player effects bus, not the taxi's
                // environment Objects bus which can be independently attenuated.
                _source.outputAudioMixerGroup = Resources.FindObjectsOfTypeAll<AudioMixerGroup>()
                    .FirstOrDefault(g => g != null && g.name == "Player" && g.audioMixer.name == "Environment")
                    ?? donor?.outputAudioMixerGroup;
                _source.priority = 32;
                _source.mute = false;
                clip.LoadAudioData();
                _source.loop = true;
                _source.volume = 0;
                _source.pitch = 1;
                _source.spatialBlend = 1;
                _source.dopplerLevel = 0;
                _source.rolloffMode = AudioRolloffMode.Linear;
                _source.minDistance = 2;
                _source.maxDistance = 14;
                Plugin.Logger.LogInfo("HOBA hover audio: " + clip.name);
            }
            if (!audible)
            {
                if (_source.isPlaying) { _source.Pause(); _paused = true; }
                return;
            }
            if (_source.clip.loadState == AudioDataLoadState.Failed)
                throw new InvalidOperationException("Native hover clip failed to load audio data.");
            if (_source.clip.loadState != AudioDataLoadState.Loaded) return;
            _source.spatialBlend = mounted ? 0 : 1;
            _volume.Step(accelerating, _baseVolume.Value, _boost.Value, Time.deltaTime);
            _source.volume = _volume.Value;
            if (_paused) { _source.UnPause(); _paused = false; }
            // An area hide disables the board object and stops its source; restart on return.
            if (!_source.isPlaying) _source.Play();
        }
        catch (Exception e)
        {
            _failed = true;
            if (_source != null) _source.Stop();
            Plugin.Logger.LogWarning("HOBA audio disabled for this board: " + e.Message);
        }
    }
}
