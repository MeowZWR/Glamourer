using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using Dalamud.Interface.Utility;
using Dalamud.Utility;
using Glamourer.Designs;
using Glamourer.Interop.Penumbra;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Text.Widget;

namespace Glamourer.Gui.Tabs.DesignTab;

public class ModAssociationsTab(PenumbraService penumbra, DesignFileSystemSelector selector, DesignManager manager, Configuration config)
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
        using var table = ImUtf8.Table("Mods"u8, config.UseTemporarySettings ? 7 : 6, ImGuiTableFlags.RowBg);
        if (!table)
            return;

        ImUtf8.TableSetupColumn("##Buttons"u8, ImGuiTableColumnFlags.WidthFixed,
            ImGui.GetFrameHeight() * 3 + ImGui.GetStyle().ItemInnerSpacing.X * 2);
        ImUtf8.TableSetupColumn("模组名称"u8, ImGuiTableColumnFlags.WidthStretch);
        if (config.UseTemporarySettings)
            ImUtf8.TableSetupColumn("移除"u8, ImGuiTableColumnFlags.WidthFixed, ImUtf8.CalcTextSize("Remove"u8).X);
        ImUtf8.TableSetupColumn("继承"u8, ImGuiTableColumnFlags.WidthFixed, ImUtf8.CalcTextSize("Inherit"u8).X);
        ImUtf8.TableSetupColumn("状态"u8,     ImGuiTableColumnFlags.WidthFixed, ImUtf8.CalcTextSize("State"u8).X);
        ImUtf8.TableSetupColumn("优先级"u8,  ImGuiTableColumnFlags.WidthFixed, ImUtf8.CalcTextSize("Priority"u8).X);
        ImUtf8.TableSetupColumn("##Options"u8, ImGuiTableColumnFlags.WidthFixed, ImUtf8.CalcTextSize("Applym"u8).X);
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
        var canDelete = config.DeleteDesignModifier.IsActive();
        if (canDelete)
        {
            if (ImUtf8.IconButton(FontAwesomeIcon.Trash, "从关联中删除此模组。"u8))
                removedMod = mod;
        }
        else
        {
            ImUtf8.IconButton(FontAwesomeIcon.Trash, $"从关联中删除此模组。\n按住{config.DeleteDesignModifier}来删除。",
                disabled: true);
        }

        ImUtf8.SameLineInner();
        if (ImUtf8.IconButton(FontAwesomeIcon.Clipboard, "复制这个模组设置到剪贴板。"u8))
            _copy = [(mod, settings)];

        ImUtf8.SameLineInner();
        ImUtf8.IconButton(FontAwesomeIcon.RedoAlt, "更新此关联模组当前的设置。"u8);
        if (ImGui.IsItemHovered())
        {
            var newSettings = penumbra.GetModSettings(mod, out var source);
            if (ImGui.IsItemClicked())
                updatedMod = (mod, newSettings);

            using var style = ImRaii.PushStyle(ImGuiStyleVar.PopupBorderSize, 2 * ImGuiHelpers.GlobalScale);
            using var tt    = ImUtf8.Tooltip();
            if (source.Length > 0)
                ImUtf8.Text($"使用由 {source} 创建的临时设置。");
            ImGui.Separator();
            var namesDifferent = mod.Name != mod.DirectoryName;
            ImGui.Dummy(new Vector2(300 * ImGuiHelpers.GlobalScale, 0));
            using (ImRaii.Group())
            {
                if (namesDifferent)
                    ImUtf8.Text("目录名称"u8);
                ImUtf8.Text("强制继承"u8);
                ImUtf8.Text("已启用"u8);
                ImUtf8.Text("优先级"u8);
                ModCombo.DrawSettingsLeft(newSettings);
            }

            ImGui.SameLine(Math.Max(ImGui.GetItemRectSize().X + 3 * ImGui.GetStyle().ItemSpacing.X, 150 * ImGuiHelpers.GlobalScale));
            using (ImRaii.Group())
            {
                if (namesDifferent)
                    ImUtf8.Text(mod.DirectoryName);

                ImUtf8.Text(newSettings.ForceInherit.ToString());
                ImUtf8.Text(newSettings.Enabled.ToString());
                ImUtf8.Text(newSettings.Priority.ToString());
                ModCombo.DrawSettingsRight(newSettings);
            }
        }

        ImGui.TableNextColumn();

        if (ImUtf8.Selectable($"{mod.Name}##name"))
            penumbra.OpenModPage(mod);
        if (ImGui.IsItemHovered())
            ImGui.SetTooltip($"Mod Directory:    {mod.DirectoryName}\n\nClick to open mod page in Penumbra.");
        if (config.UseTemporarySettings)
        {
            ImGui.TableNextColumn();
            var remove = settings.Remove;
            if (TwoStateCheckbox.Instance.Draw("##Remove"u8, ref remove))
                updatedMod = (mod, settings with { Remove = remove });
            ImUtf8.HoverTooltip(
                "移除由 Glamourer 应用的任何临时设置，而不是应用已配置的设置。仅在使用临时设置时有效，否则会被忽略。"u8);
        }

        ImGui.TableNextColumn();
        var inherit = settings.ForceInherit;
        if (TwoStateCheckbox.Instance.Draw("##Enabled"u8, ref inherit))
            updatedMod = (mod, settings with { ForceInherit = inherit });
        ImUtf8.HoverTooltip("强制模组从继承的合集中继承其设置。"u8);
        ImGui.TableNextColumn();
        var enabled = settings.Enabled;
        if (TwoStateCheckbox.Instance.Draw("##Enabled"u8, ref enabled))
            updatedMod = (mod, settings with { Enabled = enabled });

        ImGui.TableNextColumn();
        var priority = settings.Priority;
        ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
        if (ImUtf8.InputScalarOnDeactivated("##Priority"u8, ref priority))
            updatedMod = (mod, settings with { Priority = priority });
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
