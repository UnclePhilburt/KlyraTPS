using UnityEngine;
using UnityEngine.UI;
using Photon.Pun;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Traits;

/// <summary>
/// Smart crosshair system that:
/// - Only shows when local player is alive
/// - Changes color when aiming at enemies
/// - Auto-hides on death
/// </summary>
public class SimpleCrosshair : MonoBehaviour
{
    [Header("Crosshair Settings")]
    [Tooltip("Size of the crosshair dot")]
    public float dotSize = 4f;

    [Tooltip("Normal crosshair color")]
    public Color normalColor = Color.white;

    [Tooltip("Color when aiming at enemy")]
    public Color enemyColor = Color.red;

    [Header("Enemy Detection")]
    [Tooltip("How far to raycast for enemy detection")]
    public float detectionRange = 100f;

    [Tooltip("Layer mask for enemy detection")]
    public LayerMask enemyLayers = -1;


    private Canvas canvas;
    private Image crosshairImage;
    private GameObject localPlayer;
    private UltimateCharacterLocomotion characterLocomotion;
    private Health playerHealth;
    private Camera mainCamera;

    void Start()
    {
        CreateCrosshair();
        mainCamera = Camera.main;
    }

    void Update()
    {
        // Find local player if we don't have one
        if (localPlayer == null)
        {
            FindLocalPlayer();
        }

        // Update crosshair visibility based on player state
        UpdateCrosshairVisibility();

        // Check if aiming at enemy and change color
        if (localPlayer != null && canvas != null && canvas.gameObject.activeSelf)
        {
            CheckForEnemy();
        }
    }

    void FindLocalPlayer()
    {
        // Find all characters in scene
        var allCharacters = FindObjectsOfType<UltimateCharacterLocomotion>();

        foreach (var character in allCharacters)
        {
            // Check if this is the local player (has PhotonView and is mine)
            var photonView = character.GetComponent<PhotonView>();
            if (photonView != null && photonView.IsMine)
            {
                localPlayer = character.gameObject;
                characterLocomotion = character;
                playerHealth = character.GetComponent<Health>();
                break;
            }
        }
    }

    void UpdateCrosshairVisibility()
    {
        if (canvas == null)
            return;

        // Hide if no local player found yet
        if (localPlayer == null)
        {
            canvas.gameObject.SetActive(false);
            return;
        }

        // Hide if player is dead
        if (playerHealth != null && !playerHealth.IsAlive())
        {
            canvas.gameObject.SetActive(false);
            return;
        }

        // Show crosshair - player is alive
        if (!canvas.gameObject.activeSelf)
        {
            canvas.gameObject.SetActive(true);
        }
    }

    void CheckForEnemy()
    {
        if (mainCamera == null)
        {
            mainCamera = Camera.main;
            if (mainCamera == null) return;
        }

        // Raycast from center of screen
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(0.5f, 0.5f, 0));
        RaycastHit hit;

        if (Physics.Raycast(ray, out hit, detectionRange, enemyLayers))
        {
            // Check if we hit an enemy (different team)
            var otherPhotonView = hit.collider.GetComponent<PhotonView>();
            if (otherPhotonView != null && otherPhotonView.Owner != null)
            {
                // Check if it's a bot
                var opsiveBotAI = hit.collider.GetComponent<OpsiveBotAI>();

                int otherTeam = 0;
                if (opsiveBotAI != null)
                {
                    otherTeam = opsiveBotAI.BotTeam;
                }
                else if (otherPhotonView.Owner != null)
                {
                    otherTeam = SimplePlayerSpawner.GetPlayerTeam(otherPhotonView.Owner);
                }

                // Get my team
                var myPhotonView = localPlayer.GetComponent<PhotonView>();
                if (myPhotonView != null && myPhotonView.Owner != null)
                {
                    int myTeam = SimplePlayerSpawner.GetPlayerTeam(myPhotonView.Owner);

                    // Change to red if aiming at enemy
                    if (myTeam != otherTeam)
                    {
                        crosshairImage.color = enemyColor;
                        return;
                    }
                }
            }
        }

        // Default to white
        crosshairImage.color = normalColor;
    }

    void CreateCrosshair()
    {
        // Create Canvas
        GameObject canvasObj = new GameObject("Crosshair Canvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100; // Render on top of everything

        // Add Canvas Scaler
        CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920, 1080);
        scaler.matchWidthOrHeight = 0.5f;

        // Create crosshair image
        GameObject crosshairObj = new GameObject("Crosshair Dot");
        crosshairObj.transform.SetParent(canvasObj.transform, false);

        crosshairImage = crosshairObj.AddComponent<Image>();
        crosshairImage.color = normalColor;
        crosshairImage.raycastTarget = false; // Don't block input!

        // Position in center of screen
        RectTransform rect = crosshairImage.rectTransform;
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.pivot = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(dotSize, dotSize);

        // Start hidden until we find local player
        canvas.gameObject.SetActive(false);
    }
}
