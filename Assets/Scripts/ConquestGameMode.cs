using UnityEngine;
using Photon.Pun;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Manages Conquest game mode (Battlefield-style)
/// Teams lose tickets based on flag control and deaths
/// First team to 0 tickets loses
/// Can spawn on controlled flags
/// </summary>
public class ConquestGameMode : MonoBehaviourPunCallbacks, IPunObservable
{
    [Header("Conquest Settings")]
    [Tooltip("Starting tickets for each team")]
    public int startingTickets = 200;

    [Tooltip("How often to drain tickets (seconds)")]
    public float ticketDrainInterval = 2f;

    [Tooltip("Ticket drain rate when losing flag majority")]
    public int ticketDrainRate = 1;

    [Header("Capture Points")]
    [Tooltip("All capture points in the map")]
    public CapturePoint[] capturePoints;

    // Tickets
    private int usArmyTickets = 200;
    private int insurgentTickets = 200;
    private float nextTicketDrain;

    // Game state
    private bool gameActive = true;
    private int winningTeam = -1;

    // Public properties
    public int USArmyTickets { get { return usArmyTickets; } }
    public int InsurgentTickets { get { return insurgentTickets; } }
    public bool GameActive { get { return gameActive; } }
    public int WinningTeam { get { return winningTeam; } }

    void Start()
    {
        if (capturePoints == null || capturePoints.Length == 0)
        {
            Debug.LogWarning("No capture points assigned to ConquestGameMode!");
        }

        usArmyTickets = startingTickets;
        insurgentTickets = startingTickets;
        nextTicketDrain = Time.time + ticketDrainInterval;
    }

    void Update()
    {
        // Only Master Client runs game logic
        if (!PhotonNetwork.IsMasterClient) return;
        if (!gameActive) return;

        // Ticket drain
        if (Time.time >= nextTicketDrain)
        {
            DrainTickets();
            nextTicketDrain = Time.time + ticketDrainInterval;
        }

        // Check win condition
        if (usArmyTickets <= 0)
        {
            EndGame(1); // Insurgents win
        }
        else if (insurgentTickets <= 0)
        {
            EndGame(0); // US Army wins
        }
    }

    void DrainTickets()
    {
        if (capturePoints == null) return;

        int usArmyFlags = 0;
        int insurgentFlags = 0;
        int totalFlags = capturePoints.Length;

        // Count flags
        foreach (var point in capturePoints)
        {
            if (point.OwningTeam == 0)
            {
                usArmyFlags++;
                Debug.Log($"Flag {point.PointName} owned by US Army (team 0)");
            }
            else if (point.OwningTeam == 1)
            {
                insurgentFlags++;
                Debug.Log($"Flag {point.PointName} owned by Insurgents (team 1)");
            }
            else
            {
                Debug.Log($"Flag {point.PointName} is neutral (team {point.OwningTeam})");
            }
        }

        Debug.Log($"FLAG COUNT: US Army={usArmyFlags}, Insurgents={insurgentFlags} | TICKETS: US Army={usArmyTickets}, Insurgents={insurgentTickets}");

        // Team with fewer flags loses tickets
        // If tied, no ticket drain
        if (usArmyFlags < insurgentFlags)
        {
            // US Army losing, drain their tickets
            usArmyTickets -= ticketDrainRate;
            usArmyTickets = Mathf.Max(0, usArmyTickets);
            Debug.Log($"US Army has fewer flags ({usArmyFlags} vs {insurgentFlags}) - draining US Army tickets to {usArmyTickets}");
        }
        else if (insurgentFlags < usArmyFlags)
        {
            // Insurgents losing, drain their tickets
            insurgentTickets -= ticketDrainRate;
            insurgentTickets = Mathf.Max(0, insurgentTickets);
            Debug.Log($"Insurgents have fewer flags ({insurgentFlags} vs {usArmyFlags}) - draining Insurgent tickets to {insurgentTickets}");
        }
        else
        {
            Debug.Log($"Flags are tied ({usArmyFlags} vs {insurgentFlags}) - no ticket drain");
        }
    }

    // Called when a player dies
    public void OnPlayerDeath(int team)
    {
        if (!PhotonNetwork.IsMasterClient) return;
        if (!gameActive) return;

        // Lose 1 ticket on death
        if (team == 0)
        {
            usArmyTickets--;
            usArmyTickets = Mathf.Max(0, usArmyTickets);
        }
        else if (team == 1)
        {
            insurgentTickets--;
            insurgentTickets = Mathf.Max(0, insurgentTickets);
        }
    }

    void EndGame(int team)
    {
        gameActive = false;
        winningTeam = team;

        // Call RPC to notify all clients
        photonView.RPC("RPC_GameOver", RpcTarget.All, team);
    }

    [PunRPC]
    void RPC_GameOver(int team)
    {
        gameActive = false;
        winningTeam = team;
    }

    // Photon serialization
    public void OnPhotonSerializeView(PhotonStream stream, PhotonMessageInfo info)
    {
        if (stream.IsWriting)
        {
            // Master Client sends tickets
            stream.SendNext(usArmyTickets);
            stream.SendNext(insurgentTickets);
            stream.SendNext(gameActive);
            stream.SendNext(winningTeam);
        }
        else
        {
            // Clients receive tickets
            usArmyTickets = (int)stream.ReceiveNext();
            insurgentTickets = (int)stream.ReceiveNext();
            gameActive = (bool)stream.ReceiveNext();
            winningTeam = (int)stream.ReceiveNext();
        }
    }

    // Helper to get tickets for a team
    public int GetTeamTickets(int team)
    {
        if (team == 0) return usArmyTickets;
        if (team == 1) return insurgentTickets;
        return 0;
    }

    // Helper to get how many flags a team owns
    public int GetTeamFlagCount(int team)
    {
        if (capturePoints == null) return 0;

        int count = 0;
        foreach (var point in capturePoints)
        {
            if (point.OwningTeam == team)
                count++;
        }
        return count;
    }

    // Get a random controlled spawn point for a team
    public Vector3? GetTeamSpawnPoint(int team)
    {
        if (capturePoints == null || capturePoints.Length == 0)
            return null;

        // Get all flags controlled by this team
        var controlledFlags = new List<CapturePoint>();
        foreach (var point in capturePoints)
        {
            if (point.OwningTeam == team)
                controlledFlags.Add(point);
        }

        if (controlledFlags.Count == 0)
            return null;

        // Pick random flag
        var flag = controlledFlags[Random.Range(0, controlledFlags.Count)];

        // Spawn near the flag (random offset)
        Vector2 randomOffset = Random.insideUnitCircle * 8f;
        return flag.transform.position + new Vector3(randomOffset.x, 2f, randomOffset.y);
    }
}
