using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.Game.Party;
using AnoMech.Core.Native.Interfaces;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Dsr.DsrConstants;
using Actions = AnoMech.Scenarios.Dsr.DsrActions;

namespace AnoMech.Scenarios.Dsr.P4Eyes;

// DSR P4 from the eyes' arrival to Steep in Rage: Resentment, Hatebound (Clawbound and Fangbound
// tethers that swap between two players who touch, and the four orbs only the Clawbound should
// pop), the four rounds of Mirage Dive on random Clawbound, and Steep in Rage. The eyes die on HP
// around Steep in Rage, so the enrage is left out.
public sealed class DsrP4EyesScenario : IScenario
{
    public string Name => "Eyes";
    public IPhase Phase => DsrZone.P4;
    public bool SupportsSolo => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new DsrP4EyesAi()];

    public void DrawSettings() => settingsWindow.Draw();
    public object SettingsOverrides => settingsWindow.Overrides;
    private readonly DsrP4EyesSettingsWindow settingsWindow = new();

    // Players can't trade tethers until about two seconds after Hatebound hands them out.
    private const float SwapsOpenAt = 21.3f;
    // UNVERIFIED: how close two players come to trade tethers; they have to all but stand on each
    // other. The trade goes through a moment after they touch, even if they part again.
    private const float SwapRadius = 0.3f;
    private const float SwapDelay = 0.5f;
    private const float BoundAndDeterminedSeconds = 3f;
    // Each colour's orbs grow ready together; a ready orb pops on the first player to touch it,
    // hitting everyone around the orb, and bursts over the whole arena at its deadline if nobody
    // has. An Azure orb can also be popped before it is ready by standing right on it, which is
    // left out.
    // Retail grows an orb when it becomes ready; how is unknown. A 0x5FB transform to BNpcState 50
    // crashes the client on these orbs, so each is swapped for one spawned at ModelScale row 8
    // (scale 2, UNVERIFIED as retail's size), early enough that its pop cast isn't sent the frame
    // it spawns, which the client drops.
    private const byte ReadyOrbModelScale = 8;
    private const float OrbGrowLead = 0.3f;
    private const float GildedReadyAt = 25.9f;
    private const float GildedDeadline = 28.6f;
    private const float AzureReadyAt = 32.3f;
    private const float AzureDeadline = 34.6f;
    // UNVERIFIED: the touch distance; a player waiting beside an orb doesn't pop it when it grows.
    private const float PopRadius = 1.5f;
    private const float OrbLingerAfterPop = 1.5f;

    private static readonly Vector3 WaymarkOne = new(0f, 0f, 9.5f);

    private static readonly Vector3[] GildedOrbs = [new(17f, 0f, 0f), new(-17f, 0f, 0f)];
    private static readonly Vector3[] AzureOrbs = [new(10f, 0f, -7f), new(10f, 0f, 7f), new(-10f, 0f, -7f), new(-10f, 0f, 7f)];

    private SimWorld world = null!;
    private DsrP4EyesState state = null!;
    private SimEnemy? leftEye;
    private SimEnemy? rightEye;
    private SimEnemy? estinien;
    private SimEnemy? haurchefant;
    private SimEnemy? ysayle;
    private readonly List<SimEnemy> orbs = new();
    private readonly Dictionary<SimCharacter, SimTether> tethers = new();
    private readonly HashSet<SimCharacter> swapping = new();
    private readonly List<SimEnemy> diveClones = new();

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        state = new DsrP4EyesState(world.Rng, settingsWindow.Overrides);
        leftEye = rightEye = estinien = haurchefant = ysayle = null;
        orbs.Clear();
        tethers.Clear();
        swapping.Clear();
        diveClones.Clear();
        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<DsrP4EyesState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0.00f, PlacePartyOnWaymarkOne);
        world.Events.Add(0.00f, SpawnEyesAndAllies);
        world.Events.Add(0.30f, () => leftEye?.Cast(ActionId.EyesAppear));
        world.Events.Add(0.30f, () => rightEye?.Cast(ActionId.EyesAppear));
        world.Events.Add(2.94f, Engage);
        world.Events.Add(2.98f, GrantSouls);
        world.Events.Add(6.00f, DismissAllies);
        world.Events.Add(7.20f, EyeAutoAttacks);
        world.Events.Add(10.26f, () => estinien?.Cast(Actions.Resentment));
        world.Events.Add(10.30f, EyeAutoAttacks);

        world.Events.Add(15.63f, () => leftEye?.Cast(ActionId.Hatebound, animationLock: 1f));
        world.Events.Add(15.63f, () => rightEye?.Cast(ActionId.Hatebound, animationLock: 1f));
        world.Events.Add(18.61f, SpawnOrbs);
        world.Events.Add(19.37f, BindTethers);
        world.Events.Add(22.80f, EyeAutoAttacks);
        world.Events.Add(GildedReadyAt - OrbGrowLead, () => GrowOrbs(BNpcBaseId.GildedPrice, BNpcNameId.GildedPrice));
        world.Events.Add(26.00f, EyeAutoAttacks);
        world.Events.Add(GildedDeadline, () => BurstUnpopped(BNpcBaseId.GildedPrice, Actions.FlareNovaUnpopped));
        world.Events.Add(29.10f, EyeAutoAttacks);
        world.Events.Add(AzureReadyAt - OrbGrowLead, () => GrowOrbs(BNpcBaseId.AzurePrice, BNpcNameId.AzurePrice));
        world.Events.Add(32.30f, EyeAutoAttacks);
        world.Events.Add(AzureDeadline, () => BurstUnpopped(BNpcBaseId.AzurePrice, Actions.FlareStarUnpopped));
        world.Events.Add(35.40f, EyeAutoAttacks);
        world.Events.Add(38.50f, EyeAutoAttacks);

        world.Events.Add(41.71f, () => leftEye?.Cast(ActionId.MirageDiveCast, animationLock: 1f));
        world.Events.Add(41.71f, () => rightEye?.Cast(ActionId.MirageDiveCast, animationLock: 1f));
        world.Events.Add(45.20f, SpawnDiveClones);
        world.Events.Add(45.51f, () => MirageDive(0));
        world.Events.Add(46.60f, DismissDiveClones);
        world.Events.Add(49.00f, EyeAutoAttacks);
        world.Events.Add(50.32f, SpawnDiveClones);
        world.Events.Add(50.63f, () => MirageDive(1));
        world.Events.Add(51.70f, DismissDiveClones);
        world.Events.Add(52.10f, EyeAutoAttacks);
        world.Events.Add(55.20f, EyeAutoAttacks);
        world.Events.Add(55.44f, SpawnDiveClones);
        world.Events.Add(55.75f, () => MirageDive(2));
        world.Events.Add(56.85f, DismissDiveClones);
        world.Events.Add(58.40f, EyeAutoAttacks);
        world.Events.Add(60.56f, SpawnDiveClones);
        world.Events.Add(60.87f, () => MirageDive(3));
        world.Events.Add(61.50f, EyeAutoAttacks);
        world.Events.Add(61.97f, DismissDiveClones);

        world.Events.Add(64.71f, () => rightEye?.Cast(Actions.SteepInRage));
        world.Events.Add(72.00f, ReleaseTethers);
        world.Events.Add(74.00f, DespawnAll);
    }

    public void Tick(float delta, float elapsed)
    {
        if (elapsed >= SwapsOpenAt) SwapTouchingTethers();
        PopTouchedOrbs(elapsed);
    }

    // Damage a Fangbound player takes heals the right eye, which the DPS check can't afford.
    public void OnActionResolved(ActionResolved resolved)
    {
        if (resolved.Action != Actions.FlareNova && resolved.Action != Actions.FlareStar && resolved.Action != Actions.MirageDive) return;
        if (resolved.Hits.Any(hit => hit.HasStatus(StatusId.Fangbound)))
            world.Fail($"{Natives.Data.ActionName(resolved.Action.ActionId)} hit someone Fangbound, healing the right eye.");
    }

    // P4 opens with the whole party on waymark 1, south of the middle, not at the entrance.
    private void PlacePartyOnWaymarkOne()
    {
        foreach (var member in world.Party.ActiveMembers().ToList())
            if (member.IsAlive()) member.TeleportTo(new Placement(WaymarkOne, MathF.PI));
    }

    private SimEnemy? Spawn(uint baseId, uint nameId, Vector3 position, EnemyListMode enemyList = EnemyListMode.Never,
        SpawnVisibility visibility = SpawnVisibility.Visible, NpcAppearance? appearance = null, bool isHostile = true,
        byte modelScale = 0) =>
        world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: baseId,
            NameId: nameId,
            Level: Level,
            Targetable: false,
            EnemyList: enemyList,
            Visibility: visibility,
            IsHostile: isHostile,
            Appearance: appearance,
            ModelScale: modelScale,
            Placement: new Placement(position, 0f).Face(Vector3.Zero)));

    private void SpawnEyesAndAllies()
    {
        leftEye = Spawn(BNpcBaseId.LeftEye, BNpcNameId.LeftEye, Geometry.LeftEye, EnemyListMode.Always);
        rightEye = Spawn(BNpcBaseId.RightEye, BNpcNameId.RightEye, Geometry.RightEye, EnemyListMode.Always);
        estinien = Spawn(BNpcBaseId.Estinien, BNpcNameId.Estinien, Vector3.Zero, appearance: Appearance.Estinien);
        // Hostile, unlike retail's allies: the client draws no Soul puddle for a friendly caster.
        // Preloading the puddles' timelines (9853, 9854) is refused by the client and didn't help.
        haurchefant = Spawn(BNpcBaseId.Haurchefant, BNpcNameId.Haurchefant, new Vector3(-3f, 0f, 2f), appearance: Appearance.Haurchefant);
        ysayle = Spawn(BNpcBaseId.Ysayle, BNpcNameId.Ysayle, new Vector3(3f, 0f, 2f), appearance: Appearance.Ysayle);
    }

    private void Engage()
    {
        leftEye?.SetTargetable(true);
        rightEye?.SetTargetable(true);
    }

    private void GrantSouls()
    {
        if (SoulTarget(state.FriendshipTarget) is { } friendship) haurchefant?.Cast(Actions.SoulOfFriendship, friendship);
        if (SoulTarget(state.DevotionTarget) is { } devotion) ysayle?.Cast(Actions.SoulOfDevotion, devotion);
    }

    private SimCharacter? SoulTarget(PartyRole rolled) =>
        world.Party.Get(rolled) is { } member && member.IsAlive() ? member : world.Party.Find.Closest(WaymarkOne);

    // UNVERIFIED: when Haurchefant and Ysayle leave.
    private void DismissAllies()
    {
        haurchefant?.Despawn();
        ysayle?.Despawn();
        haurchefant = ysayle = null;
    }

    // The right eye keeps to the main tank, the left eye to the off tank.
    private void EyeAutoAttacks()
    {
        if (world.Party.Get(PartyRole.MainTank) is { } mainTank && mainTank.IsAlive()) rightEye?.Cast(Actions.EyeAutoAttack, mainTank);
        if (world.Party.Get(PartyRole.OffTank) is { } offTank && offTank.IsAlive()) leftEye?.Cast(Actions.EyeAutoAttack, offTank);
    }

    // UNVERIFIED: the orbs' spawn time, taken as Hatebound's end.
    private void SpawnOrbs()
    {
        foreach (var at in GildedOrbs)
            if (Spawn(BNpcBaseId.GildedPrice, BNpcNameId.GildedPrice, at) is { } orb) orbs.Add(orb);
        foreach (var at in AzureOrbs)
            if (Spawn(BNpcBaseId.AzurePrice, BNpcNameId.AzurePrice, at) is { } orb) orbs.Add(orb);
    }

    private void BindTethers()
    {
        foreach (var role in Enum.GetValues<PartyRole>())
            if (world.Party.Get(role) is { } member && member.IsAlive())
                Bind(member, state.ClawboundAtStart.Contains(role));
    }

    private void Bind(SimCharacter member, bool clawbound)
    {
        if (tethers.Remove(member, out var old)) old.Despawn();
        member.RemoveStatus(clawbound ? StatusId.Fangbound : StatusId.Clawbound);
        member.AddStatus(clawbound ? StatusId.Clawbound : StatusId.Fangbound);
        tethers[member] = world.Tether(member, clawbound ? leftEye : rightEye, clawbound ? TetherId.Clawbound : TetherId.Fangbound);
    }

    private static bool CanSwap(SimCharacter member) => member.IsAlive() && !member.HasStatus(StatusId.BoundAndDetermined);

    private void SwapTouchingTethers()
    {
        var free = tethers.Keys.Where(m => CanSwap(m) && !swapping.Contains(m)).ToList();
        foreach (var claw in free.Where(m => m.HasStatus(StatusId.Clawbound)))
        {
            var fang = free
                .Where(m => m.HasStatus(StatusId.Fangbound) && !swapping.Contains(m) && FlatDistance(m.Position, claw.Position) <= SwapRadius)
                .OrderBy(m => FlatDistance(m.Position, claw.Position))
                .FirstOrDefault();
            if (fang == null) continue;
            swapping.Add(claw);
            swapping.Add(fang);
            world.Events.Add(SwapDelay, () => TradeTethers(claw, fang));
        }
    }

    private void TradeTethers(SimCharacter claw, SimCharacter fang)
    {
        swapping.Remove(claw);
        swapping.Remove(fang);
        if (!tethers.ContainsKey(claw) || !tethers.ContainsKey(fang) || !CanSwap(claw) || !CanSwap(fang)
            || !claw.HasStatus(StatusId.Clawbound) || !fang.HasStatus(StatusId.Fangbound)) return;
        Bind(claw, clawbound: false);
        Bind(fang, clawbound: true);
        claw.AddStatus(StatusId.BoundAndDetermined, BoundAndDeterminedSeconds);
        fang.AddStatus(StatusId.BoundAndDetermined, BoundAndDeterminedSeconds);
    }

    private void GrowOrbs(uint baseId, uint nameId)
    {
        for (var i = 0; i < orbs.Count; i++)
        {
            if (orbs[i].BNpcBaseId != baseId) continue;
            var small = orbs[i];
            if (Spawn(baseId, nameId, small.Position, modelScale: ReadyOrbModelScale) is not { } grown) continue;
            small.Despawn();
            orbs[i] = grown;
        }
    }

    private void PopTouchedOrbs(float elapsed)
    {
        foreach (var orb in orbs.ToList())
        {
            var gilded = orb.BNpcBaseId == BNpcBaseId.GildedPrice;
            if (elapsed < (gilded ? GildedReadyAt : AzureReadyAt)) continue;
            if (world.Party.Find.Closest(orb.Position) is not { } popper || !popper.IsAlive()
                || FlatDistance(popper.Position, orb.Position) > PopRadius) continue;

            orbs.Remove(orb);
            orb.Cast(gilded ? Actions.FlareNova : Actions.FlareStar);
            world.Events.Add(OrbLingerAfterPop, orb.Despawn);
        }
    }

    private void BurstUnpopped(uint baseId, EnemyAction burst)
    {
        var unpopped = orbs.Where(o => o.BNpcBaseId == baseId).ToList();
        if (unpopped.Count == 0) return;
        foreach (var orb in unpopped)
        {
            orbs.Remove(orb);
            orb.Cast(burst);
            world.Events.Add(OrbLingerAfterPop, orb.Despawn);
        }
        world.Fail($"{Natives.Data.ActionName(burst.ActionId)} went unpopped and burst over the whole arena.");
    }

    // The client drops a cast sent the frame an actor spawns, so each dive's clone is spawned
    // hidden a moment early and shown by the dive's own animation.
    private void SpawnDiveClones()
    {
        for (var i = 0; i < 2; i++)
            if (Spawn(BNpcBaseId.NidhoggClone, BNpcNameId.Nidhogg, Vector3.Zero, visibility: SpawnVisibility.HiddenUntilShown) is { } clone)
                diveClones.Add(clone);
    }

    private void MirageDive(int dive)
    {
        var targets = state.DivePriority[dive]
            .Select(role => world.Party.Get(role))
            .Where(m => m is { } member && member.IsAlive() && member.HasStatus(StatusId.Clawbound))
            .Cast<SimCharacter>()
            .Take(diveClones.Count)
            .ToList();
        for (var i = 0; i < targets.Count; i++)
        {
            diveClones[i].TeleportTo(new Placement(targets[i].Position, targets[i].Rotation));
            diveClones[i].Cast(Actions.MirageDive, targets[i]);
        }
    }

    private void DismissDiveClones()
    {
        foreach (var clone in diveClones) clone.Despawn();
        diveClones.Clear();
    }

    private void ReleaseTethers()
    {
        foreach (var (member, tether) in tethers)
        {
            tether.Despawn();
            member.RemoveStatus(StatusId.Clawbound);
            member.RemoveStatus(StatusId.Fangbound);
            member.RemoveStatus(StatusId.BoundAndDetermined);
        }
        tethers.Clear();
        swapping.Clear();
    }

    private void DespawnAll()
    {
        ReleaseTethers();
        foreach (var orb in orbs) orb.Despawn();
        orbs.Clear();
        DismissDiveClones();
        DismissAllies();
        leftEye?.Despawn();
        rightEye?.Despawn();
        estinien?.Despawn();
        leftEye = rightEye = estinien = null;
    }

    private static float FlatDistance(Vector3 a, Vector3 b) =>
        Vector2.Distance(new Vector2(a.X, a.Z), new Vector2(b.X, b.Z));
}
