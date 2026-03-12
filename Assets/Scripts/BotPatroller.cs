using UnityEngine;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Abilities.AI;

/// <summary>
/// Patrols around capture points looking for enemies
/// </summary>
public class BotPatroller : MonoBehaviour
{
    [Header("Settings")]
    public float checkInterval = 5f;
    public float patrolRange = 25f;
    public float sprintDistance = 20f; // Sprint when this far from patrol point

    [Header("Obstacle Detection")]
    public float obstacleCheckDistance = 2f; // Only detect very close obstacles
    public float stuckCheckInterval = 1f; // Check if stuck more frequently
    public float stuckThreshold = 0.5f; // If moved less than this in 1s, stuck
    public LayerMask obstacleLayerMask = ~0; // Everything by default, configure in Inspector to exclude characters
    private float lastAvoidanceTime = -999f; // Cooldown for avoidance
    public float avoidanceCooldown = 0.5f; // Faster avoidance checks

    [Header("Smart Jump")]
    public bool enableSmartJump = true;
    public float maxJumpableHeight = 2.0f; // Max obstacle height bot can jump over
    public float jumpCheckDistance = 3.5f; // How far ahead to check for jumpable obstacles
    public float jumpCooldown = 1f; // Seconds between jump attempts (reduced for faster response)
    private float lastJumpTime = -999f;

    public int botTeam = -1;
    private CapturePoint nearestFlag;
    private Vector3 patrolDestination;
    private float nextCheckTime = 0f;

    private UltimateCharacterLocomotion characterLocomotion;
    private NavMeshAgentMovement navMeshMovement;
    private SpeedChange speedChangeAbility;
    private Jump jumpAbility;

    private Vector3 lastPosition;
    private float nextStuckCheckTime = 0f;

    public void SetTeam(int team)
    {
        botTeam = team;
        FindNearestFlag();
        PickPatrolPoint();
        MoveToTarget(); // Start moving immediately
    }

    void Start()
    {
        characterLocomotion = GetComponent<UltimateCharacterLocomotion>();
        if (characterLocomotion != null)
        {
            navMeshMovement = characterLocomotion.GetAbility<NavMeshAgentMovement>();
            if (navMeshMovement != null)
            {
                characterLocomotion.TryStartAbility(navMeshMovement);
            }

            speedChangeAbility = characterLocomotion.GetAbility<SpeedChange>();
            jumpAbility = characterLocomotion.GetAbility<Jump>();
        }
    }

    void Update()
    {
        if (botTeam == -1) return;

        // Find nearest flag and new patrol point periodically
        if (Time.time >= nextCheckTime)
        {
            FindNearestFlag();
            PickPatrolPoint();
            nextCheckTime = Time.time + checkInterval;
        }

        // Check if stuck
        if (Time.time >= nextStuckCheckTime)
        {
            CheckIfStuck();
            nextStuckCheckTime = Time.time + stuckCheckInterval;
        }

        MoveToTarget();
    }

    void MoveToTarget()
    {
        if (navMeshMovement != null)
        {
            float distance = Vector3.Distance(transform.position, patrolDestination);

            // Check for obstacles ahead (only if not on cooldown)
            if (Time.time - lastAvoidanceTime > avoidanceCooldown)
            {
                Vector3 forward = transform.forward;
                RaycastHit hit;
                bool obstacleAhead = Physics.Raycast(transform.position + Vector3.up, forward, out hit, obstacleCheckDistance, obstacleLayerMask);

                if (obstacleAhead)
                {
                    // Ignore if we hit ourselves or our own equipment
                    if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform)
                    {
                        obstacleAhead = false;
                    }
                }

                if (obstacleAhead && hit.distance < 2f) // Only for very close obstacles
                {
                    // First, try to jump over it!
                    if (TryJumpOverObstacle())
                    {
                        return; // Successfully initiated jump
                    }

                    // If can't jump, use smart avoidance with multiple direction checks
                    if (TrySmartAvoidance(patrolDestination))
                    {
                        lastAvoidanceTime = Time.time;
                        return;
                    }
                }
            }

            // Sprint when far from patrol point, walk when close
            if (speedChangeAbility != null)
            {
                if (distance > sprintDistance && !speedChangeAbility.IsActive)
                {
                    characterLocomotion.TryStartAbility(speedChangeAbility);
                }
                else if (distance <= sprintDistance && speedChangeAbility.IsActive)
                {
                    characterLocomotion.TryStopAbility(speedChangeAbility);
                }
            }

            navMeshMovement.SetDestination(patrolDestination);
        }
    }

    void FindNearestFlag()
    {
        // Use cached flags list for better performance
        var flags = CapturePoint.AllCapturePoints;
        CapturePoint closest = null;
        float closestDistance = float.MaxValue;

        foreach (var flag in flags)
        {
            float distance = Vector3.Distance(transform.position, flag.transform.position);
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = flag;
            }
        }

        nearestFlag = closest;
    }

    void PickPatrolPoint()
    {
        if (nearestFlag != null)
        {
            // Patrol around nearest flag
            Vector2 offset = Random.insideUnitCircle * patrolRange;
            patrolDestination = nearestFlag.transform.position + new Vector3(offset.x, 0, offset.y);
        }
        else
        {
            // No flags, just wander
            Vector2 offset = Random.insideUnitCircle * patrolRange;
            patrolDestination = transform.position + new Vector3(offset.x, 0, offset.y);
        }
    }

    void CheckIfStuck()
    {
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);

        if (distanceMoved < stuckThreshold)
        {
            // Bot is stuck - first try jumping over obstacle
            if (TryJumpOverObstacle())
            {
                Debug.Log($"{gameObject.name} stuck - attempting to jump over obstacle!");
            }
            else
            {
                // If can't jump, pick new random patrol point
                PickPatrolPoint();
            }
        }

        lastPosition = transform.position;
    }

    bool TryJumpOverObstacle()
    {
        // Check if jump is enabled and available
        if (!enableSmartJump || jumpAbility == null || Time.time - lastJumpTime < jumpCooldown)
        {
            return false;
        }

        Vector3 forward = transform.forward;
        Vector3 origin = transform.position + Vector3.up * 0.5f; // Check at waist height

        RaycastHit hit;
        // Check for obstacle ahead
        if (!Physics.Raycast(origin, forward, out hit, jumpCheckDistance, obstacleLayerMask))
        {
            return false; // No obstacle
        }

        // Ignore if we hit ourselves
        if (hit.collider.transform.IsChildOf(transform) || hit.collider.transform == transform)
        {
            return false;
        }

        // Measure obstacle height by casting down from above the obstacle
        Vector3 topCheckPos = hit.point + Vector3.up * maxJumpableHeight;
        RaycastHit topHit;

        float obstacleHeight = 0f;
        if (Physics.Raycast(topCheckPos, Vector3.down, out topHit, maxJumpableHeight + 1f, obstacleLayerMask))
        {
            obstacleHeight = topHit.point.y - transform.position.y;
        }
        else
        {
            // Can't determine height, assume too tall
            return false;
        }

        // Check if obstacle is jumpable height (between 0.3m and maxJumpableHeight)
        if (obstacleHeight < 0.3f || obstacleHeight > maxJumpableHeight)
        {
            return false;
        }

        // Check if there's clearance above (check for ceiling/overhang above the obstacle)
        Vector3 obstacleTop = new Vector3(hit.point.x, transform.position.y + obstacleHeight + 0.1f, hit.point.z);
        RaycastHit clearanceHit;
        float clearanceNeeded = 2f; // Need 2m clearance for jump arc
        if (Physics.Raycast(obstacleTop, Vector3.up, out clearanceHit, clearanceNeeded, obstacleLayerMask))
        {
            // Something blocking above (ceiling or overhang)
            Debug.Log($"{gameObject.name}: No clearance above obstacle - ceiling/overhang at {clearanceHit.distance}m above top of obstacle");
            return false;
        }

        Debug.Log($"{gameObject.name}: Clearance check passed - {clearanceNeeded}m clearance above obstacle");

        // Check landing spot on the other side
        Vector3 landingSpot = hit.point + forward * 2f;
        landingSpot.y = transform.position.y;

        UnityEngine.AI.NavMeshHit navHit;
        if (!UnityEngine.AI.NavMesh.SamplePosition(landingSpot, out navHit, 3f, UnityEngine.AI.NavMesh.AllAreas))
        {
            return false; // No valid NavMesh on other side
        }

        // All checks passed - trigger jump!
        Debug.Log($"{gameObject.name} jumping over obstacle! Height: {obstacleHeight:F2}m");

        if (characterLocomotion.TryStartAbility(jumpAbility))
        {
            lastJumpTime = Time.time;
            return true;
        }

        return false;
    }

    bool TrySmartAvoidance(Vector3 goalPosition)
    {
        // Multi-directional avoidance - check 8 directions around the bot
        Vector3 toGoal = (goalPosition - transform.position).normalized;
        Vector3 right = transform.right;

        // Test directions: 45° left, 45° right, 90° left, 90° right, 135° left, 135° right
        float[] angles = { -45f, 45f, -90f, 90f, -135f, 135f, -22.5f, 22.5f };
        float bestScore = -1000f;
        Vector3 bestDirection = Vector3.zero;

        foreach (float angle in angles)
        {
            // Calculate test direction
            Quaternion rotation = Quaternion.Euler(0, angle, 0);
            Vector3 testDirection = rotation * transform.forward;
            Vector3 testPosition = transform.position + testDirection * 4f;

            // Check if this direction is clear of obstacles
            RaycastHit obstacleCheck;
            if (Physics.Raycast(transform.position + Vector3.up, testDirection, out obstacleCheck, 4f, obstacleLayerMask))
            {
                // Skip if we hit ourselves
                if (obstacleCheck.collider.transform.IsChildOf(transform) || obstacleCheck.collider.transform == transform)
                {
                    // It's us, ignore
                }
                else
                {
                    continue; // Direction blocked, skip
                }
            }

            // Check if NavMesh exists in this direction
            UnityEngine.AI.NavMeshHit navHit;
            if (!UnityEngine.AI.NavMesh.SamplePosition(testPosition, out navHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                continue; // No NavMesh, skip
            }

            // Score this direction: prefer directions closer to goal
            float goalAlignment = Vector3.Dot(testDirection, toGoal);
            float score = goalAlignment * 100f; // Weight toward goal heavily

            if (score > bestScore)
            {
                bestScore = score;
                bestDirection = testDirection;
            }
        }

        // If we found a good direction, use it
        if (bestScore > -1000f)
        {
            Vector3 avoidPosition = transform.position + bestDirection * 4f;
            UnityEngine.AI.NavMeshHit finalNavHit;
            if (UnityEngine.AI.NavMesh.SamplePosition(avoidPosition, out finalNavHit, 5f, UnityEngine.AI.NavMesh.AllAreas))
            {
                navMeshMovement.SetDestination(finalNavHit.position);
                return true;
            }
        }

        return false;
    }
}
