using UnityEngine;
using Photon.Pun;
using Opsive.UltimateCharacterController.Character;
using Opsive.UltimateCharacterController.Character.Abilities;
using Opsive.UltimateCharacterController.Character.Abilities.AI;
using Opsive.UltimateCharacterController.Character.Abilities.Items;

/// <summary>
/// Bot AI that works WITH Opsive's systems, not against them
/// Uses LocalLookSource for aiming, NavMeshAgentMovement for pathfinding
/// </summary>
public class OpsiveBotAI : MonoBehaviour
{
    // Static list of all OpsiveBotAI instances for performance (avoids FindObjectsOfType)
    private static System.Collections.Generic.List<OpsiveBotAI> allBots = new System.Collections.Generic.List<OpsiveBotAI>();
    public static System.Collections.Generic.List<OpsiveBotAI> AllBots { get { return allBots; } }

    [Header("Detection")]
    [Tooltip("How far the bot can see enemies")]
    public float detectionRange = 50f;

    [Tooltip("How often to search for enemies (seconds)")]
    public float targetUpdateInterval = 0.5f;

    [Tooltip("Number of raycasts to shoot in a cone for detection")]
    public int detectionRayCount = 5;

    [Tooltip("Horizontal angle of detection cone (degrees)")]
    public float detectionAngle = 120f;

    [Tooltip("Layers that block vision")]
    public LayerMask visionBlockingLayers = -1;

    [Header("Combat")]
    [Tooltip("Distance to maintain from enemy")]
    public float engagementRange = 15f;

    [Tooltip("How long to shoot in bursts (seconds)")]
    public float burstDuration = 0.5f;

    [Tooltip("Cooldown between bursts (seconds)")]
    public float burstCooldown = 0.3f;

    [Header("Patrol")]
    [Tooltip("How far to wander when no enemy")]
    public float patrolRadius = 20f;

    [Tooltip("Time between picking new patrol points")]
    public float patrolInterval = 5f;

    [Header("Conquest Mode")]
    [Tooltip("Enable conquest flag capturing behavior")]
    public bool conquestMode = true;

    [Tooltip("How close to get to flag before defending")]
    public float flagDefenseRange = 15f;

    [Tooltip("Max defenders per captured flag")]
    public int maxDefendersPerFlag = 2;

    [Tooltip("Distance to consider a bot 'at' a flag")]
    public float flagProximityCheck = 20f;

    [Header("Stuck Detection")]
    [Tooltip("Time without movement before considering stuck (seconds)")]
    public float stuckTimeout = 3f;

    [Tooltip("Minimum distance to move to not be considered stuck")]
    public float stuckDistanceThreshold = 0.5f;

    [Tooltip("How far to try moving when unsticking")]
    public float unstuckDistance = 20f;

    [Tooltip("Check for obstacles this close when moving")]
    public float obstacleCheckDistance = 2f;

    [Header("Safety")]
    [Tooltip("Y position below which bot is considered out of bounds")]
    public float outOfBoundsY = -50f;

    // State
    private enum BotState { Patrol, Combat }
    private BotState currentState = BotState.Patrol;

    // Opsive Components (the proper way!)
    private UltimateCharacterLocomotion characterLocomotion;
    private NavMeshAgentMovement navMeshMovement;
    private Use useAbility;
    private Jump jumpAbility;
    private LocalLookSource lookSource;
    private PhotonView photonView;

    // Targeting
    private GameObject currentTarget;
    private float nextTargetUpdateTime;

    // Combat
    private bool inBurst;
    private float burstEndTime;
    private float nextBurstTime;

    // Patrol
    private Vector3 patrolDestination;
    private float nextPatrolTime;
    private CapturePoint targetFlag;
    private float nextFlagCheckTime;

    // Squad system
    public BotSquad currentSquad;
    private Vector3 squadFormationOffset;
    private Vector3 objectiveHoldPosition; // Position to hold at objective
    private float nextObjectivePositionUpdate; // When to pick new position

    // Team
    private int botTeam = -1; // -1 = unassigned
    public bool teamAssigned = false;
    public int BotTeam { get { return botTeam; } }

    // Stuck detection
    private Vector3 lastPosition;
    private float lastMovementTime;
    private bool isStuck = false;
    private int unstuckAttempts = 0;
    private float lastUnstuckTime = 0f;

    public void SetTeam(int team)
    {
        botTeam = team;
        teamAssigned = true;
    }

    public void AssignToSquad(BotSquad squad)
    {
        currentSquad = squad;
        // Assign random formation offset
        squadFormationOffset = new Vector3(
            Random.Range(-squad.formationSpread, squad.formationSpread),
            0,
            Random.Range(-squad.formationSpread, squad.formationSpread)
        );
    }

    void OnEnable()
    {
        if (!allBots.Contains(this))
        {
            allBots.Add(this);
        }
    }

    void OnDisable()
    {
        allBots.Remove(this);
    }

    void Start()
    {
        // Get Opsive components the proper way
        characterLocomotion = GetComponent<UltimateCharacterLocomotion>();
        photonView = GetComponent<PhotonView>();

        if (characterLocomotion == null)
        {
            Debug.LogError($"Bot {gameObject.name} missing UltimateCharacterLocomotion!");
            enabled = false;
            return;
        }

        // Disable player input
        var unityInput = GetComponent("UnityInput") as MonoBehaviour;
        if (unityInput != null) unityInput.enabled = false;

        // Get the NavMeshAgentMovement ability (Opsive's AI movement system)
        navMeshMovement = characterLocomotion.GetAbility<NavMeshAgentMovement>();
        if (navMeshMovement == null)
        {
            Debug.LogError($"Bot {gameObject.name} missing NavMeshAgentMovement ability! Add it in the Inspector.");
            enabled = false;
            return;
        }

        // Make sure NavMeshAgentMovement is enabled
        if (!navMeshMovement.Enabled)
        {
            navMeshMovement.Enabled = true;
        }

        // Try to start the NavMeshAgentMovement ability
        characterLocomotion.TryStartAbility(navMeshMovement);

        // Get the Use ability for shooting
        useAbility = characterLocomotion.GetAbility<Use>();
        if (useAbility == null)
        {
            Debug.LogError($"Bot {gameObject.name} missing Use ability!");
        }

        // Get the Jump ability for unsticking
        jumpAbility = characterLocomotion.GetAbility<Jump>();
        // Jump is optional, no error if missing

        // Get LocalLookSource if it exists (optional for performance with many bots)
        lookSource = GetComponent<LocalLookSource>();
        // Don't auto-create to avoid scheduler overflow with many bots

        // Start patrol
        SetNewPatrolPoint();

        // Initialize stuck detection
        lastPosition = transform.position;
        lastMovementTime = Time.time;
        isStuck = false;

        // Initialize objective position
        objectiveHoldPosition = transform.position;
        nextObjectivePositionUpdate = 0f; // Update immediately on first frame
        nextFlagCheckTime = Time.time + Random.Range(0f, 0.5f); // Pick flag immediately (with slight randomization)

        // Register with SquadManager (if it exists) - but we're not using squads anymore
        if (SquadManager.Instance != null)
        {
            SquadManager.Instance.RegisterBot(this);
        }
    }

    void Update()
    {
        // Only run on Master Client
        if (!PhotonNetwork.IsMasterClient) return;
        if (characterLocomotion == null) return;

        // Don't do anything until team is assigned
        if (!teamAssigned)
        {
            return;
        }

        // Update targeting
        if (Time.time >= nextTargetUpdateTime)
        {
            FindBestTarget();
            nextTargetUpdateTime = Time.time + targetUpdateInterval;
        }

        // Update state
        UpdateState();

        // Check if stuck
        CheckIfStuck();

        // Check if out of bounds (fell through map)
        CheckOutOfBounds();

        // Execute state behavior
        switch (currentState)
        {
            case BotState.Patrol:
                PatrolBehavior();
                break;
            case BotState.Combat:
                CombatBehavior();
                break;
        }
    }

    void UpdateState()
    {
        if (currentTarget != null)
        {
            float distance = Vector3.Distance(transform.position, currentTarget.transform.position);

            if (distance <= detectionRange)
            {
                if (currentState != BotState.Combat)
                {
                    currentState = BotState.Combat;
                }
            }
            else
            {
                // Target too far, lose it
                currentTarget = null;
                currentState = BotState.Patrol;
            }
        }
        else
        {
            // No target, patrol
            if (currentState == BotState.Combat)
            {
                currentState = BotState.Patrol;
            }
        }
    }

    void FindBestTarget()
    {
        GameObject bestTarget = null;
        float closestDistance = detectionRange;

        // Raycast-based detection - cast rays in a cone pattern
        Vector3 startPos = transform.position + Vector3.up * 1.5f;
        Vector3 forward = transform.forward;

        // Calculate angle step between each ray
        float angleStep = detectionAngle / (detectionRayCount - 1);
        float startAngle = -detectionAngle / 2f;

        // Cast rays in a horizontal cone
        for (int i = 0; i < detectionRayCount; i++)
        {
            float currentAngle = startAngle + (angleStep * i);
            Quaternion rotation = Quaternion.Euler(0, currentAngle, 0);
            Vector3 direction = rotation * forward;

            RaycastHit hit;
            if (Physics.Raycast(startPos, direction, out hit, detectionRange, ~0, QueryTriggerInteraction.Ignore))
            {
                // Check if we hit a character
                var hitCharacter = hit.collider.GetComponentInParent<UltimateCharacterLocomotion>();
                if (hitCharacter == null || hitCharacter.gameObject == gameObject)
                    continue;

                // Get team
                int otherTeam = GetCharacterTeam(hitCharacter);

                // Skip if their team is unknown or same team
                if (otherTeam == -1 || otherTeam == botTeam)
                    continue;

                // Found an enemy, check if closest
                float distance = hit.distance;
                if (distance < closestDistance)
                {
                    closestDistance = distance;
                    bestTarget = hitCharacter.gameObject;
                }
            }
        }

        // If we didn't find anyone with the cone, check if we still have line of sight to current target
        if (bestTarget == null && currentTarget != null)
        {
            Vector3 targetPos = currentTarget.transform.position + Vector3.up * 1.5f;
            Vector3 directionToTarget = (targetPos - startPos).normalized;
            float distance = Vector3.Distance(startPos, targetPos);

            RaycastHit hit;
            if (Physics.Raycast(startPos, directionToTarget, out hit, distance, ~0, QueryTriggerInteraction.Ignore))
            {
                Transform hitRoot = hit.collider.transform.root;
                Transform targetRoot = currentTarget.transform.root;

                if (hitRoot == targetRoot && distance <= detectionRange)
                {
                    // Still have line of sight to current target
                    bestTarget = currentTarget;
                }
            }
        }

        currentTarget = bestTarget;
    }

    int GetCharacterTeam(UltimateCharacterLocomotion character)
    {
        // Check for OpsiveBotAI
        var opsiveBotAI = character.GetComponent<OpsiveBotAI>();
        if (opsiveBotAI != null)
        {
            return opsiveBotAI.BotTeam;
        }

        // Check for player
        var pv = character.GetComponent<PhotonView>();
        if (pv != null && pv.Owner != null)
        {
            int playerTeam = SimplePlayerSpawner.GetPlayerTeam(pv.Owner);
            return playerTeam;
        }

        return -1; // Unknown team
    }

    void PatrolBehavior()
    {
        // Stop shooting when patrolling
        StopShooting();

        // Clear look target
        if (lookSource != null)
        {
            lookSource.Target = null;
        }

        // CONQUEST MODE: Try to capture flags
        if (conquestMode)
        {
            ConquestPatrol();
        }
        else
        {
            // Regular patrol (random wandering)
            RegularPatrol();
        }
    }

    void ConquestPatrol()
    {
        // SIMPLIFIED: Individual bots pick and move to flags
        // Check for new flag target periodically
        if (Time.time >= nextFlagCheckTime)
        {
            FindFlagToCapture();
            nextFlagCheckTime = Time.time + 3f; // Re-evaluate every 3 seconds
        }

        // If we have a flag target, move to it
        if (targetFlag != null)
        {
            float distanceToFlag = Vector3.Distance(transform.position, targetFlag.transform.position);

            // DEBUG ONLY FOR TEAM 0
            if (botTeam == 0 && Time.frameCount % 180 == 0)
            {
                bool navActive = navMeshMovement != null && navMeshMovement.IsActive;
                Debug.LogError($"[TEAM 0 DEBUG] {gameObject.name}: Flag={targetFlag.name}, Dist={distanceToFlag:F1}, NavActive={navActive}, Moving={distanceToFlag > flagDefenseRange}");
            }

            if (distanceToFlag > flagDefenseRange)
            {
                // Move toward flag
                SetValidDestination(targetFlag.transform.position);
            }
            else
            {
                // At flag - pick a position around it and hold
                if (Time.time >= nextObjectivePositionUpdate)
                {
                    Vector2 randomOffset = Random.insideUnitCircle * 12f;
                    objectiveHoldPosition = targetFlag.transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);
                    nextObjectivePositionUpdate = Time.time + 5f;
                }
                SetValidDestination(objectiveHoldPosition);
            }
        }
        else
        {
            // No flag target, just patrol randomly
            RegularPatrol();
        }
    }

    void SquadTactics()
    {
        if (navMeshMovement == null || currentSquad == null) return;

        CapturePoint squadTarget = currentSquad.targetFlag;

        // DEBUG: Check if squad has a target
        if (squadTarget == null)
        {
            // Squad has no objective yet, just wait
            return;
        }

        Vector3 squadRallyPoint = currentSquad.rallyPoint;

        // Calculate my position in squad formation
        Vector3 formationPosition = squadRallyPoint + squadFormationOffset;
        float distanceToFormation = Vector3.Distance(transform.position, formationPosition);
        float distanceToFlag = Vector3.Distance(transform.position, squadTarget.transform.position);

        // Squad cohesion - stay with squad
        if (distanceToFormation > 20f)
        {
            // Too far from squad, move to formation
            SetValidDestination(formationPosition);
            return;
        }

        // At objective
        if (distanceToFlag < flagDefenseRange)
        {
            // Pick a new position to hold at objective (only update every 5 seconds)
            if (Time.time >= nextObjectivePositionUpdate)
            {
                switch (currentSquad.role)
                {
                    case BotSquad.SquadRole.Attack:
                    case BotSquad.SquadRole.Capture:
                        // Aggressive positioning - push into capture zone
                        Vector2 aggressiveOffset = Random.insideUnitCircle * 12f;
                        objectiveHoldPosition = squadTarget.transform.position + new Vector3(aggressiveOffset.x, 0, aggressiveOffset.y);
                        break;

                    case BotSquad.SquadRole.Defend:
                        // Defensive positioning - patrol perimeter
                        Vector2 perimeterOffset = Random.insideUnitCircle.normalized * flagDefenseRange * 0.7f;
                        objectiveHoldPosition = squadTarget.transform.position + new Vector3(perimeterOffset.x, 0, perimeterOffset.y);
                        break;

                    case BotSquad.SquadRole.Roaming:
                        // Spread out and cover more area
                        RegularPatrol();
                        return;
                }

                nextObjectivePositionUpdate = Time.time + 5f; // Update position every 5 seconds
            }

            // Move to (and hold) the objective position
            SetValidDestination(objectiveHoldPosition);
        }
        else
        {
            // Moving toward objective - maintain formation
            SetValidDestination(formationPosition);
        }
    }

    int CountTeammatesAtFlag(CapturePoint flag)
    {
        int count = 0;
        // Use cached bots list for better performance
        foreach (var bot in allBots)
        {
            if (bot.BotTeam == botTeam && bot != this)
            {
                float distance = Vector3.Distance(bot.transform.position, flag.transform.position);
                if (distance <= flagProximityCheck)
                    count++;
            }
        }
        return count;
    }

    void FindFlagToCapture()
    {
        // Use cached flags list for better performance
        var allFlags = CapturePoint.AllCapturePoints;
        if (allFlags.Count == 0)
        {
            targetFlag = null;
            return;
        }

        // Get all bots on my team from cached list
        var myTeamBots = new System.Collections.Generic.List<OpsiveBotAI>();
        foreach (var bot in allBots)
        {
            if (bot.BotTeam == botTeam)
                myTeamBots.Add(bot);
        }

        // Count flags owned by each team
        int ourFlags = 0;
        int enemyFlags = 0;
        int neutralFlags = 0;
        foreach (var flag in allFlags)
        {
            if (flag.OwningTeam == botTeam) ourFlags++;
            else if (flag.OwningTeam == -1) neutralFlags++;
            else enemyFlags++;
        }

        // Strategic mode: Are we winning or losing?
        bool losing = ourFlags < enemyFlags;
        bool winning = ourFlags > enemyFlags;

        CapturePoint bestFlag = null;
        float bestPriority = -1000f;

        foreach (var flag in allFlags)
        {
            float distance = Vector3.Distance(transform.position, flag.transform.position);

            // Count teammates at this flag
            int teammatesAtFlag = 0;
            int enemiesAtFlag = 0;

            foreach (var bot in allBots)
            {
                float botDistance = Vector3.Distance(bot.transform.position, flag.transform.position);
                if (botDistance <= flagProximityCheck)
                {
                    if (bot.BotTeam == botTeam)
                    {
                        teammatesAtFlag++;
                    }
                    else if (bot.BotTeam != -1 && bot.teamAssigned)
                    {
                        // Only count as enemy if they have a valid team assignment
                        enemiesAtFlag++;
                    }
                    // Ignore bots with BotTeam == -1 (uninitialized)
                }
            }

            // SMART PRIORITY SYSTEM
            float priority = 0f;

            if (flag.OwningTeam == botTeam)
            {
                // OUR FLAG
                if (enemiesAtFlag > 0)
                {
                    // Under attack! Defend it!
                    priority = 100f - distance * 0.5f; // High priority, closer = better
                }
                else if (teammatesAtFlag >= maxDefendersPerFlag)
                {
                    // Already enough defenders, skip it
                    priority = -1000f;
                }
                else
                {
                    // Need a defender
                    priority = 20f - distance * 0.2f;

                    // If winning, boost defense priority
                    if (winning)
                        priority += 20f;
                }
            }
            else if (flag.OwningTeam == -1)
            {
                // NEUTRAL FLAG - capture it!
                if (teammatesAtFlag >= 5)
                {
                    // Already plenty capturing this one
                    priority = -1000f;
                }
                else
                {
                    // Go capture! Higher priority if closer
                    priority = 70f - distance * 0.3f;
                    if (teammatesAtFlag > 0)
                    {
                        // Someone's already there, help them
                        priority += 10f;
                    }

                    // GAME START: If most flags are neutral, EVERYONE captures!
                    if (neutralFlags >= allFlags.Count - 1)
                    {
                        priority = 150f - distance * 0.2f; // SUPER HIGH priority
                    }
                    // If losing, neutral flags are super important!
                    else if (losing)
                    {
                        priority += 25f;
                    }
                }
            }
            else
            {
                // ENEMY FLAG - attack it!
                if (teammatesAtFlag >= 3)
                {
                    // Already enough attackers
                    priority = -1000f;
                }
                else
                {
                    // Attack! Very high priority
                    priority = 90f - distance * 0.4f;

                    // Bonus if teammates are there (coordinate attack)
                    priority += teammatesAtFlag * 15f;

                    // Penalty if too many enemies (avoid suicide)
                    if (enemiesAtFlag > teammatesAtFlag + 2)
                    {
                        priority -= 30f;
                    }

                    // If losing badly, MUST attack enemy flags!
                    if (losing)
                        priority += 30f;
                }
            }

            // Distance penalty (prefer closer flags)
            priority -= distance * 0.1f;

            // Add small random factor so not all bots pick same flag
            priority += Random.Range(-5f, 5f);

            if (priority > bestPriority)
            {
                bestPriority = priority;
                bestFlag = flag;
            }
        }

        // Only change target if significantly better
        if (bestFlag != targetFlag)
        {
            if (bestFlag != null && bestPriority > 0)
            {
                targetFlag = bestFlag;
            }
            else if (bestPriority <= 0)
            {
                // No good flags, just roam
                targetFlag = null;
            }
        }
    }

    void RegularPatrol()
    {
        // Pick new patrol point periodically
        if (Time.time >= nextPatrolTime)
        {
            SetNewPatrolPoint();
            nextPatrolTime = Time.time + patrolInterval;
        }

        // Move to patrol point using Opsive's NavMesh system
        if (navMeshMovement != null)
        {
            SetValidDestination(patrolDestination);
        }
    }

    void CombatBehavior()
    {
        if (currentTarget == null)
        {
            StopShooting();
            return;
        }

        Vector3 targetPos = currentTarget.transform.position;
        float distance = Vector3.Distance(transform.position, targetPos);

        // THE OPSIVE WAY: Set LocalLookSource.Target for aiming
        // Opsive handles all rotation and aiming automatically!
        if (lookSource != null)
        {
            lookSource.Target = currentTarget.transform;
        }

        // Move to engagement range using Opsive's NavMesh system
        if (navMeshMovement != null)
        {
            if (distance > engagementRange)
            {
                // Move closer
                SetValidDestination(targetPos);
            }
            else if (distance < engagementRange * 0.7f)
            {
                // Too close, back up a bit
                Vector3 retreatPos = transform.position + (transform.position - targetPos).normalized * 5f;
                SetValidDestination(retreatPos);
            }
            else
            {
                // Good range, stop moving
                SetValidDestination(transform.position);
            }
        }

        // Burst fire shooting
        if (distance <= engagementRange * 1.5f)
        {
            CombatShooting();
        }
        else
        {
            StopShooting();
        }
    }

    void CombatShooting()
    {
        // Burst fire control
        if (Time.time >= nextBurstTime && !inBurst)
        {
            StartBurst();
        }

        if (inBurst)
        {
            TryShoot();

            if (Time.time >= burstEndTime)
            {
                EndBurst();
            }
        }
    }

    void StartBurst()
    {
        inBurst = true;
        burstEndTime = Time.time + burstDuration;
    }

    void EndBurst()
    {
        inBurst = false;
        nextBurstTime = Time.time + burstCooldown;
        StopShooting();
    }

    void SetNewPatrolPoint()
    {
        Vector2 randomCircle = Random.insideUnitCircle * patrolRadius;
        patrolDestination = transform.position + new Vector3(randomCircle.x, 0, randomCircle.y);
    }

    void TryShoot()
    {
        if (useAbility == null) return;

        // The Opsive way: Use TryStartAbility
        if (!useAbility.IsActive)
        {
            characterLocomotion.TryStartAbility(useAbility);
        }
    }

    void StopShooting()
    {
        if (useAbility == null) return;

        if (useAbility.IsActive)
        {
            characterLocomotion.TryStopAbility(useAbility);
        }
    }

    void CheckIfStuck()
    {
        // SMART STUCK DETECTION: Don't flag as stuck when bot is legitimately stationary

        bool shouldBeStationary = false;

        // 1. In combat and at good engagement range
        if (currentState == BotState.Combat && currentTarget != null)
        {
            float distanceToTarget = Vector3.Distance(transform.position, currentTarget.transform.position);
            if (distanceToTarget >= engagementRange * 0.7f && distanceToTarget <= engagementRange * 1.2f)
            {
                shouldBeStationary = true; // At good firing position
            }
        }

        // 2. At a flag defending/capturing
        if (targetFlag != null)
        {
            float distanceToFlag = Vector3.Distance(transform.position, targetFlag.transform.position);
            if (distanceToFlag < flagDefenseRange)
            {
                shouldBeStationary = true; // At objective
            }
        }

        // 3. In patrol state and at patrol destination
        if (currentState == BotState.Patrol && targetFlag == null)
        {
            float distanceToPatrol = Vector3.Distance(transform.position, patrolDestination);
            if (distanceToPatrol < 2f)
            {
                shouldBeStationary = true; // Waiting at patrol point
            }
        }

        // If bot should be stationary, don't check for stuck
        if (shouldBeStationary)
        {
            lastMovementTime = Time.time;
            lastPosition = transform.position;
            isStuck = false;
            return;
        }

        // Check if bot has moved enough
        float distanceMoved = Vector3.Distance(transform.position, lastPosition);

        if (distanceMoved > stuckDistanceThreshold)
        {
            // Bot is moving, reset stuck timer
            lastMovementTime = Time.time;
            lastPosition = transform.position;
            isStuck = false;

            // If bot has been moving successfully for 5 seconds, reset unstuck attempts
            if (Time.time - lastUnstuckTime > 5f)
            {
                unstuckAttempts = 0;
            }
        }
        else
        {
            // Bot hasn't moved much
            if (Time.time - lastMovementTime > stuckTimeout)
            {
                // Bot is stuck!
                if (!isStuck)
                {
                    isStuck = true;
                    UnstickBot();
                }
            }
        }
    }

    void CheckOutOfBounds()
    {
        // If bot falls below the map, respawn it
        if (transform.position.y < outOfBoundsY)
        {
            // Find a valid spawn point based on team
            Vector3 respawnPos = Vector3.zero;

            // Try to find a spawn point
            GameObject[] spawnPoints = GameObject.FindGameObjectsWithTag("Respawn");
            if (spawnPoints != null && spawnPoints.Length > 0)
            {
                // Pick random spawn point
                Transform spawn = spawnPoints[Random.Range(0, spawnPoints.Length)].transform;
                respawnPos = spawn.position;
            }
            else
            {
                // No spawn points, just move up to a safe height
                respawnPos = new Vector3(transform.position.x, 10f, transform.position.z);
            }

            // Teleport bot to spawn
            characterLocomotion.SetPosition(respawnPos);

            // Reset stuck detection
            lastMovementTime = Time.time;
            lastPosition = respawnPos;
            isStuck = false;
            unstuckAttempts = 0;
        }
    }

    void UnstickBot()
    {
        unstuckAttempts++;

        Vector3 unstuckDestination = transform.position;
        bool tryJump = false;

        // Escalating recovery strategies based on attempts
        if (unstuckAttempts == 1)
        {
            // Strategy 1: Try jumping forward with momentum
            if (jumpAbility != null)
            {
                tryJump = true;
                unstuckDestination = transform.position + transform.forward * 2f;
            }
            else
            {
                // No jump ability, move backward
                unstuckDestination = transform.position - transform.forward * 3f;
            }
        }
        else if (unstuckAttempts == 2)
        {
            // Strategy 2: Jump to the side
            Vector3 sideDirection = Random.value > 0.5f ? transform.right : -transform.right;
            if (jumpAbility != null)
            {
                tryJump = true;
                unstuckDestination = transform.position + sideDirection * 3f;
            }
            else
            {
                unstuckDestination = transform.position + sideDirection * 4f;
            }
        }
        else if (unstuckAttempts == 3)
        {
            // Strategy 3: Move backward
            unstuckDestination = transform.position - transform.forward * 4f;
        }
        else if (unstuckAttempts >= 4)
        {
            // Strategy 4: Pick a random distant point (teleport)
            Vector2 randomOffset = Random.insideUnitCircle * unstuckDistance;
            unstuckDestination = transform.position + new Vector3(randomOffset.x, 0, randomOffset.y);

            // Reset attempts after trying random teleport
            if (unstuckAttempts >= 5)
            {
                unstuckAttempts = 0;
            }
        }

        // Find nearest valid NavMesh position
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(unstuckDestination, out hit, 25f, UnityEngine.AI.NavMesh.AllAreas))
        {
            unstuckDestination = hit.position;

            // For aggressive unsticking, directly move the character if very stuck
            if (unstuckAttempts >= 4)
            {
                characterLocomotion.SetPosition(unstuckDestination);
            }
            else if (navMeshMovement != null)
            {
                navMeshMovement.SetDestination(unstuckDestination);

                // Try jumping if this strategy requires it
                if (tryJump && jumpAbility != null)
                {
                    // Give the bot a moment to start moving, then jump
                    Invoke("TryJump", 0.2f);
                }
            }
        }
        else
        {
            // Couldn't find valid position, try moving backward
            Vector3 backupPos = transform.position - transform.forward * 2f;
            if (UnityEngine.AI.NavMesh.SamplePosition(backupPos, out hit, 10f, UnityEngine.AI.NavMesh.AllAreas))
            {
                navMeshMovement.SetDestination(hit.position);
            }
        }

        // Clear current target to avoid pathing to same stuck location
        if (unstuckAttempts >= 2)
        {
            currentTarget = null;
        }

        // Force re-evaluation of flag target
        if (unstuckAttempts >= 3)
        {
            targetFlag = null;
            nextFlagCheckTime = Time.time + 0.5f; // Pick new flag soon
        }

        // Reset timers
        nextPatrolTime = Time.time + 1f;
        lastUnstuckTime = Time.time;

        // Reset stuck detection
        lastMovementTime = Time.time;
        lastPosition = transform.position;
        isStuck = false;
    }

    void TryJump()
    {
        // Attempt to jump to clear obstacle
        if (jumpAbility != null && characterLocomotion != null)
        {
            // Make sure bot is grounded before jumping
            if (characterLocomotion.Grounded)
            {
                characterLocomotion.TryStartAbility(jumpAbility);
            }
        }
    }

    void SetValidDestination(Vector3 desiredDestination)
    {
        if (navMeshMovement == null)
        {
            return;
        }

        // CRITICAL: Make sure NavMeshAgentMovement ability is ACTIVE
        if (!navMeshMovement.IsActive)
        {
            characterLocomotion.TryStartAbility(navMeshMovement);
        }

        // Check if destination is on NavMesh (larger radius for better results)
        UnityEngine.AI.NavMeshHit hit;
        if (UnityEngine.AI.NavMesh.SamplePosition(desiredDestination, out hit, 20f, UnityEngine.AI.NavMesh.AllAreas))
        {
            Vector3 validDestination = hit.position;

            // Check if there's a direct obstacle in the way
            Vector3 directionToDestination = (validDestination - transform.position).normalized;
            RaycastHit obstacleHit;

            // Check at foot level for rocks and low obstacles
            Vector3 footPos = transform.position + Vector3.up * 0.3f;
            if (Physics.Raycast(footPos, directionToDestination, out obstacleHit, obstacleCheckDistance, visionBlockingLayers))
            {
                // There's an obstacle, check if it's not a character
                if (obstacleHit.collider.GetComponentInParent<UltimateCharacterLocomotion>() == null)
                {
                    // It's a rock or wall - try to find an alternative path or don't move
                    // Let NavMesh handle pathing around it
                }
            }

            // Use the corrected position on NavMesh
            navMeshMovement.SetDestination(validDestination);
        }
        else
        {
            // Destination not valid, stay at current position
            navMeshMovement.SetDestination(transform.position);
        }
    }

    void OnDrawGizmos()
    {
        // Draw detection range
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRange);

        // Draw engagement range
        Gizmos.color = Color.green;
        Gizmos.DrawWireSphere(transform.position, engagementRange);

        // Draw current target
        if (currentTarget != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawLine(transform.position + Vector3.up, currentTarget.transform.position + Vector3.up);
        }

        // Draw patrol destination
        if (Application.isPlaying)
        {
            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(patrolDestination, 1f);
        }
    }
}
