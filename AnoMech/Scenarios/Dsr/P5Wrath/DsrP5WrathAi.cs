using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Dsr.P5Wrath;

public sealed class DsrP5WrathAi : IScenarioAi<DsrP5WrathState>
{
    public string Name =>
        "NA: Skyward Leap left of Vedrfolnir, tethers cross to the far side, the rest right; spread for Mercy; "
        + "Altar Flare's target loops the left side, Liquid Heaven's the right; stack on Ser Grinnaux, Thunderstruck either side behind him";

    private static readonly Vector2 Stack = new(0f, -5f);
    private static readonly Vector2 ThunderLeft = new(-3.5f, -12.5f);
    private static readonly Vector2 ThunderRight = new(3.5f, -12.5f);
    private static readonly float[] MercyBearingsRight = [35f, 65f, 95f];
    private static readonly Vector2[] LiquidHeavenRoute = [new(11.5f, 8f), new(15f, 4f), new(14.5f, -1f), new(12.5f, -5f), new(9f, -8.5f)];
    private static readonly float[] LiquidHeavenMoves = [25.02f, 26.18f, 27.34f, 28.50f];
    private static readonly float[] AltarFlareMoves = [25.00f, 26.47f, 27.97f];
    private const float CauterizeBaitLeaves = 25.86f;
    private const float AltarFlareRouteEnds = 29.47f;
    private const float LiquidHeavenRouteEnds = 29.66f;

    private DsrP5WrathState state = null!;

    public void Run(DsrP5WrathState stateParam, SimWorld world)
    {
        state = stateParam;
        var ai = new AiManager(world);

        ai.Move(13.0f, () => ByRole(SpreadBeforeTheDive));
        ai.Move(18.85f, () => ByRole(SpreadForMercy), jitter: 0f);
        ai.Move(25.00f, () => ByRole(role => IsFreeAfterMercy(role) ? FinalSpot(role) : null));
        ai.Move(CauterizeBaitLeaves, () => ByRole(role => role == state.Cauterize && role != state.AltarFlare ? FinalSpot(role) : null));

        for (var step = 0; step < AltarFlareMoves.Length; step++)
        {
            var next = step;
            var at = next == 0 && state.AltarFlare == state.Cauterize ? CauterizeBaitLeaves : AltarFlareMoves[next];
            ai.Move(at, () => ByRole(role => role == state.AltarFlare ? G(AltarFlareRoute()[next]) : null), jitter: 0f);
        }
        ai.Move(AltarFlareRouteEnds, () => ByRole(role => role == state.AltarFlare ? FinalSpot(role) : null), jitter: 0f);

        for (var step = 0; step < LiquidHeavenMoves.Length; step++)
        {
            var next = step + 1;
            ai.Move(LiquidHeavenMoves[step], () => ByRole(role => role == state.LiquidHeaven ? G(LiquidHeavenRoute[next]) : null), jitter: 0f);
        }
        ai.Move(LiquidHeavenRouteEnds, () => ByRole(role => role == state.LiquidHeaven ? FinalSpot(role) : null), jitter: 0f);
    }

    private Vector3 V(float x, float z) => state.FromVedrfolnir(x, z);

    private Vector3 G(Vector2 p) => state.FromGrinnaux(p.X, p.Y);

    private Vector3 GAtBearing(float degrees, float radius)
    {
        var (sin, cos) = MathF.SinCos(degrees * MathF.PI / 180f);
        return state.FromGrinnaux(sin * radius, -cos * radius);
    }

    private bool IsPierceOrLeap(PartyRole role) =>
        role == state.SkywardLeap || role == state.IgnasseTether || role == state.VellguineTether;

    private Vector3? SpreadBeforeTheDive(PartyRole role)
    {
        if (role == state.SkywardLeap) return V(-18f, -9f);
        if (role == state.IgnasseTether) return V(-9f, 18f);
        if (role == state.VellguineTether) return V(9f, 18f);
        var right = Enum.GetValues<PartyRole>().Where(r => !IsPierceOrLeap(r)).ToList();
        return V(18f, -8f + 4f * right.IndexOf(role));
    }

    private Vector3? SpreadForMercy(PartyRole role)
    {
        if (role == state.SkywardLeap) return V(-14f, -13f);
        if (role == state.IgnasseTether) return V(-15f, 8f);
        if (role == state.VellguineTether) return V(-17f, 0f);
        if (role == state.Cauterize) return G(new Vector2(0f, 18f));
        if (role == state.LiquidHeaven) return G(LiquidHeavenRoute[0]);
        var unmarked = Enum.GetValues<PartyRole>().Where(r => state.MarkerOf(r) == WrathMarker.Unmarked).ToList();
        return GAtBearing(MercyBearingsRight[unmarked.IndexOf(role)], 14f);
    }

    private bool IsFreeAfterMercy(PartyRole role) =>
        role != state.AltarFlare && role != state.LiquidHeaven && role != state.Cauterize;

    private Vector3 FinalSpot(PartyRole role)
    {
        if (!state.Thunderstruck.Contains(role)) return G(Stack);
        var left = state.Thunderstruck.Contains(state.AltarFlare)
            ? state.AltarFlare
            : state.Thunderstruck.Min();
        return G(role == left ? ThunderLeft : ThunderRight);
    }

    private Vector2[] AltarFlareRoute() => (state.MarkerOf(state.AltarFlare), state.GrinnauxOnVedrfolnirSide) switch
    {
        (WrathMarker.SkywardLeap, true) => [new(-16f, -4f), new(-12f, 4f), new(-11f, -5f)],
        (WrathMarker.SkywardLeap, false) => [new(-6f, 11f), new(-12f, 4f), new(-14f, -2f)],
        (WrathMarker.IgnasseTether, true) => [new(-7f, 8f), new(-13f, 1f), new(-14f, -5f)],
        (WrathMarker.IgnasseTether, false) => [new(-18f, 0f), new(-16f, 8f), new(-10f, 1f)],
        (WrathMarker.VellguineTether, _) => [new(-12f, 7f), new(-4f, 11f), new(-8f, 3f)],
        _ => [new(-1f, 15f), new(-7f, 8f), new(-13f, 1f)],
    };

    private static IAiMove ByRole(Func<PartyRole, Vector3?> at) =>
        AiMove.Create(Enum.GetValues<PartyRole>().Select(role => at(role) is { } p ? new Vector2(p.X, p.Z) : (Vector2?)null).ToArray()).NaturalOrder();
}
