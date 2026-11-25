using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Party;

namespace AnoMech.Scenarios.Dsr.P5Wrath;

public enum WrathMarker
{
    SkywardLeap,
    IgnasseTether,
    VellguineTether,
    Cauterize,
    LiquidHeaven,
    Unmarked,
}

// The layout turns with Vedrfolnir, who dives from a random cardinal; the knights and dragons keep
// their places around him. Ser Grinnaux stands on Vedrfolnir's side or the far side at random, Ser
// Charibert opposite him. Skyward Leap, the two Spiral Pierce tethers and Cauterize's marker go to
// four different players, Thunderstruck to any two. Liquid Heaven follows one of the unmarked
// players who isn't Thunderstruck, and all four Altar Flares one of the four marked players.
public sealed class DsrP5WrathState
{
    public float VedrfolnirBearing { get; }
    public bool GrinnauxOnVedrfolnirSide { get; }
    public PartyRole SkywardLeap { get; }
    public PartyRole IgnasseTether { get; }
    public PartyRole VellguineTether { get; }
    public PartyRole Cauterize { get; }
    public PartyRole LiquidHeaven { get; }
    public PartyRole AltarFlare { get; }
    public IReadOnlyList<PartyRole> Thunderstruck { get; }

    public DsrP5WrathState(Rng rng, DsrP5WrathStateOverrides overrides, PartyRole player)
    {
        VedrfolnirBearing = rng.Next(4) * MathF.PI / 2f;
        GrinnauxOnVedrfolnirSide = overrides.GrinnauxOnVedrfolnirSide ?? rng.NextBool();

        var roles = rng.Shuffle(Enum.GetValues<PartyRole>()).ToList();
        if (overrides.Marker is { } forced && forced != WrathMarker.LiquidHeaven)
        {
            var slot = forced == WrathMarker.Unmarked ? 4 + rng.Next(4) : (int)forced;
            var at = roles.IndexOf(player);
            (roles[at], roles[slot]) = (roles[slot], roles[at]);
        }
        SkywardLeap = roles[0];
        IgnasseTether = roles[1];
        VellguineTether = roles[2];
        Cauterize = roles[3];
        var unmarked = roles.Skip(4).ToList();

        var wantsLiquidHeaven = overrides.Marker == WrathMarker.LiquidHeaven && unmarked.Contains(player);
        if (overrides.Marker == WrathMarker.LiquidHeaven && !wantsLiquidHeaven)
        {
            var swap = unmarked[rng.Next(unmarked.Count)];
            var at = roles.IndexOf(player);
            (roles[at], roles[roles.IndexOf(swap)]) = (swap, player);
            (SkywardLeap, IgnasseTether, VellguineTether, Cauterize) = (roles[0], roles[1], roles[2], roles[3]);
            unmarked = roles.Skip(4).ToList();
            wantsLiquidHeaven = true;
        }

        var thunder = rng.Shuffle(Enum.GetValues<PartyRole>()).Take(2).ToList();
        var playerThunder = overrides.Thunderstruck ?? (wantsLiquidHeaven ? false : null);
        if (playerThunder == true && !thunder.Contains(player)) thunder[rng.Next(2)] = player;
        if (playerThunder == false && thunder.Remove(player))
            thunder.Add(rng.NextObj(Enum.GetValues<PartyRole>().Where(r => r != player && !thunder.Contains(r)).ToArray()));
        Thunderstruck = thunder;

        var followed = unmarked.Where(r => !thunder.Contains(r)).ToArray();
        LiquidHeaven = wantsLiquidHeaven && followed.Contains(player) ? player : rng.NextObj(followed);
        AltarFlare = rng.NextObj(SkywardLeap, IgnasseTether, VellguineTether, Cauterize);
    }

    public WrathMarker MarkerOf(PartyRole role) =>
        role == SkywardLeap ? WrathMarker.SkywardLeap
        : role == IgnasseTether ? WrathMarker.IgnasseTether
        : role == VellguineTether ? WrathMarker.VellguineTether
        : role == Cauterize ? WrathMarker.Cauterize
        : role == LiquidHeaven ? WrathMarker.LiquidHeaven
        : WrathMarker.Unmarked;

    // Vedrfolnir's frame: x east and z south as if he were due north.
    public Vector3 FromVedrfolnir(float x, float z)
    {
        var (sin, cos) = MathF.SinCos(VedrfolnirBearing);
        return new Vector3(x * cos - z * sin, 0f, x * sin + z * cos);
    }

    // Vedrfolnir's frame, mirrored north-south when needed so Ser Grinnaux is north.
    public Vector3 FromGrinnaux(float x, float z) => FromVedrfolnir(x, GrinnauxOnVedrfolnirSide ? z : -z);
}
