#if DEBUG
using System.Collections.Generic;
using AnoMech.Core.Map;
using System.Linq;
using System.Numerics;
using Dalamud.Bindings.ImGui;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine;
using FFXIVClientStructs.FFXIV.Client.Graphics.Scene;
using FFXIVClientStructs.FFXIV.Client.LayoutEngine.Group;
using Lumina.Data.Files;
using Lumina.Excel.Sheets;

namespace AnoMech.Windows;

// DEBUG-only live view of the active zone's layout: every LGB layer with its objects, each
// forceable on/off through MapController's per-instance overrides, and SharedGroups' own
// timelines playable by index. For finding which pieces make up a phase's arena by eye.
// Overrides reset with the scenario, like the scenarios' own layer overrides.
internal sealed unsafe class LayoutInspector
{
    private const uint MaxTimelineProbe = 8;
    // The level files that hold arena layers. Each read is guarded: a failed parse (seen once
    // under process-wide memory pressure) must not break the whole debug window.
    private static readonly string[] LevelFiles = ["bg", "planevent", "planmap", "vfx"];

    private readonly Plugin plugin;
    private uint namesForTerritory;
    private Dictionary<ushort, string> layerNames = new();
    private string filter = "";

    public LayoutInspector(Plugin plugin) => this.plugin = plugin;

    public void Draw()
    {
        var world = LayoutWorld.Instance();
        if (world == null || world->ActiveLayout == null)
        {
            ImGui.TextDisabled("No active layout.");
            return;
        }
        RefreshLayerNames();

        ImGui.SetNextItemWidth(200);
        ImGui.InputText("Path filter", ref filter, 128);
        ImGui.TextDisabled("Overrides clear when the scenario resets.");

        foreach (var layerKv in world->ActiveLayout->Layers)
        {
            var layer = layerKv.Item2.Value;
            if (layer == null) continue;
            var key = layerKv.Item1;
            var instances = new List<nint>();
            foreach (var instKv in layer->Instances)
                if (instKv.Item2.Value != null) instances.Add((nint)instKv.Item2.Value);
            if (instances.Count == 0) continue;

            var visible = instances.Where(p => MatchesFilter((ILayoutInstance*)p)).ToList();
            if (visible.Count == 0) continue;

            var name = layerNames.TryGetValue(key, out var n) ? n : "?";
            if (!ImGui.TreeNode($"0x{key:X4} {name} ({visible.Count})##layer{key}")) continue;

            if (ImGui.SmallButton($"All on##{key}")) SetAll(visible, true);
            ImGui.SameLine();
            if (ImGui.SmallButton($"All off##{key}")) SetAll(visible, false);
            ImGui.SameLine();
            if (ImGui.SmallButton($"Clear##{key}")) SetAll(visible, null);

            foreach (var ptr in visible) DrawInstance(ptr);
            ImGui.TreePop();
        }
    }

    private void DrawInstance(nint ptr)
    {
        var inst = (ILayoutInstance*)ptr;
        var map = plugin.Game.World.Map;
        var pos = *inst->GetTranslationImpl();
        var current = map.GetInstanceOverride(ptr);

        ImGui.PushID((int)(ptr & 0x7FFFFFFF));
        ImGui.TextUnformatted($"{(inst->IsActive ? "[on] " : "[off]")} {inst->Id.Type} {PathOf(inst)}  ({pos.X:F1}, {pos.Z:F1})");
        ImGui.Indent();
        if (ImGui.RadioButton("Default", current == null)) map.SetInstanceOverride(ptr, null);
        ImGui.SameLine();
        if (ImGui.RadioButton("On", current == true)) map.SetInstanceOverride(ptr, true);
        ImGui.SameLine();
        if (ImGui.RadioButton("Off", current == false)) map.SetInstanceOverride(ptr, false);

        if (inst->Id.Type == InstanceType.SharedGroup)
        {
            var sg = (SharedGroupLayoutInstance*)ptr;
            var any = false;
            for (uint i = 0; i < MaxTimelineProbe; i++)
            {
                if (!sg->IsTimelineIndexValid(i)) continue;
                ImGui.SameLine();
                if (ImGui.SmallButton($"TL {i}{(sg->IsTimelinePlaying(i) ? "*" : "")}"))
                    map.PlaySharedGroupTimeline(ptr, i);
                any = true;
            }
            if (!any) { ImGui.SameLine(); ImGui.TextDisabled("(no timelines)"); }
        }

        if (inst->Id.Type == InstanceType.Vfx)
        {
            ImGui.SameLine();
            if (ImGui.SmallButton("Play")) PlayLayoutVfx(inst);
            ImGui.SameLine();
            DrawColor(inst);
        }
        ImGui.Unindent();
        ImGui.PopID();
    }

    // Layout VFX authored with IsAutoPlay = 0 sit idle even while active; the real duty starts
    // them. Run the underlying VfxObject the same way VfxFunctions starts a static VFX.
    private static void PlayLayoutVfx(ILayoutInstance* inst)
    {
        var graphics = (VfxObject*)inst->GetGraphics();
        if (graphics == null)
        {
            Plugin.Log.Warning($"[LayoutInspector] {PathOf(inst)} has no VFX object to play (never created)");
            return;
        }
        graphics->Update(0f, -1);
        Plugin.Log.Info($"[LayoutInspector] Played {PathOf(inst)}");
    }

    private static void DrawColor(ILayoutInstance* inst)
    {
        Vector4 color;
        inst->GetColor(&color);
        ImGui.SetNextItemWidth(220);
        if (ImGui.ColorEdit4("##tint", ref color, ImGuiColorEditFlags.Float))
            inst->SetColor(&color);
    }

    private void SetAll(List<nint> instances, bool? active)
    {
        foreach (var ptr in instances) plugin.Game.World.Map.SetInstanceOverride(ptr, active);
    }

    private bool MatchesFilter(ILayoutInstance* inst) =>
        filter.Length == 0 || PathOf(inst).Contains(filter, System.StringComparison.OrdinalIgnoreCase);

    private static string PathOf(ILayoutInstance* inst) => LayoutQuery.PathOf(inst);

    // Layer names live only in the LGB files, so read them for the current territory.
    private void RefreshLayerNames()
    {
        var territory = Plugin.ClientState.TerritoryType;
        if (territory == namesForTerritory) return;
        namesForTerritory = territory;
        layerNames = new Dictionary<ushort, string>();

        if (!Plugin.DataManager.GetExcelSheet<TerritoryType>().TryGetRow(territory, out var row)) return;
        var bg = row.Bg.ExtractText();
        var levelDir = bg.Contains("/level/") ? bg[..(bg.IndexOf("/level/") + "/level/".Length)] : null;
        if (levelDir == null) return;

        foreach (var file in LevelFiles)
        {
            try
            {
                var lgb = Plugin.DataManager.GetFile<LgbFile>($"bg/{levelDir}{file}.lgb");
                if (lgb == null) continue;
                foreach (var layer in lgb.Layers)
                    layerNames[(ushort)(layer.LayerId & 0xFFFF)] = $"{file}:{layer.Name}";
            }
            catch (System.Exception ex)
            {
                Plugin.Log.Warning($"[LayoutInspector] Couldn't read layer names from {file}.lgb: {ex.Message}");
            }
        }
    }
}
#endif
