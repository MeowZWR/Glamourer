using Dalamud.Interface;
using Dalamud.Interface.ImGuiFileDialog;
using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.System.Framework;
using Glamourer.Automation;
using Glamourer.Designs;
using Glamourer.Designs.History;
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
using OtterGui.Text;
using OtterGuiInternal.Structs;
using Penumbra.GameData.Enums;
using static Glamourer.Gui.Tabs.HeaderDrawer;

namespace Glamourer.Gui.Tabs.DesignTab;

public class DesignPanel
{
    private readonly FileDialogManager        _fileDialog = new();
    private readonly DesignFileSystemSelector _selector;
    private readonly CustomizationDrawer      _customizationDrawer;
    private readonly DesignManager            _manager;
    private readonly ObjectManager            _objects;
    private readonly StateManager             _state;
    private readonly EquipmentDrawer          _equipmentDrawer;
    private readonly ModAssociationsTab       _modAssociations;
    private readonly Configuration            _config;
    private readonly DesignDetailTab          _designDetails;
    private readonly ImportService            _importService;
    private readonly DesignConverter          _converter;
    private readonly MultiDesignPanel         _multiDesignPanel;
    private readonly CustomizeParameterDrawer _parameterDrawer;
    private readonly DesignLinkDrawer         _designLinkDrawer;
    private readonly MaterialDrawer           _materials;
    private readonly EditorHistory            _history;
    private readonly Button[]                 _leftButtons;
    private readonly Button[]                 _rightButtons;


    public DesignPanel(DesignFileSystemSelector selector,
        CustomizationDrawer customizationDrawer,
        DesignManager manager,
        ObjectManager objects,
        StateManager state,
        EquipmentDrawer equipmentDrawer,
        ModAssociationsTab modAssociations,
        Configuration config,
        DesignDetailTab designDetails,
        DesignConverter converter,
        ImportService importService,
        MultiDesignPanel multiDesignPanel,
        CustomizeParameterDrawer parameterDrawer,
        DesignLinkDrawer designLinkDrawer,
        MaterialDrawer materials,
        EditorHistory history)
    {
        _selector            = selector;
        _customizationDrawer = customizationDrawer;
        _manager             = manager;
        _objects             = objects;
        _state               = state;
        _equipmentDrawer     = equipmentDrawer;
        _modAssociations     = modAssociations;
        _config              = config;
        _designDetails       = designDetails;
        _importService       = importService;
        _converter           = converter;
        _multiDesignPanel    = multiDesignPanel;
        _parameterDrawer     = parameterDrawer;
        _designLinkDrawer    = designLinkDrawer;
        _materials           = materials;
        _history             = history;
        _leftButtons =
        [
            new SetFromClipboardButton(this),
            new DesignUndoButton(this),
            new ExportToClipboardButton(this),
            new ApplyCharacterButton(this),
            new UndoButton(this),
        ];
        _rightButtons =
        [
            new LockButton(this),
            new IncognitoButton(_config.Ephemeral),
        ];
    }

    private void DrawHeader()
        => HeaderDrawer.Draw(SelectionName, 0, ImGui.GetColorU32(ImGuiCol.FrameBg), _leftButtons, _rightButtons);

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
                _manager.ChangeStains(_selector.Selected, slot, newAllStain);
        }

        var mainhand = EquipDrawData.FromDesign(_manager, _selector.Selected!, EquipSlot.MainHand);
        var offhand  = EquipDrawData.FromDesign(_manager, _selector.Selected!, EquipSlot.OffHand);
        _equipmentDrawer.DrawWeapons(mainhand, offhand, true);

        foreach (var slot in BonusExtensions.AllFlags)
        {
            var data = BonusDrawData.FromDesign(_manager, _selector.Selected!, slot);
            _equipmentDrawer.DrawBonusItem(data);
        }

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

        if (_customizationDrawer.Draw(_selector.Selected!.DesignData.Customize, _selector.Selected.Application.Customize,
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

        using var h = ImRaii.CollapsingHeader("外貌（高级）- 调色盘");
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
        using var id        = ImRaii.PushId("队徽");
        var       flags     = (uint)_selector.Selected!.Application.Crest;
        var       bigChange = ImGui.CheckboxFlags("应用所有队徽", ref flags, (uint)CrestExtensions.AllRelevant);
        foreach (var flag in CrestExtensions.AllRelevantSet)
        {
            var apply = bigChange ? ((CrestFlag)flags & flag) == flag : _selector.Selected!.DoApplyCrest(flag);
            if (ImGui.Checkbox($"应用{flag.ToLabel()}队徽", ref apply) || bigChange)
                _manager.ChangeApplyCrest(_selector.Selected!, flag, apply);
        }
    }

    private void DrawApplicationRules()
    {
        using var h = ImRaii.CollapsingHeader("应用规则");
        if (!h)
            return;

        using var disabled = ImRaii.Disabled(_selector.Selected!.WriteProtected());

        using (var _ = ImRaii.Group())
        {
            DrawCustomizeApplication();
            ImUtf8.IconDummy();
            DrawCrestApplication();
            ImUtf8.IconDummy();
            if (_config.UseAdvancedParameters)
            {
                DrawMetaApplication();
                ImUtf8.IconDummy();
                DrawBonusSlotApplication();
            }
        }

        ImGui.SameLine(ImGui.GetContentRegionAvail().X / 2);
        using (var _ = ImRaii.Group())
        {
            void ApplyEquip(string label, EquipFlag allFlags, bool stain, IEnumerable<EquipSlot> slots)
            {
                var       flags     = (uint)(allFlags & _selector.Selected!.Application.Equip);
                using var id        = ImRaii.PushId(label);
                var       bigChange = ImGui.CheckboxFlags($"应用全部{label}", ref flags, (uint)allFlags);
                if (stain)
                    foreach (var slot in slots)
                    {
                        var apply = bigChange ? ((EquipFlag)flags).HasFlag(slot.ToStainFlag()) : _selector.Selected!.DoApplyStain(slot);
                        if (ImGui.Checkbox($"应用{slot.ToName()}染色", ref apply) || bigChange)
                            _manager.ChangeApplyStains(_selector.Selected!, slot, apply);
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

            ImUtf8.IconDummy();
            ApplyEquip("服装", ApplicationTypeExtensions.ArmorFlags, false, EquipSlotExtensions.EquipmentSlots);

            ImUtf8.IconDummy();
            ApplyEquip("饰品", ApplicationTypeExtensions.AccessoryFlags, false, EquipSlotExtensions.AccessorySlots);

            ImUtf8.IconDummy();
            ApplyEquip("染色", ApplicationTypeExtensions.StainFlags, true,
                EquipSlotExtensions.FullSlots);

            ImUtf8.IconDummy();
            if (_config.UseAdvancedParameters)
            {
                DrawParameterApplication();
            }
            else
            {
                DrawMetaApplication();
                ImUtf8.IconDummy();
                DrawBonusSlotApplication();
            }
        }
    }

    private static readonly IReadOnlyList<string> MetaLabels =
    [
        "应用湿身",
        "应用头部装备可见性",
        "应用头部装备状态",
        "应用武器可见性",
    ];

    private void DrawMetaApplication()
    {
        using var  id        = ImRaii.PushId("Meta");
        const uint all       = (uint)MetaExtensions.All;
        var        flags     = (uint)_selector.Selected!.Application.Meta;
        var        bigChange = ImGui.CheckboxFlags("应用全部元数据修改", ref flags, all);

        foreach (var (index, label) in MetaExtensions.AllRelevant.Zip(MetaLabels))
        {
            var apply = bigChange ? ((MetaFlag)flags).HasFlag(index.ToFlag()) : _selector.Selected!.DoApplyMeta(index);
            if (ImGui.Checkbox(label, ref apply) || bigChange)
                _manager.ChangeApplyMeta(_selector.Selected!, index, apply);
        }
    }

    private static readonly IReadOnlyList<string> BonusSlotLabels =
    [
        "应用面甲",
    ];

    private void DrawBonusSlotApplication()
    {
        using var id        = ImUtf8.PushId("Bonus"u8);
        var       flags     = _selector.Selected!.Application.BonusItem;
        var       bigChange = BonusExtensions.AllFlags.Count > 1 && ImUtf8.Checkbox("Apply All Bonus Slots"u8, ref flags, BonusExtensions.All);
        foreach (var (index, label) in BonusExtensions.AllFlags.Zip(BonusSlotLabels))
        {
            var apply = bigChange ? flags.HasFlag(index) : _selector.Selected!.DoApplyBonusItem(index);
            if (ImUtf8.Checkbox(label, ref apply) || bigChange)
                _manager.ChangeApplyBonusItem(_selector.Selected!, index, apply);
        }
    }


    private void DrawParameterApplication()
    {
        using var id        = ImRaii.PushId("Parameter");
        var       flags     = (uint)_selector.Selected!.Application.Parameters;
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
        using var table = ImUtf8.Table("##Panel", 1, ImGuiTableFlags.BordersOuter | ImGuiTableFlags.ScrollY, ImGui.GetContentRegionAvail());
        if (!table || _selector.Selected == null)
            return;
        ImGui.TableSetupScrollFreeze(0, 1);
        ImGui.TableNextColumn();
        if (_selector.Selected == null)
            return;
        ImGui.Dummy(Vector2.Zero);
        DrawButtonRow();
        ImGui.TableNextColumn();

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


    private void DrawApplyToSelf()
    {
        var (id, data) = _objects.PlayerData;
        if (!ImGuiUtil.DrawDisabledButton("应用到自己", Vector2.Zero,
                "将当前设计按其中设置应用到你的角色。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。",
                !data.Valid))
            return;

        if (_state.GetOrCreate(id, data.Objects[0], out var state))
        {
            using var _ = _selector.Selected!.TemporarilyRestrictApplication(ApplicationCollection.FromKeys());
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
            using var _ = _selector.Selected!.TemporarilyRestrictApplication(ApplicationCollection.FromKeys());
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
        => Framework.Instance()->UserPathString;


    private sealed class LockButton(DesignPanel panel) : Button
    {
        public override bool Visible
            => panel._selector.Selected != null;

        protected override string Description
            => panel._selector.Selected!.WriteProtected()
                ? "解锁设计，使其可以被编辑。"
                : "锁定设计，使其不能被编辑。";

        protected override FontAwesomeIcon Icon
            => panel._selector.Selected!.WriteProtected()
                ? FontAwesomeIcon.Lock
                : FontAwesomeIcon.LockOpen;

        protected override void OnClick()
            => panel._manager.SetWriteProtection(panel._selector.Selected!, !panel._selector.Selected!.WriteProtected());
    }

    private sealed class SetFromClipboardButton(DesignPanel panel) : Button
    {
        public override bool Visible
            => panel._selector.Selected != null;

        protected override bool Disabled
            => panel._selector.Selected?.WriteProtected() ?? true;

        protected override string Description
            => "尝试使用剪贴板中的设计数据覆盖此设计。\n按住CTRL仅应用装备。\n按住Shift仅应用外貌。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Clipboard;

        protected override void OnClick()
        {
            try
            {
                var text = ImGui.GetClipboardText();
                var (applyEquip, applyCustomize) = UiHelpers.ConvertKeysToBool();
                var design = panel._converter.FromBase64(text, applyCustomize, applyEquip, out _)
                 ?? throw new Exception("剪贴板未包含有效数据。");
                panel._manager.ApplyDesign(panel._selector.Selected!, design);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, $"无法将剪贴板数据应用于{panel._selector.Selected!.Name}。",
                    $"无法将剪贴板数据应用于设计{panel._selector.Selected!.Identifier}", NotificationType.Error, false);
            }
        }
    }

    private sealed class DesignUndoButton(DesignPanel panel) : Button
    {
        public override bool Visible
            => panel._selector.Selected != null;

        protected override bool Disabled
            => !panel._manager.CanUndo(panel._selector.Selected) || (panel._selector.Selected?.WriteProtected() ?? true);

        protected override string Description
            => "如果不小心用不同的设计覆盖了您的设计，请撤消上一次更改。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.SyncAlt;

        protected override void OnClick()
        {
            try
            {
                panel._manager.UndoDesignChange(panel._selector.Selected!);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, $"无法为{panel._selector.Selected!.Name}撤消上次更改。",
                    NotificationType.Error,
                    false);
            }
        }
    }

    private sealed class ExportToClipboardButton(DesignPanel panel) : Button
    {
        public override bool Visible
            => panel._selector.Selected != null;

        protected override string Description
            => "复制当前设计的数据到剪贴板。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Copy;

        protected override void OnClick()
        {
            try
            {
                var text = panel._converter.ShareBase64(panel._selector.Selected!);
                ImGui.SetClipboardText(text);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, $"无法复制{panel._selector.Selected!.Name}的数据到剪贴板。",
                    $"无法复制来自设计{panel._selector.Selected!.Identifier}的数据到剪贴板。", NotificationType.Error, false);
            }
        }
    }

    private sealed class ApplyCharacterButton(DesignPanel panel) : Button
    {
        public override bool Visible
            => panel._selector.Selected != null && panel._objects.Player.Valid;

        protected override string Description
            => "用你的角色的当前状态覆盖此设计。";

        protected override bool Disabled
            => panel._selector.Selected?.WriteProtected() ?? true;

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.UserEdit;

        protected override void OnClick()
        {
            try
            {
                var (player, actor) = panel._objects.PlayerData;
                if (!player.IsValid || !actor.Valid || !panel._state.GetOrCreate(player, actor.Objects[0], out var state))
                    throw new Exception("没有可用的玩家状态。");

                var design = panel._converter.Convert(state, ApplicationRules.FromModifiers(state))
                 ?? throw new Exception("剪贴板中没有有效数据。");
                panel._manager.ApplyDesign(panel._selector.Selected!, design);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, $"无法应用玩家状态到{panel._selector.Selected!.Name}。",
                    $"无法应用玩家状态到设计：{panel._selector.Selected!.Identifier}", NotificationType.Error, false);
            }
        }
    }

    private sealed class UndoButton(DesignPanel panel) : Button
    {
        protected override string Description
            => "撤消上次更改。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Undo;

        public override bool Visible
            => panel._selector.Selected != null;

        protected override bool Disabled
            => (panel._selector.Selected?.WriteProtected() ?? true) || !panel._history.CanUndo(panel._selector.Selected);

        protected override void OnClick()
            => panel._history.Undo(panel._selector.Selected!);
    }
}
