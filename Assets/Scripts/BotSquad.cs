using UnityEngine;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Squad system for tactical bot coordination
/// Bots organize into squads with specific objectives
/// </summary>
public class BotSquad
{
    public enum SquadRole
    {
        Attack,     // Attacking enemy flags
        Defend,     // Defending our flags
        Capture,    // Capturing neutral flags
        Roaming     // Hunting enemies, general mayhem
    }

    // Squad data
    public int squadId;
    public SquadRole role;
    public int team;
    public CapturePoint targetFlag;
    public Vector3 rallyPoint;

    // Squad members
    public List<OpsiveBotAI> members = new List<OpsiveBotAI>();
    public OpsiveBotAI leader; // First member is squad leader

    // Squad size limits
    public const int MIN_SQUAD_SIZE = 2;
    public const int MAX_SQUAD_SIZE = 6;
    public const int IDEAL_ATTACK_SQUAD = 4;
    public const int IDEAL_DEFEND_SQUAD = 3;

    // Squad state
    public float formationSpread = 10f; // How spread out the squad is
    public bool needsReinforcements = false;

    public BotSquad(int id, int team)
    {
        squadId = id;
        this.team = team;
        rallyPoint = Vector3.zero; // Will be set when target is assigned
    }

    public void AddMember(OpsiveBotAI bot)
    {
        if (!members.Contains(bot))
        {
            members.Add(bot);
            if (leader == null)
                leader = bot;
        }
    }

    public void RemoveMember(OpsiveBotAI bot)
    {
        members.Remove(bot);
        if (leader == bot && members.Count > 0)
            leader = members[0];
    }

    public bool IsActive()
    {
        members.RemoveAll(m => m == null);
        return members.Count >= MIN_SQUAD_SIZE;
    }

    public bool CanAcceptMembers()
    {
        return members.Count < MAX_SQUAD_SIZE;
    }

    public int GetIdealSize()
    {
        switch (role)
        {
            case SquadRole.Attack: return IDEAL_ATTACK_SQUAD;
            case SquadRole.Defend: return IDEAL_DEFEND_SQUAD;
            case SquadRole.Capture: return 3;
            case SquadRole.Roaming: return 2;
            default: return 3;
        }
    }

    public Vector3 GetSquadCenter()
    {
        if (members.Count == 0) return Vector3.zero;

        Vector3 sum = Vector3.zero;
        foreach (var member in members)
        {
            if (member != null)
                sum += member.transform.position;
        }
        return sum / members.Count;
    }

    public void UpdateRallyPoint()
    {
        if (targetFlag != null)
        {
            rallyPoint = targetFlag.transform.position;
        }
        else if (leader != null)
        {
            rallyPoint = leader.transform.position;
        }
    }
}
