using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using System.Linq;
using Opsive.UltimateCharacterController.Character;

/// <summary>
/// Capture point for Domination game mode
/// Players stand in the zone to capture it for their team
/// </summary>
public class CapturePoint : MonoBehaviourPunCallbacks, IPunObservable
{
    // Static list of all capture points for performance (avoids FindObjectsOfType)
    private static List<CapturePoint> allCapturePoints = new List<CapturePoint>();
    public static List<CapturePoint> AllCapturePoints { get { return allCapturePoints; } }

    [Header("Capture Settings")]
    [Tooltip("Name of this capture point (A, B, C, etc.)")]
    public string pointName = "A";

    [Tooltip("Radius of the capture zone")]
    public float captureRadius = 10f;

    [Tooltip("Time to capture when contested (seconds)")]
    public float captureTime = 10f;

    [Tooltip("How many points per second this flag awards")]
    public float pointsPerSecond = 1f;

    [Header("Visual")]
    [Tooltip("Color when neutral")]
    public Color neutralColor = Color.white;

    [Tooltip("Color when US Army owns it")]
    public Color usArmyColor = Color.blue;

    [Tooltip("Color when Insurgents own it")]
    public Color insurgentColor = Color.red;

    // State
    private int owningTeam = -1; // -1 = neutral, 0 = US Army, 1 = Insurgents
    private float captureProgress = 0f; // 0 to 1
    private int capturingTeam = -1;

    // Auto-created visuals
    private GameObject groundCircle;
    private GameObject flagPole;
    private GameObject flag;
    private Renderer flagRenderer;
    private Renderer groundRenderer;

    // Public properties
    public int OwningTeam { get { return owningTeam; } }
    public float CaptureProgress { get { return captureProgress; } }
    public string PointName { get { return pointName; } }

    void OnEnable()
    {
        if (!allCapturePoints.Contains(this))
        {
            allCapturePoints.Add(this);
        }
    }

    void OnDisable()
    {
        allCapturePoints.Remove(this);
    }

    void Start()
    {
        CreateVisuals();
        UpdateVisuals();
    }

    void CreateVisuals()
    {
        // Create ground circle (capture zone indicator)
        groundCircle = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        groundCircle.name = "CaptureZone";
        groundCircle.transform.SetParent(transform);
        groundCircle.transform.localPosition = Vector3.up * 0.1f;
        groundCircle.transform.localScale = new Vector3(captureRadius * 2, 0.1f, captureRadius * 2);

        groundRenderer = groundCircle.GetComponent<Renderer>();
        groundRenderer.material.color = new Color(1, 1, 1, 0.5f);

        // Destroy the collider so it doesn't interfere
        Destroy(groundCircle.GetComponent<Collider>());

        // Create flag pole
        flagPole = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        flagPole.name = "FlagPole";
        flagPole.transform.SetParent(transform);
        flagPole.transform.localPosition = Vector3.up * 2.5f;
        flagPole.transform.localScale = new Vector3(0.2f, 2.5f, 0.2f);

        var poleRenderer = flagPole.GetComponent<Renderer>();
        poleRenderer.material.color = Color.gray;
        Destroy(flagPole.GetComponent<Collider>());

        // Create flag (cube stretched to look like a flag)
        flag = GameObject.CreatePrimitive(PrimitiveType.Cube);
        flag.name = "Flag";
        flag.transform.SetParent(transform);
        flag.transform.localPosition = new Vector3(1.5f, 4f, 0);
        flag.transform.localScale = new Vector3(2f, 1f, 0.1f);

        flagRenderer = flag.GetComponent<Renderer>();
        Destroy(flag.GetComponent<Collider>());

        // Create point name text (3D text)
        GameObject textObj = new GameObject("PointName");
        textObj.transform.SetParent(transform);
        textObj.transform.localPosition = Vector3.up * 6f;
        textObj.transform.localRotation = Quaternion.identity;

        TextMesh textMesh = textObj.AddComponent<TextMesh>();
        textMesh.text = pointName;
        textMesh.characterSize = 0.5f;
        textMesh.fontSize = 48;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.color = Color.white;
    }

    void Update()
    {
        // Only Master Client runs capture logic
        if (!PhotonNetwork.IsMasterClient) return;

        // Find all characters in capture zone using Physics.OverlapSphere (MUCH faster than FindObjectsOfType)
        Dictionary<int, int> teamCounts = new Dictionary<int, int>();
        teamCounts[0] = 0; // US Army
        teamCounts[1] = 0; // Insurgents

        // Use OverlapSphere to only get nearby colliders
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, captureRadius);
        int charactersChecked = 0;

        foreach (var collider in nearbyColliders)
        {
            // Try to get UltimateCharacterLocomotion from collider or parent
            var character = collider.GetComponent<UltimateCharacterLocomotion>();
            if (character == null)
            {
                character = collider.GetComponentInParent<UltimateCharacterLocomotion>();
            }
            if (character == null) continue;

            charactersChecked++;

            // Check if alive
            var health = character.GetComponent<Opsive.UltimateCharacterController.Traits.Health>();
            if (health != null && !health.IsAlive()) continue;

            int team = GetCharacterTeam(character);

            if (team >= 0 && team <= 1)
            {
                teamCounts[team]++;
            }
        }

        // Only log when there are characters in the zone
        if (charactersChecked > 0)
        {
            Debug.Log($"Point {pointName}: {charactersChecked} characters in zone | US Army: {teamCounts[0]}, Insurgents: {teamCounts[1]} | Checked {nearbyColliders.Length} colliders");
        }

        // Determine capture state
        int usArmyCount = teamCounts[0];
        int insurgentCount = teamCounts[1];

        if (usArmyCount > 0 && insurgentCount == 0)
        {
            // US Army capturing
            CaptureTick(0, usArmyCount);
        }
        else if (insurgentCount > 0 && usArmyCount == 0)
        {
            // Insurgents capturing
            CaptureTick(1, insurgentCount);
        }
        else if (usArmyCount > 0 && insurgentCount > 0)
        {
            // Contested - no progress
            capturingTeam = -1;
        }
        else
        {
            // No one in zone - decay progress if not owned
            if (owningTeam == -1)
            {
                captureProgress = Mathf.Max(0, captureProgress - Time.deltaTime / captureTime);
                capturingTeam = -1;
            }
        }
    }

    void CaptureTick(int team, int playerCount)
    {
        capturingTeam = team;

        // If already owned by this team, nothing to capture
        if (owningTeam == team)
        {
            captureProgress = 1f;
            return;
        }

        // Capturing (faster with more players)
        float captureSpeed = (1f / captureTime) * Mathf.Min(playerCount, 3); // Max 3x speed
        captureProgress += Time.deltaTime * captureSpeed;

        if (captureProgress >= 1f)
        {
            // Captured!
            captureProgress = 1f;
            owningTeam = team;
            string teamName = (team == 0) ? "US Army" : (team == 1) ? "Insurgents" : "Unknown";
            Debug.Log($"FLAG CAPTURED! Point {pointName} captured by {teamName} (team {team})");
            UpdateVisuals();
        }
    }

    void UpdateVisuals()
    {
        if (flagRenderer == null || groundRenderer == null) return;

        Color targetColor = neutralColor;
        if (owningTeam == 0)
            targetColor = usArmyColor;
        else if (owningTeam == 1)
            targetColor = insurgentColor;

        // Update flag color
        flagRenderer.material.color = targetColor;

        // Update ground circle color (semi-transparent)
        Color groundColor = targetColor;
        groundColor.a = 0.3f;
        groundRenderer.material.color = groundColor;
    }

    int GetCharacterTeam(UltimateCharacterLocomotion character)
    {
        // Check for bot components (try all possible bot types)
        var attacker = character.GetComponent<BotAttacker>();
        if (attacker != null) return attacker.botTeam;

        var defender = character.GetComponent<BotDefender>();
        if (defender != null) return defender.botTeam;

        var patroller = character.GetComponent<BotPatroller>();
        if (patroller != null) return patroller.botTeam;

        var combat = character.GetComponent<BotCombat>();
        if (combat != null) return combat.BotTeam;

        // Check for OpsiveBotAI (if it exists)
        var opsiveBotAI = character.GetComponent<OpsiveBotAI>();
        if (opsiveBotAI != null) return opsiveBotAI.BotTeam;

        // Check for player
        var pv = character.GetComponent<PhotonView>();
        if (pv != null && pv.Owner != null)
        {
            return SimplePlayerSpawner.GetPlayerTeam(pv.Owner);
        }

        return -1;
    }

    // Photon serialization
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Master Client sends state
            stream.SendNext(owningTeam);
            stream.SendNext(captureProgress);
            stream.SendNext(capturingTeam);
        }
        else
        {
            // Clients receive state
            int newOwningTeam = (int)stream.ReceiveNext();
            captureProgress = (float)stream.ReceiveNext();
            capturingTeam = (int)stream.ReceiveNext();

            if (newOwningTeam != owningTeam)
            {
                owningTeam = newOwningTeam;
                UpdateVisuals();
            }
        }
    }

    void OnDrawGizmos()
    {
        // Draw capture zone
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, captureRadius);

        // Draw vertical line
        Gizmos.DrawLine(transform.position, transform.position + Vector3.up * 5f);
    }
}
