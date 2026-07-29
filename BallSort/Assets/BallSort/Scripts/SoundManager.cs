using System;
using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class SoundManager : MonoBehaviour
{
    public static SoundManager Instance { get; private set; }

    // Lowercase alias included so either calling style works.
    public static SoundManager instance => Instance;

    private const string SoundKey = "BallSort_Sound";
    private const string MusicKey = "BallSort_Music";

    [Serializable]
    private class NamedSound
    {
        public string name;
        public AudioClip clip;

        [Range(0f, 1f)]
        public float volume = 1f;
    }

    [Header("Sound Effects")]
    [Tooltip("Two or three AudioSources are recommended so effects can overlap.")]
    [SerializeField] private AudioSource[] soundSources;

    [SerializeField] private NamedSound[] sounds;

    [Header("Music")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField] private AudioClip backgroundMusic;

    [Range(0f, 1f)]
    [SerializeField] private float musicVolume = 1f;

    private readonly Dictionary<string, NamedSound> soundLookup =
        new Dictionary<string, NamedSound>(
            StringComparer.OrdinalIgnoreCase);

    private int nextSoundSourceIndex;
    private bool soundEnabled = true;
    private bool musicEnabled = true;

    public bool SoundEnabled => soundEnabled;
    public bool MusicEnabled => musicEnabled;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        BuildSoundLookup();

        soundEnabled = PlayerPrefs.GetInt(SoundKey, 1) == 1;
        musicEnabled = PlayerPrefs.GetInt(MusicKey, 1) == 1;

        PrepareMusicSource();
        ApplySettings();
    }

    private void Start()
    {
        StartMusicIfAllowed();
    }

    private void BuildSoundLookup()
    {
        soundLookup.Clear();

        if (sounds == null)
        {
            return;
        }

        for (int i = 0; i < sounds.Length; i++)
        {
            NamedSound sound = sounds[i];

            if (sound == null ||
                string.IsNullOrWhiteSpace(sound.name) ||
                sound.clip == null)
            {
                continue;
            }

            soundLookup[sound.name.Trim()] = sound;
        }
    }

    private void PrepareMusicSource()
    {
        if (musicSource == null)
        {
            Debug.LogWarning(
                "Assign a Music Source to SoundManager.",
                this);
            return;
        }

        musicSource.loop = true;
        musicSource.playOnAwake = false;
        musicSource.clip = backgroundMusic;
        musicSource.volume = musicVolume;
    }

    public void PlaySound(string soundName)
    {
        if (!soundEnabled ||
            string.IsNullOrWhiteSpace(soundName))
        {
            return;
        }

        if (!soundLookup.TryGetValue(soundName.Trim(), out NamedSound sound))
        {
            Debug.LogWarning(
                "SoundManager could not find sound: " + soundName,
                this);
            return;
        }

        AudioSource source = GetNextSoundSource();

        if (source == null)
        {
            Debug.LogWarning(
                "Assign at least one Sound Source to SoundManager.",
                this);
            return;
        }

        source.PlayOneShot(sound.clip, sound.volume);
    }

    public void playsound(string soundName)
    {
        PlaySound(soundName);
    }

    private AudioSource GetNextSoundSource()
    {
        if (soundSources == null || soundSources.Length == 0)
        {
            return null;
        }

        // Prefer an idle source.
        for (int i = 0; i < soundSources.Length; i++)
        {
            int index =
                (nextSoundSourceIndex + i) % soundSources.Length;

            if (soundSources[index] != null &&
                !soundSources[index].isPlaying)
            {
                nextSoundSourceIndex =
                    (index + 1) % soundSources.Length;

                return soundSources[index];
            }
        }

        // All sources are busy, so reuse them round-robin.
        for (int i = 0; i < soundSources.Length; i++)
        {
            int index =
                nextSoundSourceIndex % soundSources.Length;

            nextSoundSourceIndex =
                (nextSoundSourceIndex + 1) %
                soundSources.Length;

            if (soundSources[index] != null)
            {
                return soundSources[index];
            }
        }

        return null;
    }

    public void SetSoundEnabled(bool enabled)
    {
        soundEnabled = enabled;
        PlayerPrefs.SetInt(SoundKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (!enabled && soundSources != null)
        {
            for (int i = 0; i < soundSources.Length; i++)
            {
                if (soundSources[i] != null)
                {
                    soundSources[i].Stop();
                }
            }
        }
    }

    public void SetMusicEnabled(bool enabled)
    {
        musicEnabled = enabled;
        PlayerPrefs.SetInt(MusicKey, enabled ? 1 : 0);
        PlayerPrefs.Save();

        if (musicSource == null)
        {
            return;
        }

        // Awake may have muted this source because music was saved as off.
        // Explicitly restore the mute state whenever the setting changes.
        musicSource.mute = !enabled;

        if (enabled)
        {
            StartMusicIfAllowed();
        }
        else
        {
            musicSource.Pause();
        }
    }

    private void StartMusicIfAllowed()
    {
        if (!musicEnabled ||
            musicSource == null ||
            backgroundMusic == null)
        {
            return;
        }

        if (musicSource.clip != backgroundMusic)
        {
            musicSource.clip = backgroundMusic;
        }

        musicSource.loop = true;
        musicSource.volume = musicVolume;
        musicSource.mute = false;

        if (!musicSource.isPlaying)
        {
            musicSource.Play();
        }
    }

    private void ApplySettings()
    {
        if (musicSource != null)
        {
            musicSource.mute = !musicEnabled;
        }
    }

    private void OnValidate()
    {
        BuildSoundLookup();

        if (musicSource != null)
        {
            musicSource.loop = true;
            musicSource.playOnAwake = false;
            musicSource.volume = musicVolume;
        }
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}