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

    [Header("Defouloir options (menu)")]
    public float defouloirDurationMinutes = 5f;
    public Difficulty defouloirDifficulty = Difficulty.Moyen;

    private GameMode selectedMode = GameMode.Defouloir;
    private GameObject defouloirOptionsGroup;
    private Button btnSelectDefouloir;
    private Button btnSelectRecette;

    private TextMeshProUGUI goScoreText;
    private TextMeshProUGUI goFruitsText;
    private TextMeshProUGUI goBombsText;

    private WeaponType selectedWeapon = WeaponType.Couteaux;
    private Button btnWeaponKnives;
    private Button btnWeaponSabre;

    private void Start()
    {
        AutoWirePanelsIfNeeded();
        EnsureEventSystemExists();
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

        TeleportPlayerInFrontOfMenu();
    }

    public void SelectMode(GameMode mode)
    {
        selectedMode = mode;
        if (defouloirOptionsGroup != null)
        {
            defouloirOptionsGroup.SetActive(selectedMode == GameMode.Defouloir);
        }
        
        SetButtonSelected(btnSelectDefouloir, selectedMode == GameMode.Defouloir);
        SetButtonSelected(btnSelectRecette, selectedMode == GameMode.Recette);
    }

    public void StartSelectedMode()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.currentMode = selectedMode;
            if (selectedMode == GameMode.Defouloir)
            {
                GameManager.Instance.gameDuration = Mathf.Clamp(defouloirDurationMinutes, 1f, 20f) * 60f;
                GameManager.Instance.difficulty = defouloirDifficulty;
                GameManager.Instance.weapon = selectedWeapon;
            }
            GameManager.Instance.StartGame();
        }
        RefreshGameUiForMode();
        HideMenusAndShowGameUI();
    }

    private void HideMenusAndShowGameUI()
    {
        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);
        if (gameOverPanel != null) gameOverPanel.SetActive(false);
        if (gameUIPanel != null) 
        {
            gameUIPanel.SetActive(true);
            
            // Snap HUD to a fixed position like a banner on a wall
            var cam = Camera.main;
            if (cam != null)
            {
                var fwd = GetFlattenedForward(cam);
                // Placed further away and higher up
                gameUIPanel.transform.position = cam.transform.position + fwd * 6.5f + Vector3.up * 2.8f;
                gameUIPanel.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
        }
    }

    public void ShowGameOverMenu()
    {
        if (gameUIPanel != null) gameUIPanel.SetActive(false);
        EnsureGameOverUIExists();
        if (gameOverPanel != null) 
        {
            gameOverPanel.SetActive(true);
            RefreshGameOverStats();
            
            var cam = Camera.main;
            if (cam != null)
            {
                var fwd = GetFlattenedForward(cam);
                gameOverPanel.transform.position = cam.transform.position + fwd * 2.5f + Vector3.up * 0.0f;
                gameOverPanel.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
            }
        }
    }

    private void EnsureGameOverUIExists()
    {
        if (gameOverPanel != null) return;
        gameOverPanel = CreateGameOverCanvas();
    }

    private void RefreshGameOverStats()
    {
        if (GameManager.Instance == null) return;
        if (goScoreText != null) goScoreText.text = $"Score Final : {GameManager.Instance.score}";
        if (goFruitsText != null) goFruitsText.text = $"Fruits Découpés : {GameManager.Instance.fruitsSliced}";
        if (goBombsText != null) goBombsText.text = $"Bombes Touchées : {GameManager.Instance.bombsHit}";
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
        rect.sizeDelta = new Vector2(900, 700);
        rect.localScale = Vector3.one * 0.003f;

        var panelGo = new GameObject("Panel");
        panelGo.layer = canvasGo.layer;
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0, 0, 0, 0.85f);
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero; panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero; panelRect.offsetMax = Vector2.zero;

        var title = CreateLabel(panelGo.transform, "FIN DE PARTIE", 75);
        title.color = new Color(1f, 0.4f, 0.4f);
        title.rectTransform.anchoredPosition = new Vector2(0, 250);

        goScoreText = CreateLabel(panelGo.transform, "Score Final : 0", 50);
        goScoreText.color = new Color(0f, 0.8f, 1f);
        goScoreText.rectTransform.anchoredPosition = new Vector2(0, 100);

        goFruitsText = CreateLabel(panelGo.transform, "Fruits Découpés : 0", 40);
        goFruitsText.rectTransform.anchoredPosition = new Vector2(0, 0);

        goBombsText = CreateLabel(panelGo.transform, "Bombes Touchées : 0", 40);
        goBombsText.color = new Color(1f, 0.5f, 0f);
        goBombsText.rectTransform.anchoredPosition = new Vector2(0, -80);

        var btnReturn = CreateButton(panelGo.transform, "Bouton_Retour", "Retour au Menu");
        btnReturn.GetComponent<Image>().color = new Color(0.8f, 0.2f, 0.2f, 0.5f);
        var rtBtn = btnReturn.GetComponent<RectTransform>();
        rtBtn.sizeDelta = new Vector2(450, 90);
        rtBtn.anchoredPosition = new Vector2(0, -220);
        btnReturn.onClick.AddListener(ShowMainMenu);

        return canvasGo;
    }

    private void AutoWirePanelsIfNeeded()
    {
        // If scene already has a HUD canvas (RecipeUI), treat it as the in-game UI panel.
        if (gameUIPanel == null)
        {
            var recipeUi = FindFirstObjectByType<RecipeUI>();
            if (recipeUi != null) gameUIPanel = recipeUi.gameObject;
        }
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
        rect.sizeDelta = new Vector2(950, 900);
        rect.localScale = Vector3.one * 0.002f;
        rect.position = GetDefaultMenuPosition();
        rect.rotation = Quaternion.LookRotation(GetFlattenedForward(Camera.main), Vector3.up);

        // ===== FOND PRINCIPAL SOMBRE =====
        var panelGo = new GameObject("Panel");
        panelGo.layer = canvasGo.layer;
        panelGo.transform.SetParent(canvasGo.transform, false);
        var panelImage = panelGo.AddComponent<Image>();
        panelImage.color = new Color(0.05f, 0.05f, 0.12f, 0.92f); // Bleu très sombre quasi opaque
        var panelRect = panelGo.GetComponent<RectTransform>();
        panelRect.anchorMin = Vector2.zero;
        panelRect.anchorMax = Vector2.one;
        panelRect.offsetMin = Vector2.zero;
        panelRect.offsetMax = Vector2.zero;

        // ===== BORDURE HAUT (accent cyan) =====
        CreateAccentBar(panelGo.transform, new Vector2(0, 1), new Vector2(1, 1), new Vector2(0, -4), 6f, new Color(0f, 0.85f, 1f, 0.7f));
        // ===== BORDURE BAS (accent cyan) =====
        CreateAccentBar(panelGo.transform, new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 4), 6f, new Color(0f, 0.85f, 1f, 0.7f));

        CreateTitle(panelGo.transform);

        // ===== SÉPARATEUR SOUS LE TITRE =====
        CreateAccentBar(panelGo.transform, new Vector2(0.1f, 1f), new Vector2(0.9f, 1f), new Vector2(0, -140), 3f, new Color(1f, 1f, 1f, 0.15f));

        defouloirOptionsGroup = CreateDefouloirOptions(panelGo.transform);

        // Mode Selection Buttons
        btnSelectDefouloir = CreateStyledModeButton(panelGo.transform, "Bouton_Select_Defouloir", "DEFOULOIR", new Color(1f, 0.4f, 0.2f, 0.35f));
        btnSelectDefouloir.onClick.AddListener(() => SelectMode(GameMode.Defouloir));
        
        btnSelectRecette = CreateStyledModeButton(panelGo.transform, "Bouton_Select_Recette", "RECETTE", new Color(0.2f, 0.7f, 1f, 0.35f));
        btnSelectRecette.onClick.AddListener(() => SelectMode(GameMode.Recette));

        // Play Button
        var btnPlay = CreateButton(panelGo.transform, "Bouton_Jouer", "JOUER");
        btnPlay.GetComponent<Image>().color = new Color(0f, 0.7f, 0.3f, 0.5f);
        var playText = btnPlay.GetComponentInChildren<TextMeshProUGUI>();
        if (playText != null) { playText.fontSize = 52; playText.color = new Color(0.7f, 1f, 0.7f); }
        btnPlay.onClick.AddListener(StartSelectedMode);

        PositionButtons(btnSelectDefouloir.GetComponent<RectTransform>(), btnSelectRecette.GetComponent<RectTransform>(), btnPlay.GetComponent<RectTransform>());
        
        // Hide options initially to let user select
        if (defouloirOptionsGroup != null) defouloirOptionsGroup.SetActive(false);

        return canvasGo;
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

    private GameObject CreateDefouloirOptions(Transform parent)
    {
        var group = new GameObject("DefouloirOptions");
        group.layer = parent.gameObject.layer;
        group.transform.SetParent(parent, false);

        var rt = group.AddComponent<RectTransform>();
        rt.anchorMin = new Vector2(0.5f, 1f);
        rt.anchorMax = new Vector2(0.5f, 1f);
        rt.pivot = new Vector2(0.5f, 1f);
        rt.anchoredPosition = new Vector2(0, -310);
        rt.sizeDelta = new Vector2(800, 400);

        // Fond semi-transparent pour la zone d'options
        var optionsBg = new GameObject("OptionsBg");
        optionsBg.layer = group.layer;
        optionsBg.transform.SetParent(group.transform, false);
        var optBgImg = optionsBg.AddComponent<Image>();
        optBgImg.color = new Color(1f, 1f, 1f, 0.05f);
        var optBgRt = optionsBg.GetComponent<RectTransform>();
        optBgRt.anchorMin = Vector2.zero; optBgRt.anchorMax = Vector2.one;
        optBgRt.offsetMin = new Vector2(-10, -10); optBgRt.offsetMax = new Vector2(10, 10);

        // === HEADER ===
        var header = CreateLabel(group.transform, "-- PARAMETRES --", 30);
        header.color = new Color(0f, 0.85f, 1f);
        header.rectTransform.anchorMin = new Vector2(0.5f, 1f);
        header.rectTransform.anchorMax = new Vector2(0.5f, 1f);
        header.rectTransform.pivot = new Vector2(0.5f, 1f);
        header.rectTransform.anchoredPosition = new Vector2(0, -10);
        header.rectTransform.sizeDelta = new Vector2(750, 40);

        // === LIGNE 1 : DURÉE ===
        float row1Y = -65f;
        var durationLabel = CreateLabel(group.transform, $"Duree: {Mathf.RoundToInt(defouloirDurationMinutes)} min", 26);
        durationLabel.alignment = TextAlignmentOptions.Left;
        durationLabel.color = new Color(0.8f, 0.8f, 0.8f);
        durationLabel.rectTransform.anchorMin = new Vector2(0, 1f);
        durationLabel.rectTransform.anchorMax = new Vector2(0, 1f);
        durationLabel.rectTransform.pivot = new Vector2(0, 0.5f);
        durationLabel.rectTransform.anchoredPosition = new Vector2(20, row1Y);
        durationLabel.rectTransform.sizeDelta = new Vector2(300, 35);

        var slider = CreateSlider(group.transform);
        slider.minValue = 1f; slider.maxValue = 15f; slider.wholeNumbers = true;
        slider.value = Mathf.Clamp(defouloirDurationMinutes, slider.minValue, slider.maxValue);
        var sliderRt = slider.GetComponent<RectTransform>();
        sliderRt.anchorMin = new Vector2(0.5f, 1f);
        sliderRt.anchorMax = new Vector2(0.5f, 1f);
        sliderRt.anchoredPosition = new Vector2(160, row1Y);
        slider.onValueChanged.AddListener(v =>
        {
            defouloirDurationMinutes = v;
            if (durationLabel != null)
                durationLabel.text = $"Duree: {Mathf.RoundToInt(defouloirDurationMinutes)} min";
        });

        // === SÉPARATEUR ===
        CreateAccentBar(group.transform, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f), new Vector2(0, -100), 2f, new Color(1f, 1f, 1f, 0.1f));

        // === LIGNE 2 : DIFFICULTÉ ===
        float row2Y = -135f;
        var diffLabel = CreateLabel(group.transform, "Difficulte:", 26);
        diffLabel.alignment = TextAlignmentOptions.Left;
        diffLabel.color = new Color(0.8f, 0.8f, 0.8f);
        diffLabel.rectTransform.anchorMin = new Vector2(0, 1f);
        diffLabel.rectTransform.anchorMax = new Vector2(0, 1f);
        diffLabel.rectTransform.pivot = new Vector2(0, 0.5f);
        diffLabel.rectTransform.anchoredPosition = new Vector2(20, row2Y);
        diffLabel.rectTransform.sizeDelta = new Vector2(220, 35);

        var easy = CreateSmallButton(group.transform, "Facile");
        var medium = CreateSmallButton(group.transform, "Moyen");
        var hard = CreateSmallButton(group.transform, "Difficile");

        easy.onClick.AddListener(() => SetDifficulty(Difficulty.Facile, easy, medium, hard));
        medium.onClick.AddListener(() => SetDifficulty(Difficulty.Moyen, easy, medium, hard));
        hard.onClick.AddListener(() => SetDifficulty(Difficulty.Difficile, easy, medium, hard));

        // Position difficulté sur la même ligne
        LayoutRowButtons(easy.GetComponent<RectTransform>(), 0.32f, row2Y);
        LayoutRowButtons(medium.GetComponent<RectTransform>(), 0.52f, row2Y);
        LayoutRowButtons(hard.GetComponent<RectTransform>(), 0.78f, row2Y);
        SetDifficulty(defouloirDifficulty, easy, medium, hard);

        // === SÉPARATEUR ===
        CreateAccentBar(group.transform, new Vector2(0.05f, 1f), new Vector2(0.95f, 1f), new Vector2(0, -175), 2f, new Color(1f, 1f, 1f, 0.1f));

        // === LIGNE 3 : ARME ===
        float row3Y = -210f;
        var weaponLabel = CreateLabel(group.transform, "Arme:", 26);
        weaponLabel.alignment = TextAlignmentOptions.Left;
        weaponLabel.color = new Color(0.8f, 0.8f, 0.8f);
        weaponLabel.rectTransform.anchorMin = new Vector2(0, 1f);
        weaponLabel.rectTransform.anchorMax = new Vector2(0, 1f);
        weaponLabel.rectTransform.pivot = new Vector2(0, 0.5f);
        weaponLabel.rectTransform.anchoredPosition = new Vector2(20, row3Y);
        weaponLabel.rectTransform.sizeDelta = new Vector2(220, 35);

        btnWeaponKnives = CreateSmallButton(group.transform, "Couteaux");
        btnWeaponSabre = CreateSmallButton(group.transform, "Sabre");

        btnWeaponKnives.onClick.AddListener(() => SetWeapon(WeaponType.Couteaux));
        btnWeaponSabre.onClick.AddListener(() => SetWeapon(WeaponType.SabreLaser));

        LayoutRowButtons(btnWeaponKnives.GetComponent<RectTransform>(), 0.38f, row3Y);
        LayoutRowButtons(btnWeaponSabre.GetComponent<RectTransform>(), 0.68f, row3Y);

        SetWeapon(selectedWeapon);
        
        return group;
    }

    private static void LayoutRowButtons(RectTransform rt, float anchorX, float yOffset)
    {
        rt.anchorMin = new Vector2(anchorX, 1f);
        rt.anchorMax = new Vector2(anchorX, 1f);
        rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0, yOffset);
    }

    private void SetWeapon(WeaponType w)
    {
        selectedWeapon = w;
        SetButtonSelected(btnWeaponKnives, w == WeaponType.Couteaux);
        SetButtonSelected(btnWeaponSabre, w == WeaponType.SabreLaser);
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
            ? new Color(0f, 0.85f, 1f, 0.35f)  // Cyan lumineux quand sélectionné
            : new Color(1f, 1f, 1f, 0.08f);      // Quasi invisible sinon
        var txt = btn.GetComponentInChildren<TextMeshProUGUI>();
        if (txt != null) txt.color = selected
            ? new Color(0.7f, 1f, 1f)
            : new Color(0.5f, 0.5f, 0.5f);
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
        rt.sizeDelta = new Vector2(520, 30);

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
        fillAreaRt.anchorMin = new Vector2(0, 0.25f);
        fillAreaRt.anchorMax = new Vector2(1, 0.75f);
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
        handleRt.sizeDelta = new Vector2(18, 18);

        slider.targetGraphic = handleImg;
        slider.fillRect = fillRt;
        slider.handleRect = handleRt;

        return slider;
    }

    private static Button CreateSmallButton(Transform parent, string label)
    {
        var btn = CreateButton(parent, $"Diff_{label}", label);
        var rt = btn.GetComponent<RectTransform>();
        if (rt != null) rt.sizeDelta = new Vector2(150, 55);

        var textTmp = btn.gameObject.GetComponentInChildren<TextMeshProUGUI>();
        if (textTmp != null) textTmp.fontSize = 26;

        return btn;
    }

    private static void CreateTitle(Transform parent)
    {
        // Titre principal
        var titleGo = new GameObject("Title");
        titleGo.layer = parent.gameObject.layer;
        titleGo.transform.SetParent(parent, false);
        var tmp = titleGo.AddComponent<TextMeshProUGUI>();
        tmp.text = "EDN XR";
        tmp.alignment = TextAlignmentOptions.Center;
        tmp.fontSize = 72;
        tmp.color = new Color(0f, 0.9f, 1f); // Cyan flamboyant
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
        subTmp.text = "FRUIT NINJA VR";
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
        if (cam == null) return new Vector3(0, 1.5f, 2.5f);
        return cam.transform.position + GetFlattenedForward(cam) * 2.5f + Vector3.up * 0.0f;
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
