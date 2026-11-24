using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;

namespace AnoMech.Scenarios.Dsr.P4Eyes;

public sealed class DsrP4EyesAi : IScenarioAi<DsrP4EyesState>
{
    public string Name =>
        "NA: wrong colors swap in the middle; tanks pop yellow E, melee W; tanks swap healers (blue E), melee swap ranged (blue W); "
        + "healers dive inner, ranged outer around the right eye, then tanks, melee, earliest dived take over "
        + "(G1 searches NW counterclockwise, G2 NE clockwise; for the last dive the first pair keep their relative order)";

    private const float SpotTolerance = 1.5f;

    private static readonly Dictionary<PartyRole, Vector3> Idle = new()
    {
        [PartyRole.MainTank] = new(6f, 0f, -2.5f), [PartyRole.OffTank] = new(6f, 0f, 2.5f),
        [PartyRole.RegenHealer] = new(3f, 0f, -6f), [PartyRole.ShieldHealer] = new(3f, 0f, 6f),
        [PartyRole.MeleeDpsA] = new(-6f, 0f, -2.5f), [PartyRole.MeleeDpsB] = new(-6f, 0f, 2.5f),
        [PartyRole.PhysRangedDps] = new(-3f, 0f, -6f), [PartyRole.CasterDps] = new(-3f, 0f, 6f),
    };

    private static readonly Dictionary<PartyRole, Vector3> OrbSpot = new()
    {
        [PartyRole.MainTank] = new(15f, 0f, -1f), [PartyRole.OffTank] = new(15f, 0f, 1f),
        [PartyRole.RegenHealer] = new(10f, 0f, -4.5f), [PartyRole.ShieldHealer] = new(10f, 0f, 4.5f),
        [PartyRole.MeleeDpsA] = new(-15f, 0f, -1f), [PartyRole.MeleeDpsB] = new(-15f, 0f, 1f),
        [PartyRole.PhysRangedDps] = new(-10f, 0f, -4.5f), [PartyRole.CasterDps] = new(-10f, 0f, 4.5f),
    };

    private static readonly Dictionary<PartyRole, Vector3> OrbToPop = new()
    {
        [PartyRole.MainTank] = new(17f, 0f, 0f), [PartyRole.OffTank] = new(17f, 0f, 0f),
        [PartyRole.MeleeDpsA] = new(-17f, 0f, 0f), [PartyRole.MeleeDpsB] = new(-17f, 0f, 0f),
        [PartyRole.RegenHealer] = new(10f, 0f, -7f), [PartyRole.ShieldHealer] = new(10f, 0f, 7f),
        [PartyRole.PhysRangedDps] = new(-10f, 0f, -7f), [PartyRole.CasterDps] = new(-10f, 0f, 7f),
    };

    private static readonly Dictionary<PartyRole, PartyRole> BluePopperFor = new()
    {
        [PartyRole.MainTank] = PartyRole.RegenHealer, [PartyRole.OffTank] = PartyRole.ShieldHealer,
        [PartyRole.MeleeDpsA] = PartyRole.PhysRangedDps, [PartyRole.MeleeDpsB] = PartyRole.CasterDps,
    };

    private static readonly Vector3[] DiveSpots =
        [new(-5f, 0f, -5f), new(-15f, 0f, -5f), new(-5f, 0f, 5f), new(-15f, 0f, 5f)];

    private const int NorthEast = 0, NorthWest = 1, SouthEast = 2, SouthWest = 3;
    private static readonly int[] GroupOneSearch = [NorthWest, SouthWest, SouthEast, NorthEast];
    private static readonly int[] GroupTwoSearch = [NorthEast, SouthEast, SouthWest, NorthWest];
    private static readonly float[] DegreesCounterclockwiseFromNorth = [315f, 45f, 225f, 135f];

    private static readonly Dictionary<PartyRole, int> FirstDiveSpot = new()
    {
        [PartyRole.RegenHealer] = 0, [PartyRole.PhysRangedDps] = 1,
        [PartyRole.ShieldHealer] = 2, [PartyRole.CasterDps] = 3,
    };

    private static readonly Vector3 RightEyeStack = new(-10f, 0f, 0f);

    private static readonly float[] DiveTimes = [45.51f, 50.63f, 55.75f, 60.87f];

    private SimWorld world = null!;
    private readonly List<PartyRole> takeOverQueue = new();
    private readonly Dictionary<PartyRole, int> takingOver = new();
    private readonly Dictionary<PartyRole, int> firstDivedSpots = new();

    public void Run(DsrP4EyesState state, SimWorld worldParam)
    {
        world = worldParam;
        takeOverQueue.Clear();
        takeOverQueue.AddRange([PartyRole.MainTank, PartyRole.OffTank, PartyRole.MeleeDpsA, PartyRole.MeleeDpsB]);
        takingOver.Clear();
        firstDivedSpots.Clear();
        var ai = new AiManager(world);

        ai.Move(3.1f, () => ByRole(role => Idle[role]));
        ai.Move(19.6f, () => ByRole(role => HasWrongColor(role) ? Vector3.Zero : null), jitter: 0f);
        ai.Move(23.0f, () => ByRole(role => OrbSpot[role]));
        ai.Move(25.9f, () => ByRole(role => IsTankOrMelee(role) ? OrbToPop[role] : null));
        ai.Move(26.4f, () => ByRole(OntoBluePopper), jitter: 0f);
        ai.Move(29.3f, () => ByRole(role => BluePopperFor.ContainsKey(role) ? RightEyeStack : null));
        ai.Move(32.3f, () => ByRole(role => IsTankOrMelee(role) ? null : OrbToPop[role]));
        ai.Move(33.0f, () => ByRole(role => FirstDiveSpot.TryGetValue(role, out var spot) ? DiveSpots[spot] : null));

        foreach (var (dive, index) in DiveTimes.Take(DiveTimes.Length - 1).Select((t, i) => (t, i)))
        {
            var isFirst = index == 0;
            var setsUpLast = index == DiveTimes.Length - 2;
            ai.Move(dive + 0.8f, () => TakeOverFromDivedPlayers(isFirst, setsUpLast), jitter: 0f);
            ai.Move(dive + 2.6f, () => ByRole(SettleAfterTakeOver), jitter: 0f);
            ai.Move(dive + 3.4f, () => ByRole(LeaveSpotOnceFangbound));
        }
    }

    private static bool IsTankOrMelee(PartyRole role) =>
        role is PartyRole.MainTank or PartyRole.OffTank or PartyRole.MeleeDpsA or PartyRole.MeleeDpsB;

    private SimCharacter? Alive(PartyRole role) =>
        world.Party.Get(role) is { } member && member.IsAlive() ? member : null;

    private bool HasWrongColor(PartyRole role) =>
        Alive(role) is { } member && member.HasStatus(StatusId.Clawbound) != IsTankOrMelee(role);

    private static bool IsGroupOne(PartyRole role) =>
        role is PartyRole.MainTank or PartyRole.RegenHealer or PartyRole.MeleeDpsA or PartyRole.PhysRangedDps;

    private Vector3? OntoBluePopper(PartyRole role) =>
        BluePopperFor.TryGetValue(role, out var popper) && Alive(popper) is { } receiver ? receiver.Position : null;

    private int? DiveSpotOf(SimCharacter member)
    {
        for (var i = 0; i < DiveSpots.Length; i++)
            if (FlatDistance(member.Position, DiveSpots[i]) <= SpotTolerance) return i;
        return null;
    }

    private IAiMove TakeOverFromDivedPlayers(bool rememberAsFirstPair, bool firstPairKeepsOrder)
    {
        var divedBySpot = Enum.GetValues<PartyRole>()
            .Select(role => (role, member: Alive(role)))
            .Where(e => e.member is { } m && m.HasStatus(StatusId.Clawbound) && m.HasStatus(StatusId.PiercingResistanceDown))
            .Select(e => (e.role, member: e.member!, spot: DiveSpotOf(e.member!)))
            .Where(e => e.spot != null)
            .ToDictionary(e => e.spot!.Value, e => (e.role, e.member));
        if (rememberAsFirstPair)
            foreach (var (spot, (role, _)) in divedBySpot) firstDivedSpots[role] = spot;
        var moves = new Dictionary<PartyRole, Vector3>();
        var takers = takeOverQueue.Take(divedBySpot.Count).ToList();
        var pairedByOrder = firstPairKeepsOrder ? FirstPairKeepingOrder(takers, divedBySpot.Keys) : null;
        foreach (var taker in takers)
        {
            var search = IsGroupOne(taker) ? GroupOneSearch : GroupTwoSearch;
            var spot = pairedByOrder != null && pairedByOrder.TryGetValue(taker, out var kept) ? kept : search.First(divedBySpot.ContainsKey);
            var (divedRole, divedMember) = divedBySpot[spot];
            divedBySpot.Remove(spot);
            takeOverQueue.Remove(taker);
            takeOverQueue.Add(divedRole);
            takingOver[taker] = spot;
            moves[taker] = divedMember.Position;
        }
        return ByRole(role => moves.TryGetValue(role, out var at) ? at : null);
    }

    private Dictionary<PartyRole, int>? FirstPairKeepingOrder(List<PartyRole> takers, IEnumerable<int> divedSpots)
    {
        if (takers.Count != 2 || !takers.All(firstDivedSpots.ContainsKey)) return null;
        var spots = divedSpots.OrderBy(spot => DegreesCounterclockwiseFromNorth[spot]).ToList();
        if (spots.Count != 2) return null;
        var byOrder = takers.OrderBy(taker => DegreesCounterclockwiseFromNorth[firstDivedSpots[taker]]).ToList();
        return new Dictionary<PartyRole, int> { [byOrder[0]] = spots[0], [byOrder[1]] = spots[1] };
    }

    private Vector3? SettleAfterTakeOver(PartyRole role)
    {
        if (takingOver.Remove(role, out var spot)) return DiveSpots[spot];
        return LeaveSpotOnceFangbound(role);
    }

    private Vector3? LeaveSpotOnceFangbound(PartyRole role) =>
        Alive(role) is { } member && member.HasStatus(StatusId.Fangbound) && DiveSpotOf(member) != null
            ? RightEyeStack
            : null;

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at) =>
        AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();

    private static float FlatDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));
}
