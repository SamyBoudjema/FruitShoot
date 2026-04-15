using UnityEngine;
using UnityEngine.Events;

public enum GameMode { Defouloir, Recette }
public enum Difficulty { Facile, Moyen, Difficile }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Settings")]
    public float gameDuration = 300f; // 5 minutes
    public GameMode currentMode = GameMode.Defouloir;
    public Difficulty difficulty = Difficulty.Moyen;

    [Header("State")]
    public float currentTime;
    public int score;
    public int fruitsSliced;
    public int bombsHit;
    public bool isPlaying;

    [Header("Audio")]
    [Tooltip("Musique jouée pendant la partie.")]
    public AudioClip backgroundMusic;
    [Tooltip("Son court joué au lancement de la partie.")]
    public AudioClip gameStartSound;
    [Tooltip("Son joué lors de l'écran Game Over.")]
    public AudioClip gameOverSound;

    private AudioSource bgmSource;
    private AudioSource sfxSource;

    public UnityEvent OnGameStart;
    public UnityEvent OnGameOver;
    public UnityEvent<int> OnScoreChanged;
    public UnityEvent<float> OnTimeChanged;

    [Header("Dev / Safety")]
    [Tooltip("If no MenuManager is present, auto-create one at runtime so the game can start via menu.")]
    public bool autoCreateMenuManager = true;

    [Tooltip("Editor-only fallback: if no menu exists, auto-start after a short delay.")]
    public bool autoStartInEditorIfNoMenu = true;
    public float autoStartDelaySeconds = 0.75f;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            SetupAudioSource();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    private void SetupAudioSource()
    {
        bgmSource = gameObject.AddComponent<AudioSource>();
        bgmSource.loop = true;
        bgmSource.playOnAwake = false;
        bgmSource.volume = 0.35f; // Volume d'ambiance plus bas

        sfxSource = gameObject.AddComponent<AudioSource>();
        sfxSource.loop = false;
        sfxSource.playOnAwake = false;
        sfxSource.volume = 0.8f; // Effets sonores plus forts
    }

    private void Start()
    {
        // On ne démarre plus la partie automatiquement. Le MenuManager va s'en charger !
        ResetGame();

        EnsureMenuManagerExists();

#if UNITY_EDITOR
        if (autoStartInEditorIfNoMenu && FindFirstObjectByType<MenuManager>() == null)
        {
            Invoke(nameof(EditorAutoStart), Mathf.Max(0.05f, autoStartDelaySeconds));
        }
#endif
    }

    private void Update()
    {
        if (isPlaying)
        {
            currentTime -= Time.deltaTime;
            OnTimeChanged?.Invoke(currentTime);

            if (currentTime <= 0)
            {
                EndGame();
            }
        }
    }

    private void EnsureMenuManagerExists()
    {
        if (!autoCreateMenuManager) return;
        if (FindFirstObjectByType<MenuManager>() != null) return;

        var go = new GameObject("MenuManager (Auto)");
        go.AddComponent<MenuManager>();
    }

    private void EditorAutoStart()
    {
#if UNITY_EDITOR
        // If we got here, we failed to create a menu. Don't block iteration.
        if (!isPlaying) StartGame();
#endif
    }

    public void StartGame()
    {
        ResetGame();
        isPlaying = true;
        OnGameStart?.Invoke();

        if (gameStartSound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(gameStartSound);
        }

        if (backgroundMusic != null && bgmSource != null)
        {
            bgmSource.clip = backgroundMusic;
            bgmSource.Play();
        }
    }

    public void EndGame()
    {
        isPlaying = false;
        currentTime = 0;
        OnTimeChanged?.Invoke(currentTime);
        OnGameOver?.Invoke();

        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
        }

        if (gameOverSound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(gameOverSound);
        }
    }

    public void ResetGame()
    {
        isPlaying = false;
        currentTime = gameDuration;
        score = 0;
        fruitsSliced = 0;
        bombsHit = 0;
        OnScoreChanged?.Invoke(score);
        OnTimeChanged?.Invoke(currentTime);
        // If Recette mode, reset recipe here
    }

    public float GetDifficultyDelayMultiplier()
    {
        return difficulty switch
        {
            Difficulty.Facile => 1.25f,
            Difficulty.Moyen => 1.0f,
            Difficulty.Difficile => 0.45f, // Fréquence extrême en difficile
            _ => 1.0f
        };
    }

    public float GetDifficultyForceMultiplier()
    {
        return difficulty switch
        {
            Difficulty.Facile => 0.95f,
            Difficulty.Moyen => 1.0f,
            Difficulty.Difficile => 1.35f, // Jaillissement plus haut / plus aléatoire
            _ => 1.0f
        };
    }

    public float GetDifficultyDragMultiplier()
    {
        return difficulty switch
        {
            Difficulty.Facile => 1.10f,
            Difficulty.Moyen => 1.0f,
            Difficulty.Difficile => 0.65f, // Retombe beaucoup plus vite
            _ => 1.0f
        };
    }

    public void AddScore(int points)
    {
        if (!isPlaying) return;
        score += points;
        OnScoreChanged?.Invoke(score);
    }

    public void AddTimePenalty(float penaltySeconds) // For bombs
    {
        if (!isPlaying) return;
        currentTime -= penaltySeconds;
        if (currentTime <= 0)
        {
            EndGame();
        }
    }

    public void AddFruitSliced()
    {
        if (isPlaying) fruitsSliced++;
    }

    public void AddBombHit()
    {
        if (isPlaying) bombsHit++;
    }
}
