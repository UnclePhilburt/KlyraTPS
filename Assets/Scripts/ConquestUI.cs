using UnityEngine;
using UnityEngine.UI;
using TMPro;

/// <summary>
/// Simple UI for Conquest game mode
/// Shows team tickets and flag status
/// </summary>
public class ConquestUI : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The Conquest game mode manager")]
    public ConquestGameMode gameMode;

    [Header("UI Settings")]
    [Tooltip("Font size for text")]
    public int fontSize = 24;

    private Canvas canvas;
    private TextMeshProUGUI scoreText;
    private TextMeshProUGUI gameOverText;

    void Start()
    {
        CreateUI();
    }

    void Update()
    {
        if (gameMode == null) return;

        UpdateScoreDisplay();
        UpdateGameOverDisplay();
    }

    void CreateUI()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("Domination UI Canvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 50;

        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);

        // Create score text (top center)
        GameObject scoreObj = new GameObject("Score Text");
        scoreObj.transform.SetParent(canvasObj.transform, false);

        scoreText = scoreObj.AddComponent<TextMeshProUGUI>();
        scoreText.fontSize = fontSize;
        scoreText.alignment = TextAlignmentOptions.Center;
        scoreText.color = Color.white;
        scoreText.raycastTarget = false;

        RectTransform scoreRect = scoreText.rectTransform;
        scoreRect.anchorMin = new Vector2(0.5f, 1f);
        scoreRect.anchorMax = new Vector2(0.5f, 1f);
        scoreRect.pivot = new Vector2(0.5f, 1f);
        scoreRect.anchoredPosition = new Vector2(0, -20);
        scoreRect.sizeDelta = new Vector2(600, 100);

        // Create game over text (center)
        GameObject gameOverObj = new GameObject("Game Over Text");
        gameOverObj.transform.SetParent(canvasObj.transform, false);

        gameOverText = gameOverObj.AddComponent<TextMeshProUGUI>();
        gameOverText.fontSize = fontSize * 2;
        gameOverText.alignment = TextAlignmentOptions.Center;
        gameOverText.color = Color.yellow;
        gameOverText.raycastTarget = false;
        gameOverText.text = "";

        RectTransform gameOverRect = gameOverText.rectTransform;
        gameOverRect.anchorMin = new Vector2(0.5f, 0.5f);
        gameOverRect.anchorMax = new Vector2(0.5f, 0.5f);
        gameOverRect.pivot = new Vector2(0.5f, 0.5f);
        gameOverRect.anchoredPosition = Vector2.zero;
        gameOverRect.sizeDelta = new Vector2(800, 200);
    }

    void UpdateScoreDisplay()
    {
        if (scoreText == null) return;

        int usArmyFlags = gameMode.GetTeamFlagCount(0);
        int insurgentFlags = gameMode.GetTeamFlagCount(1);

        scoreText.text = $"<color=#4444FF>US ARMY: {gameMode.USArmyTickets} tickets [{usArmyFlags} flags]</color>  |  " +
                        $"<color=#FF4444>INSURGENTS: {gameMode.InsurgentTickets} tickets [{insurgentFlags} flags]</color>";
    }

    void UpdateGameOverDisplay()
    {
        if (gameOverText == null) return;

        if (!gameMode.GameActive)
        {
            string teamName = (gameMode.WinningTeam == 0) ? "US ARMY" : "INSURGENTS";
            gameOverText.text = $"GAME OVER\n{teamName} WINS!";
        }
        else
        {
            gameOverText.text = "";
        }
    }
}
