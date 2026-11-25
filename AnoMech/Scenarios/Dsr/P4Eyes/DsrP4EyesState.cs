using System;
using System.Collections.Generic;
using System.Linq;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Dsr.P4Eyes;

public enum StartingTethers
{
    TanksAndMelee,
    HealersAndRanged,
}

[Flags]
public enum BoundStatus
{
    None = 0,
    Clawbound = 1,
    Fangbound = 2,
    PiercingResistanceDown = 4,
}

// Soul of Friendship and Soul of Devotion each land on a random player (UNVERIFIED: the logs name no
// target), which is why the party stacks for them. Hatebound binds four random players to the left
// eye (Clawbound) and the other four to the right (Fangbound). Each Mirage Dive lands on two of
// whoever is Clawbound as it goes off, at random.
public sealed class DsrP4EyesState
{
    public const int Dives = 4;

    private static readonly PartyRole[] TanksAndMelee =
        [PartyRole.MainTank, PartyRole.OffTank, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB];

    public IReadOnlySet<PartyRole> ClawboundAtStart { get; }
    // Per dive, the order its targets are picked from the Clawbound.
    public IReadOnlyList<IReadOnlyList<PartyRole>> DivePriority { get; }
    public PartyRole FriendshipTarget { get; }
    public PartyRole DevotionTarget { get; }
    // Each player's tether colour and Mirage Dive debuff as the host last saw them, which a peer's
    // world doesn't carry for other players.
    public Dictionary<PartyRole, BoundStatus> Bound { get; } = new();

    public bool Has(PartyRole role, BoundStatus status) => Bound.GetValueOrDefault(role).HasFlag(status);

    public DsrP4EyesState(Rng rng, DsrP4EyesStateOverrides overrides)
    {
        var roles = Enum.GetValues<PartyRole>();
        ClawboundAtStart = (overrides.StartingTethers switch
        {
            StartingTethers.TanksAndMelee => TanksAndMelee,
            StartingTethers.HealersAndRanged => roles.Except(TanksAndMelee),
            _ => rng.Shuffle(roles).Take(4),
        }).ToHashSet();
        DivePriority = Enumerable.Range(0, Dives).Select(_ => (IReadOnlyList<PartyRole>)rng.Shuffle(roles).ToList()).ToList();
        FriendshipTarget = roles[rng.Next(roles.Length)];
        DevotionTarget = roles[rng.Next(roles.Length)];
    }

    private DsrP4EyesState(IReadOnlySet<PartyRole> clawboundAtStart, IReadOnlyList<IReadOnlyList<PartyRole>> divePriority,
        PartyRole friendshipTarget, PartyRole devotionTarget)
    {
        ClawboundAtStart = clawboundAtStart;
        DivePriority = divePriority;
        FriendshipTarget = friendshipTarget;
        DevotionTarget = devotionTarget;
    }

    // Null unless the host's roll is one this fight can deal.
    public static DsrP4EyesState? FromNetworkReplay(
        PartyRole[] clawboundAtStart, PartyRole[][] divePriority, PartyRole friendshipTarget, PartyRole devotionTarget)
    {
        var roles = Enum.GetValues<PartyRole>();
        var clawbound = clawboundAtStart.ToHashSet();
        if (clawbound.Count != 4 || clawboundAtStart.Length != 4 || !clawbound.All(roles.Contains)) return null;
        if (divePriority.Length != Dives || !divePriority.All(p => p.Length == roles.Length && p.ToHashSet().SetEquals(roles))) return null;
        if (!roles.Contains(friendshipTarget) || !roles.Contains(devotionTarget)) return null;
        return new DsrP4EyesState(clawbound, divePriority.Select(p => (IReadOnlyList<PartyRole>)p.ToList()).ToList(), friendshipTarget, devotionTarget);
    }
}
