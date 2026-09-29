using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using AnoMech.Core.Game;

namespace AnoMech.Scenarios.Ucob.P4Adds;

public static class UcobP4AddsConstants
{
    public static class Geometry
    {
        // P1 drops a Neurolink under Twintania at each HP threshold, and the NA strat parks her on
        // waymarks 1/2/3 for them, so the links sit on the Aether Markers.
        public static readonly IReadOnlyList<Vector3> Neurolinks =
        [
            MarkerOffset(WaymarkSlot.One),
            MarkerOffset(WaymarkSlot.Two),
            MarkerOffset(WaymarkSlot.Three),
        ];

        // UNVERIFIED: not in any sheet; bracketed by where the Neurolink status comes and goes.
        public const float NeurolinkRadius = 2f;

        public static readonly Vector3 BahamutSpawn = new(0f, 0f, -24f);
        public static readonly Vector3 TwintaniaSpawn = new(-5.5f, 0f, -11f);
        public static readonly Vector3 NaelSpawn = new(5.5f, 0f, -11f);

        // Self-centred AOEs reach EffectRange past the caster's hitbox edge, so a 6y Iron
        // Chariot hits out to 8.55y from Nael's centre.
        public const float NaelHitboxRadius = 2.55f;
        public const float TwintaniaHitboxRadius = 3.96f;
        public const float ChariotRadius = 6f + NaelHitboxRadius;
        // UNVERIFIED: the safe spot's size isn't in the sheet; assumed to match Chariot.
        public const float DynamoInnerRadius = 6f + NaelHitboxRadius;
        public const float DynamoOuterRadius = 22f + NaelHitboxRadius;
        public const float ThermionicBeamRadius = 4f;
        public const float RavenDiveRadius = 3f;
        public const float PlummetLength = 8f + TwintaniaHitboxRadius;
        public const float PlummetHalfAngle = 0.7854f;
        public const float LiquidHellRadius = 6f;
        // UNVERIFIED: Burns lands on players about 1-3.3y from a puddle's centre.
        public const float LiquidHellPuddleRadius = 3.5f;

        // Oviforms set off ~1.3s after Generate finishes, travel at ~4.7y/s, and detonate ~3y
        // short of their target.
        public const float HatchPopDistance = 3f;
        public const float OviformSpeed = 4.7f;

        // UNVERIFIED: the touch radius isn't in any sheet.
        public const float TwisterTriggerRadius = 1.5f;
        public const float TwisterKeepOutRadius = 2.4f;
        public const float TwisterKnockbackRadius = 8f;

        private static Vector3 MarkerOffset(WaymarkSlot slot) =>
            UcobConstants.AetherWaymarks.First(w => w.Slot == slot).Offset;
    }

    public static class ActionId
    {
        public const uint TwintaniaAttack = 0x26A7;
        public const uint Plummet = 0x26A8;
        public const uint DeathSentence = 0x26A9;
        public const uint Twister = 0x26AA;
        public const uint TwisterBurst = 0x26AB;
        public const uint LiquidHell = 0x26AD;
        public const uint Generate = 0x26AE;
        public const uint Hatch = 0x26AF;
        public const uint DeepHatch = 0x26B0;
        public const uint NaelAttack = 0x26B4;
        public const uint BahamutsClaw = 0x26B5;
        public const uint Ravensbeak = 0x26B6;
        public const uint Megaflare = 0x26BA;
        public const uint IronChariot = 0x26BB;
        public const uint LunarDynamo = 0x26BC;
        public const uint ThermionicBeam = 0x26BD;
        public const uint RavenDive = 0x26BE;
        public const uint BahamutsFavor = 0x26E8;
        public const uint Provoke = 7533;
    }

    public static class StatusId
    {
        public const ushort Burns = 250;
        public const ushort DamageUp = 290;
        // Held by anyone standing in a link; a Hatch that lands on a target without it is what
        // triggers Deep Hatch.
        public const ushort Neurolink = 344;
        public const ushort SlashingResistanceDownII = 1272;
        public const ushort ManaHypersensitivity = 1434;
        public const ushort PiercingResistanceDownII = 1435;
    }

    public static class LockonId
    {
        public const uint Hatch = 0x76;
    }

    public static class TetherId
    {
        public const ushort GuardingBahamut = 0x43;
    }

    public static class KnockbackId
    {
        public const uint TwisterBurst = 0x6F;
    }

    public static class ActionTimelineId
    {
        public const ushort WarpEnd = 7747;
        public const ushort WarpEnd2 = 7748;
        public const ushort BahamutDivineJudgmentPose = 3216;
    }

    // Server-spawned event objects. Each flips to state 1 right after spawning and back to 0
    // just before it is removed.
    public static class EObjId
    {
        public const uint LiquidHellPuddle = 2001150;
        public const uint Neurolink = 2001151;
        public const uint Twister = 2001168;
    }

    public const ushort EObjActiveState = 1;

    // vfx.lgb's sgvf_f1bz_b1297 SharedGroup ("BahTeraWall_on") is the fiery backdrop while
    // Bahamut charges Teraflare; nothing the client receives switches it, so the scenario plays
    // its timelines itself. PlayTimeline takes the timeline's position in the SGB (tera_off,
    // tera_on2off, tera_on, tera_off2on), not its id.
    public static class TeraflareWall
    {
        public const string Sgb = "bg/ffxiv/fst_f1/shared/for_vfx/sgvf_f1bz_b1297.sgb";
        public const uint Off = 0;
        public const uint On = 2;
        public const uint OffToOn = 3;
    }

    // Bahamut switches to this model state at the start of P4, just before his mon_sp016 pose.
    public const byte BahamutChargingModelState = 4;

    public static class Text
    {
        public const string NaelName = "Nael deus Darnus";
        public const string StandGuard = "O Bahamut! We shall stand guard as you make ready your divine judgment!";
    }
}
