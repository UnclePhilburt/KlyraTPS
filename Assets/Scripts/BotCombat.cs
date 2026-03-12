using UnityEngine;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Abilities.Items;

/// <summary>
/// Handles bot combat - detecting and shooting enemies
/// Add this to any bot along with their behavior component
/// </summary>
public class BotCombat : MonoBehaviour
{
    [Header("Combat Settings")]
    public float detectionRange = 50f;
    public float engagementRange = 20f;
    public float checkInterval = 0.5f;

    private int botTeam = -1;
    public int BotTeam { get { return botTeam; } }

    private GameObject currentTarget;
    private float nextCheckTime;

    private UltimateCharacterLocomotion characterLocomotion;
    private Use useAbility;
    private LocalLookSource lookSource;

    public void SetTeam(int team)
    {
        botTeam = team;
    }

    void Start()
    {
        characterLocomotion = GetComponent<UltimateCharacterLocomotion>();
        if (characterLocomotion != null)
        {
            useAbility = characterLocomotion.GetAbility<Use>();
            lookSource = GetComponent<LocalLookSource>();
        }
    }

    void Update()
    {
        if (botTeam == -1) return;

        // Find enemies
        if (Time.time >= nextCheckTime)
        {
            FindEnemy();
            nextCheckTime = Time.time + checkInterval;
        }

        // Shoot at enemy
        if (currentTarget != null)
        {
            float distance = Vector3.Distance(transform.position, currentTarget.transform.position);

            // Aim at target
            if (lookSource != null)
            {
                lookSource.Target = currentTarget.transform;
            }

            // Shoot if in range
            if (distance <= engagementRange && useAbility != null)
            {
                if (!useAbility.IsActive)
                {
                    characterLocomotion.TryStartAbility(useAbility);
                }
            }
            else
            {
                StopShooting();
            }
        }
        else
        {
            StopShooting();
            if (lookSource != null)
            {
                lookSource.Target = null;
            }
        }
    }

    void FindEnemy()
    {
        // Use Physics.OverlapSphere instead of FindObjectsOfType for better performance
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, detectionRange);

        GameObject bestTarget = null;
        float closestDistance = detectionRange;
        int charactersFound = 0;

        foreach (var collider in nearbyColliders)
        {
            // Try to get UltimateCharacterLocomotion from collider or parent
            var character = collider.GetComponent<UltimateCharacterLocomotion>();
            if (character == null)
            {
                character = collider.GetComponentInParent<UltimateCharacterLocomotion>();
            }
            if (character == null || character.gameObject == gameObject) continue;

            charactersFound++;

            // Check if alive
            var health = character.GetComponent<Opsive.UltimateCharacterController.Traits.Health>();
            if (health != null && !health.IsAlive()) continue;

            // Get team
            int otherTeam = GetCharacterTeam(character);
            if (otherTeam == -1 || otherTeam == botTeam) continue;

            // Check distance
            float distance = Vector3.Distance(transform.position, character.transform.position);
            if (distance < closestDistance)
            {
                // Line of sight check
                Vector3 directionToTarget = (character.transform.position + Vector3.up) - (transform.position + Vector3.up);
                RaycastHit hit;
                if (Physics.Raycast(transform.position + Vector3.up, directionToTarget, out hit, distance))
                {
                    if (hit.collider.GetComponentInParent<UltimateCharacterLocomotion>() == character)
                    {
                        closestDistance = distance;
                        bestTarget = character.gameObject;
                    }
                }
            }
        }

        currentTarget = bestTarget;

        // Only log when we find or lose a target
        if (currentTarget != null)
        {
            Debug.Log($"{gameObject.name} (Team {botTeam}): Found enemy target {currentTarget.name} at {closestDistance:F1}m | Checked {charactersFound} characters from {nearbyColliders.Length} colliders");
        }
    }

    int GetCharacterTeam(UltimateCharacterLocomotion character)
    {
        // Check for bot behaviors
        var attacker = character.GetComponent<BotAttacker>();
        if (attacker != null) return attacker.botTeam;

        var defender = character.GetComponent<BotDefender>();
        if (defender != null) return defender.botTeam;

        var patroller = character.GetComponent<BotPatroller>();
        if (patroller != null) return patroller.botTeam;

        // Check for BotCombat (fallback)
        var combat = character.GetComponent<BotCombat>();
        if (combat != null && combat != this) return combat.BotTeam;

        // Check for player
        var pv = character.GetComponent<Photon.Pun.PhotonView>();
        if (pv != null && pv.Owner != null)
        {
            return SimplePlayerSpawner.GetPlayerTeam(pv.Owner);
        }

        return -1;
    }

    void StopShooting()
    {
        if (useAbility != null && useAbility.IsActive)
        {
            characterLocomotion.TryStopAbility(useAbility);
        }
    }
}
