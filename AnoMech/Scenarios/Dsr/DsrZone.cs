using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Map;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Dsr;

public sealed class DsrZone : IZone
{
    public static readonly DsrZone Instance = new();
    // The weather picks the arena: every phase's floor is in the one layout, each in an OB set shown
    // only under its own weathers. 66 shows P3's square r1fz_e00 floor.
    public static readonly Phase P3 = new(Instance, "P3", 66, DsrConstants.BgmId.Freefall,
        init: world => world.EnforceSquareArenaBoundary(DsrConstants.Geometry.P3ArenaHalfWidth, "Went past the arena's edge."));
    // The eyes fight on P3's square floor. UNVERIFIED: P4's weather, taken to carry on from P3.
    public static readonly Phase P4 = new(Instance, "P4", 66, DsrConstants.BgmId.Contention,
        init: world => world.EnforceSquareArenaBoundary(DsrConstants.Geometry.P3ArenaHalfWidth, "Went past the arena's edge."));
    // 2 shows the r1fz_b00 battle_stage03 floor; 59 (a cutscene weather) shows most of it too.
    // UNVERIFIED: that it is P5's, and P5's BGM.
    public static readonly Phase P5 = new(Instance, "P5", 2, 0,
        init: world => world.EnforceArenaBoundary(DsrConstants.Geometry.P5ArenaRadius, "Went past the arena's edge."));

    public string Name => "Dragonsong's Reprise";
    public uint TerritoryId => 968;
    public Vector3 Origin => new(100f, 0f, 100f);

    public IReadOnlyList<WaymarkLayout> WaymarkPresets { get; } =
        [new WaymarkLayout("NAUR", DsrConstants.NaurWaymarks)];

    public DutyInitData DutyInitData => DsrReplayData.DutyInitData;
}
