using UnityEngine;
using TMPro;

public class RecipeUI : MonoBehaviour
{
    public TextMeshProUGUI recipeText;
    public TextMeshProUGUI scoreText;
    public TextMeshProUGUI timerText;

    private void Start()
    {
        if (GameManager.Instance != null)
        {
            GameManager.Instance.OnScoreChanged.AddListener(UpdateScore);
            GameManager.Instance.OnTimeChanged.AddListener(UpdateTimer);
        }
        // Appliquer un paramétrage par défaut robuste et plus esthétique
        ApplyReadableDefaults(recipeText, 32, new Color(1f, 0.6f, 0f), FontStyles.Bold); // Orange
        ApplyReadableDefaults(scoreText, 45, new Color(0f, 0.8f, 1f), FontStyles.Bold); // Bleu Cyan
        ApplyReadableDefaults(timerText, 45, new Color(0.6f, 1f, 0.4f), FontStyles.Bold | FontStyles.UpperCase); // Vert Clair

        ApplyHudLayout();

        // Affichage initial
        UpdateRecipeText("Salade d'Acides : 3 Citrons, 2 Pommes"); // Placeholder
        UpdateModeUI();
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
