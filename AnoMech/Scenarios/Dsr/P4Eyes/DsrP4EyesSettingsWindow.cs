using Dalamud.Bindings.ImGui;

namespace AnoMech.Scenarios.Dsr.P4Eyes;

public sealed class DsrP4EyesSettingsWindow
{
    public DsrP4EyesStateOverrides Overrides { get; } = new();

    public void Draw()
    {
        if (ImGui.Button("Auto")) Overrides.StartingTethers = null;
        if (SettingsGrid.Begin("##dsrp4eyes"))
        {
            SettingsGrid.FightOnlyNote();
            SettingsGrid.Row("Clawbound at start:");
            if (ImGui.RadioButton("Auto##claw", Overrides.StartingTethers == null)) Overrides.StartingTethers = null;
            ImGui.SameLine();
            if (ImGui.RadioButton("Tanks & melee##claw", Overrides.StartingTethers == StartingTethers.TanksAndMelee))
                Overrides.StartingTethers = StartingTethers.TanksAndMelee;
            ImGui.SameLine();
            if (ImGui.RadioButton("Healers & ranged##claw", Overrides.StartingTethers == StartingTethers.HealersAndRanged))
                Overrides.StartingTethers = StartingTethers.HealersAndRanged;
            SettingsGrid.End();
        }
    }
}
