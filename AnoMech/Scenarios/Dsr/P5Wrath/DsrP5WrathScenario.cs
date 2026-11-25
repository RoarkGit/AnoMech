using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;
using Actions = AnoMech.Scenarios.Dsr.DsrActions;

namespace AnoMech.Scenarios.Dsr.P5Wrath;

// DSR P5's Wrath of the Heavens, from Thordan's cast to Empty Dimension and the Thunderstruck
// bursts: Skyward Leap, two Spiral Pierce tethers and Twisting Dive with its twisters, then
// Ascalon's Mercy Revealed, Altar Flare and Liquid Heaven chasing a player each, Cauterize from
// both side dragons and Ser Grinnaux's donut. Times count from 22s into P5, after Knights of the
// Round.
public sealed class DsrP5WrathScenario : IScenario
{
    public string Name => "Wrath of the Heavens";
    public IPhase Phase => DsrZone.P5;
    public bool SupportsSolo => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new DsrP5WrathAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly DsrP5WrathSettingsWindow settingsWindow = new();

    private const float AltarFlareInterval = 1.5f;
    private const float LiquidHeavenInterval = 1.16f;
    private const float ThunderstruckSeconds = 15f;
    // UNVERIFIED: twister and puddle timings and sizes, taken from UCOB's Twister and Liquid Hell.
    private const float TwisterSpawnDelay = 0.5f;
    private const float TwisterArmDelay = 0.5f;
    private const float TwisterLifetime = 5.7f;
    private const float TwisterTriggerRadius = 1.5f;
    private const float TwisterKeepOutRadius = 2.4f;
    private const float PuddleRadius = 3.5f;
    private const float PuddleArmDelay = 2f;
    private const float PuddleLifetime = 11.7f;
    private const float BurnsDeathDelay = 3f;
    private const float BurnsSeconds = 15f;
    private const ushort EObjActiveState = 1;

    private sealed record Twister(Vector3 Position, SimEventObject? Visual, CircleObstacle KeepOut, float ArmAt, float ExpireAt);

    private SimWorld world = null!;
    private DsrP5WrathState state = null!;
    private SimEnemy? thordan;
    private SimEnemy? vedrfolnir;
    private SimEnemy? darkscale;
    private SimEnemy? vidofnir;
    private SimEnemy? ignasse;
    private SimEnemy? vellguine;
    private SimEnemy? paulecrain;
    private SimEnemy? grinnaux;
    private SimEnemy? charibert;
    private readonly List<SimTether> tethers = new();
    private readonly List<Vector3> twisterSpots = new();
    private readonly List<Twister> twisters = new();
    private readonly List<(Vector3 Center, float ArmAt, float ExpireAt)> puddles = new();
    private readonly HashSet<SimCharacter> burning = new();
    private float clock;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new DsrP5WrathState(world.Rng, settingsWindow.Overrides, world.Party.PlayerRole);
        thordan = vedrfolnir = darkscale = vidofnir = ignasse = vellguine = paulecrain = grinnaux = charibert = null;
        tethers.Clear();
        twisterSpots.Clear();
        twisters.Clear();
        puddles.Clear();
        burning.Clear();
        clock = 0f;
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<DsrP5WrathState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0.00f, PlacePartyAroundThordan);
        world.Events.Add(0.00f, SpawnThordan);
        world.Events.Add(2.80f, () => thordan?.Cast(ActionId.WrathOfTheHeavens, animationLock: 1f));
        world.Events.Add(9.84f, () => thordan?.SetTargetable(false));

        world.Events.Add(12.30f, SpawnVedrfolnirAndTetheringKnights);
        world.Events.Add(12.56f, MarkSkywardLeap);
        world.Events.Add(12.56f, TetherSpiralPierce);
        world.Events.Add(12.65f, () => vedrfolnir?.Cast(Actions.TwistingDive));
        world.Events.Add(16.00f, SpawnSideDragons);
        world.Events.Add(16.25f, () => darkscale?.Cast(ActionId.ChainLightning));
        world.Events.Add(17.23f, Thunderstrike);
        world.Events.Add(18.62f, SnapshotTwisters);
        world.Events.Add(18.62f + TwisterSpawnDelay, SpawnTwisters);
        world.Events.Add(18.67f, LeapOntoSkywardTarget);
        world.Events.Add(18.70f, MarkCauterize);
        world.Events.Add(18.71f, SpiralPierce);

        world.Events.Add(18.90f, SpawnGrinnauxAndCharibert);
        world.Events.Add(20.89f, () => thordan?.Cast(ActionId.AscalonsMercyRevealed, animationLock: 1f));
        world.Events.Add(24.95f, AscalonsMercyCones);
        world.Events.Add(24.95f, () => charibert?.Cast(ActionId.AltarFlare, animationLock: 1f));
        for (var i = 0; i < 4; i++) world.Events.Add(24.95f + i * AltarFlareInterval, AltarFlare);
        for (var i = 0; i < 5; i++) world.Events.Add(25.00f + i * LiquidHeavenInterval, LiquidHeaven);
        world.Events.Add(25.83f, Cauterize);
        world.Events.Add(27.05f, () => grinnaux?.Cast(Actions.EmptyDimension));
        world.Events.Add(32.29f, ChainLightningBursts);
        world.Events.Add(33.00f, () => thordan?.SetTargetable(true));
        world.Events.Add(36.00f, DespawnAll);
    }

    public void Tick(float delta, float elapsed)
    {
        clock = elapsed;
        UpdateTwisters();
        BurnPuddleStanders();
    }

    private IEnumerable<SimCharacter> Alive() => world.Party.ActiveMembers().Where(m => m.IsAlive());

    private SimCharacter? AliveAs(PartyRole role) =>
        world.Party.Get(role) is { } member && member.IsAlive() ? member : null;

    private void PlacePartyAroundThordan()
    {
        var members = world.Party.ActiveMembers().ToList();
        for (var i = 0; i < members.Count; i++)
        {
            var bearing = i * MathF.Tau / members.Count;
            var at = new Vector3(MathF.Sin(bearing) * 6f, 0f, -MathF.Cos(bearing) * 6f);
            members[i].TeleportTo(new Placement(at, 0f).Face(Vector3.Zero));
        }
    }

    private SimEnemy? Spawn(uint baseId, uint nameId, Vector3 position, NpcAppearance? appearance = null,
        EnemyListMode enemyList = EnemyListMode.Never, SpawnVisibility visibility = SpawnVisibility.Visible) =>
        world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: baseId,
            NameId: nameId,
            Level: Level,
            Targetable: false,
            EnemyList: enemyList,
            Visibility: visibility,
            Appearance: appearance,
            Placement: new Placement(position, 0f).Face(Vector3.Zero)));

    private void SpawnThordan()
    {
        thordan = Spawn(BNpcBaseId.KingThordan, BNpcNameId.KingThordan, Vector3.Zero, Appearance.KingThordan, EnemyListMode.Always);
        thordan?.SetTargetable(true);
    }

    // Each arrives just before its first move, a moment early since the client drops a cast sent the
    // frame its caster spawns. UNVERIFIED: when retail shows each one. The knights stand 23y out from
    // the middle, 30° to either side of Vedrfolnir.
    private void SpawnVedrfolnirAndTetheringKnights()
    {
        vedrfolnir = Spawn(BNpcBaseId.Vedrfolnir, BNpcNameId.Vedrfolnir, state.FromVedrfolnir(0f, -23f));
        ignasse = Spawn(BNpcBaseId.SerIgnasse, BNpcNameId.SerIgnasse, state.FromVedrfolnir(11.5f, -19.92f), Appearance.SerIgnasse);
        vellguine = Spawn(BNpcBaseId.SerVellguine, BNpcNameId.SerVellguine, state.FromVedrfolnir(-11.5f, -19.92f), Appearance.SerVellguine);
        paulecrain = Spawn(BNpcBaseId.SerPaulecrain, BNpcNameId.SerPaulecrain, state.FromVedrfolnir(0f, -18f), Appearance.SerPaulecrain,
            visibility: SpawnVisibility.HiddenUntilShown);
    }

    private void SpawnSideDragons()
    {
        darkscale = Spawn(BNpcBaseId.Darkscale, BNpcNameId.Darkscale, state.FromVedrfolnir(-23f, 0f));
        vidofnir = Spawn(BNpcBaseId.Vidofnir, BNpcNameId.Vidofnir, state.FromVedrfolnir(23f, 0f));
    }

    private void SpawnGrinnauxAndCharibert()
    {
        grinnaux = Spawn(BNpcBaseId.SerGrinnaux, BNpcNameId.SerGrinnaux, state.FromGrinnaux(0f, -9f), Appearance.SerGrinnaux);
        charibert = Spawn(BNpcBaseId.SerCharibert, BNpcNameId.SerCharibert, state.FromGrinnaux(0f, 9f), Appearance.SerCharibert);
    }

    private void MarkSkywardLeap() => AliveAs(state.SkywardLeap)?.AttachLockonVfx(LockonId.SkywardLeap);

    private void MarkCauterize() => AliveAs(state.Cauterize)?.AttachLockonVfx(LockonId.Cauterize);

    private void TetherSpiralPierce()
    {
        if (AliveAs(state.IgnasseTether) is { } a) tethers.Add(world.Tether(ignasse, a, TetherId.SpiralPierce));
        if (AliveAs(state.VellguineTether) is { } b) tethers.Add(world.Tether(vellguine, b, TetherId.SpiralPierce));
    }

    private void SpiralPierce()
    {
        foreach (var tether in tethers) tether.Despawn();
        tethers.Clear();
        if (AliveAs(state.IgnasseTether) is { } a) ignasse?.Cast(Actions.SpiralPierce, a);
        if (AliveAs(state.VellguineTether) is { } b) vellguine?.Cast(Actions.SpiralPierce, b);
    }

    private void LeapOntoSkywardTarget()
    {
        if (AliveAs(state.SkywardLeap) is not { } target || paulecrain is null) return;
        paulecrain.TeleportTo(new Placement(target.Position, target.Rotation));
        paulecrain.Cast(Actions.SkywardLeap, target);
    }

    private void Thunderstrike()
    {
        foreach (var role in state.Thunderstruck)
            AliveAs(role)?.AddStatus(StatusId.Thunderstruck, ThunderstruckSeconds);
    }

    // Twisting Dive leaves a twister under everyone, where they stood as it landed.
    private void SnapshotTwisters()
    {
        twisterSpots.Clear();
        twisterSpots.AddRange(Alive().Select(m => m.Position));
    }

    private void SpawnTwisters()
    {
        foreach (var at in twisterSpots)
        {
            var visual = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = EObjId.Twister,
                Placement = new Placement(at, 0f),
                TimelineState = EObjActiveState,
            });
            var keepOut = world.Obstacles.Add(new CircleObstacle(new Vector2(at.X, at.Z), TwisterKeepOutRadius));
            twisters.Add(new Twister(at, visual, keepOut, clock + TwisterArmDelay, clock + TwisterLifetime));
        }
        twisterSpots.Clear();
    }

    private void UpdateTwisters()
    {
        foreach (var twister in twisters.ToList())
        {
            if (clock >= twister.ExpireAt)
            {
                RemoveTwister(twister);
                continue;
            }
            if (clock < twister.ArmAt) continue;
            var victim = world.Party.Find.Closest(twister.Position);
            if (victim == null || !victim.IsAlive() || FlatDistance(victim.Position, twister.Position) > TwisterTriggerRadius) continue;
            RemoveTwister(twister);
            vedrfolnir?.CastFromHelper(Actions.TwisterBurst, victim, at: new Placement(twister.Position, 0f));
        }
    }

    private void RemoveTwister(Twister twister)
    {
        twisters.Remove(twister);
        twister.Visual?.Despawn();
        world.Obstacles.Remove(twister.KeepOut);
    }

    private void AscalonsMercyCones()
    {
        foreach (var member in Alive().ToList())
            thordan?.CastFromHelper(Actions.AscalonsMercyRevealedCone, member, at: new Placement(Vector3.Zero, 0f).Face(member.Position));
    }

    // Each Altar Flare lands where its target stood as it was cast.
    private void AltarFlare()
    {
        if (AliveAs(state.AltarFlare) is { } target)
            charibert?.CastFromHelper(Actions.AltarFlarePuddle, target.Position, at: new Placement(target.Position, 0f));
    }

    private void LiquidHeaven()
    {
        if (AliveAs(state.LiquidHeaven) is not { } target || vedrfolnir is null) return;
        var at = target.Position;
        vedrfolnir.Cast(Actions.LiquidHeaven, at);
        world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = EObjId.LiquidHeavenPuddle,
            Placement = new Placement(at, 0f),
            TimelineState = EObjActiveState,
            Lifetime = PuddleLifetime,
        });
        puddles.Add((at, clock + PuddleArmDelay, clock + PuddleLifetime));
    }

    private void BurnPuddleStanders()
    {
        puddles.RemoveAll(p => clock >= p.ExpireAt);
        foreach (var (center, armAt, _) in puddles)
        {
            if (clock < armAt) continue;
            foreach (var member in Alive().Where(m => FlatDistance(m.Position, center) <= PuddleRadius && burning.Add(m)).ToList())
            {
                member.AddStatus(StatusId.Burns, BurnsSeconds);
                world.Events.Add(BurnsDeathDelay, () => member.DieOfStatus(StatusId.Burns,
                    "stood in a Liquid Heaven puddle; they turn deadly a moment after they drop, so keep away from them"));
            }
        }
    }

    // Both dragons dive at where the marked player stood as they began.
    private void Cauterize()
    {
        var at = AliveAs(state.Cauterize)?.Position ?? state.FromGrinnaux(0f, 18f);
        darkscale?.Face(at);
        darkscale?.Cast(Actions.CauterizeDarkscale);
        vidofnir?.Face(at);
        vidofnir?.Cast(Actions.CauterizeVidofnir);
    }

    private void ChainLightningBursts()
    {
        foreach (var role in state.Thunderstruck)
        {
            if (AliveAs(role) is not { } member) continue;
            member.RemoveStatus(StatusId.Thunderstruck);
            darkscale?.CastFromHelper(Actions.ChainLightningBurst, member, at: new Placement(member.Position, 0f));
        }
    }

    private void DespawnAll()
    {
        foreach (var tether in tethers) tether.Despawn();
        tethers.Clear();
        foreach (var twister in twisters.ToList()) RemoveTwister(twister);
        puddles.Clear();
        foreach (var npc in new[] { thordan, vedrfolnir, darkscale, vidofnir, ignasse, vellguine, paulecrain, grinnaux, charibert })
            npc?.Despawn();
        thordan = vedrfolnir = darkscale = vidofnir = ignasse = vellguine = paulecrain = grinnaux = charibert = null;
    }

    private static float FlatDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));
}
