using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.UmadConstants;
using static AnoMech.Scenarios.Umad.P5Celestriad.UmadP5CelestriadConstants;

namespace AnoMech.Scenarios.Umad.P5Celestriad;

// UMAD P5 "Celestriad": all 9 towers (3 each of Fire/Ice/Lightning) spawn once and stay for the
// whole mechanic; each of the 3 sets lights up 4 of them (2 single-element towers plus a doubled
// element's 2). Each party member is permanently debuffed with an element (two each) or left
// "free" (two more). A debuffed player's actual soak target cycles through all 3 elements across
// the 3 sets (UmadP5CelestriadState.ElementForSet), never their debuff element until the final
// set, so nobody soaks the same element twice. Free players always fill the doubled element's
// second active tower. Sets 0 and 2 (the 1st and 3rd soaks) each get a single Catastrophic
// Choice cast while their towers are lit, and that set resolves exactly when the cast completes;
// set 1 has no Catastrophic Choice and resolves independently in between.
//
// See UmadP5CelestriadConstants for what's replay-confirmed vs. still an estimate.
public sealed class UmadP5CelestriadScenario : IScenario
{
    public string Name => "Celestriad";
    public IPhase Phase => UmadZone.P5;
    public bool SupportsSolo => true;

    public IReadOnlyList<IScenarioAi> AiStrats => [new UmadP5CelestriadAi()];

    public void DrawSettings() => settingsWindow.Draw();
    private readonly UmadP5CelestriadSettingsWindow settingsWindow = new();

    private UmadP5CelestriadState state = null!;
    private SimWorld world = null!;
    private SimParty party = null!;
    private SimEnemy? kefka;
    private readonly Dictionary<(CelestriadElement, int), SimEventObject> towerObjects = new();
    private readonly Dictionary<(CelestriadElement, int), SimEventObject> activeOverlays = new();
    private readonly Dictionary<(CelestriadElement, int), SimEnemy> towerMarkers = new();

    public void Run(SimWorld worldParam, int? selectedAi)
    {
        world = worldParam;
        party = worldParam.Party;
        state = new UmadP5CelestriadState(party, settingsWindow.Overrides);
        towerObjects.Clear();
        activeOverlays.Clear();
        towerMarkers.Clear();

        if (selectedAi is { } idx && idx < AiStrats.Count)
            ((IScenarioAi<UmadP5CelestriadState>)AiStrats[idx]).Run(state, world);

        world.Events.Add(0f, SpawnKefka);
        world.Events.Add(CelestriadTiming.CelestriadCastAt,
            () => kefka?.Cast(CelestriadActionId.Celestriad, castSeconds: CelestriadTiming.CelestriadCastTime));
        world.Events.Add(CelestriadTiming.DebuffApplyAt, ApplyDebuffs);
        world.Events.Add(CelestriadTiming.TowerStart[0], SpawnAllTowers);

        for (var set = 0; set < 3; set++)
        {
            var s = set;
            world.Events.Add(CelestriadTiming.TowerStart[s], () => ActivateTowers(s));
            if (CelestriadTiming.CcAt[s] is { } cc) world.Events.Add(cc, () => LaunchChoice(s));
            world.Events.Add(CelestriadTiming.ResolveAt[s], () => ResolveSet(s));
            world.Events.Add(CelestriadTiming.DeactivateAt[s], () => DeactivateTowers(s));
        }

        var teardownAt = CelestriadTiming.DeactivateAt[2] + CelestriadTiming.TowerDespawnBuffer;
        world.Events.Add(teardownAt, DespawnAllTowers);
        world.Events.Add(teardownAt, () => kefka?.Despawn());
    }

    public void Tick(float delta, float elapsed) { }

    private void SpawnKefka()
    {
        kefka = world.SpawnEnemy(new EnemySpawnConfig(
            BNpcBaseId: BNpcBaseId.KefkaP5,
            NameId: BNpcNameId.Kefka,
            Level: 100,
            Targetable: true,
            EnemyList: EnemyListMode.Always,
            IsVisible: true,
            Placement: new Placement(Vector3.Zero, MathF.PI)));
    }

    // Silent: no cast or animation on the player when the initial debuff lands, just the status.
    private void ApplyDebuffs()
    {
        foreach (var (role, element) in state.PlayerDebuffElement)
        {
            if (element is not { } e) continue;
            party.Get(role)?.AddStatus(ElementDebuff(e), CelestriadTiming.DebuffDuration);
        }
    }

    // One cast per applicable set; that set resolves exactly when this cast completes. The
    // green donut / black circle is duty-scripted decoration in retail, not part of the
    // action's own release animation, so it needs its own telegraph spawn (matching
    // TopUtils.ResolveOpticalLaser's pattern for the same kind of scripted-visual gap) rather
    // than relying on the cast to produce it.
    private void LaunchChoice(int set)
    {
        if (state.AeroVariant[set] is not { } aero || kefka is null) return;
        var actionId = aero ? CelestriadActionId.CatastrophicChoiceAero : CelestriadActionId.CatastrophicChoiceEarth;
        kefka.Cast(actionId, castSeconds: CelestriadTiming.CatastrophicChoiceCastTime);
        world.SpawnActionOmen(actionId, Vector3.Zero, kefka.Rotation, CelestriadTiming.CatastrophicChoiceCastTime);
    }

    // Real tower props (EObjId per UmadP5CelestriadConstants), not a cast/omen. All 9 spawn once
    // at DormantState and are never touched again, so they can never flicker. "Activating" a
    // tower spawns a second, independent EObj at ActiveState layered on the same spot; the
    // underlying dormant tower stays exactly as it was underneath. Re-attaching a single EObj's
    // SG state in place (the original approach) caused a visible flicker on every transition;
    // spawning/despawning a fresh instance doesn't.
    //
    // Alongside each tower, spawn its invisible resolve-target marker (see PlayResolveEffect)
    // right away too and keep it alive for the whole mechanic. Despawning a marker within the
    // same tick its cast fires cuts the release VFX off before it renders (this is what a
    // per-resolve spawn/despawn was doing, since DeactivateTowers follows ResolveSet by only
    // ~0.1s), so the marker's lifetime has to be fully decoupled from any single set's resolve.
    private void SpawnAllTowers()
    {
        foreach (var tower in state.AllTowers)
        {
            var key = (tower.Element, tower.SubIndex);
            var eobj = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = TowerEObjId(tower.Element),
                Placement = new Placement(tower.Position, 0f),
                TimelineState = CelestriadTowerEObjId.DormantState,
            });
            if (eobj is not null) towerObjects[key] = eobj;

            var marker = world.SpawnEnemy(new EnemySpawnConfig(
                BNpcBaseId: BNpcBaseId.KefkaHelper,
                NameId: BNpcNameId.Kefka,
                Level: 1,
                Targetable: false,
                EnemyList: EnemyListMode.Never,
                IsVisible: false,
                Placement: new Placement(tower.Position, 0f)));
            if (marker is not null) towerMarkers[key] = marker;
        }
    }

    private void ActivateTowers(int set)
    {
        foreach (var active in state.SetActiveTowers[set])
        {
            var key = (active.Tower.Element, active.Tower.SubIndex);
            var overlay = world.SpawnEventObject(new EventObjectSpawnConfig
            {
                EObjId = TowerEObjId(active.Tower.Element),
                Placement = new Placement(active.Tower.Position, 0f),
                TimelineState = CelestriadTowerEObjId.ActiveState,
            });
            if (overlay is not null) activeOverlays[key] = overlay;
        }
    }

    private void DeactivateTowers(int set)
    {
        foreach (var active in state.SetActiveTowers[set])
        {
            var key = (active.Tower.Element, active.Tower.SubIndex);
            if (activeOverlays.Remove(key, out var overlay))
                overlay.Despawn();
        }
    }

    // Fireball / ice pillar / lightning strike burst on resolve, targeted dead-centre via the
    // tower's own persistent invisible marker (spawned once in SpawnAllTowers). EventObjects
    // (the towers themselves) don't resolve as valid single-target cast targets here, but a
    // real (BattleChara-based) actor does, the same way a player does. The marker casts on
    // itself rather than having Kefka cast at it: SimCast is one instance per caster, so a
    // cast from Kefka here would fight over the same cast state as his Catastrophic Choice
    // cast on Catastrophic Choice sets.
    private void PlayResolveEffect(CelestriadTower tower)
    {
        if (!towerMarkers.TryGetValue((tower.Element, tower.SubIndex), out var marker)) return;
        marker.Cast(ElementAction(tower.Element), castSeconds: 0f, targetId: marker.GameObjectId);
    }

    private void ResolveSet(int set)
    {
        var anyUnsoaked = false;
        foreach (var active in state.SetActiveTowers[set])
        {
            var tower = active.Tower;
            var inside = party.Find.InsideCircle(tower.Position, CelestriadGeometry.SoakRadius);
            if (inside.Count < 2)
            {
                anyUnsoaked = true;
                // Hard failure: whoever was assigned here dies, whether they showed up or not,
                // on top of the party-wide penalty below.
                foreach (var role in state.AssignedRoles(active, set))
                    party.Get(role)?.Die("Celestriad, tower unsoaked");
            }

            PlayResolveEffect(tower);

            // Soaking a tower gives its element's resistance-down, independent of whoever was
            // originally debuffed with it.
            foreach (var soaker in inside)
                soaker.AddStatus(ElementDebuff(tower.Element), CelestriadTiming.DebuffDuration);

            // Wrong-half Catastrophic Choice is a personal hit (death), not a party-wide
            // penalty; that's reserved for an unsoaked tower, checked above.
            if (state.AeroVariant[set] is { } aero)
                foreach (var p in inside)
                    if (!IsSafeHalf(p.Position, tower.Position, aero))
                        p.Die("Catastrophic Choice");
        }

        if (!anyUnsoaked) return;
        for (var i = 0; i < 8; i++)
        {
            var member = party.Get(i);
            if (member is null || !member.IsAlive()) continue;
            member.AddStatus(StatusId.DamageDown, CelestriadTiming.DamageDownDuration);
        }
    }

    private void DespawnAllTowers()
    {
        foreach (var eobj in towerObjects.Values) eobj.Despawn();
        towerObjects.Clear();
        foreach (var eobj in activeOverlays.Values) eobj.Despawn();
        activeOverlays.Clear();
        foreach (var marker in towerMarkers.Values) marker.Despawn();
        towerMarkers.Clear();
    }

    // Aero (green): safe half is away from the boss/centre. Earth (brown): safe half is toward
    // the boss/centre.
    private static bool IsSafeHalf(Vector3 playerPos, Vector3 towerPos, bool aero)
    {
        var inward = -Vector3.Normalize(towerPos);
        var dot = Vector3.Dot(playerPos - towerPos, inward);
        return aero ? dot < 0f : dot > 0f;
    }

    private static uint TowerEObjId(CelestriadElement e) => e switch
    {
        CelestriadElement.Fire => CelestriadTowerEObjId.Fire,
        CelestriadElement.Ice => CelestriadTowerEObjId.Ice,
        _ => CelestriadTowerEObjId.Lightning,
    };

    private static uint ElementAction(CelestriadElement e) => e switch
    {
        CelestriadElement.Fire => CelestriadActionId.FireIII,
        CelestriadElement.Ice => CelestriadActionId.BlizzardIII,
        _ => CelestriadActionId.ThunderIII,
    };

    private static ushort ElementDebuff(CelestriadElement e) => e switch
    {
        CelestriadElement.Fire => CelestriadStatusId.FireResistanceDownII,
        CelestriadElement.Ice => CelestriadStatusId.IceResistanceDownII,
        _ => StatusId.LightningResistanceDownII, // shared UmadConstants.StatusId, same value as ForsakenNull's local copy
    };
}
