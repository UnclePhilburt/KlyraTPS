using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using Photon.Pun;

/// <summary>
/// Manages all bot squads for tactical coordination
/// Automatically creates, assigns, and coordinates squads
/// </summary>
public class SquadManager : MonoBehaviour
{
    private static SquadManager instance;
    public static SquadManager Instance { get { return instance; } }

    // Squads by team
    private Dictionary<int, List<BotSquad>> teamSquads = new Dictionary<int, List<BotSquad>>();
    private int nextSquadId = 0;

    // Update intervals
    public float squadUpdateInterval = 3f;
    private float nextSquadUpdate;

    // Cache all bots to avoid FindObjectsOfType spam
    private List<OpsiveBotAI> allKnownBots = new List<OpsiveBotAI>();
    private float nextBotCacheUpdate = 0f;
    private float botCacheInterval = 5f;

    void Awake()
    {
        if (instance == null)
        {
            instance = this;
            teamSquads[0] = new List<BotSquad>();
            teamSquads[1] = new List<BotSquad>();
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Update()
    {
        // Only Master Client manages squads
        if (!PhotonNetwork.IsMasterClient) return;

        if (Time.time >= nextSquadUpdate)
        {
            UpdateAllSquads();
            nextSquadUpdate = Time.time + squadUpdateInterval;
        }
    }

    void UpdateAllSquads()
    {
        // Refresh bot cache periodically (less often than squad updates)
        if (Time.time >= nextBotCacheUpdate)
        {
            RefreshBotCache();
            nextBotCacheUpdate = Time.time + botCacheInterval;
        }

        // Update both teams
        UpdateTeamSquads(0);
        UpdateTeamSquads(1);
    }

    void RefreshBotCache()
    {
        // Update our cached list of bots
        allKnownBots.Clear();
        var foundBots = FindObjectsOfType<OpsiveBotAI>();
        allKnownBots.AddRange(foundBots);
    }

    void UpdateTeamSquads(int team)
    {
        // Get all bots on this team from cache (much faster than FindObjectsOfType!)
        var teamBots = allKnownBots.Where(b => b != null && b.BotTeam == team && b.teamAssigned).ToList();

        // Clean up dead squads
        teamSquads[team].RemoveAll(s => !s.IsActive());

        // Get unassigned bots
        var unassignedBots = teamBots.Where(b => b.currentSquad == null).ToList();

        // Try to fill existing squads
        foreach (var squad in teamSquads[team])
        {
            if (squad.CanAcceptMembers() && unassignedBots.Count > 0)
            {
                int needed = squad.GetIdealSize() - squad.members.Count;
                for (int i = 0; i < needed && unassignedBots.Count > 0; i++)
                {
                    var bot = unassignedBots[0];
                    AssignBotToSquad(bot, squad);
                    unassignedBots.RemoveAt(0);
                }
            }
        }

        // Create new squads from remaining unassigned bots
        while (unassignedBots.Count >= BotSquad.MIN_SQUAD_SIZE)
        {
            CreateNewSquad(team, unassignedBots);
        }

        // Assign objectives to squads
        AssignSquadObjectives(team);
    }

    void CreateNewSquad(int team, List<OpsiveBotAI> unassignedBots)
    {
        var squad = new BotSquad(nextSquadId++, team);

        // Determine squad role based on current situation
        squad.role = DetermineSquadRole(team);

        // Add members
        int squadSize = Mathf.Min(squad.GetIdealSize(), unassignedBots.Count);
        for (int i = 0; i < squadSize; i++)
        {
            AssignBotToSquad(unassignedBots[0], squad);
            unassignedBots.RemoveAt(0);
        }

        teamSquads[team].Add(squad);

        // NOTE: Don't update rally point here - targetFlag is not assigned yet!
        // Rally point will be updated in AssignSquadObjectives after flag assignment
    }

    BotSquad.SquadRole DetermineSquadRole(int team)
    {
        var flags = FindObjectsOfType<CapturePoint>();
        int ourFlags = flags.Count(f => f.OwningTeam == team);
        int enemyFlags = flags.Count(f => f.OwningTeam != team && f.OwningTeam != -1);
        int neutralFlags = flags.Count(f => f.OwningTeam == -1);

        var existingSquads = teamSquads[team];
        int attackSquads = existingSquads.Count(s => s.role == BotSquad.SquadRole.Attack);
        int defendSquads = existingSquads.Count(s => s.role == BotSquad.SquadRole.Defend);
        int captureSquads = existingSquads.Count(s => s.role == BotSquad.SquadRole.Capture);

        // GAME START: If there are neutral flags, everyone captures!
        if (neutralFlags > 0)
        {
            return BotSquad.SquadRole.Capture;
        }

        // No neutrals left - now use tactical roles
        // Losing badly? All attack!
        if (ourFlags < enemyFlags - 1)
        {
            return BotSquad.SquadRole.Attack;
        }

        // Winning? Balance defense and attack
        if (ourFlags > enemyFlags)
        {
            if (defendSquads < ourFlags / 2)
                return BotSquad.SquadRole.Defend;
            else
                return BotSquad.SquadRole.Attack;
        }

        // Even game - attack
        return attackSquads < defendSquads ? BotSquad.SquadRole.Attack : BotSquad.SquadRole.Defend;
    }

    void AssignSquadObjectives(int team)
    {
        var flags = FindObjectsOfType<CapturePoint>();
        string teamName = team == 0 ? "US Army" : "Insurgents";

        if (flags.Length == 0)
        {
            return;
        }

        // Count neutral flags
        int neutralFlagCount = flags.Count(f => f.OwningTeam == -1);

        // If there are neutral flags, convert all squads to Capture role
        if (neutralFlagCount > 0)
        {
            foreach (var squad in teamSquads[team])
            {
                if (squad.role != BotSquad.SquadRole.Capture)
                {
                    squad.role = BotSquad.SquadRole.Capture;
                }
            }
        }

        foreach (var squad in teamSquads[team])
        {
            CapturePoint bestFlag = null;

            switch (squad.role)
            {
                case BotSquad.SquadRole.Attack:
                    // Attack enemy flags
                    var enemyFlags = flags.Where(f => f.OwningTeam != team && f.OwningTeam != -1).ToList();
                    if (enemyFlags.Count > 0)
                    {
                        // Pick closest enemy flag
                        Vector3 squadPos = squad.GetSquadCenter();
                        bestFlag = enemyFlags.OrderBy(f => Vector3.Distance(squadPos, f.transform.position)).First();
                    }
                    break;

                case BotSquad.SquadRole.Defend:
                    // Defend our flags
                    var ourFlags = flags.Where(f => f.OwningTeam == team).ToList();
                    if (ourFlags.Count > 0)
                    {
                        // Pick flag with fewest defenders
                        bestFlag = ourFlags.OrderBy(f => CountBotsAtFlag(f, team)).First();
                    }
                    break;

                case BotSquad.SquadRole.Capture:
                    // Capture neutral flags
                    var neutralFlags = flags.Where(f => f.OwningTeam == -1).ToList();
                    if (neutralFlags.Count > 0)
                    {
                        Vector3 squadPos = squad.GetSquadCenter();
                        bestFlag = neutralFlags.OrderBy(f => Vector3.Distance(squadPos, f.transform.position)).First();
                    }
                    break;
            }

            // FALLBACK: If no flag found for role, capture neutral flags instead
            if (bestFlag == null)
            {
                var fallbackNeutrals = flags.Where(f => f.OwningTeam == -1).ToList();
                if (fallbackNeutrals.Count > 0)
                {
                    Vector3 squadPos = squad.GetSquadCenter();
                    bestFlag = fallbackNeutrals.OrderBy(f => Vector3.Distance(squadPos, f.transform.position)).First();
                }
            }

            if (bestFlag != squad.targetFlag)
            {
                squad.targetFlag = bestFlag;
            }

            // ALWAYS update rally point to keep it fresh
            squad.UpdateRallyPoint();
        }
    }

    int CountBotsAtFlag(CapturePoint flag, int team)
    {
        return allKnownBots.Count(b => b != null && b.BotTeam == team &&
                           Vector3.Distance(b.transform.position, flag.transform.position) < 15f);
    }

    void AssignBotToSquad(OpsiveBotAI bot, BotSquad squad)
    {
        squad.AddMember(bot);
        bot.AssignToSquad(squad);
    }

    public BotSquad GetBotSquad(OpsiveBotAI bot)
    {
        foreach (var squadList in teamSquads.Values)
        {
            foreach (var squad in squadList)
            {
                if (squad.members.Contains(bot))
                    return squad;
            }
        }
        return null;
    }

    public List<OpsiveBotAI> GetSquadMembers(BotSquad squad)
    {
        if (squad == null) return new List<OpsiveBotAI>();
        return squad.members.Where(m => m != null).ToList();
    }

    // Allow bots to register themselves when spawned
    public void RegisterBot(OpsiveBotAI bot)
    {
        if (!allKnownBots.Contains(bot))
        {
            allKnownBots.Add(bot);
        }
    }
}
