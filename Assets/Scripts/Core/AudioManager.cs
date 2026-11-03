using System;
using UnityEngine;

public enum SfxId { Throw, Hit, Recall, EnemyDie, PlayerHurt, Explosion, Coin, Purchase, UIClick }

[DisallowMultipleComponent]
public class AudioManager : MonoBehaviour
{
    [Serializable]
    private struct SfxClip
    {
        public SfxId id;
        public AudioClip clip;
    }

    private const string VolumePreference = "NightShift.MasterVolume";

    [Header("Clips")]
    [SerializeField] private SfxClip[] soundEffects = new SfxClip[0];
    [SerializeField] private AudioClip musicClip;

    [Header("Playback")]
    [SerializeField] private AudioSource musicSource;
    [SerializeField, Range(1, 32)] private int sfxVoiceCount = 12;
    [SerializeField, Range(0f, 1f)] private float sfxVolume = 1f;
    [SerializeField, Range(0f, 1f)] private float musicVolume = 0.5f;

    private readonly AudioClip[] clips = new AudioClip[9];
    private AudioSource[] sfxSources;
    private int nextVoice;
    private float masterVolume = 1f;

    public static AudioManager Instance { get; private set; }

    public float MasterVolume
    {
        get => masterVolume;
        set
        {
            masterVolume = float.IsNaN(value) ? 1f : Mathf.Clamp01(value);
            AudioListener.volume = masterVolume;
            PlayerPrefs.SetFloat(VolumePreference, masterVolume);
        }
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }

        Instance = this;
        // A persistent audio root must not remain under a scene-owned manager hierarchy.
        transform.SetParent(null, true);
        DontDestroyOnLoad(gameObject);
        MasterVolume = PlayerPrefs.GetFloat(VolumePreference, 1f);
        if (soundEffects != null)
        {
            foreach (SfxClip entry in soundEffects)
            {
                int index = (int)entry.id;
                if (index >= 0 && index < clips.Length)
                    clips[index] = entry.clip;
            }
        }

        sfxSources = new AudioSource[Mathf.Clamp(sfxVoiceCount, 1, 32)];
        for (int i = 0; i < sfxSources.Length; i++)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = false;
            source.spatialBlend = 0f;
            source.volume = sfxVolume;
            sfxSources[i] = source;
        }
        PrepareMusicSource();
    }

    private void OnEnable()
    {
        if (Instance != this)
            return;

        GameEvents.BladeThrown += OnBladeThrown;
        GameEvents.EnemyHit += OnEnemyHit;
        GameEvents.RecallStarted += OnRecallStarted;
        GameEvents.EnemyKilled += OnEnemyKilled;
        GameEvents.PlayerHit += OnPlayerHit;
        GameEvents.BarrelExploded += OnBarrelExploded;
        GameEvents.CoinCollected += OnCoinCollected;
        GameEvents.UpgradePurchased += OnUpgradePurchased;
        GameEvents.GameStateChanged += OnGameStateChanged;
        SyncMusicToState();
    }

    private void OnDisable()
    {
        GameEvents.BladeThrown -= OnBladeThrown;
        GameEvents.EnemyHit -= OnEnemyHit;
        GameEvents.RecallStarted -= OnRecallStarted;
        GameEvents.EnemyKilled -= OnEnemyKilled;
        GameEvents.PlayerHit -= OnPlayerHit;
        GameEvents.BarrelExploded -= OnBarrelExploded;
        GameEvents.CoinCollected -= OnCoinCollected;
        GameEvents.UpgradePurchased -= OnUpgradePurchased;
        GameEvents.GameStateChanged -= OnGameStateChanged;
        if (Instance == this)
        {
            StopMusic();
            if (sfxSources != null)
            {
                foreach (AudioSource source in sfxSources)
                    if (source != null) source.Stop();
            }
        }
    }

    private void Start()
    {
        if (Instance == this)
            SyncMusicToState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
            Instance = null;
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused && Instance == this)
            PlayerPrefs.Save();
    }

    private void OnApplicationQuit()
    {
        if (Instance == this)
            PlayerPrefs.Save();
    }

    public void PlaySfx(SfxId id, float pitchJitter = 0.1f)
    {
        int index = (int)id;
        if (Instance != this || !isActiveAndEnabled || index < 0 || index >= clips.Length
            || clips[index] == null || sfxSources == null || sfxSources.Length == 0)
            return;

        AudioSource source = null;
        for (int i = 0; i < sfxSources.Length; i++)
        {
            int voice = (nextVoice + i) % sfxSources.Length;
            if (sfxSources[voice] != null && !sfxSources[voice].isPlaying)
            {
                source = sfxSources[voice];
                nextVoice = (voice + 1) % sfxSources.Length;
                break;
            }
        }
        if (source == null)
        {
            source = sfxSources[nextVoice];
            nextVoice = (nextVoice + 1) % sfxSources.Length;
        }
        if (source == null)
            return;

        source.Stop();
        source.clip = clips[index];
        source.volume = sfxVolume;
        float jitter = float.IsNaN(pitchJitter) ? 0f : Mathf.Clamp(pitchJitter, 0f, 0.9f);
        source.pitch = UnityEngine.Random.Range(1f - jitter, 1f + jitter);
        source.Play();
    }

    public void PlayMusic()
    {
        if (Instance != this || !isActiveAndEnabled || musicClip == null)
            return;

        PrepareMusicSource();
        if (musicSource.clip == musicClip && musicSource.isPlaying)
            return;
        musicSource.clip = musicClip;
        musicSource.Play();
    }

    public void StopMusic()
    {
        if (musicSource != null)
            musicSource.Stop();
    }

    private void PrepareMusicSource()
    {
        if (musicSource == null)
            musicSource = gameObject.AddComponent<AudioSource>();
        musicSource.playOnAwake = false;
        musicSource.loop = true;
        musicSource.spatialBlend = 0f;
        musicSource.volume = musicVolume;
        musicSource.pitch = 1f;
    }

    private void OnGameStateChanged(GameState state)
    {
        if (state == GameState.Playing)
            PlayMusic();
        else if (state == GameState.MainMenu || state == GameState.GameOver || state == GameState.Victory)
            StopMusic();
    }

    private void SyncMusicToState()
    {
        if (GameManager.Instance == null)
            return;
        GameState state = GameManager.Instance.State;
        if (state == GameState.Playing || state == GameState.Paused || state == GameState.Shop)
            PlayMusic();
        else
            StopMusic();
    }

    private void OnBladeThrown(Vector2 position) => PlaySfx(SfxId.Throw);
    private void OnEnemyHit(Vector2 position, bool fromRecall) => PlaySfx(SfxId.Hit);
    private void OnRecallStarted() => PlaySfx(SfxId.Recall);
    private void OnEnemyKilled(Vector2 position, EnemyType type) => PlaySfx(SfxId.EnemyDie);
    private void OnPlayerHit(Vector2 position) => PlaySfx(SfxId.PlayerHurt);
    private void OnBarrelExploded(Vector2 position) => PlaySfx(SfxId.Explosion);
    private void OnCoinCollected(Vector2 position, int amount) => PlaySfx(SfxId.Coin);
    private void OnUpgradePurchased(UpgradeType type, int newLevel) => PlaySfx(SfxId.Purchase);

    private void OnValidate()
    {
        sfxVoiceCount = Mathf.Clamp(sfxVoiceCount, 1, 32);
        sfxVolume = Mathf.Clamp01(sfxVolume);
        musicVolume = Mathf.Clamp01(musicVolume);
    }
}
