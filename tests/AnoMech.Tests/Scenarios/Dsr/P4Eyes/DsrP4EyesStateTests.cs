using System.Text.Json;
using AnoMech.Core.Game.Party;
using AnoMech.Multiplayer;
using AnoMech.Scenarios;
using AnoMech.Scenarios.Dsr.P4Eyes;

namespace AnoMech.Tests;

public class DsrP4EyesStateTests
{
    [Test]
    public void HostRollSurvivesTheWireAndReplaysAsDealt()
    {
        foreach (var seed in Enumerable.Range(0, 20))
        {
            var host = new DsrP4EyesState(new Rng(seed), new DsrP4EyesStateOverrides());
            var msg = (DsrP4EyesAiReplayStateMessage)JsonSerializer.Deserialize<MpMessage>(JsonSerializer.Serialize<MpMessage>(ReplayMessage(host)))!;
            var replayed = DsrP4EyesState.FromNetworkReplay(msg.ClawboundAtStart, msg.DivePriority, msg.FriendshipTarget, msg.DevotionTarget);
            Assert.That(replayed, Is.Not.Null, $"seed {seed}");
            Assert.That(replayed!.ClawboundAtStart, Is.EquivalentTo(host.ClawboundAtStart), $"seed {seed}");
            Assert.That(replayed.DivePriority, Is.EqualTo(host.DivePriority), $"seed {seed}");
            Assert.That(replayed.FriendshipTarget, Is.EqualTo(host.FriendshipTarget), $"seed {seed}");
            Assert.That(replayed.DevotionTarget, Is.EqualTo(host.DevotionTarget), $"seed {seed}");
        }
    }

    [Test]
    public void ReplayRefusesADealTheFightCantMake()
    {
        var msg = ReplayMessage(new DsrP4EyesState(new Rng(1), new DsrP4EyesStateOverrides()));
        Assert.That(DsrP4EyesState.FromNetworkReplay(msg.ClawboundAtStart.Take(3).ToArray(), msg.DivePriority, msg.FriendshipTarget, msg.DevotionTarget), Is.Null);
        Assert.That(DsrP4EyesState.FromNetworkReplay(msg.ClawboundAtStart, msg.DivePriority.Take(3).ToArray(), msg.FriendshipTarget, msg.DevotionTarget), Is.Null);
        var repeated = msg.DivePriority.Select(p => p.ToArray()).ToArray();
        repeated[0][1] = repeated[0][0];
        Assert.That(DsrP4EyesState.FromNetworkReplay(msg.ClawboundAtStart, repeated, msg.FriendshipTarget, msg.DevotionTarget), Is.Null);
    }

    private static DsrP4EyesAiReplayStateMessage ReplayMessage(DsrP4EyesState s)
        => new(s.ClawboundAtStart.ToArray(), s.DivePriority.Select(p => p.ToArray()).ToArray(), s.FriendshipTarget, s.DevotionTarget);
}
