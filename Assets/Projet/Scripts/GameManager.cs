using UnityEngine;
using UnityEngine.Events;
using UnityEngine.XR;
using UnityEngine.InputSystem;

public enum GameMode { Defouloir, Recette }
public enum Difficulty { Facile, Moyen, Difficile }
public enum WeaponType { Couteaux, SabreLaser }

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Settings")]
    public float gameDuration = 300f; // 5 minutes
    public GameMode currentMode = GameMode.Defouloir;
    public Difficulty difficulty = Difficulty.Moyen;
    public WeaponType weapon = WeaponType.Couteaux;

    [Header("State")]
    public float currentTime;
    public int score;
    public int fruitsSliced;
    public int bombsHit;
    public bool isPlaying;

    [Header("Arcade Systems")]
    public int currentStrikes = 0;
    public int maxStrikes = 3;
    public int currentComboCount = 0;
    public float lastSliceTime = 0f;
    public float comboTimerWindow = 1.2f; // Fenêtre large pour combo plus facile

    [Header("Audio")]
    [Tooltip("Musique jouée pendant la partie.")]
    public AudioClip backgroundMusic;
    [Tooltip("Musique joue dans le menu.")]
    public AudioClip menuMusic;
    [Tooltip("Son court joué au lancement de la partie.")]
    public AudioClip gameStartSound;
    [Tooltip("Son joué lors de l'écran Game Over.")]
    public AudioClip gameOverSound;
    [Tooltip("Son joué lorsqu'un combo est réalisé.")]
    public AudioClip comboSound;
    [Tooltip("Son 'tic-tac' joué quand le temps presse.")]
    public AudioClip timerTickSound;
    [Tooltip("Son joué quand le temps est écoulé.")]
    public AudioClip timerUpSound;

    private AudioSource bgmSource;
    private AudioSource sfxSource;
    private float lastTickPlayedTime = 11f;

    public UnityEvent OnGameStart;
    public UnityEvent OnGameOver;
    public UnityEvent<int> OnScoreChanged;
    public UnityEvent<float> OnTimeChanged;
    public UnityEvent<int, int> OnStrikeChanged; // current, max
    public UnityEvent<int, float> OnComboTriggered; // fruits in combo, score multiplier
    public UnityEvent<string> OnRecipeStringUpdated; // Liste des fruits à couper
    public UnityEvent<float> OnRecipeProgressChanged; // 0..1
    public UnityEvent OnRecipeCompleted;

    private System.Collections.Generic.Dictionary<string, int> currentRecipe = new System.Collections.Generic.Dictionary<string, int>();
    private int recipeTotalCount;
    private int recipesCompleted;

    [Header("Dev / Safety")]
    [Tooltip("If no MenuManager is present, auto-create one at runtime so the game can start via menu.")]
    public bool autoCreateMenuManager = true;

    [Tooltip("Editor-only fallback: if no menu exists, auto-start after a short delay.")]
    public bool autoStartInEditorIfNoMenu = true;
    public float autoStartDelaySeconds = 0.75f;

    // Menu input edge-detection (avoid firing every frame while held).
    private bool prevMenuKey;
    private bool prevLeftMenuButton;
    private bool prevRightMenuButton;
    private bool prevRightPrimaryButton;

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
        // On ne dmarre plus la partie automatiquement. Le MenuManager va s'en charger !
        ResetGame();

        // Jouer la musique du menu au dmarrage
        if (menuMusic != null && bgmSource != null)
        {
            bgmSource.clip = menuMusic;
            bgmSource.Play();
        }

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
            else
            {
                HandleTimerSounds();
            }

            // Gestion de l'expiration du Combo
            if (currentComboCount > 0 && Time.time - lastSliceTime > comboTimerWindow)
            {
                if (currentComboCount >= 3)
                {
                    float multiplier = 1f + (currentComboCount * 0.1f);
                    OnComboTriggered?.Invoke(currentComboCount, multiplier);
                    
                    // Jouer le son de Combo
                    if (comboSound != null && sfxSource != null)
                    {
                        sfxSource.PlayOneShot(comboSound);
                    }
                    
                    // Donner un bonus de points final
                    int bonus = Mathf.RoundToInt(currentComboCount * multiplier);
                    score += bonus;
                    OnScoreChanged?.Invoke(score);
                }
                currentComboCount = 0; // Reset
            }
        }

        HandleMenuInput();
        HandleRestartInput();
    }

    private void HandleMenuInput()
    {
        bool menuPressedThisFrame = false;

        // Clavier (New Input System)
        bool keyNow = Keyboard.current != null && Keyboard.current[Key.M].isPressed;
        if (keyNow && !prevMenuKey) menuPressedThisFrame = true;
        prevMenuKey = keyNow;

        // VR Controllers (Primary Button ou Menu Button)
        UnityEngine.XR.InputDevice leftHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.LeftHand);
        bool leftNow = false;
        if (leftHand.isValid && leftHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool lb)) leftNow = lb;
        if (leftNow && !prevLeftMenuButton) menuPressedThisFrame = true;
        prevLeftMenuButton = leftNow;
        
        UnityEngine.XR.InputDevice rightHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        bool rightNow = false;
        if (rightHand.isValid && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.menuButton, out bool rb)) rightNow = rb;
        if (rightNow && !prevRightMenuButton) menuPressedThisFrame = true;
        prevRightMenuButton = rightNow;

        if (menuPressedThisFrame)
        {
            var mm = FindFirstObjectByType<MenuManager>();
            if (mm != null)
            {
                EndGame();
                mm.ShowMainMenu();
            }
        }
    }

    private void HandleRestartInput()
    {
        // Keyboard fallback (Editor)
        bool restartPressedThisFrame = false;
        if (Keyboard.current != null && Keyboard.current[Key.R].isPressed && !prevRightPrimaryButton)
            restartPressedThisFrame = true;

        // Right hand controller primary button (PICO: usually A / X depending on profile).
        UnityEngine.XR.InputDevice rightHand = UnityEngine.XR.InputDevices.GetDeviceAtXRNode(UnityEngine.XR.XRNode.RightHand);
        bool rightPrimaryNow = false;
        if (rightHand.isValid && rightHand.TryGetFeatureValue(UnityEngine.XR.CommonUsages.primaryButton, out bool pb))
            rightPrimaryNow = pb;

        if (rightPrimaryNow && !prevRightPrimaryButton)
            restartPressedThisFrame = true;

        prevRightPrimaryButton = rightPrimaryNow;

        if (!restartPressedThisFrame) return;

        // Restart current mode with current settings.
        StartGame();
        var mm = FindFirstObjectByType<MenuManager>();
        if (mm != null)
        {
            // Hide menus if they are up
            mm.SendMessage("HideMenusAndShowGameUI", SendMessageOptions.DontRequireReceiver);
        }
    }

    private void HandleTimerSounds()
    {
        if (currentTime <= 10.1f && currentTime > 0)
        {
            // Joue un son toutes les secondes environ
            if (Mathf.FloorToInt(currentTime) != Mathf.FloorToInt(lastTickPlayedTime))
            {
                if (timerTickSound != null && sfxSource != null)
                {
                    sfxSource.PlayOneShot(timerTickSound);
                }
                lastTickPlayedTime = currentTime;
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

        if (currentMode == GameMode.Recette)
        {
            GenerateRandomRecipe();
        }

        if (bgmSource != null && bgmSource.isPlaying)
        {
            bgmSource.Stop();
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

        // Retour  la musique du menu
        if (menuMusic != null && bgmSource != null)
        {
            bgmSource.clip = menuMusic;
            bgmSource.Play();
        }

        if (gameOverSound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(gameOverSound);
        }

        if (timerUpSound != null && sfxSource != null)
        {
            sfxSource.PlayOneShot(timerUpSound);
        }
    }

    public void ResetGame()
    {
        isPlaying = false;
        currentTime = gameDuration;
        score = 0;
        fruitsSliced = 0;
        bombsHit = 0;
        currentStrikes = 0;
        currentComboCount = 0;
        lastTickPlayedTime = 11f;
        recipesCompleted = 0;
        recipeTotalCount = 0;
        OnScoreChanged?.Invoke(score);
        OnTimeChanged?.Invoke(currentTime);
        OnStrikeChanged?.Invoke(currentStrikes, maxStrikes);
        OnRecipeProgressChanged?.Invoke(0f);
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
        
        // Multiplicateur actif si on est en train de combo
        float multiplier = 1f;
        if (currentComboCount >= 3) multiplier = 1f + (currentComboCount * 0.1f);
        
        score += Mathf.RoundToInt(points * multiplier);
        OnScoreChanged?.Invoke(score);
    }

    public void AddTimePenalty(float penaltySeconds) // Gardé au cas où, mais non utilisé par défaut
    {
        if (!isPlaying) return;
        currentTime -= penaltySeconds;
        if (currentTime <= 0) EndGame();
    }

    public void AddFruitSliced()
    {
        if (!isPlaying) return;
        
        fruitsSliced++;
        
        if (Time.time - lastSliceTime <= comboTimerWindow)
        {
            currentComboCount++;
        }
        else
        {
            currentComboCount = 1;
        }
        
        lastSliceTime = Time.time;
    }

    public void GenerateRandomRecipe()
    {
        currentRecipe.Clear();
        // Must match existing prefabs' `FruitTarget.ingredientName` values.
        // Current project prefabs: Pomme, Citron, Fraise, Banane, Cerise, Pêche, Pasteque.
        string[] allFruits = { "Pomme", "Citron", "Fraise", "Banane", "Cerise", "Pêche", "Pasteque" };
        
        // Choisir 2 ou 3 types de fruits au hasard
        int typesCount = Random.Range(2, 4);
        System.Collections.Generic.List<string> selectedTypes = new System.Collections.Generic.List<string>();
        
        while (selectedTypes.Count < typesCount)
        {
            string f = allFruits[Random.Range(0, allFruits.Length)];
            if (!selectedTypes.Contains(f)) selectedTypes.Add(f);
        }

        foreach (string fruit in selectedTypes)
        {
            currentRecipe[fruit] = Random.Range(2, 5); // Entre 2 et 4 de chaque
        }

        recipeTotalCount = GetRecipeRemainingCount();
        UpdateRecipeString();
        OnRecipeProgressChanged?.Invoke(0f);
    }

    private void UpdateRecipeString()
    {
        if (currentRecipe.Count == 0)
        {
            OnRecipeStringUpdated?.Invoke("Recette terminée !");
            return;
        }

        string s = "";
        foreach (var kvp in currentRecipe)
        {
            s += $"{kvp.Value} {kvp.Key}(s), ";
        }
        OnRecipeStringUpdated?.Invoke(s.TrimEnd(',', ' '));
    }

    private int GetRecipeRemainingCount()
    {
        int remaining = 0;
        foreach (var kv in currentRecipe) remaining += Mathf.Max(0, kv.Value);
        return remaining;
    }

    private void EmitRecipeProgress()
    {
        if (recipeTotalCount <= 0)
        {
            OnRecipeProgressChanged?.Invoke(0f);
            return;
        }
        var remaining = GetRecipeRemainingCount();
        var progress = Mathf.Clamp01(1f - (remaining / (float)recipeTotalCount));
        OnRecipeProgressChanged?.Invoke(progress);
    }

    public void ProcessFruitSlice(string fruitName)
    {
        if (!isPlaying) return;

        // On appelle la logique de combo de base
        AddFruitSliced();

        if (currentMode == GameMode.Recette)
        {
            if (currentRecipe.ContainsKey(fruitName))
            {
                // Succes : Fruit dans la commande
                currentRecipe[fruitName]--;
                if (currentRecipe[fruitName] <= 0) currentRecipe.Remove(fruitName);
                
                UpdateRecipeString();
                EmitRecipeProgress();

                // Si tout est fini, on regenere une petite suite
                if (currentRecipe.Count == 0)
                {
                    recipesCompleted++;
                    // Mode Recette: le score = nombre de recettes réussies (objectif: en faire un max).
                    score = recipesCompleted;
                    OnScoreChanged?.Invoke(score);
                    OnRecipeCompleted?.Invoke();
                    GenerateRandomRecipe();
                }
            }
            else
            {
                // Erreur : Mauvais fruit ! -> Strike (XXX)
                AddBombHit(); // On reutilise AddBombHit car elle gere les strikes et le game over
            }
        }
        else
        {
            // Mode Defouloir : Juste des points et combo
            AddScore(10);
        }
    }
    public void AddBombHit()
    {
        if (!isPlaying) return;
        
        bombsHit++;
        currentComboCount = 0; // Perte du combo
        
        currentStrikes++;
        OnStrikeChanged?.Invoke(currentStrikes, maxStrikes);
        
        if (currentStrikes >= maxStrikes)
        {
            EndGame();
        }
    }
}
