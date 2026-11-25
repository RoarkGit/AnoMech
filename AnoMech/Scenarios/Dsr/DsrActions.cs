using System;
using AnoMech.Core.EnemyActions;
using AnoMech.Core.Native.Interfaces;
using static AnoMech.Core.EnemyActions.Distribution;
using static AnoMech.Core.EnemyActions.EnemyActionEffects;
using static AnoMech.Core.EnemyActions.Severity;
using static AnoMech.Scenarios.Dsr.DsrConstants;

namespace AnoMech.Scenarios.Dsr;

public static class DsrActions
{
    // A jump or a tower on someone still burning from the last one kills them.
    private static readonly VulnSpec Burning =
        VulnerableTo(StatusId.FireResistanceDownII).VulnerableTo(StatusId.PhysicalVulnerabilityUp);

    public static readonly EnemyAction AutoAttack = new(ActionId.NidhoggAutoAttack)
    {
        Effects = [Damage()],
        Timing = new() { DamageDelay = 0.8f },
    };

    public static readonly EnemyAction FinalChorus = new(ActionId.FinalChorusRaidwide)
    {
        Effects = [Damage()],
        Timing = new() { DamageDelay = 0.18f },
    };

    private static EnemyAction Jump(uint actionId) => new(actionId)
    {
        Effects =
        [
            Damage(Burning),
            ApplyStatus(StatusId.FireResistanceDownII, 12f),
            ApplyStatus(StatusId.PhysicalVulnerabilityUp, 3f),
        ],
        Timing = new() { DamageDelay = 0.58f },
        DeathExplanation = "hit by a jump while still burning from a jump or tower",
    };

    public static readonly EnemyAction DarkHighJump = Jump(ActionId.DarkHighJump);
    public static readonly EnemyAction DarkSpineshatterDive = Jump(ActionId.DarkSpineshatterDive);
    public static readonly EnemyAction DarkElusiveJump = Jump(ActionId.DarkElusiveJump);

    private static readonly IEnemyActionEffect[] TowerEffects =
    [
        Damage(Burning),
        ApplyStatus(StatusId.FireResistanceDownII, 12f),
        ApplyStatus(StatusId.PhysicalVulnerabilityUp, 7f),
    ];

    public static readonly EnemyAction DarkdragonDive = new(ActionId.DarkdragonDive)
    {
        Effects = TowerEffects,
        Timing = new() { DamageDelay = 0.79f },
        DeathExplanation = "soaked a tower while still burning from a jump or tower",
    };

    public static readonly EnemyAction[] DarkdragonDiveForSoakers =
    [
        .. Array.ConvertAll(ActionId.DarkdragonDiveForSoakers, id => new EnemyAction(id)
        {
            Effects = TowerEffects,
            Timing = new() { DamageDelay = 0.79f },
            DeathExplanation = "soaked a tower while still burning from a jump or tower",
        }),
    ];

    public static readonly EnemyAction DarkdragonDiveFail = new(ActionId.DarkdragonDiveFail)
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "a tower went unsoaked",
    };

    // Four sharing it is the fewest who survive.
    public static readonly EnemyAction EyeOfTheTyrant = new(ActionId.EyeOfTheTyrant)
    {
        Effects = [Damage(split: Stack(4))],
        Timing = new() { DamageDelay = 0.8f },
    };

    public static readonly EnemyAction GnashingWheel = new(ActionId.GnashingWheel)
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "inside Gnashing Wheel; get out",
    };

    public static readonly EnemyAction LashingWheel = new(ActionId.LashingWheel)
    {
        Area = new() { Size = 8f },
        Effects = [Damage(Lethal)],
        DeathExplanation = "outside Lashing Wheel; get in",
    };

    public static readonly EnemyAction Geirskogul = new(ActionId.Geirskogul)
    {
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 1.19f },
        // Retail shows the line only for a moment as the cast ends; the clone's wind-up is the tell.
        Cast = new() { OmenDelay = 4f },
        DeathExplanation = "in a Geirskogul line; each is aimed at whoever is closest to its clone when it starts",
    };

    // Sent with no cast bar, so the client draws no omen; the scenario fires it when the bar would end.
    public static readonly EnemyAction DrachenlanceCleave = new(ActionId.DrachenlanceCleave)
    {
        Cast = new() { CastSecondsOverride = 0f },
        Area = new() { Size = MathF.PI / 4f },
        Effects = [Damage(Lethal)],
        DeathExplanation = "in front of Nidhogg for Drachenlance",
    };

    public static readonly EnemyAction SoulTether = new(ActionId.SoulTether)
    {
        Effects = [Damage(TankBuster)],
        Timing = new() { DamageDelay = 1.16f },
        DeathExplanation = "Soul Tether is a tank buster on whoever holds the tether",
    };

    public static readonly EnemyAction SoulOfFriendship = new(ActionId.SoulOfFriendship)
    {
        Effects = [ApplyStatus(StatusId.SoulOfFriendship, 90f)],
        Timing = new() { DamageDelay = 0f },
    };

    public static readonly EnemyAction SoulOfDevotion = new(ActionId.SoulOfDevotion)
    {
        Effects = [ApplyStatus(StatusId.SoulOfDevotion, 90f)],
        Timing = new() { DamageDelay = 0f },
    };

    // Each orb burns whoever it hits; a second orb on someone still burning kills them.
    private static readonly VulnSpec Scorched = VulnerableTo(StatusId.FireResistanceDownII);

    public static readonly EnemyAction EyeAutoAttack = new(ActionId.EyeAutoAttack)
    {
        Effects = [Damage()],
        Timing = new() { DamageDelay = 0.8f },
    };

    public static readonly EnemyAction Resentment = new(ActionId.Resentment)
    {
        Effects = [Damage(), ApplyStatus(StatusId.Bleeding, 6f)],
        Timing = new() { DamageDelay = 1.3f },
    };

    public static readonly EnemyAction FlareNova = new(ActionId.FlareNova)
    {
        Effects = [Damage(Scorched), ApplyStatus(StatusId.FireResistanceDownII, 12f)],
        Timing = new() { DamageDelay = 0.8f },
        DeathExplanation = "hit by a second orb while still burning from the first",
    };

    public static readonly EnemyAction FlareStar = new(ActionId.FlareStar)
    {
        Effects = [Damage(Scorched), ApplyStatus(StatusId.FireResistanceDownII, 12f)],
        Timing = new() { DamageDelay = 0.27f },
        DeathExplanation = "hit by a second orb while still burning from the first",
    };

    private static EnemyAction UnpoppedOrb(uint actionId) => new(actionId)
    {
        Effects = [Damage(Scorched), ApplyStatus(StatusId.FireResistanceDownII, 12f)],
        Timing = new() { DamageDelay = 0.8f },
        DeathExplanation = "an orb went unpopped and burst over the whole arena while you were still burning",
    };

    public static readonly EnemyAction FlareNovaUnpopped = UnpoppedOrb(ActionId.FlareNovaUnpopped);
    public static readonly EnemyAction FlareStarUnpopped = UnpoppedOrb(ActionId.FlareStarUnpopped);

    public static readonly EnemyAction MirageDive = new(ActionId.MirageDive)
    {
        Effects = [Damage(VulnerableTo(StatusId.PiercingResistanceDown)), ApplyStatus(StatusId.PiercingResistanceDown, 13f)],
        Timing = new() { DamageDelay = 0.58f },
        DeathExplanation = "hit by a Mirage Dive while still pierced by an earlier one",
    };

    public static readonly EnemyAction SteepInRage = new(ActionId.SteepInRage)
    {
        Effects = [Damage()],
        Timing = new() { DamageDelay = 0.94f },
    };

    public static readonly EnemyAction TwistingDive = new(ActionId.TwistingDive)
    {
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.5f },
        DeathExplanation = "stood in Vedrfolnir's Twisting Dive through the middle",
    };

    // Only one Twister at a time catches a player; it throws them out of the fight.
    public static readonly EnemyAction TwisterBurst = new(ActionId.Twister)
    {
        Area = new() { CastTypeOverride = CastType.SingleTarget },
        Effects = [Damage(Lethal)],
        DeathExplanation = "walked into a Twister; step off your spot as Twisting Dive lands and don't walk back through it",
    };

    private static readonly IEnemyActionEffect[] MagicVulnOnTarget =
    [
        OnTarget(Damage(VulnerableTo(StatusId.MagicVulnerabilityUp)), ApplyStatus(StatusId.MagicVulnerabilityUp, 12f)),
    ];

    public static readonly EnemyAction SkywardLeap = new(ActionId.SkywardLeap)
    {
        Effects = [.. MagicVulnOnTarget, OnOthers(Damage(Lethal))],
        Timing = new() { DamageDelay = 0.5f },
        DeathExplanation = "caught in Skyward Leap; its target takes it alone, far from everyone",
    };

    public static readonly EnemyAction SpiralPierce = new(ActionId.SpiralPierce)
    {
        Effects = [.. MagicVulnOnTarget, OnOthers(Damage(Lethal))],
        Timing = new() { DamageDelay = 0.63f },
        DeathExplanation = "caught in a Spiral Pierce line; only the tethered player belongs in it",
    };

    // UNVERIFIED: the half-angle. Neighbours 20° apart around Thordan are never both hit.
    public static readonly EnemyAction AscalonsMercyRevealedCone = new(ActionId.AscalonsMercyRevealedCone)
    {
        Area = new() { Size = 15f * MathF.PI / 180f },
        Effects = [Damage(VulnerableTo(StatusId.PhysicalVulnerabilityUp)), ApplyStatus(StatusId.PhysicalVulnerabilityUp, 3f)],
        Timing = new() { DamageDelay = 0.4f },
        DeathExplanation = "took two Ascalon's Mercy cones; spread so each cone from Thordan hits only its own target",
    };

    public static readonly EnemyAction AltarFlarePuddle = new(ActionId.AltarFlarePuddle)
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "stood in Altar Flare; each one lands where its target stood as it was cast, so keep moving",
    };

    public static readonly EnemyAction LiquidHeaven = new(ActionId.LiquidHeaven)
    {
        Cast = new() { AnimationLock = 0.1f },
        Effects = [Damage()],
        Timing = new() { DamageDelay = 2.2f },
    };

    public static readonly EnemyAction CauterizeDarkscale = new(ActionId.CauterizeDarkscale)
    {
        Effects = [Damage(Lethal)],
        DeathExplanation = "hit by Cauterize; the dragons dive at where the marked player stood as they began",
    };

    public static readonly EnemyAction CauterizeVidofnir = CauterizeDarkscale with { ActionId = ActionId.CauterizeVidofnir };

    public static readonly EnemyAction EmptyDimension = new(ActionId.EmptyDimension)
    {
        Area = new() { Size = 6f },
        Effects = [Damage(Lethal)],
        Timing = new() { DamageDelay = 0.5f },
        DeathExplanation = "outside Empty Dimension's safe spot; stand close to Ser Grinnaux",
    };

    public static readonly EnemyAction ChainLightningBurst = new(ActionId.ChainLightningBurst)
    {
        Area = new() { CastTypeOverride = CastType.Circle },
        Effects = [OnTarget(Damage()), OnOthers(Damage(Lethal))],
        DeathExplanation = "too close to someone Thunderstruck when it went off",
    };
}
