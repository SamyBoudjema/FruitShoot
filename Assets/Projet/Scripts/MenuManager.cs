using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using TMPro;
using System;
using System.Reflection;

public class MenuManager : MonoBehaviour
{
    [Header("UI Panels")]
    public GameObject mainMenuPanel;    // Le menu avec les boutons de mode de jeu
    public GameObject gameUIPanel;      // L'interface en jeu (Score, Temps, etc.)
    public GameObject gameOverPanel;    // Le menu de fin de partie
    public GameObject leaderboardPanel; // Panneau séparé à droite (Game Over)

    [Header("UI Anchor (fixed world position)")]
    [Tooltip("Place a scene object named UI_Anchor and assign it here to keep menus in a fixed position.")]
    public Transform uiAnchor;

    [Header("HUD Anchor (fixed world position)")]
    [Tooltip("Optional: scene object named HUD_Anchor. If present, the in-game HUD is placed here.")]
    public Transform hudAnchor;

    [Header("Leaderboard Anchor (fixed world position)")]
    [Tooltip("Optional: scene object named Leaderboard_Anchor. If present, the leaderboard tablet is placed here.")]
    public Transform leaderboardAnchor;

    [Header("Defouloir options (menu)")]
    public float defouloirDurationMinutes = 5f;
    public Difficulty defouloirDifficulty = Difficulty.Moyen;

    private GameMode selectedMode = GameMode.Defouloir;
    
    // Conteneurs pour les sous-menus
    private GameObject groupModeSelection;
    private GameObject groupConfig;

    // Lignes d'options (pour les masquer/afficher)
    private GameObject rowDuration;
    private GameObject rowDifficulty;
    private GameObject rowWeapon;

    private Button btnSelectDefouloir;
    private Button btnSelectRecette;

    private TextMeshProUGUI goScoreText;
    private TextMeshProUGUI goFruitsText;
    private TextMeshProUGUI goBombsText;

    // Leaderboard (separate panel)
    private TextMeshProUGUI leaderboardText;
    private TMP_InputField nameInputField;
    private const string LeaderboardPrefsKey_Defouloir = "edn.leaderboard.defouloir.v1";
    private const string LeaderboardPrefsKey_Recette = "edn.leaderboard.recette.v1";

    [Serializable]
    private class LeaderboardEntry
    {
        public string name;
        public int score;
    }

    [Serializable]
    private class LeaderboardData
    {
        public System.Collections.Generic.List<LeaderboardEntry> entries = new System.Collections.Generic.List<LeaderboardEntry>();
    }

    private WeaponType selectedWeapon = WeaponType.Couteaux;
    private Button btnWeaponKnives;
    private Button btnWeaponSabre;

    // Fixed UI pose (captured once at spawn so menus don't appear "where you look").
    private bool fixedUiPoseCaptured;
    private Vector3 fixedMenuPosition;
    private Quaternion fixedMenuRotation;

    private void Start()
    {
        // If MenuManager is auto-created, we can't assign references in Inspector.
        // Auto-find a scene anchor if present.
        if (uiAnchor == null)
        {
            var anchorGo = GameObject.Find("UI_Anchor");
            if (anchorGo != null) uiAnchor = anchorGo.transform;
        }
        if (hudAnchor == null)
        {
            var hudGo = GameObject.Find("HUD_Anchor");
            if (hudGo != null) hudAnchor = hudGo.transform;
        }
        if (leaderboardAnchor == null)
        {
            var lbGo = GameObject.Find("Leaderboard_Anchor");
            if (lbGo != null) leaderboardAnchor = lbGo.transform;
        }

        CaptureFixedUiPoseIfNeeded();
        AutoWirePanelsIfNeeded();
        EnsureEventSystemExists();
        EnsureXRInteractionManagerExists();
        EnsureRightHandRayInteractorForUI();
        EnsureMenuUIExists();
        EnsureBannerHudLayout();
        ShowMainMenu();

        // On écoute l'événement de fin de partie du GameManager pour réafficher un menu
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnGameOver.AddListener(ShowGameOverMenu);
        }
    }

    public void ShowMainMenu()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(true);
        if (gameUIPanel != null) gameUIPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);

        ShowModeSelection(); // On revient toujours au choix du mode au dbut
        TeleportPlayerInFrontOfMenu();
    }

    public void ShowModeSelection()
    {
        if (groupModeSelection != null) groupModeSelection.SetActive(true);
        if (groupConfig != null) groupConfig.SetActive(false);
    }

    public void ShowModeConfig(GameMode mode)
    {
        selectedMode = mode;
        if (groupModeSelection != null) groupModeSelection.SetActive(false);
        if (groupConfig != null) groupConfig.SetActive(true);

        // On masque/affiche les lignes selon le mode
        bool isDefouloir = (mode == GameMode.Defouloir);
        if (rowDuration != null) rowDuration.SetActive(isDefouloir);
        if (rowDifficulty != null) rowDifficulty.SetActive(isDefouloir);
        // Recette: uniquement couteaux (pas de sabre).
        if (!isDefouloir)
            selectedWeapon = WeaponType.Couteaux;
        if (rowWeapon != null) rowWeapon.SetActive(isDefouloir);

        // On ajuste le titre de la config
        var title = groupConfig.transform.Find("ConfigTitle")?.GetComponent<TextMeshProUGUI>();
        if (title != null) title.text = isDefouloir ? "CONFIGURATION DÉFOULOIR" : "CONFIGURATION RECETTE";
    }

    public void StartMode()
    {
        // On lance le mode qui a été sélectionné précédemment
        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentMode = selectedMode;
            // Defouloir uses configurable duration; Recette uses a shorter fixed session by default.
            GameManager.Instance.gameDuration = (selectedMode == GameMode.Recette)
                ? 150f // 2m30
                : Mathf.Clamp(defouloirDurationMinutes, 1f, 20f) * 60f;
            GameManager.Instance.difficulty = defouloirDifficulty;
            // Recette: forcer couteaux
            GameManager.Instance.weapon = (selectedMode == GameMode.Recette) ? WeaponType.Couteaux : selectedWeapon;
            
            GameManager.Instance.StartGame();
        }
        RefreshGameUiForMode();
        HideMenusAndShowGameUI();
    }

    private void HideMenusAndShowGameUI()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (leaderboardPanel != null) leaderboardPanel.SetActive(false);

        // On s'assure d'avoir bien raccordé le HUD avant de l'afficher
        AutoWirePanelsIfNeeded();

        if (gameUIPanel != null) 
        {
            gameUIPanel.SetActive(true);
            gameUIPanel.layer = LayerMask.NameToLayer("UI");
            
            // On s'assure que le Canvas est bien en WorldSpace pour être positionné
            var canvas = gameUIPanel.GetComponent<Canvas>();
            if (canvas != null) 
            {
                canvas.renderMode = RenderMode.WorldSpace;
            }

            // Place HUD using anchor if present (fixed in world).
            if (hudAnchor != null)
            {
                gameUIPanel.transform.position = hudAnchor.position;
                gameUIPanel.transform.rotation = hudAnchor.rotation;
                gameUIPanel.transform.localScale = Vector3.one * 0.007f;
            }
            else
            {
                // Fallback: in front of the player
                var cam = Camera.main;
                if (cam != null)
                {
                    var fwd = GetFlattenedForward(cam);
                    gameUIPanel.transform.position = cam.transform.position + fwd * 6.5f + Vector3.up * 3.2f;
                    gameUIPanel.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
                    gameUIPanel.transform.localScale = Vector3.one * 0.007f;
                }
            }
        }
    }

    public void ShowGameOverMenu()
    {
        if (gameUIPanel != null) gameUIPanel.SetActive(false);
        EnsureGameOverUIExists();
        EnsureLeaderboardUIExists();
        if (gameOverPanel != null) 
        {
            gameOverPanel.SetActive(true);
            RefreshGameOverStats();
        }

        PositionLeaderboardPanel();
    }

    private void EnsureGameOverUIExists()
    {
        if (gameOverPanel != null) return;
        gameOverPanel = CreateGameOverCanvas();
    }

    private void EnsureLeaderboardUIExists()
    {
        if (leaderboardPanel != null) return;
        leaderboardPanel = CreateLeaderboardCanvas();
    }

    private void RefreshGameOverStats()
    {
        if (GameManager.Instance == null) return;
        if (goScoreText != null) goScoreText.text = $"SCORE FINAL : {GameManager.Instance.score}";
        if (goFruitsText != null) goFruitsText.text = $"FRUITS TRANCHÉS : {GameManager.Instance.fruitsSliced}";
        // goBombsText n'est plus utilisé

        RefreshLeaderboardUI();
    }

    private GameObject CreateGameOverCanvas()
    {
        var canvasGo = new GameObject("GameOver_Canvas");
        canvasGo.layer = LayerMask.NameToLayer("UI");

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;

        canvasGo.AddComponent<GraphicRaycaster>();
        TryAddComponentByName(
            canvasGo,
            "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit"
        );

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(900, 720);
        rect.localScale = Vector3.one * 0.003f;
        rect.position = GetFixedMenuPosition();
        rect.rotation = GetFixedMenuRotation();

        var panelGo = new GameObject("Panel");
        panelGo.layer = canvasGo.layer;
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0.18f, 0.18f, 0.18f, 1f); // Gris anthracite moderne
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;

        // BORDURES FINES
        CreateAccentBar(panelGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), 2f, new Color(1f, 1f, 1f, 0.15f));
        CreateAccentBar(panelGo.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2), 2f, new Color(1f, 1f, 1f, 0.15f));

        var title = CreateLabel(panelGo.transform, "PARTIE TERMINÉE", 75);
        title.color = Color.white;
        title.fontStyle = FontStyles.Bold;
        title.rectTransform.anchoredPosition = new Vector2(0, 240);

        var perfLabel = CreateLabel(panelGo.transform, "-- PERFORMANCE --", 25);
        perfLabel.color = new Color(1f, 1f, 1f, 0.3f);
        perfLabel.rectTransform.anchoredPosition = new Vector2(0, 160);

        goScoreText = CreateLabel(panelGo.transform, "SCORE FINAL : 0", 60);
        goScoreText.color = Color.white;
        goScoreText.rectTransform.anchoredPosition = new Vector2(0, 70);

        goFruitsText = CreateLabel(panelGo.transform, "FRUITS TRANCHÉS : 0", 45);
        goFruitsText.color = new Color(0.8f, 0.8f, 0.85f);
        goFruitsText.rectTransform.anchoredPosition = new Vector2(0, -30);

        var btnReturn = CreateButton(panelGo.transform, "Bouton_Retour", "RETOUR AU MENU");
        btnReturn.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.08f);
        var rtBtn = btnReturn.GetComponent<RectTransform>();
        rtBtn.sizeDelta = new Vector2(520, 105);
        rtBtn.anchoredPosition = new Vector2(0, -240);
        btnReturn.onClick.AddListener(ShowMainMenu);

        return canvasGo;
    }

    private GameObject CreateLeaderboardCanvas()
    {
        var canvasGo = new GameObject("Leaderboard_Canvas");
        canvasGo.layer = LayerMask.NameToLayer("UI");

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;

        canvasGo.AddComponent<GraphicRaycaster>();
        TryAddComponentByName(
            canvasGo,
            "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit"
        );

        var rect = canvasGo.GetComponent<RectTransform>();
        rect.sizeDelta = new Vector2(520, 720);
        rect.localScale = Vector3.one * 0.003f;
        rect.position = GetFixedMenuPosition();
        rect.rotation = GetFixedMenuRotation();

        var panelGo = new GameObject("Panel");
        panelGo.layer = canvasGo.layer;
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0.18f, 0.18f, 0.18f, 1f);
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;

        var lbTitle = CreateLabel(panelGo.transform, "LEADERBOARD", 30);
        lbTitle.color = Color.white;
        lbTitle.fontStyle = FontStyles.Bold;
        lbTitle.rectTransform.anchoredPosition = new Vector2(0, 300);
        lbTitle.rectTransform.sizeDelta = new Vector2(480, 50);

        leaderboardText = CreateLabel(panelGo.transform, "1. ---\n2. ---\n3. ---", 26);
        leaderboardText.alignment = TextAlignmentOptions.TopLeft;
        leaderboardText.enableWordWrapping = false;
        leaderboardText.color = new Color(1f, 1f, 1f, 0.80f);
        // Keep text fully inside the panel.
        leaderboardText.rectTransform.anchoredPosition = new Vector2(-10, 225);
        leaderboardText.rectTransform.sizeDelta = new Vector2(460, 360);

        var nameLabel = CreateLabel(panelGo.transform, "TON NOM :", 22);
        nameLabel.alignment = TextAlignmentOptions.Left;
        nameLabel.color = new Color(1f, 1f, 1f, 0.55f);
        nameLabel.rectTransform.anchoredPosition = new Vector2(-195, -210);
        nameLabel.rectTransform.sizeDelta = new Vector2(340, 40);

        nameInputField = CreateNameInputField(panelGo.transform);
        var nameRt = nameInputField.GetComponent<RectTransform>();
        nameRt.anchoredPosition = new Vector2(0, -255);
        nameRt.sizeDelta = new Vector2(430, 70);

        var btnSave = CreateButton(panelGo.transform, "Btn_SaveScore", "ENREGISTRER");
        btnSave.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.10f);
        var rtSave = btnSave.GetComponent<RectTransform>();
        rtSave.sizeDelta = new Vector2(430, 85);
        // Keep inside bottom bounds.
        rtSave.anchoredPosition = new Vector2(0, -335);
        btnSave.onClick.AddListener(SaveLeaderboardNameAndScore);

        canvasGo.SetActive(false);
        return canvasGo;
    }

    private void PositionLeaderboardPanel()
    {
        if (leaderboardPanel == null) return;
        if (gameOverPanel == null) return;
        leaderboardPanel.SetActive(true);
        if (leaderboardAnchor != null)
        {
            leaderboardPanel.transform.position = leaderboardAnchor.position;
            leaderboardPanel.transform.rotation = leaderboardAnchor.rotation;
            return;
        }

        // Fallback: tablet next to GameOver (world-space).
        leaderboardPanel.transform.position = gameOverPanel.transform.position + gameOverPanel.transform.right * 1.25f;
        leaderboardPanel.transform.rotation = gameOverPanel.transform.rotation;
    }

    private TMP_InputField CreateNameInputField(Transform parent)
    {
        var root = new GameObject("NameInputField");
        root.layer = parent.gameObject.layer;
        root.transform.SetParent(parent, false);

        var img = root.AddComponent<Image>();
        img.color = new Color(0f, 0f, 0f, 0.25f);

        var input = root.AddComponent<TMP_InputField>();

        // Text
        var textGo = new GameObject("Text");
        textGo.layer = root.layer;
        textGo.transform.SetParent(root.transform, false);
        var text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = "";
        text.fontSize = 34;
        text.color = new Color(0.9f, 0.95f, 1f, 0.95f);
        text.alignment = TextAlignmentOptions.MidlineLeft;
        text.enableWordWrapping = false;
        var textRt = text.rectTransform;
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(18, 10);
        textRt.offsetMax = new Vector2(-18, -10);

        // Placeholder
        var phGo = new GameObject("Placeholder");
        phGo.layer = root.layer;
        phGo.transform.SetParent(root.transform, false);
        var ph = phGo.AddComponent<TextMeshProUGUI>();
        ph.text = "Entre ton nom...";
        ph.fontSize = 32;
        ph.color = new Color(1f, 1f, 1f, 0.25f);
        ph.alignment = TextAlignmentOptions.MidlineLeft;
        ph.enableWordWrapping = false;
        var phRt = ph.rectTransform;
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = new Vector2(18, 10);
        phRt.offsetMax = new Vector2(-18, -10);

        input.textComponent = text;
        input.placeholder = ph;
        input.characterLimit = 14;
        input.contentType = TMP_InputField.ContentType.Name;

        return input;
    }

    private void SaveLeaderboardNameAndScore()
    {
        if (GameManager.Instance == null) return;
        if (nameInputField == null) return;

        var name = (nameInputField.text ?? "").Trim();
        if (string.IsNullOrWhiteSpace(name)) return;

        var data = LoadLeaderboard();
        var existing = data.entries.Find(e => string.Equals(e.name, name, StringComparison.OrdinalIgnoreCase));
        if (existing == null)
        {
            data.entries.Add(new LeaderboardEntry { name = name, score = GameManager.Instance.score });
        }
        else
        {
            // Update score (use latest, as requested).
            existing.score = GameManager.Instance.score;
            existing.name = name; // keep user's capitalization
        }

        data.entries.Sort((a, b) => b.score.CompareTo(a.score));
        if (data.entries.Count > 12) data.entries.RemoveRange(12, data.entries.Count - 12);

        PlayerPrefs.SetString(GetLeaderboardKeyForCurrentMode(), JsonUtility.ToJson(data));
        PlayerPrefs.Save();
        RefreshLeaderboardUI();
    }

    private void RefreshLeaderboardUI()
    {
        if (leaderboardText == null) return;
        var data = LoadLeaderboard();
        data.entries.Sort((a, b) => b.score.CompareTo(a.score));

        int max = Mathf.Min(8, data.entries.Count);
        if (max <= 0)
        {
            leaderboardText.text = "1. ---\n2. ---\n3. ---\n4. ---\n5. ---";
            return;
        }

        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < max; i++)
        {
            var e = data.entries[i];
            sb.Append($"{i + 1}. {e.name}  —  {e.score}");
            if (i != max - 1) sb.Append('\n');
        }
        leaderboardText.text = sb.ToString();
    }

    private static LeaderboardData LoadLeaderboard()
    {
        try
        {
            var key = GetLeaderboardKeyForCurrentMode();
            var json = PlayerPrefs.GetString(key, "");
            if (string.IsNullOrWhiteSpace(json)) return new LeaderboardData();
            var data = JsonUtility.FromJson<LeaderboardData>(json);
            return data ?? new LeaderboardData();
        }
        catch
        {
            return new LeaderboardData();
        }
    }

    private static string GetLeaderboardKeyForCurrentMode()
    {
        if (GameManager.Instance == null) return LeaderboardPrefsKey_Defouloir;
        return GameManager.Instance.currentMode == GameMode.Recette
            ? LeaderboardPrefsKey_Recette
            : LeaderboardPrefsKey_Defouloir;
    }

    private void AutoWirePanelsIfNeeded()
    {
        if (gameUIPanel == null)
        {
            var recipeUi = FindFirstObjectByType<RecipeUI>(FindObjectsInactive.Include);
            if (recipeUi != null) 
            {
                gameUIPanel = recipeUi.gameObject;
                Debug.Log("HUD existant trouvé.");
            }
            else
            {
                // CAS CRITIQUE : Le HUD a disparu de la scène, on le recrée proprement
                Debug.LogWarning("HUD introuvable. Création d'un nouveau HUD de secours...");
                gameUIPanel = CreateGameplayHUD();
            }
        }
    }

    private GameObject CreateGameplayHUD()
    {
        var hudGo = new GameObject("Gameplay_HUD");
        hudGo.layer = LayerMask.NameToLayer("UI");
        
        var canvas = hudGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        hudGo.AddComponent<CanvasScaler>();
        hudGo.AddComponent<GraphicRaycaster>();

        var rt = hudGo.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(1400, 200);
        rt.localScale = Vector3.one * 0.007f;

        var recipeUiComp = hudGo.AddComponent<RecipeUI>();

        // Création des 3 textes principaux
        recipeUiComp.scoreText = CreateLabel(hudGo.transform, "Score: 0", 45);
        recipeUiComp.timerText = CreateLabel(hudGo.transform, "00:00", 45);
        recipeUiComp.recipeText = CreateLabel(hudGo.transform, "Commande", 32);

        // AJOUT DES ÉLÉMENTS ARCADE (Croix et Combos)
        var stGo = new GameObject("StrikesText");
        stGo.transform.SetParent(hudGo.transform, false);
        var strikesTmp = stGo.AddComponent<TextMeshProUGUI>();
        strikesTmp.fontSize = 60; strikesTmp.alignment = TextAlignmentOptions.Center;
        strikesTmp.rectTransform.anchoredPosition = new Vector2(0, -90);
        strikesTmp.text = "";
        // On utilise l'accès privé via reflection ou on modifie RecipeUI (on va supposer qu'on peut assigner via un champ sérialisé si on l'ajoute ou par nom)
        // Mais comme on a les références dans RecipeUI publiques, on va les utiliser.
        // Wait, RecipeUI.cs showed they are private/public?
        // scoreText, timerText, recipeText are public. strikesText, comboText are PRIVATE.
        
        // On va rendre strikesText et comboText publics dans RecipeUI.cs dans l'étape suivante, 
        // mais pour l'instant on va les créer et laisser RecipeUI les trouver par nom ou on les injecte.
        stGo.name = "StrikesText";
        
        var ctGo = new GameObject("ComboText");
        ctGo.transform.SetParent(hudGo.transform, false);
        var comboTmp = ctGo.AddComponent<TextMeshProUGUI>();
        comboTmp.fontSize = 55; comboTmp.alignment = TextAlignmentOptions.Center;
        comboTmp.fontStyle = FontStyles.Bold | FontStyles.Italic;
        comboTmp.color = new Color(1f, 0.85f, 0.1f); // Gold/Yellow Premium
        
        // --- FIX : On élargit la zone pour éviter le retour à la ligne ---
        comboTmp.rectTransform.sizeDelta = new Vector2(1200, 200); 
        comboTmp.enableWordWrapping = false;
        
        comboTmp.rectTransform.anchoredPosition = new Vector2(0, 110); // Plus haut pour ne pas gêner
        comboTmp.text = "";
        ctGo.name = "ComboText";
        ctGo.SetActive(false);

        // On raccorde les références
        recipeUiComp.strikesText = strikesTmp;
        recipeUiComp.comboText = comboTmp;

        // INITIALISATION DES CROIX (XXX) - On utilise la logique de RecipeUI
        strikesTmp.text = "<color=#555555>X </color><color=#555555>X </color><color=#555555>X </color>";

        // Positionnement (ApplyHudLayout() de RecipeUI s'en chargera aussi au Start)
        recipeUiComp.scoreText.rectTransform.anchoredPosition = new Vector2(-450, 0);
        recipeUiComp.timerText.rectTransform.anchoredPosition = new Vector2(450, 0);
        recipeUiComp.recipeText.rectTransform.anchoredPosition = new Vector2(0, 0);

        return hudGo;
    }

    private void EnsureMenuUIExists()
    {
        if (mainMenuPanel != null) return;
        mainMenuPanel = CreateMainMenuCanvas();
    }

    private void EnsureBannerHudLayout()
    {
        if (gameUIPanel == null) return;
        
        var existingHud = gameUIPanel.GetComponent<HeadLockedHud>();
        if (existingHud != null) Destroy(existingHud);
        var existingBillboard = gameUIPanel.GetComponent<BillboardToCamera>();
        if (existingBillboard != null) Destroy(existingBillboard);

        var rt = gameUIPanel.GetComponent<RectTransform>();
        if (rt != null)
        {
            rt.localScale = Vector3.one * 0.007f; // Scaled up slightly since it's further away
            rt.sizeDelta = new Vector2(1400, 200); // Forcer la taille de la bannière
        }

        EnsureHudBackground(gameUIPanel.transform);
    }

    private static void EnsureHudBackground(Transform hudRoot)
    {
        // Add a subtle background panel for readability if none exists.
        if (hudRoot.Find("HudBackground") != null) return;

        var bg = new GameObject("HudBackground");
        bg.layer = hudRoot.gameObject.layer;
        bg.transform.SetParent(hudRoot, false);
        bg.transform.SetAsFirstSibling();

        var img = bg.AddComponent<Image>();
        img.color = new Color(0.1f, 0.1f, 0.1f, 0.75f); // Darker and more opaque for banner

        var rt = bg.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(-40, -40);
        rt.offsetMax = new Vector2(40, 40);
    }

    private void RefreshGameUiForMode()
    {
        if (gameUIPanel == null) return;
        var recipeUi = gameUIPanel.GetComponent<RecipeUI>();
        if (recipeUi != null) recipeUi.UpdateModeUI();
    }

    private GameObject CreateMainMenuCanvas()
    {
        var canvasGo = new GameObject("MainMenu_Canvas");
        canvasGo.layer = LayerMask.NameToLayer("UI");

        var canvas = canvasGo.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        var scaler = canvasGo.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ConstantPixelSize;
        scaler.dynamicPixelsPerUnit = 10f;

        // XR UI raycasting uses TrackedDeviceGraphicRaycaster (XR Interaction Toolkit UI).
        // Keep a standard GraphicRaycaster as a fallback for non-XR / mouse in editor.
        canvasGo.AddComponent<GraphicRaycaster>();
        TryAddComponentByName(
            canvasGo,
            "UnityEngine.XR.Interaction.Toolkit.UI.TrackedDeviceGraphicRaycaster, Unity.XR.Interaction.Toolkit"
        );

        var rect = canvasGo.GetComponent<RectTransform>();
        // Slightly taller so the config screen breathes (VR comfort).
        rect.sizeDelta = new Vector2(980, 980);
        rect.localScale = Vector3.one * 0.002f;
        rect.position = GetFixedMenuPosition();
        rect.rotation = GetFixedMenuRotation();

        // ===== FOND PRINCIPAL SOMBRE =====
        var panelGo = new GameObject("Panel");
        panelGo.layer = canvasGo.layer;
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0.18f, 0.18f, 0.18f, 1f); // Fond gris anthracite
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // BORDURES FINES ET DISCRETES
        CreateAccentBar(panelGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -2), 2f, new Color(1f, 1f, 1f, 0.15f));
        CreateAccentBar(panelGo.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 2), 2f, new Color(1f, 1f, 1f, 0.15f));

        CreateTitle(panelGo.transform);

        // SÉPARATEUR MINIMALISTE
        CreateAccentBar(panelGo.transform, new Vector2(0.2f, 1f), new Vector2(0.8f, 1f), new Vector2(0, -130), 1f, new Color(1f, 1f, 1f, 0.1f));

        // --- GROUPE 1 : SÉLECTION DU MODE ---
        groupModeSelection = new GameObject("Group_ModeSelection");
        groupModeSelection.layer = canvasGo.layer;
        groupModeSelection.transform.SetParent(panelGo.transform, false);
        var rtSel = groupModeSelection.AddComponent<RectTransform>();
        rtSel.anchorMin = Vector2.zero; rtSel.anchorMax = Vector2.one;
        rtSel.offsetMin = Vector2.zero; rtSel.offsetMax = Vector2.zero;

        var subTitle = CreateLabel(groupModeSelection.transform, "CHOISISSEZ VOTRE MODE DE JEU", 30);
        subTitle.color = new Color(1f, 1f, 1f, 0.4f);
        subTitle.rectTransform.anchoredPosition = new Vector2(0, 100);

        btnSelectDefouloir = CreateStyledModeButton(groupModeSelection.transform, "Btn_GoTo_Defouloir", "DÉFOULOIR", new Color(1f, 1f, 1f, 0.08f));
        btnSelectDefouloir.onClick.AddListener(() => ShowModeConfig(GameMode.Defouloir));
        var rtDef = btnSelectDefouloir.GetComponent<RectTransform>();
        rtDef.sizeDelta = new Vector2(450, 140);
        rtDef.anchoredPosition = new Vector2(0, 0);

        btnSelectRecette = CreateStyledModeButton(groupModeSelection.transform, "Btn_GoTo_Recette", "RECETTE", new Color(1f, 1f, 1f, 0.08f));
        // Recette: lance direct (pas d'écran config).
        btnSelectRecette.onClick.AddListener(() => { selectedMode = GameMode.Recette; StartMode(); });
        var rtRec = btnSelectRecette.GetComponent<RectTransform>();
        rtRec.sizeDelta = new Vector2(450, 140);
        rtRec.anchoredPosition = new Vector2(0, -160);

        // --- GROUPE 2 : CONFIGURATION ---
        groupConfig = new GameObject("Group_Config");
        groupConfig.layer = canvasGo.layer;
        groupConfig.transform.SetParent(panelGo.transform, false);
        var rtConf = groupConfig.AddComponent<RectTransform>();
        rtConf.anchorMin = Vector2.zero; rtConf.anchorMax = Vector2.one;
        rtConf.offsetMin = Vector2.zero; rtConf.offsetMax = Vector2.zero;

        // ----- CONFIG LAYOUT (stable margins/spacings) -----
        var configRoot = new GameObject("ConfigRoot");
        configRoot.layer = canvasGo.layer;
        configRoot.transform.SetParent(groupConfig.transform, false);
        var configRootRt = configRoot.AddComponent<RectTransform>();
        configRootRt.anchorMin = new Vector2(0.5f, 0.5f);
        configRootRt.anchorMax = new Vector2(0.5f, 0.5f);
        configRootRt.pivot = new Vector2(0.5f, 0.5f);
        configRootRt.anchoredPosition = new Vector2(0, -20);
        configRootRt.sizeDelta = new Vector2(860, 760);

        var configTitle = CreateLabel(configRoot.transform, "CONFIGURATION", 35);
        configTitle.name = "ConfigTitle";
        configTitle.color = Color.white;
        configTitle.rectTransform.anchoredPosition = new Vector2(0, 320);

        SetupConfigRows(configRoot.transform);

        // Bouton RETOUR
        var btnBack = CreateButton(configRoot.transform, "Btn_Back", "RETOUR");
        btnBack.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.05f);
        var rtBack = btnBack.GetComponent<RectTransform>();
        rtBack.sizeDelta = new Vector2(420, 90);
        rtBack.anchoredPosition = new Vector2(0, -250);
        btnBack.onClick.AddListener(ShowModeSelection);

        // Bouton LANCER
        var btnPlay = CreateButton(configRoot.transform, "Btn_Lancer", "LANCER LA PARTIE");
        btnPlay.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.15f);
        var rtPlay = btnPlay.GetComponent<RectTransform>();
        rtPlay.sizeDelta = new Vector2(620, 130);
        rtPlay.anchoredPosition = new Vector2(0, -355);
        btnPlay.onClick.AddListener(StartMode);

        groupConfig.SetActive(false);

        return canvasGo;
    }

    private void SetupConfigRows(Transform parent)
    {
        // More spacing + slightly higher placement (less cramped in XR).
        rowDuration = CreateConfigRow(parent, "DURÉE", 170);
        rowDifficulty = CreateConfigRow(parent, "DIFFICULTÉ", 55);
        rowWeapon = CreateConfigRow(parent, "ARME", -60);

        PopulateRowDuration(rowDuration.transform);
        PopulateRowDifficulty(rowDifficulty.transform);
        PopulateRowWeapon(rowWeapon.transform);
    }

    private GameObject CreateConfigRow(Transform parent, string label, float yPos)
    {
        var row = new GameObject("Row_" + label);
        row.layer = parent.gameObject.layer;
        row.transform.SetParent(parent, false);
        var rt = row.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0, 0.5f); rt.anchorMax = new Vector2(1, 0.5f);
        rt.anchoredPosition = new Vector2(0, yPos);
        rt.sizeDelta = new Vector2(0, 90);

        var lbl = CreateLabel(row.transform, label + " :", 26);
        lbl.alignment = TextAlignmentOptions.Left;
        // Keep inside panel bounds (avoid overflow left).
        lbl.rectTransform.anchoredPosition = new Vector2(-285, 0);
        lbl.rectTransform.sizeDelta = new Vector2(240, 50);

        return row;
    }

    private void PopulateRowDuration(Transform rowParent)
    {
        var slider = CreateSlider(rowParent);
        slider.minValue = 1f; slider.maxValue = 15f; slider.wholeNumbers = true;
        slider.value = defouloirDurationMinutes;
        var rt = slider.GetComponent<RectTransform>();
        rt.anchoredPosition = new Vector2(40, 0);
        rt.sizeDelta = new Vector2(390, 48);

        var valLbl = CreateLabel(rowParent, $"{Mathf.RoundToInt(defouloirDurationMinutes)} min", 24);
        valLbl.alignment = TextAlignmentOptions.Right;
        // Keep inside panel bounds (avoid overflow right).
        valLbl.rectTransform.anchoredPosition = new Vector2(300, 0);
        valLbl.rectTransform.sizeDelta = new Vector2(170, 50);
        
        slider.onValueChanged.AddListener(v => {
            defouloirDurationMinutes = v;
            valLbl.text = $"{Mathf.RoundToInt(v)} min";
        });
    }

    private void PopulateRowDifficulty(Transform rowParent)
    {
        var easy = CreateSmallButton(rowParent, "Facile");
        var med = CreateSmallButton(rowParent, "Moyen");
        var hard = CreateSmallButton(rowParent, "Difficile");

        // Shift right so it doesn't hug the label.
        float startX = -60f; float stepX = 185f;
        easy.GetComponent<RectTransform>().anchoredPosition = new Vector2(startX, 0);
        med.GetComponent<RectTransform>().anchoredPosition = new Vector2(startX + stepX, 0);
        hard.GetComponent<RectTransform>().anchoredPosition = new Vector2(startX + stepX * 2, 0);

        easy.onClick.AddListener(() => SetDifficulty(Difficulty.Facile, easy, med, hard));
        med.onClick.AddListener(() => SetDifficulty(Difficulty.Moyen, easy, med, hard));
        hard.onClick.AddListener(() => SetDifficulty(Difficulty.Difficile, easy, med, hard));
        
        SetDifficulty(defouloirDifficulty, easy, med, hard);
    }

    private void PopulateRowWeapon(Transform rowParent)
    {
        var knives = CreateSmallButton(rowParent, "Couteaux");
        var sabre = CreateSmallButton(rowParent, "Sabre");

        knives.GetComponent<RectTransform>().anchoredPosition = new Vector2(-95, 0);
        sabre.GetComponent<RectTransform>().anchoredPosition = new Vector2(95, 0);

        knives.onClick.AddListener(() => SetWeapon(WeaponType.Couteaux, knives, sabre));
        sabre.onClick.AddListener(() => SetWeapon(WeaponType.SabreLaser, knives, sabre));
        
        SetWeapon(selectedWeapon, knives, sabre);
    }

    private static void CreateAccentBar(Transform parent, Vector2 anchorMin, Vector2 anchorMax, Vector2 offset, float height, Color color)
    {
        var bar = new GameObject("AccentBar");
        bar.layer = parent.gameObject.layer;
        bar.transform.SetParent(parent, false);
        var img = bar.AddComponent<Image>();
        img.color = color;
        var rt = bar.GetComponent<RectTransform>();
        rt.anchorMin = anchorMin;
        rt.anchorMax = anchorMax;
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = offset;
        rt.sizeDelta = new Vector2(0, height);
    }

    private static Button CreateStyledModeButton(Transform parent, string name, string label, Color bgColor)
    {
        var btn = CreateButton(parent, name, label);
        btn.GetComponent<Image>().color = bgColor;
        var textTmp = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (textTmp != null) { textTmp.fontSize = 38; textTmp.color = Color.white; }
        return btn;
    }

    // Anciennes méthodes de menu supprimées (Nettoyage)

    // Nettoyage : LayoutRowButtons supprimé

    private void SetWeapon(WeaponType w, Button k, Button s)
    {
        selectedWeapon = w;
        SetButtonSelected(k, w == WeaponType.Couteaux);
        SetButtonSelected(s, w == WeaponType.SabreLaser);
    }

    private void SetDifficulty(Difficulty d, Button easy, Button medium, Button hard)
    {
        defouloirDifficulty = d;
        SetButtonSelected(easy, d == Difficulty.Facile);
        SetButtonSelected(medium, d == Difficulty.Moyen);
        SetButtonSelected(hard, d == Difficulty.Difficile);
    }

    private static void SetButtonSelected(Button btn, bool selected)
    {
        if (btn == null) return;
        var img = btn.GetComponent<Image>();
        if (img != null) img.color = selected 
            ? new Color(1f, 1f, 1f, 0.2f)    // Blanc translucide quand sélectionné
            : new Color(1f, 1f, 1f, 0.04f);   // Presque invisible sinon
        var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.color = selected
            ? Color.white
            : new Color(1f, 1f, 1f, 0.4f);
    }

    private static void LayoutDifficultyButtons(RectTransform easy, RectTransform medium, RectTransform hard)
    {
        if (easy != null)
        {
            easy.anchorMin = new Vector2(0.25f, 0.15f);
            easy.anchorMax = new Vector2(0.25f, 0.15f);
            easy.pivot = new Vector2(0.5f, 0.5f);
            easy.anchoredPosition = Vector2.zero;
        }
        if (medium != null)
        {
            medium.anchorMin = new Vector2(0.55f, 0.15f);
            medium.anchorMax = new Vector2(0.55f, 0.15f);
            medium.pivot = new Vector2(0.5f, 0.5f);
            medium.anchoredPosition = Vector2.zero;
        }
        if (hard != null)
        {
            hard.anchorMin = new Vector2(0.85f, 0.15f);
            hard.anchorMax = new Vector2(0.85f, 0.15f);
            hard.pivot = new Vector2(0.5f, 0.5f);
            hard.anchoredPosition = Vector2.zero;
        }
    }

    private static TextMeshProUGUI CreateLabel(Transform parent, string text, float size)
    {
        var go = new GameObject("Label");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var tmp = go.AddComponent<TextMeshProUGUI>();
        tmp.text = text;
        tmp.fontSize = size;
        tmp.color = Color.white;
        tmp.enableWordWrapping = false;
        tmp.alignment = TextAlignmentOptions.Center;
        return tmp;
    }

    private static Slider CreateSlider(Transform parent)
    {
        var go = new GameObject("DurationSlider");
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);
        var rt = go.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 0.55f);
        rt.anchorMax = new Vector2(0.5f, 0.55f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(160, 0);
        rt.sizeDelta = new Vector2(520, 48);

        var slider = go.AddComponent<Slider>();

        // Background
        var bg = new GameObject("Background");
        bg.layer = go.layer;
        bg.transform.SetParent(go.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.08f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // Fill Area
        var fillArea = new GameObject("Fill Area");
        fillArea.layer = go.layer;
        fillArea.transform.SetParent(go.transform, false);
        var fillAreaRt = fillArea.AddComponent<RectTransform>();
        fillAreaRt.anchorMin = new Vector2(0, 0.30f);
        fillAreaRt.anchorMax = new Vector2(1, 0.70f);
        fillAreaRt.offsetMin = new Vector2(10, 0);
        fillAreaRt.offsetMax = new Vector2(-10, 0);

        var fill = new GameObject("Fill");
        fill.layer = go.layer;
        fill.transform.SetParent(fillArea.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(0.3f, 0.9f, 1f, 0.35f);
        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        // Handle Slide Area
        var handleArea = new GameObject("Handle Slide Area");
        handleArea.layer = go.layer;
        handleArea.transform.SetParent(go.transform, false);
        var handleAreaRt = handleArea.AddComponent<RectTransform>();
        handleAreaRt.anchorMin = Vector2.zero;
        handleAreaRt.anchorMax = Vector2.one;
        handleAreaRt.offsetMin = new Vector2(10, 0);
        handleAreaRt.offsetMax = new Vector2(-10, 0);

        var handle = new GameObject("Handle");
        handle.layer = go.layer;
        handle.transform.SetParent(handleArea.transform, false);
        var handleImg = handle.AddComponent<Image>();
        handleImg.color = new Color(1f, 1f, 1f, 0.85f);
        var handleRt = handle.GetComponent<RectTransform>();
        handleRt.sizeDelta = new Vector2(34, 34);

        slider.targetGraphic = handleImg;
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;

        return slider;
    }

    private static Button CreateSmallButton(Transform parent, string label)
    {
        var btn = CreateButton(parent, $"Diff_{label}", label);
        var rt = btn.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(175, 65);

        var textTmp = btn.gameObject.GetComponentInChildren<TextMeshProUGUI>();
        if (textTmp != null) textTmp.fontSize = 28;

        return btn;
    }

    private static void CreateTitle(Transform parent)
    {
        // Titre principal
        var titleGo = new GameObject("Title");
        titleGo.layer = parent.gameObject.layer;
        titleGo.transform.SetParent(parent, false);
        var tmp = titleGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "FRUITSHOOT";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 84;
        tmp.color = Color.white;
        tmp.fontStyle = FontStyles.Bold;
        tmp.enableWordWrapping = false;

        var rt = titleGo.GetComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -15);
        rt.sizeDelta = new Vector2(700, 80);

        // Sous-titre
        var subGo = new GameObject("Subtitle");
        subGo.layer = parent.gameObject.layer;
        subGo.transform.SetParent(parent, false);
        var subTmp = subGo.AddComponent<TextMeshProUGUI>();
        subTmp.text = "sa tue";
        subTmp.alignment = TextAlignmentOptions.Center;
        subTmp.fontSize = 24;
        subTmp.color = new Color(1f, 1f, 1f, 0.4f);
        subTmp.fontStyle = FontStyles.Italic;
        subTmp.enableWordWrapping = false;

        var subRt = subGo.GetComponent<RectTransform>();
        subRt.anchorMin = new Vector2(0.5f, 1f);
        subRt.anchorMax = new Vector2(0.5f, 1f);
        subRt.pivot = new Vector2(0.5f, 1f);
        subRt.anchoredPosition = new Vector2(0, -90);
        subRt.sizeDelta = new Vector2(700, 35);
    }

    private static Button CreateButton(Transform parent, string name, string label)
    {
        var go = new GameObject(name);
        go.layer = parent.gameObject.layer;
        go.transform.SetParent(parent, false);

        var img = go.AddComponent<Image>();
        img.color = new Color(1f, 1f, 1f, 0.10f);

        var btn = go.AddComponent<Button>();
        var colors = btn.colors;
        colors.normalColor = Color.white;
        colors.highlightedColor = new Color(0.7f, 1f, 1f, 1f);
        colors.pressedColor = new Color(0.5f, 0.9f, 0.9f, 1f);
        colors.selectedColor = Color.white;
        btn.colors = colors;
        btn.targetGraphic = img;

        var rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(520, 90);

        var textGo = new GameObject("Text (TMP)");
        textGo.layer = go.layer;
        textGo.transform.SetParent(go.transform, false);
        var tmp = textGo.AddComponent<TextMeshProUGUI>();
        tmp.text = label;
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 40;
        tmp.color = new Color(0.9f, 0.95f, 1f);

        var textRt = textGo.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = new Vector2(10, 5);
        textRt.offsetMax = new Vector2(-10, -5);

        return btn;
    }

    private static void PositionButtons(RectTransform defouloir, RectTransform recette, RectTransform play)
    {
        // Boutons de mode centrés sous le titre
        defouloir.anchorMin = new Vector2(0.5f, 1f);
        defouloir.anchorMax = new Vector2(0.5f, 1f);
        defouloir.pivot = new Vector2(0.5f, 1f);
        defouloir.anchoredPosition = new Vector2(-220, -160);
        defouloir.sizeDelta = new Vector2(420, 100);

        recette.anchorMin = new Vector2(0.5f, 1f);
        recette.anchorMax = new Vector2(0.5f, 1f);
        recette.pivot = new Vector2(0.5f, 1f);
        recette.anchoredPosition = new Vector2(220, -160);
        recette.sizeDelta = new Vector2(420, 100);
        
        // Bouton Jouer tout en bas, bien visible
        play.anchorMin = new Vector2(0.5f, 0f);
        play.anchorMax = new Vector2(0.5f, 0f);
        play.pivot = new Vector2(0.5f, 0f);
        play.anchoredPosition = new Vector2(0, 40);
        play.sizeDelta = new Vector2(500, 120);
    }

    private void EnsureEventSystemExists()
    {
        if (FindFirstObjectByType<EventSystem>() != null) return;

        var es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();

        // Prefer XR UI Input Module if available (XR Interaction Toolkit), otherwise fallback.
        if (!TryAddComponentByName(es, "UnityEngine.XR.Interaction.Toolkit.UI.XRUIInputModule, Unity.XR.Interaction.Toolkit"))
        {
            // Old input system fallback (editor/mouse). If the project uses the new Input System,
            // Unity will typically auto-create the right module when adding UI; this is a safety net.
            es.AddComponent<StandaloneInputModule>();
        }
    }

    private void EnsureXRInteractionManagerExists()
    {
        // XRRayInteractor relies on an XRInteractionManager in the scene.
        // Most XR Origin setups include one, but when building everything from code we ensure it exists.
        var mgrType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRInteractionManager, Unity.XR.Interaction.Toolkit");
        if (mgrType == null) return;
        if (FindFirstObjectByType(mgrType) != null) return;

        var go = new GameObject("XR Interaction Manager (Auto)");
        go.AddComponent(mgrType);
    }

    private static bool TryAddComponentByName(GameObject go, string assemblyQualifiedTypeName)
    {
        var t = Type.GetType(assemblyQualifiedTypeName);
        if (t == null) return false;
        if (!typeof(Component).IsAssignableFrom(t)) return false;
        go.AddComponent(t);
        return true;
    }

    private void EnsureRightHandRayInteractorForUI()
    {
        var rightController = FindRightControllerObject();
        if (rightController == null) return;

        var rayInteractorType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRRayInteractor, Unity.XR.Interaction.Toolkit");
        if (rayInteractorType == null) return;

        var rayInteractor = rightController.GetComponent(rayInteractorType) ?? rightController.AddComponent(rayInteractorType);

        // Enable UI interaction if the field exists (depends on toolkit version).
        var enableUiField =
            rayInteractorType.GetField("m_EnableUIInteraction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic) ??
            rayInteractorType.GetField("enableUIInteraction", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
        if (enableUiField != null && enableUiField.FieldType == typeof(bool))
        {
            enableUiField.SetValue(rayInteractor, true);
        }

        // On ne crée plus de LineRenderer / LineVisual ici pour éviter le laser vert moche.
        // Le crosshair du LaserShooter s'occupe du viseur.
        var lineVisualType = Type.GetType("UnityEngine.XR.Interaction.Toolkit.XRInteractorLineVisual, Unity.XR.Interaction.Toolkit");
        if (lineVisualType != null)
        {
            var existing = rightController.GetComponent(lineVisualType);
            if (existing != null) ((MonoBehaviour)existing).enabled = false;
        }
        var existingLR = rightController.GetComponent<LineRenderer>();
        if (existingLR != null) existingLR.enabled = false;
    }

    private GameObject FindRightControllerObject()
    {
        // Prefer the common XR Origin path/name.
        var direct = GameObject.Find("XR Origin (XR Rig)/Camera Offset/Right Controller");
        if (direct != null) return direct;

        // Fall back to name-based search.
        var all = FindObjectsByType<Transform>(FindObjectsSortMode.None);
        foreach (var t in all)
        {
            if (t != null && string.Equals(t.name, "Right Controller", StringComparison.Ordinal))
                return t.gameObject;
        }
        return null;
    }

    private void TeleportPlayerInFrontOfMenu()
    {
        if (mainMenuPanel == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        // Find XR Origin (CoreUtils) without hard dependency.
        var xrOriginType = Type.GetType("Unity.XR.CoreUtils.XROrigin, Unity.XR.CoreUtils");
        if (xrOriginType == null) return;

        var xrOrigin = FindFirstObjectByType(xrOriginType) as Component;
        if (xrOrigin == null) return;

        // Desired camera pose: 2.5m in front of menu, looking at it (yaw-only).
        var menuPos = mainMenuPanel.transform.position;
        var toMenu = menuPos - cam.transform.position;
        toMenu.y = 0f;
        if (toMenu.sqrMagnitude < 0.001f) toMenu = GetFlattenedForward(cam);
        var menuForward = toMenu.normalized;

        var desiredCamPos = menuPos - menuForward * 2.5f;

        // Translate XR Origin so the camera ends up at desiredCamPos.
        var delta = desiredCamPos - cam.transform.position;
        xrOrigin.transform.position += delta;

        // Rotate XR Origin around the (new) camera position so the player faces the menu.
        var desiredYaw = Quaternion.LookRotation(menuForward, Vector3.up);
        var currentYaw = Quaternion.LookRotation(GetFlattenedForward(cam), Vector3.up);
        var yawDelta = desiredYaw * Quaternion.Inverse(currentYaw);
        xrOrigin.transform.rotation = yawDelta * xrOrigin.transform.rotation;
    }

    private static Vector3 GetDefaultMenuPosition()
    {
        var cam = Camera.main;
        // Position plus haute (1.8m) pour plus de confort
        if (cam == null) return new Vector3(0, 1.8f, 2.5f);
        return cam.transform.position + GetFlattenedForward(cam) * 2.5f + Vector3.up * 0.3f;
    }

    private void CaptureFixedUiPoseIfNeeded()
    {
        if (fixedUiPoseCaptured) return;

        // Prefer a scene anchor (true fixed placement).
        if (uiAnchor != null)
        {
            fixedMenuPosition = uiAnchor.position;
            fixedMenuRotation = uiAnchor.rotation;
            fixedUiPoseCaptured = true;
            return;
        }

        // Fallback: capture once from camera (can be off in XR if tracking not ready).
        var cam = Camera.main;
        if (cam == null) return;
        var fwd = GetFlattenedForward(cam);
        fixedMenuPosition = cam.transform.position + fwd * 2.5f + Vector3.up * 0.3f;
        fixedMenuRotation = Quaternion.LookRotation(fwd, Vector3.up);
        fixedUiPoseCaptured = true;
    }

    private Vector3 GetFixedMenuPosition()
    {
        CaptureFixedUiPoseIfNeeded();
        return fixedUiPoseCaptured ? fixedMenuPosition : GetDefaultMenuPosition();
    }

    private Quaternion GetFixedMenuRotation()
    {
        CaptureFixedUiPoseIfNeeded();
        if (fixedUiPoseCaptured) return fixedMenuRotation;
        return Quaternion.LookRotation(GetFlattenedForward(Camera.main), Vector3.up);
    }

    private static Vector3 GetFlattenedForward(Camera cam)
    {
        if (cam == null) return Vector3.forward;
        var fwd = cam.transform.forward;
        fwd.y = 0;
        if (fwd.sqrMagnitude < 0.0001f) return Vector3.forward;
        return fwd.normalized;
    }
}
