using UnityEngine;
using TMPro;

public class RecipeUI : MonoBehaviour
{
    public TextMeshProUGUI recipeText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;
    
    // Nouveaux éléments dynamiques Arcade
    private TextMeshProUGUI strikesText;
    private TextMeshProUGUI comboText;
    private float comboHideTime = 0f;

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged.AddListener(UpdateScore);
            GameManager.Instance.OnTimeChanged.AddListener(UpdateTimer);
            GameManager.Instance.OnStrikeChanged.AddListener(UpdateStrikes);
            GameManager.Instance.OnComboTriggered.AddListener(ShowCombo);
        }
        // Appliquer un paramétrage par défaut robuste et plus esthétique
        ApplyReadableDefaults(recipeText, 32, new Color(1f, 0.6f, 0f), FontStyles.Bold); // Orange
        ApplyReadableDefaults(scoreText, 45, new Color(0f, 0.8f, 1f), FontStyles.Bold); // Bleu Cyan
        ApplyReadableDefaults(timerText, 45, new Color(0.6f, 1f, 0.4f), FontStyles.Bold | FontStyles.UpperCase); // Vert Clair

        ApplyHudLayout();
        EnsureArcadeUIElements();

        // Affichage initial
        UpdateRecipeText("Salade d'Acides : 3 Citrons, 2 Pommes"); // Placeholder
        UpdateModeUI();
    }

    private void Update()
    {
        // Efface le texte de Combo après quelques secondes
        if (comboText != null && comboText.gameObject.activeSelf && Time.time > comboHideTime)
        {
            comboText.gameObject.SetActive(false);
        }
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
            if (GameManager.Instance != null && GameManager.Instance.currentMode == GameMode.Recette)
            {
                strikesText.text = ""; // Cache les croix si en mode recette pur
                return;
            }

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
            comboText.text = $"{count} FRUIT COMBO\nx{multiplier:0.0}";
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
        if (recipeText == null) return;
        if (GameManager.Instance == null) return;

        recipeText.gameObject.SetActive(GameManager.Instance.currentMode == GameMode.Recette);
    }

    private static void ApplyReadableDefaults(TextMeshProUGUI tmp, float baseSize, Color color, FontStyles style)
    {
        if (tmp == null) return;
        tmp.enableWordWrapping = true;
        tmp.enableAutoSizing = true;
        tmp.fontSize = baseSize;
        tmp.fontSizeMin = Mathf.Max(16, baseSize * 0.5f);
        tmp.fontSizeMax = Mathf.Max(tmp.fontSizeMin + 2, baseSize * 1.5f);
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
            rt.sizeDelta = new Vector2(500, 150);
            
            ApplyReadableDefaults(comboText, 65, new Color(1f, 0.8f, 0.1f), FontStyles.Bold);
            comboText.alignment = TextAlignmentOptions.Center;
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
            rt.anchoredPosition = new Vector2(0, 0);
            rt.sizeDelta = new Vector2(650, 150);
            recipeText.alignment = TextAlignmentOptions.Center;
        }
    }
}
