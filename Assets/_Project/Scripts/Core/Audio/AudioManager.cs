using UnityEngine;
using UnityEngine.Audio;

public sealed class AudioManager : MonoBehaviour
{
    private const float MinVolumeDb = -80f;
    private const string MasterVolumeParameter = "MasterVolume";
    private const string MusicVolumeParameter = "MusicVolume";
    private const string SfxVolumeParameter = "SfxVolume";

    [SerializeField] private AudioLibrary audioLibrary;
    [SerializeField] private AudioMixer audioMixer;
    [SerializeField] private AudioMixerGroup musicGroup;
    [SerializeField] private AudioMixerGroup sfxGroup;

    private AudioSource _musicSource;
    private AudioSource _sfxSource;
    private float _musicVolume = 1f;
    private float _sfxVolume = 1f;

    public static AudioManager Instance { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        CreateAudioSources();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    /// <summary>
    /// Plays music from the configured audio library.
    /// </summary>
    public void PlayMusic(string id, bool loop = true)
    {
        if (!TryGetClip(id, out AudioClip clip))
        {
            return;
        }

        if (_musicSource.clip == clip && _musicSource.isPlaying)
        {
            return;
        }

        _musicSource.clip = clip;
        _musicSource.loop = loop;
        _musicSource.Play();
    }

    /// <summary>
    /// Stops the active music track.
    /// </summary>
    public void StopMusic()
    {
        _musicSource.Stop();
        _musicSource.clip = null;
    }

    /// <summary>
    /// Plays a one-shot sound effect from the configured audio library.
    /// </summary>
    public void PlaySfx(string id, Vector3? position = null)
    {
        if (!TryGetClip(id, out AudioClip clip))
        {
            return;
        }

        if (position.HasValue)
        {
            PlaySpatialSfx(clip, position.Value);
            return;
        }

        _sfxSource.PlayOneShot(clip);
    }

    /// <summary>
    /// Sets master volume from 0 to 1.
    /// </summary>
    public void SetMasterVolume(float value)
    {
        SetMixerVolume(MasterVolumeParameter, value);
    }

    /// <summary>
    /// Sets music volume from 0 to 1.
    /// </summary>
    public void SetMusicVolume(float value)
    {
        _musicVolume = Mathf.Clamp01(value);

        if (_musicSource != null)
        {
            _musicSource.volume = _musicVolume;
        }

        SetMixerVolume(MusicVolumeParameter, _musicVolume);
    }

    /// <summary>
    /// Sets sound effect volume from 0 to 1.
    /// </summary>
    public void SetSfxVolume(float value)
    {
        _sfxVolume = Mathf.Clamp01(value);

        if (_sfxSource != null)
        {
            _sfxSource.volume = _sfxVolume;
        }

        SetMixerVolume(SfxVolumeParameter, _sfxVolume);
    }

    private void CreateAudioSources()
    {
        _musicSource = gameObject.AddComponent<AudioSource>();
        _musicSource.outputAudioMixerGroup = musicGroup;
        _musicSource.playOnAwake = false;
        _musicSource.loop = true;
        _musicSource.volume = _musicVolume;

        _sfxSource = gameObject.AddComponent<AudioSource>();
        _sfxSource.outputAudioMixerGroup = sfxGroup;
        _sfxSource.playOnAwake = false;
        _sfxSource.volume = _sfxVolume;
    }

    private bool TryGetClip(string id, out AudioClip clip)
    {
        clip = null;

        if (audioLibrary == null)
        {
            Debug.LogWarning("AudioManager: missing SO_AudioLibrary reference.");
            return false;
        }

        if (!audioLibrary.TryGetClip(id, out clip))
        {
            Debug.LogWarning($"AudioManager: missing audio id '{id}'.");
            return false;
        }

        return true;
    }

    private void PlaySpatialSfx(AudioClip clip, Vector3 position)
    {
        GameObject sfxObject = new($"SFX_{clip.name}");
        sfxObject.transform.position = position;

        AudioSource audioSource = sfxObject.AddComponent<AudioSource>();
        audioSource.clip = clip;
        audioSource.outputAudioMixerGroup = sfxGroup;
        audioSource.spatialBlend = 1f;
        audioSource.volume = _sfxVolume;
        audioSource.Play();

        Destroy(sfxObject, clip.length);
    }

    private void SetMixerVolume(string parameterName, float value)
    {
        if (audioMixer == null)
        {
            return;
        }

        float clampedValue = Mathf.Clamp01(value);
        float volumeDb = clampedValue <= 0f ? MinVolumeDb : Mathf.Log10(clampedValue) * 20f;
        audioMixer.SetFloat(parameterName, volumeDb);
    }
}
