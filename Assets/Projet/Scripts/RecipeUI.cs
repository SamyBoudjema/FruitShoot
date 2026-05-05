using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class RecipeUI : MonoBehaviour
{
    public TextMeshProUGUI recipeText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    
    // Nouveaux éléments dynamiques Arcade
    public TextMeshProUGUI strikesText;
    public TextMeshProUGUI comboText;
    private float comboHideTime = 0f;

    // Progress (Recette)
    private Image recipeProgressFill;
    private TextMeshProUGUI recipeToast;
    private float toastHideTime = 0f;
    private GameObject orderTabletRoot;
    private RectTransform orderTabletRect;
    private bool orderTabletPlaced;
    private Transform orderTabletAnchor;
    [Header("Order tablet")]
    [Tooltip("Flip the tablet 180° around Y if text appears reversed.")]
    public bool flipOrderTabletYaw = true;
    [Tooltip("If true, rotate the tablet (yaw-only) to face the player without moving its position.")]
    public bool orderTabletFacePlayer = true;

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged.AddListener(UpdateScore);
            GameManager.Instance.OnTimeChanged.AddListener(UpdateTimer);
            GameManager.Instance.OnStrikeChanged.AddListener(UpdateStrikes);
            GameManager.Instance.OnComboTriggered.AddListener(ShowCombo);
            GameManager.Instance.OnRecipeStringUpdated.AddListener(UpdateRecipeText);
            GameManager.Instance.OnRecipeProgressChanged.AddListener(UpdateRecipeProgress);
            GameManager.Instance.OnRecipeCompleted.AddListener(ShowRecipeCompleted);
        }
        // Appliquer un paramétrage par défaut robuste et plus esthétique
        ApplyReadableDefaults(recipeText, 28, new Color(1f, 0.6f, 0f), FontStyles.Bold, wordWrap: true); // Orange
        ApplyReadableDefaults(scoreText, 45, new Color(0f, 0.8f, 1f), FontStyles.Bold); // Bleu Cyan
        ApplyReadableDefaults(timerText, 45, new Color(0.6f, 1f, 0.4f), FontStyles.Bold | FontStyles.UpperCase); // Vert Clair

        ApplyHudLayout();
        EnsureArcadeUIElements();
        EnsureRecipeProgressUI();
        EnsureOrderTabletUI();

        // Affichage initial
        UpdateRecipeText("Salade d'Acides : 3 Citrons, 2 Pommes"); // Placeholder
        UpdateModeUI();
        if (GameManager.Instance != null)
        {
            UpdateStrikes(GameManager.Instance.currentStrikes, GameManager.Instance.maxStrikes);
            UpdateScore(GameManager.Instance.score);
        }
    }

    private void Update()
    {
        // Efface le texte de Combo après quelques secondes
        if (comboText != null && comboText.gameObject.activeSelf && Time.time > comboHideTime)
        {
            comboText.gameObject.SetActive(false);
        }

        if (recipeToast != null && recipeToast.gameObject.activeSelf && Time.time > toastHideTime)
        {
            recipeToast.gameObject.SetActive(false);
        }

        // Tablet stays in world space (no following the camera position), but can rotate to face the player.
        if (orderTabletFacePlayer && orderTabletRoot != null && orderTabletRoot.activeSelf)
            UpdateOrderTabletYawOnly();
    }

    public void UpdateScore(int score)
    {
        if (scoreText != null)
            scoreText.text = $"Score: {score}";
    }

    public void UpdateTimer(float time)
    {
        if (timerText != null)
        {
            int minutes = Mathf.FloorToInt(time / 60);
            int seconds = Mathf.FloorToInt(time % 60);
            timerText.text = string.Format("{0:00}:{1:00}", minutes, seconds);
        }
    }

    public void UpdateStrikes(int current, int max)
    {
        if (strikesText != null)
        {
            string s = "";
            for (int i = 0; i < max; i++)
            {
                if (i < current) s += "<color=#FF2222>X </color>"; // Croix Rouge
                else s += "<color=#555555>X </color>"; // Croix Grise (Vide)
            }
            strikesText.text = s.TrimEnd();
        }
    }

    public void ShowCombo(int count, float multiplier)
    {
        if (comboText != null)
        {
            // Single-line to avoid overflowing in world-space HUD.
            comboText.text = $"{count} COMBO  x{multiplier:0.0}";
            comboText.gameObject.SetActive(true);
            comboHideTime = Time.time + 1.8f; // Reste affiché 1.8 secondes
        }
    }

    public void UpdateRecipeText(string newRecipe)
    {
        if (recipeText != null)
            recipeText.text = "Commande:\n" + newRecipe;
    }

    public void UpdateModeUI()
    {
        if (GameManager.Instance == null) return;

        bool show = GameManager.Instance.currentMode == GameMode.Recette && GameManager.Instance.weapon == WeaponType.Couteaux;
        if (orderTabletRoot != null) orderTabletRoot.SetActive(show);
        if (recipeToast != null) recipeToast.gameObject.SetActive(false);
        if (show) PlaceOrderTabletOnce();
    }

    private static void ApplyReadableDefaults(TextMeshProUGUI tmp, float baseSize, Color color, FontStyles style, bool wordWrap = false)
    {
        if (tmp == null) return;
        tmp.enableWordWrapping = wordWrap;
        tmp.enableAutoSizing = true;
        tmp.fontSize = baseSize;
        tmp.fontSizeMin = 14;
        tmp.fontSizeMax = baseSize * 1.2f;
        tmp.extraPadding = true;
        tmp.color = color;
        tmp.fontStyle = style;
    }

    private void EnsureArcadeUIElements()
    {
        if (strikesText == null)
        {
            GameObject stGo = new GameObject("StrikesText");
            stGo.transform.SetParent(transform, false);
            strikesText = stGo.AddComponent<TextMeshProUGUI>();
            var rt = strikesText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, -90); // Sous le chronomètre
            rt.sizeDelta = new Vector2(400, 100);
            
            ApplyReadableDefaults(strikesText, 60, Color.white, FontStyles.Bold);
            strikesText.alignment = TextAlignmentOptions.Center;
            strikesText.text = "";
        }

        if (comboText == null)
        {
            GameObject ctGo = new GameObject("ComboText");
            ctGo.transform.SetParent(transform, false);
            comboText = ctGo.AddComponent<TextMeshProUGUI>();
            var rt = comboText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 70); // En gros au milieu
            rt.sizeDelta = new Vector2(850, 170);
            
            ApplyReadableDefaults(comboText, 62, new Color(1f, 0.8f, 0.1f), FontStyles.Bold, wordWrap: false);
            comboText.alignment = TextAlignmentOptions.Center;
            comboText.enableWordWrapping = false;
            comboText.gameObject.SetActive(false);
        }
    }

    private void ApplyHudLayout()
    {
        // Layout for a wide wall Banner (1400x200)
        if (scoreText != null)
        {
            var rt = scoreText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(-450, 0);
            rt.sizeDelta = new Vector2(300, 150);
            scoreText.alignment = TextAlignmentOptions.Center;
        }

        if (timerText != null)
        {
            var rt = timerText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(450, 0);
            rt.sizeDelta = new Vector2(300, 150);
            timerText.alignment = TextAlignmentOptions.Center;
        }

        if (recipeText != null)
        {
            var rt = recipeText.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = new Vector2(0, 5); // Remonté un peu
            rt.sizeDelta = new Vector2(850, 180); // Élargi et plus haut
            recipeText.alignment = TextAlignmentOptions.Center;
        }
    }

    private void EnsureRecipeProgressUI()
    {
        if (transform.Find("RecipeProgress") != null) return;

        var root = new GameObject("RecipeProgress");
        root.transform.SetParent(transform, false);

        var rootRt = root.AddComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.5f, 0.5f);
        rootRt.anchorMax = new Vector2(0.5f, 0.5f);
        rootRt.pivot = new Vector2(0.5f, 0.5f);
        rootRt.anchoredPosition = new Vector2(0, -55);
        rootRt.sizeDelta = new Vector2(900, 22);

        var bg = new GameObject("Bg");
        bg.transform.SetParent(root.transform, false);
        var bgImg = bg.AddComponent<Image>();
        bgImg.color = new Color(1f, 1f, 1f, 0.10f);
        var bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        var fill = new GameObject("Fill");
        fill.transform.SetParent(bg.transform, false);
        var fillImg = fill.AddComponent<Image>();
        fillImg.color = new Color(1f, 0.75f, 0.15f, 0.85f);
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 0f;

        var fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = Vector2.zero;
        fillRt.anchorMax = Vector2.one;
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        recipeProgressFill = fillImg;

        // Toast / feedback
        var toastGo = new GameObject("RecipeToast");
        toastGo.transform.SetParent(transform, false);
        recipeToast = toastGo.AddComponent<TextMeshProUGUI>();
        ApplyReadableDefaults(recipeToast, 52, new Color(1f, 0.9f, 0.3f), FontStyles.Bold, wordWrap: false);
        recipeToast.alignment = TextAlignmentOptions.Center;
        recipeToast.text = "";
        var toastRt = recipeToast.rectTransform;
        toastRt.anchorMin = new Vector2(0.5f, 0.5f);
        toastRt.anchorMax = new Vector2(0.5f, 0.5f);
        toastRt.pivot = new Vector2(0.5f, 0.5f);
        toastRt.anchoredPosition = new Vector2(0, 95);
        toastRt.sizeDelta = new Vector2(1000, 120);
        recipeToast.gameObject.SetActive(false);
    }

    private void EnsureOrderTabletUI()
    {
        if (orderTabletRoot != null) return;

        if (orderTabletAnchor == null)
        {
            var go = GameObject.Find("OrderTablet_Anchor");
            if (go != null) orderTabletAnchor = go.transform;
        }

        // Create a separate world-space tablet so it doesn't overlap the banner HUD.
        orderTabletRoot = new GameObject("OrderTablet_World");
        orderTabletRoot.layer = gameObject.layer;

        var canvas = orderTabletRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;
        orderTabletRoot.AddComponent<CanvasScaler>().dynamicPixelsPerUnit = 10f;
        orderTabletRoot.AddComponent<GraphicRaycaster>();

        var tabletRt = orderTabletRoot.GetComponent<RectTransform>();
        tabletRt.sizeDelta = new Vector2(620, 520);
        tabletRt.localScale = Vector3.one * 0.0028f;
        orderTabletRect = tabletRt;
        orderTabletPlaced = false;

        // We handle yaw-only facing ourselves (see UpdateOrderTabletYawOnly()).

        var bgGo = new GameObject("Bg");
        bgGo.layer = orderTabletRoot.layer;
        bgGo.transform.SetParent(orderTabletRoot.transform, false);
        var bg = bgGo.AddComponent<Image>();
        bg.color = new Color(0.06f, 0.06f, 0.07f, 0.92f);
        var bgRt = bgGo.GetComponent<RectTransform>();
        bgRt.anchorMin = Vector2.zero;
        bgRt.anchorMax = Vector2.one;
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        // Header
        var headerGo = new GameObject("Header");
        headerGo.transform.SetParent(bgGo.transform, false);
        var header = headerGo.AddComponent<TextMeshProUGUI>();
        ApplyReadableDefaults(header, 34, new Color(1f, 1f, 1f, 0.9f), FontStyles.Bold, wordWrap: false);
        header.text = "CARNET DE COMMANDES";
        header.alignment = TextAlignmentOptions.Center;
        var headerRt = header.rectTransform;
        headerRt.anchorMin = new Vector2(0.5f, 1f);
        headerRt.anchorMax = new Vector2(0.5f, 1f);
        headerRt.pivot = new Vector2(0.5f, 1f);
        headerRt.anchoredPosition = new Vector2(0, -18);
        headerRt.sizeDelta = new Vector2(600, 60);

        // Body panel (paper)
        var paperGo = new GameObject("Paper");
        paperGo.transform.SetParent(bgGo.transform, false);
        var paperImg = paperGo.AddComponent<Image>();
        paperImg.color = new Color(1f, 1f, 1f, 0.06f);
        var paperRt = paperGo.GetComponent<RectTransform>();
        paperRt.anchorMin = new Vector2(0.5f, 0.5f);
        paperRt.anchorMax = new Vector2(0.5f, 0.5f);
        paperRt.pivot = new Vector2(0.5f, 0.5f);
        paperRt.anchoredPosition = new Vector2(0, -15);
        paperRt.sizeDelta = new Vector2(580, 380);

        // Re-parent recipe text into the tablet (list style)
        if (recipeText != null)
        {
            recipeText.transform.SetParent(paperGo.transform, false);
            recipeText.enableWordWrapping = true;
            recipeText.enableAutoSizing = false;
            recipeText.fontSize = 30;
            recipeText.color = new Color(1f, 0.8f, 0.35f, 0.95f);
            recipeText.alignment = TextAlignmentOptions.TopLeft;

            var rrt = recipeText.rectTransform;
            rrt.anchorMin = Vector2.zero;
            rrt.anchorMax = Vector2.one;
            rrt.offsetMin = new Vector2(22, 18);
            rrt.offsetMax = new Vector2(-22, -18);
        }

        // Move progress bar into tablet under the paper
        if (recipeProgressFill != null)
        {
            var progressRoot = recipeProgressFill.transform.parent != null ? recipeProgressFill.transform.parent.gameObject : null;
            if (progressRoot != null)
            {
                progressRoot.transform.SetParent(bgGo.transform, false);
                var prt = progressRoot.GetComponent<RectTransform>();
                if (prt != null)
                {
                    prt.anchorMin = new Vector2(0.5f, 0f);
                    prt.anchorMax = new Vector2(0.5f, 0f);
                    prt.pivot = new Vector2(0.5f, 0f);
                    prt.anchoredPosition = new Vector2(0, 55);
                    prt.sizeDelta = new Vector2(560, 22);
                }
            }
        }

        orderTabletRoot.SetActive(false);
    }

    private void PlaceOrderTabletOnce()
    {
        if (orderTabletRoot == null) return;
        if (orderTabletPlaced) return;

        if (orderTabletAnchor == null)
        {
            var go = GameObject.Find("OrderTablet_Anchor");
            if (go != null) orderTabletAnchor = go.transform;
        }

        if (orderTabletAnchor != null)
        {
            orderTabletRoot.transform.position = orderTabletAnchor.position;
            orderTabletRoot.transform.rotation = orderTabletAnchor.rotation;
            // Prevent accidental mirroring from negative scales.
            var s = orderTabletRoot.transform.localScale;
            orderTabletRoot.transform.localScale = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));

            // World-space Canvas front/back is easy to get reversed; allow a deterministic flip.
            if (flipOrderTabletYaw)
                orderTabletRoot.transform.rotation = Quaternion.AngleAxis(180f, Vector3.up) * orderTabletRoot.transform.rotation;

            orderTabletPlaced = true;
            if (orderTabletFacePlayer) UpdateOrderTabletYawOnly();
            return;
        }

        var cam = Camera.main;
        if (cam == null) return;

        var fwd = cam.transform.forward;
        fwd.y = 0f;
        if (fwd.sqrMagnitude < 0.0001f) fwd = Vector3.forward;
        fwd.Normalize();

        var right = Vector3.Cross(Vector3.up, fwd).normalized;

        // Fixed tablet: slightly right, lower than banner, a bit closer.
        var pos = cam.transform.position + fwd * 1.8f + right * 0.85f + Vector3.up * 1.00f;
        orderTabletRoot.transform.position = pos;
        orderTabletRoot.transform.rotation = Quaternion.LookRotation(fwd, Vector3.up);
        orderTabletPlaced = true;
    }

    private void UpdateOrderTabletYawOnly()
    {
        if (orderTabletRoot == null) return;
        var cam = Camera.main;
        if (cam == null) return;

        var toCam = cam.transform.position - orderTabletRoot.transform.position;
        toCam.y = 0f;
        if (toCam.sqrMagnitude < 0.0001f) return;
        toCam.Normalize();

        // IMPORTANT: For UI canvases, the "front" is typically the opposite of Transform.forward.
        // Using -toCam prevents seeing the backside (mirrored text).
        var desired = Quaternion.LookRotation(-toCam, Vector3.up);
        orderTabletRoot.transform.rotation = desired;
    }

    public void UpdateRecipeProgress(float progress01)
    {
        if (recipeProgressFill != null)
            recipeProgressFill.fillAmount = Mathf.Clamp01(progress01);
    }

    public void ShowRecipeCompleted()
    {
        if (recipeToast == null) return;
        recipeToast.text = "RECETTE RÉUSSIE !";
        recipeToast.gameObject.SetActive(true);
        toastHideTime = Time.time + 1.0f;
    }
}
