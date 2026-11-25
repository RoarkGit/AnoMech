using System;
using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Dsr.P5Wrath;

public sealed class DsrP5WrathSettingsWindow
{
    public DsrP5WrathStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto"))
        {
            Overrides.Marker = null;
            Overrides.Thunderstruck = null;
            Overrides.GrinnauxOnVedrfolnirSide = null;
        }
        if (!SettingsGrid.Begin("##dsrp5wrath")) return;
        SettingsGrid.FightOnlyNote();
        SettingsGrid.Row("Your marker:");
        DrawChoices("marker", ["Skyward Leap", "Ignasse tether", "Vellguine tether", "Cauterize", "Liquid Heaven", "None"],
            [WrathMarker.SkywardLeap, WrathMarker.IgnasseTether, WrathMarker.VellguineTether, WrathMarker.Cauterize, WrathMarker.LiquidHeaven, WrathMarker.Unmarked],
            Overrides.Marker, v => Overrides.Marker = v);
        SettingsGrid.Row("Thunderstruck on you:");
        DrawChoices("thunder", ["Yes", "No"], [true, false], Overrides.Thunderstruck, v => Overrides.Thunderstruck = v);
        SettingsGrid.Row("Ser Grinnaux:");
        DrawChoices("grinnaux", ["Vedrfolnir's side", "Far side"], [true, false], Overrides.GrinnauxOnVedrfolnirSide,
            v => Overrides.GrinnauxOnVedrfolnirSide = v);
        SettingsGrid.End();
    }

    private static void DrawChoices<T>(string id, string[] labels, T[] values, T? current, Action<T?> set) where T : struct
    {
        if (ImGui.RadioButton($"Auto##{id}", current == null)) set(null);
        for (var i = 0; i < labels.Length; i++)
        {
            ImGui.SameLine();
            if (ImGui.RadioButton($"{labels[i]}##{id}{i}", current is { } c && c.Equals(values[i]))) set(values[i]);
        }
    }
}
