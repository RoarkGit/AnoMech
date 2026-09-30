using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Ucob;

public sealed class UcobZone : IZone
{
    public static readonly UcobZone Instance = new();
    // Weather is left at the territory's own (WeatherRate 0 -> row 2); the BGM is the
    // "Answers" master track, which is what the Golden Bahamut phase plays.
    public static readonly Phase P4 = new(Instance, "P4", null, 0, clientSetup: InitBahamutArena);
    public static readonly Phase P5 = new(Instance, "P5", null, 226, clientSetup: InitGoldenBahamutArena);

    public string Name => "The Unending Coil of Bahamut";
    public uint TerritoryId => 733;
    public Vector3 Origin => new(0f, 0f, 0f);
    public byte Level => UcobConstants.Level;
    public ushort ItemLevel => UcobConstants.ItemLevel;

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
        [new WaymarkLayout("Aether Markers", UcobConstants.AetherWaymarks)];

    // With no real duty director, the client-side load leaves the per-phase layers in whatever
    // state it picked (which varies with the inn the run started from), so each phase forces
    // its own. LGB layer 0x1360 (f1bz_Boss2: f1b4_t2_jari1 gravel ground clutter) is hidden in
    // every phase; it z-fights against whichever floor is meant to be visible.
    public void Run(SimWorld world) => world.EnforceArenaBoundary(UcobConstants.Geometry.ArenaRadius);

    public void RunClientSetup(SimWorld world) => world.Events.Add(1f, () => world.Map.SuppressLayer(0x1360));

    // bg.lgb splits the arena per phase: f1bz_Boss1 (+_light) is Twintania's rock-and-hand
    // platform, f1bz_Boss2 Nael's (hidden zone-wide above), and f1bz_Boss3 / f1bz_Boss3_2 the
    // m1 set pieces and m2 floor (the only "yuka" floor model outside Boss1). Replaying the
    // retail director update (0x80000001 0xC7/0xEE) doesn't swap them client-side, and the
    // load leaves the Boss3 layers undrawn, so force them on and the P1 layers off. Confirmed
    // live for P4.
    private static void InitBahamutArena(SimWorld world) => world.Events.Add(1f, () =>
    {
        world.Map.SuppressLayer(0x1372);
        world.Map.SuppressLayer(0x13A6);
        world.Map.ForceLayerActive(0x1370);
        world.Map.ForceLayerActive(0x1676);
    });

    // Golden Bahamut keeps the m2 floor but, of f1bz_Boss3's m1 pieces, only the kyuu1 sphere;
    // the rest give the inner arena the wrong pattern.
    // No layer is P5-only; its gold look is Bahamut's model variant and lighting.
    private static void InitGoldenBahamutArena(SimWorld world) => world.Events.Add(1f, () =>
    {
        world.Map.SuppressLayer(0x1372);
        world.Map.SuppressLayer(0x13A6);
        world.Map.ForceLayerOnly(0x1370, "f1bz_m1_kyuu1.mdl");
        world.Map.ForceLayerActive(0x1676);
    });
}
