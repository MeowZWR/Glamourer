﻿using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.Internal.Notifications;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Glamourer.Automation;
using Glamourer.Designs;
using Glamourer.GameData;
using Glamourer.Gui.Customization;
using Glamourer.Gui.Equipment;
using Glamourer.Gui.Materials;
using Glamourer.Interop;
using Glamourer.State;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.GameData.Enums;

namespace Glamourer.Gui.Tabs.DesignTab;

public class DesignPanel(
    DesignFileSystemSelector _selector,
    CustomizationDrawer _customizationDrawer,
    DesignManager _manager,
    ObjectManager _objects,
    StateManager _state,
    EquipmentDrawer _equipmentDrawer,
    ModAssociationsTab _modAssociations,
    Configuration _config,
    DesignDetailTab _designDetails,
    DesignConverter _converter,
    ImportService _importService,
    MultiDesignPanel _multiDesignPanel,
    CustomizeParameterDrawer _parameterDrawer,
    DesignLinkDrawer _designLinkDrawer,
    MaterialDrawer _materials)
{
    private readonly FileDialogManager _fileDialog = new();

    private HeaderDrawer.Button LockButton()
        => _selector.Selected == null
            ? HeaderDrawer.Button.Invisible
            : _selector.Selected.WriteProtected()
                ? new HeaderDrawer.Button
                {
                    Description = "解锁设计，使其可以被编辑。",
                    Icon        = FontAwesomeIcon.Lock,
                    OnClick     = () => _manager.SetWriteProtection(_selector.Selected!, false),
                }
                : new HeaderDrawer.Button
                {
                    Description = "锁定设计，使其不能被编辑。",
                    Icon        = FontAwesomeIcon.LockOpen,
                    OnClick     = () => _manager.SetWriteProtection(_selector.Selected!, true),
                };

    private HeaderDrawer.Button SetFromClipboardButton()
        => new()
        {
            Description =
                "尝试使用剪贴板中的设计数据覆盖此设计。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。",
            Icon     = FontAwesomeIcon.Clipboard,
            OnClick  = SetFromClipboard,
            Visible  = _selector.Selected != null,
            Disabled = _selector.Selected?.WriteProtected() ?? true,
        };

    private HeaderDrawer.Button UndoButton()
        => new()
        {
            Description = "Undo the last change if you accidentally overwrote your design with a different one.",
            Icon        = FontAwesomeIcon.Undo,
            OnClick     = UndoOverwrite,
            Visible     = _selector.Selected != null,
            Disabled    = !_manager.CanUndo(_selector.Selected),
        };

    private HeaderDrawer.Button ExportToClipboardButton()
        => new()
        {
            Description = "复制当前设计的数据到剪贴板。",
            Icon        = FontAwesomeIcon.Copy,
            OnClick     = ExportToClipboard,
            Visible     = _selector.Selected != null,
        };

    private void DrawHeader()
        => HeaderDrawer.Draw(SelectionName, 0, ImGui.GetColorU32(ImGuiCol.FrameBg),
            3, SetFromClipboardButton(), UndoButton(), ExportToClipboardButton(), LockButton(),
            HeaderDrawer.Button.IncognitoButton(_selector.IncognitoMode, v => _selector.IncognitoMode = v));

    private string SelectionName
        => _selector.Selected == null ? "未选择" : _selector.IncognitoMode ? _selector.Selected.Incognito : _selector.Selected.Name.Text;

    private void DrawEquipment()
    {
        using var h = ImRaii.CollapsingHeader("装备");
        if (!h)
            return;

        _equipmentDrawer.Prepare();

        var usedAllStain = _equipmentDrawer.DrawAllStain(out var newAllStain, _selector.Selected!.WriteProtected());
        foreach (var slot in EquipSlotExtensions.EqdpSlots)
        {
            var data = EquipDrawData.FromDesign(_manager, _selector.Selected!, slot);
            _equipmentDrawer.DrawEquip(data);
            if (usedAllStain)
                _manager.ChangeStain(_selector.Selected, slot, newAllStain);
        }

        var mainhand = EquipDrawData.FromDesign(_manager, _selector.Selected!, EquipSlot.MainHand);
        var offhand  = EquipDrawData.FromDesign(_manager, _selector.Selected!, EquipSlot.OffHand);
        _equipmentDrawer.DrawWeapons(mainhand, offhand, true);
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        DrawEquipmentMetaToggles();
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
    }

    private void DrawEquipmentMetaToggles()
    {
        using (var _ = ImRaii.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.HatState, _manager, _selector.Selected!));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromDesign(CrestFlag.Head, _manager, _selector.Selected!));
        }

        ImGui.SameLine();
        using (var _ = ImRaii.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.VisorState, _manager, _selector.Selected!));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromDesign(CrestFlag.Body, _manager, _selector.Selected!));
        }

        ImGui.SameLine();
        using (var _ = ImRaii.Group())
        {
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.WeaponState, _manager, _selector.Selected!));
            EquipmentDrawer.DrawMetaToggle(ToggleDrawData.CrestFromDesign(CrestFlag.OffHand, _manager, _selector.Selected!));
        }
    }

    private void DrawCustomize()
    {
        var header = _selector.Selected!.DesignData.ModelId == 0
            ? "外貌"
            : $"Customization (Model Id #{_selector.Selected!.DesignData.ModelId})###Customization";
        using var h = ImRaii.CollapsingHeader(header);
        if (!h)
            return;

        if (_customizationDrawer.Draw(_selector.Selected!.DesignData.Customize, _selector.Selected.ApplyCustomizeRaw,
                _selector.Selected!.WriteProtected(), false))
            foreach (var idx in Enum.GetValues<CustomizeIndex>())
            {
                var flag     = idx.ToFlag();
                var newValue = _customizationDrawer.ChangeApply.HasFlag(flag);
                _manager.ChangeApplyCustomize(_selector.Selected, idx, newValue);
                if (_customizationDrawer.Changed.HasFlag(flag))
                    _manager.ChangeCustomize(_selector.Selected, idx, _customizationDrawer.Customize[idx]);
            }

        EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromDesign(MetaIndex.Wetness, _manager, _selector.Selected!));
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
    }

    private void DrawCustomizeParameters()
    {
        if (!_config.UseAdvancedParameters)
            return;

        using var h = ImRaii.CollapsingHeader("外貌（高级）- 调色板");
        if (!h)
            return;

        _parameterDrawer.Draw(_manager, _selector.Selected!);
    }

    private void DrawMaterialValues()
    {
        if (!_config.UseAdvancedDyes)
            return;

        using var h = ImRaii.CollapsingHeader("染色（高级）- 颜色集");
        if (!h)
            return;

        _materials.Draw(_selector.Selected!);
    }

    private void DrawCustomizeApplication()
    {
        using var id        = ImRaii.PushId("Customizations");
        var       set       = _selector.Selected!.CustomizeSet;
        var       available = set.SettingAvailable | CustomizeFlag.Clan | CustomizeFlag.Gender | CustomizeFlag.BodyType;
        var flags = _selector.Selected!.ApplyCustomizeExcludingBodyType == 0 ? 0 :
            (_selector.Selected!.ApplyCustomize & available) == available    ? 3 : 1;
        if (ImGui.CheckboxFlags("应用全部外貌数据", ref flags, 3))
        {
            var newFlags = flags == 3;
            _manager.ChangeApplyCustomize(_selector.Selected!, CustomizeIndex.Clan,   newFlags);
            _manager.ChangeApplyCustomize(_selector.Selected!, CustomizeIndex.Gender, newFlags);
            foreach (var index in CustomizationExtensions.AllBasic)
                _manager.ChangeApplyCustomize(_selector.Selected!, index, newFlags);
        }

        var applyClan = _selector.Selected!.DoApplyCustomize(CustomizeIndex.Clan);
        if (ImGui.Checkbox($"应用{CustomizeIndex.Clan.ToDefaultName()}", ref applyClan))
            _manager.ChangeApplyCustomize(_selector.Selected!, CustomizeIndex.Clan, applyClan);

        var applyGender = _selector.Selected!.DoApplyCustomize(CustomizeIndex.Gender);
        if (ImGui.Checkbox($"应用{CustomizeIndex.Gender.ToDefaultName()}", ref applyGender))
            _manager.ChangeApplyCustomize(_selector.Selected!, CustomizeIndex.Gender, applyGender);


        foreach (var index in CustomizationExtensions.All.Where(set.IsAvailable))
        {
            var apply = _selector.Selected!.DoApplyCustomize(index);
            if (ImGui.Checkbox($"应用{set.Option(index)}", ref apply))
                _manager.ChangeApplyCustomize(_selector.Selected!, index, apply);
        }
    }

    private void DrawCrestApplication()
    {
        using var id        = ImRaii.PushId("徽章");
        var       flags     = (uint)_selector.Selected!.ApplyCrest;
        var       bigChange = ImGui.CheckboxFlags("应用所有徽章", ref flags, (uint)CrestExtensions.AllRelevant);
        foreach (var flag in CrestExtensions.AllRelevantSet)
        {
            var apply = bigChange ? ((CrestFlag)flags & flag) == flag : _selector.Selected!.DoApplyCrest(flag);
            if (ImGui.Checkbox($"应用{flag.ToLabel()}徽章", ref apply) || bigChange)
                _manager.ChangeApplyCrest(_selector.Selected!, flag, apply);
        }
    }

    private void DrawApplicationRules()
    {
        using var h = ImRaii.CollapsingHeader("应用规则");
        if (!h)
            return;

        using (var _ = ImRaii.Group())
        {
            DrawCustomizeApplication();
            ImGui.NewLine();
            DrawCrestApplication();
            ImGui.NewLine();
            if (_config.UseAdvancedParameters)
                DrawMetaApplication();
        }

        ImGui.SameLine(ImGui.GetContentRegionAvail().X / 2);
        using (var _ = ImRaii.Group())
        {
            void ApplyEquip(string label, EquipFlag allFlags, bool stain, IEnumerable<EquipSlot> slots)
            {
                var       flags     = (uint)(allFlags & _selector.Selected!.ApplyEquip);
                using var id        = ImRaii.PushId(label);
                var       bigChange = ImGui.CheckboxFlags($"应用全部{label}", ref flags, (uint)allFlags);
                if (stain)
                    foreach (var slot in slots)
                    {
                        var apply = bigChange ? ((EquipFlag)flags).HasFlag(slot.ToStainFlag()) : _selector.Selected!.DoApplyStain(slot);
                        if (ImGui.Checkbox($"应用{slot.ToName()}染色", ref apply) || bigChange)
                            _manager.ChangeApplyStain(_selector.Selected!, slot, apply);
                    }
                else
                    foreach (var slot in slots)
                    {
                        var apply = bigChange ? ((EquipFlag)flags).HasFlag(slot.ToFlag()) : _selector.Selected!.DoApplyEquip(slot);
                        if (ImGui.Checkbox($"应用{slot.ToName()}", ref apply) || bigChange)
                            _manager.ChangeApplyItem(_selector.Selected!, slot, apply);
                    }
            }

            ApplyEquip("武器", ApplicationTypeExtensions.WeaponFlags, false, new[]
            {
                EquipSlot.MainHand,
                EquipSlot.OffHand,
            });

            ImGui.NewLine();
            ApplyEquip("服装", ApplicationTypeExtensions.ArmorFlags, false, EquipSlotExtensions.EquipmentSlots);

            ImGui.NewLine();
            ApplyEquip("饰品", ApplicationTypeExtensions.AccessoryFlags, false, EquipSlotExtensions.AccessorySlots);

            ImGui.NewLine();
            ApplyEquip("染色", ApplicationTypeExtensions.StainFlags, true,
                EquipSlotExtensions.FullSlots);

            ImGui.NewLine();
            if (_config.UseAdvancedParameters)
                DrawParameterApplication();
            else
                DrawMetaApplication();
        }
    }

    private void DrawMetaApplication()
    {
        using var  id        = ImRaii.PushId("Meta");
        const uint all       = (uint)MetaExtensions.All;
        var        flags     = (uint)_selector.Selected!.ApplyMeta;
        var        bigChange = ImGui.CheckboxFlags("应用全部元数据修改", ref flags, all);

        var labels = new[]
        {
            "应用湿身状态",
            "应用头部装备可见状态",
            "应用头部装备调整状态",
            "应用武器可见状态",
        };

        foreach (var (index, label) in MetaExtensions.AllRelevant.Zip(labels))
        {
            var apply = bigChange ? ((MetaFlag)flags).HasFlag(index.ToFlag()) : _selector.Selected!.DoApplyMeta(index);
            if (ImGui.Checkbox(label, ref apply) || bigChange)
                _manager.ChangeApplyMeta(_selector.Selected!, index, apply);
        }
    }

    private void DrawParameterApplication()
    {
        using var id        = ImRaii.PushId("Parameter");
        var       flags     = (uint)_selector.Selected!.ApplyParameters;
        var       bigChange = ImGui.CheckboxFlags("应用所有外貌参数", ref flags, (uint)CustomizeParameterExtensions.All);
        foreach (var flag in CustomizeParameterExtensions.AllFlags)
        {
            var apply = bigChange ? ((CustomizeParameterFlag)flags).HasFlag(flag) : _selector.Selected!.DoApplyParameter(flag);
            if (ImGui.Checkbox($"应用{flag.ToName()}", ref apply) || bigChange)
                _manager.ChangeApplyParameter(_selector.Selected!, flag, apply);
        }
    }

    public void Draw()
    {
        using var group = ImRaii.Group();
        if (_selector.SelectedPaths.Count > 1)
        {
            _multiDesignPanel.Draw();
        }
        else
        {
            DrawHeader();
            DrawPanel();

            if (_selector.Selected == null || _selector.Selected.WriteProtected())
                return;

            if (_importService.CreateDatTarget(out var dat))
            {
                _manager.ChangeCustomize(_selector.Selected!, CustomizeIndex.Clan,   dat.Customize[CustomizeIndex.Clan]);
                _manager.ChangeCustomize(_selector.Selected!, CustomizeIndex.Gender, dat.Customize[CustomizeIndex.Gender]);
                foreach (var idx in CustomizationExtensions.AllBasic)
                    _manager.ChangeCustomize(_selector.Selected!, idx, dat.Customize[idx]);
                Glamourer.Messager.NotificationMessage(
                    $"Applied games .dat file {dat.Description} customizations to {_selector.Selected.Name}.", NotificationType.Success, false);
            }
            else if (_importService.CreateCharaTarget(out var designBase, out var name))
            {
                _manager.ApplyDesign(_selector.Selected!, designBase);
                Glamourer.Messager.NotificationMessage($"Applied Anamnesis .chara file {name} to {_selector.Selected.Name}.",
                    NotificationType.Success, false);
            }
        }

        _importService.CreateDatSource();
    }

    private void DrawPanel()
    {
        using var child = ImRaii.Child("##Panel", -Vector2.One, true);
        if (!child || _selector.Selected == null)
            return;

        DrawButtonRow();
        DrawCustomize();
        DrawEquipment();
        DrawCustomizeParameters();
        DrawMaterialValues();
        _designDetails.Draw();
        DrawApplicationRules();
        _modAssociations.Draw();
        _designLinkDrawer.Draw();
    }

    private void DrawButtonRow()
    {
        DrawApplyToSelf();
        ImGui.SameLine();
        DrawApplyToTarget();
        ImGui.SameLine();
        _modAssociations.DrawApplyButton();
        ImGui.SameLine();
        DrawSaveToDat();
    }

    private void SetFromClipboard()
    {
        try
        {
            var text = ImGui.GetClipboardText();
            var (applyEquip, applyCustomize) = UiHelpers.ConvertKeysToBool();
            var design = _converter.FromBase64(text, applyCustomize, applyEquip, out _)
             ?? throw new Exception("The clipboard did not contain valid data.");
            _manager.ApplyDesign(_selector.Selected!, design);
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex, $"无法应用剪贴板数据到{_selector.Selected!.Name}.",
                $"无法应用剪贴板数据到设计：{_selector.Selected!.Identifier}", NotificationType.Error, false);
        }
    }

    private void UndoOverwrite()
    {
        try
        {
            _manager.UndoDesignChange(_selector.Selected!);
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex, $"Could not undo last changes to {_selector.Selected!.Name}.", NotificationType.Error,
                false);
        }
    }

    private void ExportToClipboard()
    {
        try
        {
            var text = _converter.ShareBase64(_selector.Selected!);
            ImGui.SetClipboardText(text);
        }
        catch (Exception ex)
        {
            Glamourer.Messager.NotificationMessage(ex, $"Could not copy {_selector.Selected!.Name} data to clipboard.",
                $"Could not copy data from design {_selector.Selected!.Identifier} to clipboard", NotificationType.Error, false);
        }
    }

    private void DrawApplyToSelf()
    {
        var (id, data) = _objects.PlayerData;
        if (!ImGuiUtil.DrawDisabledButton("应用到自己", Vector2.Zero,
                "将当前设计按其中设置应用到你的角色。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。",
                !data.Valid))
            return;

        if (_state.GetOrCreate(id, data.Objects[0], out var state))
        {
            var (applyGear, applyCustomize, applyCrest, applyParameters) = UiHelpers.ConvertKeysToFlags();
            using var _ = _selector.Selected!.TemporarilyRestrictApplication(applyGear, applyCustomize, applyCrest, applyParameters);
            _state.ApplyDesign(state, _selector.Selected!, ApplySettings.ManualWithLinks);
        }
    }

    private void DrawApplyToTarget()
    {
        var (id, data) = _objects.TargetData;
        var tt = id.IsValid
            ? data.Valid
                ? "将当前设计按其中设置应用到你的目标。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。"
                : "无法应用到当前目标。"
            : "未选中有效目标。";
        if (!ImGuiUtil.DrawDisabledButton("应用到目标", Vector2.Zero, tt, !data.Valid))
            return;

        if (_state.GetOrCreate(id, data.Objects[0], out var state))
        {
            var (applyGear, applyCustomize, applyCrest, applyParameters) = UiHelpers.ConvertKeysToFlags();
            using var _ = _selector.Selected!.TemporarilyRestrictApplication(applyGear, applyCustomize, applyCrest, applyParameters);
            _state.ApplyDesign(state, _selector.Selected!, ApplySettings.ManualWithLinks);
        }
    }

    private void DrawSaveToDat()
    {
        var verified = _importService.Verify(_selector.Selected!.DesignData.Customize, out _);
        var tt = verified
            ? "将当前设计的外貌数据导出为游戏角色创建时可读取的文档。"
            : "当前设计包含无法在创建角色时使用的外貌数据。";
        var startPath = GetUserPath();
        if (startPath.Length == 0)
            startPath = null;
        if (ImGuiUtil.DrawDisabledButton("导出为Dat", Vector2.Zero, tt, !verified))
            _fileDialog.SaveFileDialog("保存文件...", ".dat", "FFXIV_CHARA_01.dat", ".dat", (v, path) =>
            {
                if (v && _selector.Selected != null)
                    _importService.SaveDesignAsDat(path, _selector.Selected!.DesignData.Customize, _selector.Selected!.Name);
            }, startPath);

        _fileDialog.Draw();
    }

    private static unsafe string GetUserPath()
        => Framework.Instance()->UserPath;
}
