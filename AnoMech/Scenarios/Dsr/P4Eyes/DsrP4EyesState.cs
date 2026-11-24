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
}
