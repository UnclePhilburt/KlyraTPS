using UnityEngine;
using Photon.Pun;
using Photon.Realtime;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Team-based spawner for Conquest mode
/// Assigns players to teams and spawns them at team spawn points
/// </summary>
public class SimplePlayerSpawner : MonoBehaviourPunCallbacks
{
    [Header("Character Settings")]
    [Tooltip("Name of the character prefab in Resources folder (without .prefab extension)")]
    public string characterPrefabName = "Soldier1";

    [Header("Team Spawn Points")]
    [Tooltip("Spawn points for US Army (Team A)")]
    public Transform[] teamASpawnPoints;

    [Tooltip("Spawn points for Insurgents (Team B)")]
    public Transform[] teamBSpawnPoints;

    [Header("Team Settings")]
    [Tooltip("Auto-balance teams when players join")]
    public bool autoBalance = true;

    private bool hasSpawned = false;

    // Team assignment (stored in player custom properties)
    private const string TEAM_KEY = "team";
    private const int TEAM_A = 0;
    private const int TEAM_B = 1;

    // Called when successfully joined a room
    public override void OnJoinedRoom()
    {
        // Don't spawn a player if this is a dedicated server
        if (DedicatedServerManager.IsServerBuild)
        {
            Debug.Log("Dedicated server - not spawning player character");
            return;
        }

        // Assign player to a team
        AssignTeam();

        // Spawn after team assignment
        SpawnPlayer();
    }

    void AssignTeam()
    {
        int myTeam = TEAM_A; // Default

        if (autoBalance)
        {
            // Count players on each team
            int teamACount = 0;
            int teamBCount = 0;

            foreach (Player player in PhotonNetwork.PlayerList)
            {
                if (player.CustomProperties.ContainsKey(TEAM_KEY))
                {
                    int team = (int)player.CustomProperties[TEAM_KEY];
                    if (team == TEAM_A)
                        teamACount++;
                    else
                        teamBCount++;
                }
            }

            // Assign to smaller team
            myTeam = (teamACount <= teamBCount) ? TEAM_A : TEAM_B;
        }

        // Store team in player properties
        ExitGames.Client.Photon.Hashtable props = new ExitGames.Client.Photon.Hashtable
        {
            { TEAM_KEY, myTeam }
        };
        PhotonNetwork.LocalPlayer.SetCustomProperties(props);
    }

    void SpawnPlayer()
    {
        // Only spawn once
        if (hasSpawned)
        {
            Debug.LogWarning("Already spawned a character!");
            return;
        }

        // Get player's team
        int team = TEAM_A;
        if (PhotonNetwork.LocalPlayer.CustomProperties.ContainsKey(TEAM_KEY))
        {
            team = (int)PhotonNetwork.LocalPlayer.CustomProperties[TEAM_KEY];
        }

        // Get appropriate spawn points for this team
        Transform[] spawnPoints = (team == TEAM_A) ? teamASpawnPoints : teamBSpawnPoints;

        // Choose random spawn point
        Vector3 spawnPosition = Vector3.zero;
        Quaternion spawnRotation = Quaternion.identity;

        if (spawnPoints != null && spawnPoints.Length > 0)
        {
            // Pick random spawn from the team's spawn points
            Transform chosenSpawn = spawnPoints[Random.Range(0, spawnPoints.Length)];
            spawnPosition = chosenSpawn.position;
            spawnRotation = chosenSpawn.rotation;
        }
        else
        {
            Debug.LogWarning($"No spawn points set for team {team}! Using default position.");
            spawnPosition = new Vector3(team == TEAM_A ? -10 : 10, 2, 0);
        }

        // Spawn the character over the network
        GameObject player = PhotonNetwork.Instantiate(
            characterPrefabName,
            spawnPosition,
            spawnRotation
        );

        if (player != null)
        {
            hasSpawned = true;
        }
        else
        {
            Debug.LogError($"<color=red>FAILED to spawn! Make sure '{characterPrefabName}' exists in Assets/Resources/ folder!</color>");
        }
    }

    // Helper method to get player's team (can be called from other scripts)
    public static int GetPlayerTeam(Player player)
    {
        if (player.CustomProperties.ContainsKey(TEAM_KEY))
        {
            return (int)player.CustomProperties[TEAM_KEY];
        }
        return TEAM_A; // Default
    }
}
