using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;

/// <summary>
/// Spawns AI bots on the Master Client
/// Fills the server to a target player count
/// </summary>
public class BotSpawnManager : MonoBehaviourPunCallbacks
{
    [Header("Bot Prefabs")]
    [Tooltip("Bot character prefabs for US Army (Team A) - randomly selected")]
    public GameObject[] usArmyBotPrefabs;

    [Tooltip("Bot character prefabs for Insurgents (Team B) - randomly selected")]
    public GameObject[] insurgentBotPrefabs;

    [Header("Bot Settings")]
    [Tooltip("Total combatants (players + bots)")]
    public int targetCombatants = 24;

    [Tooltip("Spawn points for US Army (Team A)")]
    public Transform[] usArmySpawnPoints;

    [Tooltip("Spawn points for Insurgents (Team B)")]
    public Transform[] insurgentSpawnPoints;

    [Header("Spawn Timing")]
    [Tooltip("Delay before spawning bots (seconds)")]
    public float initialSpawnDelay = 2f;

    [Tooltip("Delay between each bot spawn (seconds)")]
    public float spawnInterval = 1f;

    [Tooltip("How often to check if we need more bots")]
    public float spawnCheckInterval = 5f;

    private List<GameObject> spawnedBots = new List<GameObject>();
    private float nextSpawnTime;
    private float nextSpawnCheckTime;
    private bool hasInitialSpawn = false;
    private int botsToSpawn = 0;

    void Start()
    {
        nextSpawnCheckTime = Time.time + initialSpawnDelay;
        nextSpawnTime = Time.time + initialSpawnDelay;
    }

    void Update()
    {
        // Only spawn bots on dedicated server OR if no dedicated server exists
        // This prevents players from spawning bots when a dedicated server is running
        bool shouldSpawnBots = PhotonNetwork.IsMasterClient;

        // If dedicated server exists and we're not it, don't spawn bots
        if (DedicatedServerManager.Instance != null && !DedicatedServerManager.IsServerBuild)
        {
            shouldSpawnBots = false;
        }

        if (!shouldSpawnBots)
            return;

        // Check if we need to spawn more bots
        if (Time.time >= nextSpawnCheckTime)
        {
            CheckAndSpawnBots();
            nextSpawnCheckTime = Time.time + spawnCheckInterval;
        }

        // Spawn one bot at a time with delay
        if (botsToSpawn > 0 && Time.time >= nextSpawnTime)
        {
            SpawnBot();
            botsToSpawn--;
            nextSpawnTime = Time.time + spawnInterval;
            Debug.Log($"Spawned bot. {botsToSpawn} remaining. Next spawn in {spawnInterval}s");
        }
    }

    void CheckAndSpawnBots()
    {
        // Clean up null references
        spawnedBots.RemoveAll(bot => bot == null);

        // Calculate how many bots we need
        int playerCount = PhotonNetwork.PlayerList.Length;
        int currentBotCount = spawnedBots.Count;
        int neededBots = targetCombatants - playerCount - currentBotCount;

        // Queue bots to spawn (they'll spawn one at a time in Update)
        if (neededBots > 0)
        {
            botsToSpawn += neededBots;
            // Start spawning immediately if not already spawning
            if (nextSpawnTime < Time.time)
            {
                nextSpawnTime = Time.time;
            }
        }

        hasInitialSpawn = true;
    }

    void SpawnBot()
    {
        // Alternate teams to keep balance
        int team = (spawnedBots.Count % 2 == 0) ? 0 : 1; // 0 = US Army, 1 = Insurgents

        // Get spawn points for this team
        Transform[] spawnPoints = (team == 0) ? usArmySpawnPoints : insurgentSpawnPoints;

        if (spawnPoints == null || spawnPoints.Length == 0)
        {
            Debug.LogWarning($"No spawn points configured for {(team == 0 ? "US Army" : "Insurgents")}!");
            return;
        }

        // Get prefab array for this team
        GameObject[] botPrefabs = (team == 0) ? usArmyBotPrefabs : insurgentBotPrefabs;

        if (botPrefabs == null || botPrefabs.Length == 0)
        {
            Debug.LogWarning($"No bot prefabs configured for {(team == 0 ? "US Army" : "Insurgents")}!");
            return;
        }

        // Pick random spawn point
        Transform spawnPoint = spawnPoints[Random.Range(0, spawnPoints.Length)];

        // Add random offset around spawn point
        Vector2 randomOffset = Random.insideUnitCircle * 3f; // 3 meter radius
        Vector3 spawnPosition = spawnPoint.position + new Vector3(randomOffset.x, 0, randomOffset.y);

        // Pick random bot prefab from this team's prefabs
        GameObject randomPrefab = botPrefabs[Random.Range(0, botPrefabs.Length)];
        string prefabName = randomPrefab.name;

        // Create bot name
        string botName = $"Bot_{team}_{spawnedBots.Count}";

        // Spawn the bot over network
        GameObject bot = PhotonNetwork.Instantiate(
            prefabName,
            spawnPosition,
            spawnPoint.rotation
        );

        if (bot != null)
        {
            // Add weapon synchronization component FIRST
            bot.AddComponent<BotWeaponSync>();

            // Add combat AI to all bots
            var combat = bot.AddComponent<BotCombat>();
            combat.SetTeam(team);

            // Randomly assign bot behavior: 60% attackers, 30% defenders, 10% patrollers
            float roll = Random.value;

            if (roll < 0.6f)
            {
                // Attacker - captures neutral and enemy flags
                var attacker = bot.AddComponent<BotAttacker>();
                attacker.SetTeam(team);
            }
            else if (roll < 0.9f)
            {
                // Defender - defends friendly flags
                var defender = bot.AddComponent<BotDefender>();
                defender.SetTeam(team);
            }
            else
            {
                // Patroller - roams near flags
                var patroller = bot.AddComponent<BotPatroller>();
                patroller.SetTeam(team);
            }

            // IMPORTANT: Transfer ownership to Master Client to prevent auto-cleanup
            var photonView = bot.GetComponent<PhotonView>();
            if (photonView != null && PhotonNetwork.IsMasterClient)
            {
                photonView.TransferOwnership(PhotonNetwork.MasterClient);
            }

            spawnedBots.Add(bot);
        }
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        // When Master Client changes, new MC takes control of bots
        if (PhotonNetwork.IsMasterClient)
        {
            // Take ownership of all bots
            foreach (var bot in spawnedBots)
            {
                if (bot != null)
                {
                    var photonView = bot.GetComponent<PhotonView>();
                    if (photonView != null)
                    {
                        photonView.TransferOwnership(PhotonNetwork.MasterClient);
                    }
                }
            }

            // Check if we need to spawn more
            nextSpawnCheckTime = Time.time + 2f;
        }
    }

    public override void OnPlayerEnteredRoom(Player newPlayer)
    {
        // When a real player joins, we may need fewer bots
        if (PhotonNetwork.IsMasterClient)
        {
            nextSpawnCheckTime = Time.time + 1f; // Check soon
        }
    }

    public override void OnPlayerLeftRoom(Player otherPlayer)
    {
        // When a player leaves, we may need more bots
        if (PhotonNetwork.IsMasterClient)
        {
            nextSpawnCheckTime = Time.time + 1f; // Check soon
        }
    }
}
