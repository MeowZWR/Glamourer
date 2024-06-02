using Dalamud.Interface;
using Dalamud.Interface.Internal.Notifications;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using Glamourer.Designs;
using Glamourer.Interop.Penumbra;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;

namespace Glamourer.Gui.Tabs.DesignTab;

public class ModAssociationsTab(PenumbraService penumbra, DesignFileSystemSelector selector, DesignManager manager)
{
    private readonly ModCombo              _modCombo = new(penumbra, Glamourer.Log);
    private          (Mod, ModSettings)[]? _copy;

    public void Draw()
    {
        using var h = ImRaii.CollapsingHeader("模组关联");
        ImGuiUtil.HoverTooltip(
            "在此面板可以存储关联到此设计的特定模组的信息。\n\n"
          + "它不会自动更改模组的任何设置，尽管有手动应用所需模组设置的功能。\n"
          + "你可以使用它快速打开模组在Penumbra中的页面。\n\n"
          + "在一般情况下，不太可能自动应用这些更改，因为没有办法恢复这些更改并同时处理多个生效的设计。");
        if (!h)
            return;

        DrawApplyAllButton();
        DrawTable();
        DrawCopyButtons();
    }

    private void DrawCopyButtons()
    {
        var size = new Vector2((ImGui.GetContentRegionAvail().X - 2 * ImGui.GetStyle().ItemSpacing.X) / 3, 0);
        if (ImGui.Button("全部复制到剪贴板", size))
            _copy = selector.Selected!.AssociatedMods.Select(kvp => (kvp.Key, kvp.Value)).ToArray();

        ImGui.SameLine();

        if (ImGuiUtil.DrawDisabledButton("从剪贴板添加", size,
                _copy != null
                    ? $"从剪贴板添加{_copy.Length}个模组关联。"
                    : "请先将一些模组关联复制到剪贴板。", _copy == null))
            foreach (var (mod, setting) in _copy!)
                manager.UpdateMod(selector.Selected!, mod, setting);

        ImGui.SameLine();

        if (ImGuiUtil.DrawDisabledButton("从剪贴板设置", size,
                _copy != null
                    ? $"从剪贴板设置 {_copy.Length} 个模组关联并丢弃现有的。"
                    : "请先将一些模组关联复制到剪贴板。", _copy == null))
        {
            while (selector.Selected!.AssociatedMods.Count > 0)
                manager.RemoveMod(selector.Selected!, selector.Selected!.AssociatedMods.Keys[0]);
            foreach (var (mod, setting) in _copy!)
                manager.AddMod(selector.Selected!, mod, setting);
        }
    }

    private void DrawApplyAllButton()
    {
        var (id, name) = penumbra.CurrentCollection;
        if (ImGuiUtil.DrawDisabledButton($"尝试应用所有关联的模组到：{name}##applyAll",
                new Vector2(ImGui.GetContentRegionAvail().X, 0), string.Empty, id == Guid.Empty))
            ApplyAll();
    }

    public void DrawApplyButton()
    {
        var (id, name) = penumbra.CurrentCollection;
        if (ImGuiUtil.DrawDisabledButton("应用模组关联", Vector2.Zero,
                $"尝试应用所有关联的模组设置到你当前在Penumbra选中的合集：{name}",
                selector.Selected!.AssociatedMods.Count == 0 || id == Guid.Empty))
            ApplyAll();
    }

    public void ApplyAll()
    {
        foreach (var (mod, settings) in selector.Selected!.AssociatedMods)
            penumbra.SetMod(mod, settings);
    }

    private void DrawTable()
    {
        using var table = ImRaii.Table("Mods", 5, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImGui.TableSetupColumn("##Buttons", ImGuiTableColumnFlags.WidthFixed,
            ImGui.GetFrameHeight() * 3 + ImGui.GetStyle().ItemInnerSpacing.X * 2);
        ImGui.TableSetupColumn("模组名称",       ImGuiTableColumnFlags.WidthStretch);
        ImGui.TableSetupColumn("状态",          ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("State").X);
        ImGui.TableSetupColumn("优先级",       ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Priority").X);
        ImGui.TableSetupColumn("##Options",      ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Applym").X);
        ImGui.TableHeadersRow();

        Mod?                             removedMod = null;
        (Mod mod, ModSettings settings)? updatedMod = null;
        foreach (var ((mod, settings), idx) in selector.Selected!.AssociatedMods.WithIndex())
        {
            using var id = ImRaii.PushId(idx);
            DrawAssociatedModRow(mod, settings, out var removedModTmp, out var updatedModTmp);
            if (removedModTmp.HasValue)
                removedMod = removedModTmp;
            if (updatedModTmp.HasValue)
                updatedMod = updatedModTmp;
        }

        DrawNewModRow();

        if (removedMod.HasValue)
            manager.RemoveMod(selector.Selected!, removedMod.Value);

        if (updatedMod.HasValue)
            manager.UpdateMod(selector.Selected!, updatedMod.Value.mod, updatedMod.Value.settings);
    }

    private void DrawAssociatedModRow(Mod mod, ModSettings settings, out Mod? removedMod, out (Mod, ModSettings)? updatedMod)
    {
        removedMod = null;
        updatedMod = null;
        ImGui.TableNextColumn();
        var buttonSize = new Vector2(ImGui.GetFrameHeight());
        var spacing    = ImGui.GetStyle().ItemInnerSpacing.X;
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), buttonSize,
                "从关联中删除此模组", false, true))
            removedMod = mod;

        ImGui.SameLine(0, spacing);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Clipboard.ToIconString(), buttonSize,
                "复制这个模组设置到剪贴板。", false, true))
            _copy = [(mod, settings)];

        ImGui.SameLine(0, spacing);
        ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.RedoAlt.ToIconString(), buttonSize,
            "更新此关联模组当前的设置", false, true);
        if (ImGui.IsItemHovered())
        {
            var newSettings = penumbra.GetModSettings(mod);
            if (ImGui.IsItemClicked())
                updatedMod = (mod, newSettings);

            using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 2 * ImGuiHelpers.GlobalScale);
            using var tt    = ImRaii.Tooltip();
            ImGui.Separator();
            var namesDifferent = mod.Name != mod.DirectoryName;
            ImGui.Dummy(new Vector2(300 * ImGuiHelpers.GlobalScale, 0));
            using (ImRaii.Group())
            {
                if (namesDifferent)
                    ImGui.TextUnformatted("Directory Name");
                ImGui.TextUnformatted("已启用");
                ImGui.TextUnformatted("优先级");
                ModCombo.DrawSettingsLeft(newSettings);
            }

            ImGui.SameLine(Math.Max(ImGui.GetItemRectSize().X + 3 * ImGui.GetStyle().ItemSpacing.X, 150 * ImGuiHelpers.GlobalScale));
            using (ImRaii.Group())
            {
                if (namesDifferent)
                    ImGui.TextUnformatted(mod.DirectoryName);
                ImGui.TextUnformatted(newSettings.Enabled.ToString());
                ImGui.TextUnformatted(newSettings.Priority.ToString());
                ModCombo.DrawSettingsRight(newSettings);
            }
        }

        ImGui.TableNextColumn();
        
        if (ImGui.Selectable($"{mod.Name}##name"))
            penumbra.OpenModPage(mod);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"模组目录： {mod.DirectoryName}\n\n点击打开 Penumbra 中的模组页面。");
        ImGui.TableNextColumn();
        using (var font = ImRaii.PushFont(UiBuilder.IconFont))
        {
            ImGuiUtil.Center((settings.Enabled ? FontAwesomeIcon.Check : FontAwesomeIcon.Times).ToIconString());
        }

        ImGui.TableNextColumn();
        ImGuiUtil.RightAlign(settings.Priority.ToString());
        ImGui.TableNextColumn();
        if (ImGuiUtil.DrawDisabledButton("应用", new Vector2(ImGui.GetContentRegionAvail().X, 0), string.Empty,
                !penumbra.Available))
        {
            var text = penumbra.SetMod(mod, settings);
            if (text.Length > 0)
                Glamourer.Messager.NotificationMessage(text, NotificationType.Warning, false);
        }

        DrawAssociatedModTooltip(settings);
    }

    private static void DrawAssociatedModTooltip(ModSettings settings)
    {
        if (settings is not { Enabled: true, Settings.Count: > 0 } || !ImGui.IsItemHovered())
            return;

        using var t = ImRaii.Tooltip();
        ImGui.TextUnformatted("还将尝试将以下设置也应用到当前合集：");

        ImGui.NewLine();
        using (var _ = ImRaii.Group())
        {
            ModCombo.DrawSettingsLeft(settings);
        }

        ImGui.SameLine(ImGui.GetContentRegionAvail().X / 2);
        using (var _ = ImRaii.Group())
        {
            ModCombo.DrawSettingsRight(settings);
        }
    }

    private void DrawNewModRow()
    {
        var currentName = _modCombo.CurrentSelection.Mod.Name;
        ImGui.TableNextColumn();
        var tt = currentName.IsNullOrEmpty()
            ? "请先选择一个模组。"
            : selector.Selected!.AssociatedMods.ContainsKey(_modCombo.CurrentSelection.Mod)
                ? "此设计已经关联了选中的模组。"
                : string.Empty;

        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Plus.ToIconString(), new Vector2(ImGui.GetFrameHeight()), tt, tt.Length > 0,
                true))
            manager.AddMod(selector.Selected!, _modCombo.CurrentSelection.Mod, _modCombo.CurrentSelection.Settings);
        ImGui.TableNextColumn();
        _modCombo.Draw("##new", currentName.IsNullOrEmpty() ? "选择新模组..." : currentName, string.Empty,
            ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeight());
    }
}
