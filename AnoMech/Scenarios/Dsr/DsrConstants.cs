using System;
using System.Collections.Generic;
using System.Numerics;
using AnoMech.Core.Game;
using AnoMech.Core.SimObjects;

namespace AnoMech.Scenarios.Dsr;

public static class DsrConstants
{
    public const byte Level = 90;

    public static class Geometry
    {
        // P3 is a square: players reach 20y along either axis but 25y out to the corners.
        public const float P3ArenaHalfWidth = 21f;
        public static readonly Vector3 LeftEye = new(10f, 0f, 0f);
        public static readonly Vector3 RightEye = new(-10f, 0f, 0f);
        // UNVERIFIED: players never get past about 20.8y from the middle.
        public const float P5ArenaRadius = 21f;
    }

    // A-D on the cardinals, 1-4 on the intercardinals clockwise from NW.
    public static IReadOnlyList<Waymark> NaurWaymarks =>
    [
        new(WaymarkSlot.A,     new Vector3(     0f, 0f,  -9.500f)),
        new(WaymarkSlot.B,     new Vector3( 12.728f, 0f, -12.728f)),
        new(WaymarkSlot.C,     new Vector3(  9.500f, 0f,      0f)),
        new(WaymarkSlot.D,     new Vector3( 12.728f, 0f,  12.728f)),
        new(WaymarkSlot.One,   new Vector3(     0f, 0f,   9.500f)),
        new(WaymarkSlot.Two,   new Vector3(-12.728f, 0f,  12.728f)),
        new(WaymarkSlot.Three, new Vector3( -9.500f, 0f,      0f)),
        new(WaymarkSlot.Four,  new Vector3(-12.728f, 0f, -12.728f)),
    ];

    public static class BgmId
    {
        public const ushort Freefall = 922;
        public const ushort Contention = 923;
    }

    public static class BNpcBaseId
    {
        public const uint NidhoggP3 = 12605;
        public const uint NidhoggClone = 12606;
        public const uint AzurePrice = 12607;
        public const uint GildedPrice = 12608;
        public const uint LeftEye = 12609;
        public const uint RightEye = 12610;
        public const uint Haurchefant = 13117;
        public const uint Ysayle = 13118;
        public const uint Estinien = 13119;
        public const uint KingThordan = 12611;
        public const uint SerGrinnaux = 12602;
        public const uint SerCharibert = 12603;
        public const uint Darkscale = 12630;
        public const uint Vidofnir = 12631;
        public const uint SerVellguine = 12633;
        public const uint SerPaulecrain = 12634;
        public const uint SerIgnasse = 12635;
        public const uint Vedrfolnir = 12646;
    }

    public static class BNpcNameId
    {
        // The boss and its clones are both named Nidhogg.
        public const uint Nidhogg = 3458;
        // UNVERIFIED: the sheet has no DSR-era Haurchefant row; this is his oldest one.
        public const uint Haurchefant = 1455;
        public const uint Estinien = 11312;
        public const uint Ysayle = 11313;
        public const uint AzurePrice = 11315;
        public const uint GildedPrice = 11316;
        public const uint LeftEye = 11317;
        public const uint RightEye = 11318;
        public const uint KingThordan = 3632;
        public const uint SerVellguine = 3636;
        public const uint SerPaulecrain = 3637;
        public const uint SerIgnasse = 3638;
        public const uint SerGrinnaux = 3639;
        public const uint SerCharibert = 3642;
        public const uint Darkscale = 3983;
        public const uint Vedrfolnir = 3984;
        public const uint Vidofnir = 11314;
    }

    public static class ActionId
    {
        public const uint NidhoggAutoAttack = 0x6730;
        public const uint FinalChorus = 0x6708;
        public const uint FinalChorusRaidwide = 0x6709;
        public const uint Geirskogul = 0x670A;
        public const uint Drachenlance = 0x670B;
        public const uint DrachenlanceCleave = 0x670C;
        public const uint DiveFromGrace = 0x670D;
        public const uint DarkHighJump = 0x670E;
        public const uint DarkSpineshatterDive = 0x670F;
        public const uint DarkElusiveJump = 0x6710;
        public const uint DarkdragonDive = 0x6711;
        public const uint GnashAndLash = 0x6712;
        public const uint LashAndGnash = 0x6713;
        public const uint EyeOfTheTyrant = 0x6714;
        public const uint GnashingWheel = 0x6715;
        public const uint LashingWheel = 0x6716;
        public static readonly uint[] DarkdragonDiveForSoakers = [0x6717, 0x6718, 0x6719, 0x671A];
        public const uint DarkdragonDiveFail = 0x671B;
        public const uint SoulTether = 0x671C;
        public const uint RevengeOfTheHorde = 0x7436;

        public const uint EyesAppear = 0x717A;
        public const uint Resentment = 0x68BA;
        public const uint EyeAutoAttack = 0x68BB;
        public const uint SteepInRage = 0x68BD;
        public const uint Hatebound = 0x68BE;
        public const uint FlareStar = 0x68BF;
        public const uint FlareStarUnpopped = 0x68C0;
        public const uint FlareNova = 0x68C1;
        public const uint FlareNovaUnpopped = 0x68C2;
        public const uint MirageDiveCast = 0x68C3;
        public const uint MirageDive = 0x68C4;
        public const uint SoulOfFriendship = 0x68C5;
        public const uint SoulOfDevotion = 0x68C6;

        public const uint EmptyDimension = 0x62DA;
        public const uint AscalonsMercyRevealed = 0x63CA;
        public const uint AscalonsMercyRevealedCone = 0x63CB;
        public const uint AltarFlare = 0x63E4;
        public const uint AltarFlarePuddle = 0x63E5;
        public const uint WrathOfTheHeavens = 0x6B89;
        public const uint SpiralPierce = 0x6B8A;
        public const uint TwistingDive = 0x6B8B;
        public const uint Twister = 0x6B8C;
        public const uint CauterizeDarkscale = 0x6B8D;
        public const uint CauterizeVidofnir = 0x6B8E;
        public const uint ChainLightning = 0x6B8F;
        public const uint ChainLightningBurst = 0x6B90;
        public const uint LiquidHeaven = 0x6B91;
        public const uint SkywardLeap = 0x72A2;
    }

    public static class StatusId
    {
        public const ushort FirstInLine = 3004;
        public const ushort SecondInLine = 3005;
        public const ushort ThirdInLine = 3006;
        public const ushort HighJumpTarget = 2755;
        public const ushort SpineshatterDiveTarget = 2756;
        public const ushort ElusiveJumpTarget = 2757;
        public const ushort FireResistanceDownII = 2902;
        public const ushort PhysicalVulnerabilityUp = 2940;
        public const ushort SoulOfFriendship = 2748;
        public const ushort SoulOfDevotion = 2749;
        public const ushort Clawbound = 2775;
        public const ushort Fangbound = 2776;
        public const ushort BoundAndDetermined = 2777;
        public const ushort Bleeding = 2951;
        public const ushort PiercingResistanceDown = 3131;
        public const ushort Thunderstruck = 2833;
        public const ushort MagicVulnerabilityUp = 2941;
        public const ushort Burns = 2945;
    }

    public static class LockonId
    {
        public const uint FirstInLine = 0x13F;
        public const uint SecondInLine = 0x140;
        public const uint ThirdInLine = 0x141;
        public const uint SkywardLeap = 0x0E;
        public const uint Cauterize = 0x14;
    }

    public static class TetherId
    {
        public const ushort SoulTether = 84;
        public const ushort Fangbound = 51;
        public const ushort Clawbound = 52;
        public const ushort SpiralPierce = 5;
    }

    public static class EObjId
    {
        public const uint Twister = 2001168;
        // UNVERIFIED for DSR: UCOB's Liquid Hell puddle.
        public const uint LiquidHeavenPuddle = 2001150;
    }

    // Spawn-packet parts for P4's human models, from their BNpcBase rows' NpcEquip and
    // BNpcCustomize; without them the client draws a bare body.
    public static class Appearance
    {
        public static readonly NpcAppearance Haurchefant = new(MainHand: 0x5000100CA, OffHand: 0x200090065,
            Equipment: [0x0, 0x9C000F, 0xB6000B, 0x46000F, 0xA0000F],
            Customize: [0x02, 0x00, 0x01, 0x4B, 0x03, 0x02, 0x02, 0x01, 0x00, 0x92, 0x9A, 0x19, 0x60, 0x06, 0x02, 0x92, 0x02, 0x05, 0x03, 0x00, 0x00, 0x32, 0x01, 0x32]);
        public static readonly NpcAppearance Ysayle = new(MainHand: 0x1400010385,
            Equipment: [0x0, 0x1237C, 0x60078, 0x50077, 0x60078],
            Customize: [0x02, 0x01, 0x01, 0x32, 0x04, 0xCA, 0xCA, 0x01, 0x58, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x80, 0xC3, 0x32, 0x01, 0x32]);
        public static readonly NpcAppearance Estinien = new(MainHand: 0x100011F49,
            Equipment: [0x22391, 0x1239B, 0x1239B, 0x70024, 0x70024],
            Customize: [0x02, 0x00, 0x01, 0x32, 0x03, 0xD0, 0x05, 0x01, 0x4A, 0x06, 0xA1, 0x00, 0x00, 0x01, 0x02, 0x06, 0x81, 0x04, 0x00, 0x01, 0x00, 0x32, 0x01, 0x32, 0x00, 0x01]);
        public static readonly NpcAppearance KingThordan = new(MainHand: 0x1002805DD, Equipment: [0x0, 0x20001]);
        public static readonly NpcAppearance SerVellguine = new(MainHand: 0x4001401F5, Equipment: [0x10003, 0x10003, 0x0, 0x70001]);
        public static readonly NpcAppearance SerPaulecrain = new(MainHand: 0x8000501F5, Equipment: [0x20003, 0x20003, 0x0, 0x80001]);
        public static readonly NpcAppearance SerIgnasse = new(MainHand: 0x3000901F5, Equipment: [0x30003, 0x30003, 0x0, 0x90001]);
        public static readonly NpcAppearance SerGrinnaux = new(MainHand: 0x500010194, Equipment: [0x10002, 0x10002, 0x0, 0x40001]);
        public static readonly NpcAppearance SerCharibert = new(MainHand: 0x4000E03E9, Equipment: [0x10004, 0x10004, 0x0, 0xA0001]);
    }
}

// The eight compass points as indices, 0 = N clockwise to 7 = NW. Scenario-local, centred on
// the arena; north is -Z.
public static class Compass
{
    public const int N = 0, NE = 1, E = 2, SE = 3, S = 4, SW = 5, W = 6, NW = 7;

    public static float Bearing(int index) => index * MathF.PI / 4f;

    public static Vector3 Point(int index, float radius) => AtBearing(Bearing(index), radius);

    public static Vector3 AtBearing(float bearing, float radius) =>
        new(MathF.Sin(bearing) * radius, 0f, -MathF.Cos(bearing) * radius);

    public static float BearingOf(Vector3 position) => MathF.Atan2(position.X, -position.Z);
}
