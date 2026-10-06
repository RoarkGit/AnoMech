using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Geometry;
using AnoMech.Core.Game.Party;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Ucob.P4Adds.UcobP4AddsConstants;
using BNpcBaseId = AnoMech.Scenarios.Ucob.UcobConstants.BNpcBaseId;
using BNpcNameId = AnoMech.Scenarios.Ucob.UcobConstants.BNpcNameId;

namespace AnoMech.Scenarios.Ucob.P4Adds;

// UCOB P4 "adds": Twintania and Nael deus Darnus guard a charging Bahamut Prime. Covers the
// first ~135s of the phase (two Hatch sets, four Twisters, two Nael triple quotes, both
// tankbuster pairs, Megaflares and Liquid Hells). Nael's "O Bahamut! We shall stand guard..."
// lands at 8s. The adds die on HP somewhere past two minutes in, so the scenario runs to the
// second Megaflare, which slower kills see.
//
// Tank flow: each tank keeps one add, the buster pair splits across them, and both adds swap
// tanks right after it. With two bot tanks the swaps are scripted; with a human tank (the
// player or a multiplayer peer) they follow that tank's Provoke.
public sealed class UcobP4AddsScenario : IScenario
{
    public string Name => "Adds";
    public IPhase Phase => UcobZone.P4;
    public bool SupportsSolo => true;
    public bool SupportsMultiplayer => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new UcobP4AddsAi()];

    public void DrawSettings() => settingsWindow.Draw();
    private readonly UcobP4AddsSettingsWindow settingsWindow = new();

    private const float AutoAttackInterval = 3.05f;
    // UNVERIFIED: how long a fresh twister waits before it can be triggered.
    private const float TwisterArmDelay = 0.5f;
    // Twisters go inactive 5.7s after spawning, puddles after ~11.7s.
    private const float TwisterLifetime = 5.7f;
    private const float LiquidHellPuddleLifetime = 11.7f;
    // UNVERIFIED: Burns starts landing 0.8-2s after a puddle spawns.
    private const float LiquidHellPuddleArmDelay = 2f;
    private const float BurnsDeathDelay = 3f;
    private const float TwisterBurstLinger = 1.5f;
    private const float OrbLingerAfterPop = 1.5f;
    // UNVERIFIED: a clear survived the beam with 6 of 8 in it; fewer is assumed lethal.
    private const int MinThermionicBeamStack = 6;

    private sealed class HatchOrb(SimEnemy orb, SimCharacter target)
    {
        public SimEnemy Orb { get; } = orb;
        public SimCharacter Target { get; } = target;
        public bool Released { get; set; }
        public float? DespawnAt { get; set; }
    }

    private sealed class Twister(SimEnemy helper, SimEventObject? visual, CircleObstacle keepOut, float armAt, float expireAt)
    {
        public SimEnemy Helper { get; } = helper;
        public SimEventObject? Visual { get; } = visual;
        public CircleObstacle KeepOut { get; } = keepOut;
        public float ArmAt { get; } = armAt;
        public float ExpireAt { get; set; } = expireAt;
        public bool Burst { get; set; }
    }

    private SimWorld world = null!;
    private SimParty party = null!;
    private DamageSolver damage = null!;
    private UcobP4AddsState state = null!;

    private SimEnemy? bahamut;
    private readonly List<HatchOrb> orbs = new();
    private readonly List<Twister> twisters = new();
    private readonly List<(Vector3 Center, float ArmAt, float ExpireAt)> puddles = new();
    private readonly List<(SimCharacter Member, float DiesAt)> burning = new();
    private bool engaged;
    private bool bossesHeld;
    private float clock;
    private float twintaniaSwing;
    private float naelSwing;

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = world.Party;
        damage = new DamageSolver(party);
        state = new UcobP4AddsState(settingsWindow.Overrides);
        bahamut = null;
        orbs.Clear();
        twisters.Clear();
        puddles.Clear();
        burning.Clear();
        world.ActionAttempted -= OnActionAttempted;
        world.ActionAttempted += OnActionAttempted;
        engaged = false;
        bossesHeld = false;
        clock = 0f;
        twintaniaSwing = 0f;
        naelSwing = 0f;

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UcobP4AddsState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0.5f, SpawnNeurolinks);
        world.Events.Add(0.5f, SpawnBahamut);
        world.Events.Add(1.0f, () => WarpIn(bahamut, ActionTimelineId.WarpEnd));
        world.Events.Add(2.49f, () => bahamut?.SetModelState(BahamutChargingModelState));
        world.Events.Add(2.56f, () => bahamut?.PlayActionTimeline(ActionTimelineId.BahamutDivineJudgmentPose));
        world.Events.Add(4.5f, SpawnTwintaniaAndNael);
        world.Events.Add(5.0f, () => WarpIn(state.Twintania, ActionTimelineId.WarpEnd2));
        world.Events.Add(5.0f, () => WarpIn(state.Nael, ActionTimelineId.WarpEnd));
        world.Events.Add(7.0f, TetherAddsToBahamut);
        world.Events.Add(8.0f, () => NaelSays(Text.StandGuard));
        world.Events.Add(11.56f, BahamutsFavor);
        world.Events.Add(13.1f, Engage);

        world.Events.Add(21.14f, PlummetAndClaw);
        world.Events.Add(24.39f, () => LiquidHell(pickTarget: true));
        world.Events.Add(25.54f, () => LiquidHell(pickTarget: false));
        world.Events.Add(26.70f, () => LiquidHell(pickTarget: false));
        world.Events.Add(27.90f, () => LiquidHell(pickTarget: false));
        world.Events.Add(29.08f, () => LiquidHell(pickTarget: false));

        world.Events.Add(30.18f, () => MarkHatchTargets(0));
        world.Events.Add(30.28f, () => state.Twintania?.Cast(ActionId.Generate, castSeconds: 2.7f));
        world.Events.Add(30.50f, () => SpawnOviforms(0));
        world.Events.Add(34.55f, ReleaseOviforms);
        world.Events.Add(35.43f, CastTwister);
        world.Events.Add(36.71f, () => SnapshotTwisters(0));
        world.Events.Add(37.76f, () => SpawnTwisters(0));

        world.Events.Add(40.21f, () => BeginQuote(0));
        world.Events.Add(45.31f, () => ResolveNaelMove(0, 0));
        world.Events.Add(48.41f, () => ResolveNaelMove(0, 1));
        world.Events.Add(51.51f, () => ResolveNaelMove(0, 2));
        world.Events.Add(51.80f, ReleaseBosses);

        world.Events.Add(52.58f, CastTwister);
        world.Events.Add(53.86f, () => SnapshotTwisters(1));
        world.Events.Add(54.93f, () => SpawnTwisters(1));
        world.Events.Add(60.55f, () => state.Nael?.Cast(ActionId.Megaflare, castSeconds: 4.7f));
        world.Events.Add(65.54f, Megaflare);
        world.Events.Add(69.69f, CastRavensbeak);
        world.Events.Add(69.73f, CastDeathSentence);
        world.Events.Add(73.65f, ResolveRavensbeak);
        world.Events.Add(73.69f, ResolveDeathSentence);
        world.Events.Add(75.60f, () => BotTanksGiveNaelTo(PartyRole.MainTank));
        world.Events.Add(76.50f, () => BotTanksGiveTwintaniaTo(PartyRole.OffTank));
        world.Events.Add(77.84f, PlummetAndClaw);

        world.Events.Add(87.03f, () => LiquidHell(pickTarget: true));
        world.Events.Add(88.19f, () => LiquidHell(pickTarget: false));
        world.Events.Add(89.35f, () => LiquidHell(pickTarget: false));
        world.Events.Add(90.50f, () => LiquidHell(pickTarget: false));
        world.Events.Add(91.66f, () => LiquidHell(pickTarget: false));

        world.Events.Add(92.78f, () => MarkHatchTargets(1));
        world.Events.Add(92.87f, () => state.Twintania?.Cast(ActionId.Generate, castSeconds: 2.7f));
        world.Events.Add(93.11f, () => SpawnOviforms(1));
        world.Events.Add(97.15f, ReleaseOviforms);
        world.Events.Add(98.06f, CastTwister);
        world.Events.Add(99.34f, () => SnapshotTwisters(2));
        world.Events.Add(100.40f, () => SpawnTwisters(2));

        world.Events.Add(102.78f, () => BeginQuote(1));
        world.Events.Add(107.88f, () => ResolveNaelMove(1, 0));
        world.Events.Add(110.98f, () => ResolveNaelMove(1, 1));
        world.Events.Add(114.08f, () => ResolveNaelMove(1, 2));
        world.Events.Add(114.40f, ReleaseBosses);

        world.Events.Add(115.20f, CastTwister);
        world.Events.Add(116.48f, () => SnapshotTwisters(3));
        world.Events.Add(117.55f, () => SpawnTwisters(3));
        world.Events.Add(125.52f, CastDeathSentence);
        world.Events.Add(126.24f, CastRavensbeak);
        world.Events.Add(129.51f, ResolveDeathSentence);
        world.Events.Add(130.00f, () => BotTanksGiveTwintaniaTo(PartyRole.MainTank));
        world.Events.Add(130.22f, ResolveRavensbeak);
        world.Events.Add(130.90f, () => BotTanksGiveNaelTo(PartyRole.OffTank));
        world.Events.Add(137.33f, () => state.Nael?.Cast(ActionId.Megaflare, castSeconds: 4.7f));
        world.Events.Add(142.03f, Megaflare);
        world.Events.Add(146.0f, DespawnAll);
    }

    public void Tick(float delta, float elapsed)
    {
        clock = elapsed;
        SwingAutoAttacks(delta);
        BurnPuddleStandersInLiquidHell();
        UpdateNeurolinkStatus();
        UpdateOrbs();
        UpdateTwisters();
    }

    // A peer never runs Run, and the wall is fixed-time scenery.
    public void RunInstanceEvents(SimWorld instanceWorld)
    {
        instanceWorld.Events.Add(1.0f, () => PlayTeraflareWall(instanceWorld, steadyOn: false));
        instanceWorld.Events.Add(6.2f, () => PlayTeraflareWall(instanceWorld, steadyOn: true));
    }

    private static void PlayTeraflareWall(SimWorld instanceWorld, bool steadyOn) =>
        instanceWorld.Map.PlaySharedGroupTimeline(TeraflareWall.Sgb,
            steadyOn ? TeraflareWall.On : TeraflareWall.OffToOn, resetIndex: TeraflareWall.Off);

    private void SpawnNeurolinks()
    {
        foreach (var link in Geometry.Neurolinks)
            SpawnActiveEObj(EObjId.Neurolink, link);
    }

    private void SpawnBahamut()
    {
        bahamut = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.BahamutPrime,
            NameId: BNpcNameId.BahamutPrime,
            Level: UcobConstants.Level,
            Targetable: false,
            EnemyList: EnemyListMode.Never,
            IsVisible: false,
            Placement: new Placement(Geometry.BahamutSpawn, 0f)));
    }

    private static void WarpIn(SimEnemy? enemy, ushort warpTimeline)
    {
        enemy?.SetVisible(true);
        enemy?.PlayActionTimeline(warpTimeline);
    }

    private void SpawnTwintaniaAndNael()
    {
        state.Twintania = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.Twintania,
            NameId: BNpcNameId.Twintania,
            Level: UcobConstants.Level,
            Targetable: false,
            EnemyList: EnemyListMode.Manual,
            IsVisible: false,
            Placement: new Placement(Geometry.TwintaniaSpawn, 0f)));
        state.Nael = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.NaelDeusDarnus,
            NameId: BNpcNameId.NaelDeusDarnus,
            Level: UcobConstants.Level,
            Targetable: false,
            EnemyList: EnemyListMode.Manual,
            IsVisible: false,
            Placement: new Placement(Geometry.NaelSpawn, 0f)));
    }

    private void TetherAddsToBahamut()
    {
        if (bahamut == null) return;
        world.Tether(state.Twintania, bahamut, TetherId.GuardingBahamut, duration: 4.6f);
        world.Tether(state.Nael, bahamut, TetherId.GuardingBahamut, duration: 4.6f);
    }

    private void BahamutsFavor()
    {
        bahamut?.Cast(ActionId.BahamutsFavor, castSeconds: 0f);
        state.Twintania?.AddStatus(StatusId.DamageUp);
        state.Nael?.AddStatus(StatusId.DamageUp);
    }

    private void Engage()
    {
        foreach (var boss in new[] { state.Twintania, state.Nael })
        {
            boss?.SetTargetable(true);
            boss?.SetVisibleInEnemyList(true);
        }
        engaged = true;
        FollowHolders();
    }

    private void GiveNaelTo(PartyRole role) => AssignTanks(state.TwintaniaTank, role);

    private void GiveTwintaniaTo(PartyRole role) => AssignTanks(role, state.NaelTank);

    private void AssignTanks(PartyRole twintaniaTank, PartyRole naelTank)
    {
        if (twintaniaTank != state.TwintaniaTank) AnnounceSwap(twintaniaTank, "Twintania");
        if (naelTank != state.NaelTank) AnnounceSwap(naelTank, UcobP4AddsConstants.Text.NaelName);
        state.AssignTanks(twintaniaTank, naelTank);
        if (!bossesHeld) FollowHolders();
    }

    private void BotTanksGiveNaelTo(PartyRole role)
    {
        if (!AnyHumanTank) GiveNaelTo(role);
    }

    private void BotTanksGiveTwintaniaTo(PartyRole role)
    {
        if (!AnyHumanTank) GiveTwintaniaTo(role);
    }

    private bool AnyHumanTank => IsHuman(PartyRole.MainTank) || IsHuman(PartyRole.OffTank);

    private bool IsHuman(PartyRole role) => role == party.PlayerRole || party.Get(role) is SimNetworkPuppet;

    // With a human tank, swaps follow Provoke instead of the script: the add they provoke comes
    // to them and the other add goes to the other tank.
    private void OnActionAttempted(PartyRole role, uint actionId, SimEnemy enemy)
    {
        if (!engaged || actionId != ActionId.Provoke || !role.IsTank()) return;
        var coTank = role == PartyRole.MainTank ? PartyRole.OffTank : PartyRole.MainTank;

        if (enemy == state.Nael && state.NaelTank != role)
            AssignTanks(coTank, role);
        else if (enemy == state.Twintania && state.TwintaniaTank != role)
            AssignTanks(role, coTank);
    }

    private void FollowHolders()
    {
        state.Twintania?.SetTarget(party.Get(state.TwintaniaTank));
        state.Nael?.SetTarget(party.Get(state.NaelTank));
    }

    // Nael plants for her quote; releasing keeps a tank running out for Chariot from dragging
    // the AOE along behind them.
    private void BeginQuote(int index)
    {
        bossesHeld = true;
        state.Twintania?.Follow(null);
        state.Nael?.Follow(null);
        var quote = state.Quotes[index];
        NaelSays(state.SimplifiedQuotes ? quote.Simplified : quote.Text);
    }

    private void ReleaseBosses()
    {
        bossesHeld = false;
        FollowHolders();
    }

    private void AnnounceSwap(PartyRole role, string boss)
    {
        if (!AnyHumanTank) return;
        var who = role == PartyRole.MainTank ? "the main tank" : "the off tank";
        world.Announce($"Tank swap: {boss} is now on {who}.");
    }

    private void NaelSays(string text) => world.Announce(text, UcobP4AddsConstants.Text.NaelName);

    private void SwingAutoAttacks(float delta)
    {
        if (!engaged) return;
        twintaniaSwing = Swing(state.Twintania, state.TwintaniaTank, ActionId.TwintaniaAttack,
            StatusId.SlashingResistanceDownII, "Twintania", twintaniaSwing - delta);
        naelSwing = Swing(state.Nael, state.NaelTank, ActionId.NaelAttack,
            StatusId.PiercingResistanceDownII, UcobP4AddsConstants.Text.NaelName, naelSwing - delta);
    }

    // An auto from the add whose buster left its resistance-down on this tank is lethal:
    // the other tank should have taken the add first.
    private float Swing(SimEnemy? boss, PartyRole holder, uint attackId, ushort ownBusterVuln, string bossName, float untilNext)
    {
        if (untilNext > 0f) return untilNext;
        if (boss is not { IsActive: true } || boss.AnimationLock) return 0f;
        if (party.Get(holder) is not { } tank || !tank.IsAlive()) return 0f;
        boss.Cast(attackId, castSeconds: 0f, targetId: tank.GameObjectId);
        if (tank.HasStatus(ownBusterVuln))
            tank.Die($"{bossName} auto-attacked them while they still had the vulnerability from its tank buster. The other tank needed to take {bossName} right after the buster.");
        return AutoAttackInterval;
    }

    private void PlummetAndClaw()
    {
        if (state.Twintania is { } twin && party.Get(state.TwintaniaTank) is { } twinTank && twinTank.IsAlive())
        {
            twin.Cast(ActionId.Plummet, castSeconds: 0f, targetId: twinTank.GameObjectId);
            var cleave = new Placement(twin.Position, 0f).Face(twinTank.Position);
            foreach (var hit in party.Find.InsideCone(cleave, Geometry.PlummetHalfAngle, Geometry.PlummetLength).ToList())
                if (hit != twinTank)
                    hit.Die("Hit by Plummet, Twintania's frontal cleave on her tank. Only her tank should be in front of Twintania.");
        }

        if (party.Get(state.NaelTank) is { } naelTank && naelTank.IsAlive())
            state.Nael?.Cast(ActionId.BahamutsClaw, castSeconds: 0f, targetId: naelTank.GameObjectId);
    }

    // The farthest player from Twintania at the first drop gets all five puddles, each dropped
    // where they stand at that instant.
    private void LiquidHell(bool pickTarget)
    {
        if (state.Twintania is not { } twin) return;
        if (pickTarget) state.LiquidHellTarget = party.Find.Farest(twin.Position);
        if (state.LiquidHellTarget is not { } target || !target.IsAlive()) return;

        var at = target.Position;
        twin.Cast(ActionId.LiquidHell, targetLocation: at, castSeconds: 0f, animationLock: 0f);
        SpawnActiveEObj(EObjId.LiquidHellPuddle, at, LiquidHellPuddleLifetime);
        puddles.Add((at, clock + LiquidHellPuddleArmDelay, clock + LiquidHellPuddleLifetime));
        foreach (var hit in party.Find.InsideCircle(at, Geometry.LiquidHellRadius))
            damage.ApplyDamage(hit, 0.2f, ActionId.LiquidHell, "puddle", lethal: false);
    }

    private void BurnPuddleStandersInLiquidHell()
    {
        puddles.RemoveAll(p => clock >= p.ExpireAt);
        foreach (var puddle in puddles)
        {
            if (clock < puddle.ArmAt) continue;
            foreach (var member in party.Find.InsideCircle(puddle.Center, Geometry.LiquidHellPuddleRadius).ToList())
            {
                if (burning.Exists(b => b.Member == member)) continue;
                member.AddStatus(StatusId.Burns, 15f);
                burning.Add((member, clock + BurnsDeathDelay));
            }
        }

        for (var i = burning.Count - 1; i >= 0; i--)
        {
            if (clock < burning[i].DiesAt) continue;
            burning[i].Member.Die($"Burns from a Liquid Hell puddle. Puddles turn deadly {LiquidHellPuddleArmDelay:0.#}s after they drop, so keep moving away from them.");
            burning.RemoveAt(i);
        }
    }

    private void MarkHatchTargets(int set)
    {
        foreach (var role in state.Hatches[set].Targets)
            if (party.Get(role) is { } member && member.IsAlive())
                member.AttachLockonVfx(LockonId.Hatch, persistent: false);
    }

    private void SpawnOviforms(int set)
    {
        if (state.Twintania is not { } twin) return;
        foreach (var role in state.Hatches[set].Targets)
        {
            if (party.Get(role) is not { } target || !target.IsAlive()) continue;
            var orb = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.Oviform,
                NameId: BNpcNameId.Oviform,
                Level: UcobConstants.Level,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                IsVisible: true,
                Placement: new Placement(twin.Position, twin.Rotation)));
            if (orb == null) continue;
            orb.SetTarget(target, follow: false);
            orbs.Add(new HatchOrb(orb, target));
        }
    }

    private void ReleaseOviforms()
    {
        foreach (var orb in orbs.Where(o => !o.Released))
        {
            orb.Released = true;
            orb.Orb.Follow(orb.Target, Geometry.OviformSpeed);
        }
    }

    private void UpdateOrbs()
    {
        for (var i = orbs.Count - 1; i >= 0; i--)
        {
            var orb = orbs[i];
            if (orb.DespawnAt is { } at)
            {
                if (clock < at) continue;
                orb.Orb.Despawn();
                orbs.RemoveAt(i);
                continue;
            }
            if (!orb.Released) continue;
            if (!orb.Target.IsAlive())
            {
                orb.Orb.Despawn();
                orbs.RemoveAt(i);
                continue;
            }
            if (FlatDistance(orb.Orb.Position, orb.Target.Position) > Geometry.HatchPopDistance) continue;

            PopOviform(orb);
            orb.DespawnAt = clock + OrbLingerAfterPop;
        }
    }

    private void UpdateNeurolinkStatus()
    {
        foreach (var member in party.ActiveMembers())
        {
            var inside = Geometry.Neurolinks.Any(link => FlatDistance(link, member.Position) <= Geometry.NeurolinkRadius);
            var has = member.HasStatus(StatusId.Neurolink);
            if (inside && !has) member.AddStatus(StatusId.Neurolink);
            else if (!inside && has) member.RemoveStatus(StatusId.Neurolink);
        }
    }

    private void PopOviform(HatchOrb orb)
    {
        orb.Orb.Follow(null);
        orb.Orb.Cast(ActionId.Hatch, castSeconds: 0f, targetId: orb.Target.GameObjectId);
        var target = orb.Target;

        if (!target.HasStatus(StatusId.Neurolink))
        {
            target.Die("Their Hatch orb reached them outside a Neurolink. Hatch targets must be standing in their Neurolink when the orb arrives.");
            orb.Orb.Cast(ActionId.DeepHatch, castSeconds: 0f);
            party.WipeAllPlayers("Deep Hatch: a Hatch orb reached its target outside a Neurolink, which wipes the party.");
            return;
        }

        if (target.HasStatus(StatusId.ManaHypersensitivity))
        {
            target.Die("Took a second Hatch while still Mana Hypersensitive from the previous one.");
            return;
        }
        damage.ApplyDamage(target, 0.3f, ActionId.Hatch, "neurolink", lethal: false);
        target.AddStatus(StatusId.ManaHypersensitivity, 16f);
    }

    private void CastTwister() => state.Twintania?.Cast(ActionId.Twister, castSeconds: 1.7f);

    private void RemoveTwister(Twister twister)
    {
        state.LiveTwisters.RemoveAll(t => FlatDistance(t, twister.Helper.Position) < 0.1f);
        twister.Helper.Despawn();
        twister.Visual?.Despawn();
        world.Obstacles.Remove(twister.KeepOut);
    }

    private SimEventObject? SpawnActiveEObj(uint eObjId, Vector3 position, float lifetime = 0f) =>
        world.SpawnEventObject(new EventObjectSpawnConfig
        {
            EObjId = eObjId,
            Placement = new Placement(position, 0f),
            TimelineState = EObjActiveState,
            Lifetime = lifetime,
        });

    // Retail drops four twisters under random players, placed where they stood about 3/4 of
    // the way through Twister's cast; touching one kills that player and throws everyone
    // within 8y 50y (Knockback row 0x6F), i.e. into the wall.
    private void SnapshotTwisters(int set)
    {
        var snapshot = state.TwisterSnapshots[set];
        snapshot.Clear();
        foreach (var role in state.TwisterTargets[set])
            if (party.Get(role) is { } member && member.IsAlive())
                snapshot.Add((role, member.Position));
        state.LiveTwisters.AddRange(snapshot.Select(s => s.Position));
    }

    private void SpawnTwisters(int set)
    {
        foreach (var (_, position) in state.TwisterSnapshots[set])
        {
            var helper = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.Helper,
                Level: UcobConstants.Level,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                IsVisible: true,
                Placement: new Placement(position, 0f)));
            if (helper == null) continue;
            var visual = SpawnActiveEObj(EObjId.Twister, position);
            var keepOut = world.Obstacles.Add(new CircleObstacle(
                new Vector2(position.X, position.Z), Geometry.TwisterKeepOutRadius));
            twisters.Add(new Twister(helper, visual, keepOut, clock + TwisterArmDelay, clock + TwisterLifetime));
        }
    }

    private void UpdateTwisters()
    {
        for (var i = twisters.Count - 1; i >= 0; i--)
        {
            var twister = twisters[i];
            if (clock >= twister.ExpireAt)
            {
                RemoveTwister(twister);
                twisters.RemoveAt(i);
                continue;
            }
            if (twister.Burst || clock < twister.ArmAt) continue;

            var victim = party.Find.Closest(twister.Helper.Position);
            if (victim == null || FlatDistance(victim.Position, twister.Helper.Position) > Geometry.TwisterTriggerRadius)
                continue;

            BurstTwister(twister, victim);
        }
    }

    private void BurstTwister(Twister twister, SimCharacter victim)
    {
        twister.Burst = true;
        twister.ExpireAt = clock + TwisterBurstLinger;
        var source = twister.Helper.Position;
        twister.Helper.Cast(ActionId.TwisterBurst, castSeconds: 0f, targetId: victim.GameObjectId);

        var flung = party.Find.InsideCircle(source, Geometry.TwisterKnockbackRadius).Where(m => m != victim).ToList();
        victim.Die("Walked into a Twister. Twisters drop where four players stood near the end of Twintania's cast; step off your spot as the cast finishes and don't walk back through them.");
        if (!KnockbackLookup.TryGet(KnockbackId.TwisterBurst, out var distance, out var speed)) return;
        foreach (var member in flung)
            (member as ISimPartyMember)?.Knockback(source, distance, speed);
    }

    private void ResolveNaelMove(int quote, int step)
    {
        if (state.Nael is not { } nael) return;
        switch (state.Quotes[quote].Moves[step])
        {
            case NaelMove.Chariot:
                nael.Cast(ActionId.IronChariot, castSeconds: 0f);
                foreach (var hit in party.Find.InsideCircle(nael.Position, Geometry.ChariotRadius).ToList())
                    hit.Die($"Hit by Iron Chariot: they were within {Geometry.ChariotRadius:0.#}y of Nael. Get out for Chariot.");
                break;
            case NaelMove.Dynamo:
                nael.Cast(ActionId.LunarDynamo, castSeconds: 0f);
                foreach (var hit in party.Find.InsideRing(nael.Position, Geometry.DynamoInnerRadius, Geometry.DynamoOuterRadius).ToList())
                    hit.Die($"Hit by Lunar Dynamo: they were more than {Geometry.DynamoInnerRadius:0.#}y from Nael. Get in close for Dynamo.");
                break;
            case NaelMove.Beam:
                ThermionicBeam(nael, state.BeamTargets[quote]);
                break;
            case NaelMove.Dive:
                RavenDive(nael, state.DiveTargets[quote]);
                break;
        }
    }

    private void ThermionicBeam(SimEnemy nael, PartyRole preferred)
    {
        if (AliveOrAnyone(preferred) is not { } target) return;
        nael.Cast(ActionId.ThermionicBeam, targetLocation: target.Position, castSeconds: 0f, targetId: target.GameObjectId);

        var needed = Math.Min(MinThermionicBeamStack, party.ActiveMembers().Count());
        var stack = party.Find.InsideCircle(target.Position, Geometry.ThermionicBeamRadius).ToList();
        foreach (var hit in stack)
        {
            if (stack.Count < needed)
                hit.Die($"Thermionic Beam was split by only {stack.Count} players (it needs {needed}). Stack together for the beam.");
            else
                damage.ApplyDamage(hit, 0.5f, ActionId.ThermionicBeam, "stack", lethal: false);
        }
    }

    private void RavenDive(SimEnemy nael, PartyRole preferred)
    {
        if (AliveOrAnyone(preferred) is not { } target) return;
        nael.Cast(ActionId.RavenDive, targetLocation: target.Position, castSeconds: 0f, targetId: target.GameObjectId);

        damage.ApplyDamage(target, 0.4f, ActionId.RavenDive, "dive", lethal: false);
        foreach (var hit in party.Find.InsideCircle(target.Position, Geometry.RavenDiveRadius).Where(m => m != target).ToList())
            hit.Die($"Stood within {Geometry.RavenDiveRadius:0.#}y of the Raven Dive target. Spread out for the dive.");
    }

    private SimCharacter? AliveOrAnyone(PartyRole preferred)
    {
        if (party.Get(preferred) is { } member && member.IsAlive()) return member;
        return party.Find.RandomMember();
    }

    private void Megaflare()
    {
        foreach (var member in party.ActiveMembers().ToList())
            damage.ApplyDamage(member, 0.45f, ActionId.Megaflare, "raidwide", lethal: false);
    }

    private void CastRavensbeak()
    {
        state.RavensbeakTarget = party.Get(state.NaelTank);
        if (state.RavensbeakTarget is { } t && t.IsAlive())
            state.Nael?.Cast(ActionId.Ravensbeak, castSeconds: 3.7f, targetId: t.GameObjectId);
    }

    private void CastDeathSentence()
    {
        state.DeathSentenceTarget = party.Get(state.TwintaniaTank);
        if (state.DeathSentenceTarget is { } t && t.IsAlive())
            state.Twintania?.Cast(ActionId.DeathSentence, castSeconds: 3.7f, targetId: t.GameObjectId);
    }

    private void ResolveRavensbeak()
    {
        naelSwing = AutoAttackInterval;
        ResolveTankBuster(state.RavensbeakTarget, ActionId.Ravensbeak, StatusId.PiercingResistanceDownII, 45f, "Ravensbeak");
    }

    private void ResolveDeathSentence()
    {
        twintaniaSwing = AutoAttackInterval;
        ResolveTankBuster(state.DeathSentenceTarget, ActionId.DeathSentence, StatusId.SlashingResistanceDownII, 34f, "Death Sentence");
    }

    private void ResolveTankBuster(SimCharacter? target, uint actionId, ushort debuff, float debuffSeconds, string name)
    {
        if (target == null || !target.IsAlive()) return;
        if (target is not ISimPartyMember { Role: PartyRole.MainTank or PartyRole.OffTank })
        {
            target.Die($"Took {name}, a tank buster, as a non-tank.");
            return;
        }
        if (target.HasStatus(debuff))
        {
            target.Die($"Took {name} while still carrying the vulnerability from the previous {name}.");
            return;
        }
        damage.ApplyDamage(target, 0.6f, actionId, "tank buster", lethal: false);
        target.AddStatus(debuff, debuffSeconds);
    }

    private void DespawnAll()
    {
        engaged = false;
        puddles.Clear();
        foreach (var orb in orbs) orb.Orb.Despawn();
        orbs.Clear();
        foreach (var twister in twisters) RemoveTwister(twister);
        state.LiveTwisters.Clear();
        twisters.Clear();
        state.Twintania?.Despawn();
        state.Nael?.Despawn();
        bahamut?.Despawn();
    }

    private static float FlatDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));
}
