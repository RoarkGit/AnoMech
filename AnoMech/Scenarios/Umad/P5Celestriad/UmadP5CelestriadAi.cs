using System.Linq;
using System.Numerics;
using AnoMech.Core.Game.Ai;
using AnoMech.Core.SimObjects;
using static AnoMech.Scenarios.Umad.P5Celestriad.UmadP5CelestriadConstants;

namespace AnoMech.Scenarios.Umad.P5Celestriad;

// First-pass bots for Celestriad: each doppel already "knows" its own permanent element (or
// free-pair role) from state and just runs to that set's matching tower, splitting the pair
// 1y either side of the tower's tangent so both fit inside the soak radius. On Catastrophic
// Choice sets (0 and 2), bots first stack in the tower's centre (no inner/outer read yet, the
// telegraph hasn't happened) and only step to the safe half a second after the cast starts:
// Aero (green) -> away from the boss, Earth (brown) -> toward the boss.
public sealed class UmadP5CelestriadAi : IScenarioAi<UmadP5CelestriadState>
{
    public string Name => "Standard";

    private const float MoveSpeed = 6f;
    private const float PairOffset = 1f;
    private const float HalfOffset = 2f;
    // Wait until a tower is actually lit before sending anyone toward it. Moving early means a
    // bot is already standing on the spot when it activates, which reads as the tower spawning
    // under a player instead of on open ground.
    private const float MoveDelay = 1.5f;
    // How long after Catastrophic Choice's cast starts bots read the telegraph and reposition.
    private const float ChoiceReadDelay = 2f;

    public void Run(UmadP5CelestriadState state, SimWorld world)
    {
        var party = world.Party;
        var playerSlot = (int)party.PlayerRole;

        for (var set = 0; set < 3; set++)
        {
            var s = set;
            world.Events.Add(CelestriadTiming.TowerStart[s] + MoveDelay, () => PlaceSet(world, state, s, playerSlot, half: 0f));
            if (CelestriadTiming.CcAt[s] is { } cc)
                world.Events.Add(cc + ChoiceReadDelay, () => PlaceSet(world, state, s, playerSlot, HalfFor(state, s)));
        }
    }

    private static float HalfFor(UmadP5CelestriadState state, int set) =>
        state.AeroVariant[set] is { } aero ? (aero ? -1f : 1f) : 0f;

    private static void PlaceSet(SimWorld world, UmadP5CelestriadState state, int set, int playerSlot, float half)
    {
        var party = world.Party;
        foreach (var active in state.SetActiveTowers[set])
        {
            var tower = active.Tower;
            var roles = state.AssignedRoles(active, set).ToArray();

            var inward = Vector3.Normalize(-tower.Position);
            var lateral = new Vector3(-inward.Z, 0f, inward.X);

            for (var i = 0; i < roles.Length; i++)
            {
                if ((int)roles[i] == playerSlot) continue;
                var bot = party.Get(roles[i]);
                if (bot is null || !bot.IsAlive()) continue;

                var side = i == 0 ? -1f : 1f;
                var dest = tower.Position + lateral * (side * PairOffset) + inward * (half * HalfOffset);
                bot.MoveTo(dest, MoveSpeed);
            }
        }
    }
}
