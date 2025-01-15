using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Glamourer.Automation;
using Glamourer.Designs;
using Glamourer.Designs.Special;
using Glamourer.Interop;
using Glamourer.Services;
using Glamourer.Unlocks;
using ImGuiNET;
using OtterGui;
using OtterGui.Log;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Widgets;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;
using Action = System.Action;

namespace Glamourer.Gui.Tabs.AutomationTab;

public class SetPanel(
    SetSelector _selector,
    AutoDesignManager _manager,
    JobService _jobs,
    ItemUnlockManager _itemUnlocks,
    SpecialDesignCombo _designCombo,
    CustomizeUnlockManager _customizeUnlocks,
    CustomizeService _customizations,
    IdentifierDrawer _identifierDrawer,
    Configuration _config,
    RandomRestrictionDrawer _randomDrawer)
{
    private readonly JobGroupCombo          _jobGroupCombo = new(_manager, _jobs, Glamourer.Log);
    private readonly HeaderDrawer.Button[] _rightButtons  = [new HeaderDrawer.IncognitoButton(_config.Ephemeral)];
    private          string?                _tempName;
    private          int                    _dragIndex = -1;

    private Action? _endAction;

    private AutoDesignSet Selection
        => _selector.Selection!;

    public void Draw()
    {
        using var group = ImRaii.Group();
        DrawHeader();
        DrawPanel();
    }

    private void DrawHeader()
        => HeaderDrawer.Draw(_selector.SelectionName, 0, ImGui.GetColorU32(ImGuiCol.FrameBg), [], _rightButtons);

    private void DrawPanel()
    {
        using var child = ImRaii.Child("##Panel", -Vector2.One, true);
        if (!child || !_selector.HasSelection)
            return;

        var spacing = ImGui.GetStyle().ItemInnerSpacing with { Y = ImGui.GetStyle().ItemSpacing.Y };

        using (_ = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing))
        {
            var enabled = Selection.Enabled;
            if (ImGui.Checkbox("##Enabled", ref enabled))
                _manager.SetState(_selector.SelectionIndex, enabled);
            ImGuiUtil.LabeledHelpMarker("启用",
                "是否应用该自动执行集中的设计。一个角色同时只能启用一个执行集。");
        }

        ImGui.SameLine();
        using (_ = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing))
        {
            var useGame = _selector.Selection!.BaseState is AutoDesignSet.Base.Game;
            if (ImGui.Checkbox("##gameState", ref useGame))
                _manager.ChangeBaseState(_selector.SelectionIndex, useGame ? AutoDesignSet.Base.Game : AutoDesignSet.Base.Current);
            ImGuiUtil.LabeledHelpMarker("使用游戏状态作为基础。",
                "启用此选项后，符合条件的角色设计将按顺序应用于游戏中角色的外观上。\n"
              + "禁用此选项后，设计将应用于角色当前被Glamourer修改后的实际外观上。");
        }

        ImGui.SameLine();
        using (_ = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, spacing))
        {
            var editing = _config.ShowAutomationSetEditing;
            if (ImGui.Checkbox("##Show Editing", ref editing))
            {
                _config.ShowAutomationSetEditing = editing;
                _config.Save();
            }

            ImGuiUtil.LabeledHelpMarker("显示可编辑内容",
                "显示更改此执行集的名称、关联角色/NPC的选项。取消勾选以精简视图。");
        }

        if (_config.ShowAutomationSetEditing)
        {
            ImGui.Dummy(Vector2.Zero);
            ImGui.Separator();
            ImGui.Dummy(Vector2.Zero);

            var name  = _tempName ?? Selection.Name;
            var flags = _selector.IncognitoMode ? ImGuiInputTextFlags.ReadOnly | ImGuiInputTextFlags.Password : ImGuiInputTextFlags.None;
            ImGui.SetNextItemWidth(330 * ImGuiHelpers.GlobalScale);
            if (ImGui.InputText("重命名执行集##Name", ref name, 128, flags))
                _tempName = name;

            if (ImGui.IsItemDeactivated())
            {
                _manager.Rename(_selector.SelectionIndex, name);
                _tempName = null;
            }

            DrawIdentifierSelection(_selector.SelectionIndex);
        }

        ImGui.Dummy(Vector2.Zero);
        ImGui.Separator();
        ImGui.Dummy(Vector2.Zero);
        DrawDesignTable();
        _randomDrawer.Draw();
    }


    private void DrawDesignTable()
    {
        var (numCheckboxes, numSpacing) = (_config.ShowAllAutomatedApplicationRules, _config.ShowUnlockedItemWarnings) switch
        {
            (true, true)   => (9, 14),
            (true, false)  => (7, 10),
            (false, true)  => (4, 4),
            (false, false) => (2, 0),
        };

        var requiredSizeOneLine = numCheckboxes * ImGui.GetFrameHeight()
          + (30 + 220 + numSpacing) * ImGuiHelpers.GlobalScale
          + 5 * ImGui.GetStyle().CellPadding.X
          + 150 * ImGuiHelpers.GlobalScale;

        var singleRow = ImGui.GetContentRegionAvail().X >= requiredSizeOneLine || numSpacing == 0;
        var numRows = (singleRow, _config.ShowUnlockedItemWarnings) switch
        {
            (true, true)   => 6,
            (true, false)  => 5,
            (false, true)  => 5,
            (false, false) => 4,
        };

        using var table = ImRaii.Table("SetTable", numRows, ImGuiTableFlags.RowBg | ImGuiTableFlags.ScrollX | ImGuiTableFlags.ScrollY);
        if (!table)
            return;

        ImGui.TableSetupColumn("##del",   ImGuiTableColumnFlags.WidthFixed, ImGui.GetFrameHeight());
        ImGui.TableSetupColumn("##Index", ImGuiTableColumnFlags.WidthFixed, 30 * ImGuiHelpers.GlobalScale);

        if (singleRow)
        {
            ImGui.TableSetupColumn("角色设计",      ImGuiTableColumnFlags.WidthFixed, 220 * ImGuiHelpers.GlobalScale);
            if (_config.ShowAllAutomatedApplicationRules)
                ImGui.TableSetupColumn("执行规则", ImGuiTableColumnFlags.WidthFixed,
                    6 * ImGui.GetFrameHeight() + 10 * ImGuiHelpers.GlobalScale);
            else
                ImGui.TableSetupColumn("使用", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Use").X);
        }
        else
        {
            ImGui.TableSetupColumn("角色设计/职业限制", ImGuiTableColumnFlags.WidthFixed, 250 * ImGuiHelpers.GlobalScale);
            if (_config.ShowAllAutomatedApplicationRules)
                ImGui.TableSetupColumn("执行规则", ImGuiTableColumnFlags.WidthFixed,
                    3 * ImGui.GetFrameHeight() + 4 * ImGuiHelpers.GlobalScale);
            else
                ImGui.TableSetupColumn("使用", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Use").X);
        }

        if (singleRow)
            ImGui.TableSetupColumn("职业限制", ImGuiTableColumnFlags.WidthStretch);

        if (_config.ShowUnlockedItemWarnings)
            ImGui.TableSetupColumn(string.Empty, ImGuiTableColumnFlags.WidthFixed, 2 * ImGui.GetFrameHeight() + 4 * ImGuiHelpers.GlobalScale);

        ImGui.TableHeadersRow();
        foreach (var (design, idx) in Selection.Designs.WithIndex())
        {
            using var id = ImRaii.PushId(idx);
            ImGui.TableNextColumn();
            var keyValid = _config.DeleteDesignModifier.IsActive();
            var tt = keyValid
                ? "移除此角色设计。"
                : $"按住 {_config.DeleteDesignModifier} 来移除此角色设计。";

            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), new Vector2(ImGui.GetFrameHeight()), tt, !keyValid, true))
                _endAction = () => _manager.DeleteDesign(Selection, idx);
            ImGui.TableNextColumn();
            ImGui.Selectable($"#{idx + 1:D2}");
            DrawDragDrop(Selection, idx);
            ImGui.TableNextColumn();
            DrawRandomEditing(Selection, design, idx);
            _designCombo.Draw(Selection, design, idx);
            DrawDragDrop(Selection, idx);
            if (singleRow)
            {
                ImGui.TableNextColumn();
                DrawApplicationTypeBoxes(Selection, design, idx, singleRow);
                ImGui.TableNextColumn();
                DrawConditions(design, idx);
            }
            else
            {
                DrawConditions(design, idx);
                ImGui.TableNextColumn();
                DrawApplicationTypeBoxes(Selection, design, idx, singleRow);
            }

            if (_config.ShowUnlockedItemWarnings)
            {
                ImGui.TableNextColumn();
                DrawWarnings(design);
            }
        }

        ImGui.TableNextColumn();
        ImGui.TableNextColumn();
        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("添加");
        ImGui.TableNextColumn();
        _designCombo.Draw(Selection, null, -1);
        ImGui.TableNextRow();

        _endAction?.Invoke();
        _endAction = null;
    }

    private int _tmpGearset = int.MaxValue;
    private int _whichIndex = -1;

    private void DrawConditions(AutoDesign design, int idx)
    {
        var usingGearset = design.GearsetIndex >= 0;
        if (ImGui.Button($"{(usingGearset ? "套装" : "职业")}##usingGearset"))
        {
            usingGearset = !usingGearset;
            _manager.ChangeGearsetCondition(Selection, idx, (short)(usingGearset ? 0 : -1));
        }

        ImGuiUtil.HoverTooltip("单击可在职业和套装之间切换限制。");

        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        if (usingGearset)
        {
            var set = 1 + (_tmpGearset == int.MaxValue || _whichIndex != idx ? design.GearsetIndex : _tmpGearset);
            ImGui.SetNextItemWidth(ImGui.GetContentRegionAvail().X);
            if (ImGui.InputInt("##whichGearset", ref set, 0, 0))
            {
                _whichIndex = idx;
                _tmpGearset = Math.Clamp(set, 1, 100);
            }

            if (ImGui.IsItemDeactivatedAfterEdit())
            {
                _manager.ChangeGearsetCondition(Selection, idx, (short)(_tmpGearset - 1));
                _tmpGearset = int.MaxValue;
                _whichIndex = -1;
            }
        }
        else
        {
            _jobGroupCombo.Draw(Selection, design, idx);
        }
    }

    private void DrawRandomEditing(AutoDesignSet set, AutoDesign design, int designIdx)
    {
        if (design.Design is not RandomDesign)
            return;

        _randomDrawer.DrawButton(set, designIdx);
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
    }

    private void DrawWarnings(AutoDesign design)
    {
        if (design.Design is not DesignBase)
            return;

        var size = new Vector2(ImGui.GetFrameHeight());
        size.X += ImGuiHelpers.GlobalScale;

        var collection = design.ApplyWhat();
        var sb         = new StringBuilder();
        var designData = design.Design.GetDesignData(default);
        foreach (var slot in EquipSlotExtensions.EqdpSlots.Append(EquipSlot.MainHand).Append(EquipSlot.OffHand))
        {
            var flag = slot.ToFlag();
            if (!collection.Equip.HasFlag(flag))
                continue;

            var item = designData.Item(slot);
            if (!_itemUnlocks.IsUnlocked(item.Id, out _))
                sb.AppendLine($"在{slot.ToName()}部位的{item.Name}还没有获取过。请考虑在游戏中去获取它！");
        }

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(2 * ImGuiHelpers.GlobalScale, 0));

        var tt = _config.UnlockedItemMode
            ? "\n这些物品将在自动应用时被跳过。\n\n要更改此设置，请禁用“已获取物品模式”。"
            : string.Empty;
        DrawWarning(sb, _config.UnlockedItemMode ? 0xA03030F0 : 0x0, size, tt, "所有待应用的物品均已解锁。");

        sb.Clear();
        var sb2       = new StringBuilder();
        var customize = designData.Customize;
        if (!designData.IsHuman)
            sb.AppendLine("基础模型id不能自动应用于某些非人类角色。");

        var set = _customizations.Manager.GetSet(customize.Clan, customize.Gender);
        foreach (var type in CustomizationExtensions.All)
        {
            var flag = type.ToFlag();
            if (!collection.Customize.HasFlag(flag))
                continue;

            if (flag.RequiresRedraw())
                sb.AppendLine($"{type.ToDefaultName()} Customization should not be changed automatically.");
            else if (type is CustomizeIndex.Hairstyle or CustomizeIndex.FacePaint
                  && set.DataByValue(type, customize[type], out var data, customize.Face) >= 0
                  && !_customizeUnlocks.IsUnlocked(data!.Value, out _))
                sb2.AppendLine(
                    $"{type.ToDefaultName()} Customization {_customizeUnlocks.Unlockable[data.Value].Name} is not unlocked but should be applied.");
        }

        ImGui.SameLine();
        tt = _config.UnlockedItemMode
            ? "\n这些外貌将在自动应用时被跳过。\n\n要更改此设置，请禁用“已获取物品模式”。"
            : string.Empty;
        DrawWarning(sb2, _config.UnlockedItemMode ? 0xA03030F0 : 0x0, size, tt, "所有待应用的外貌均已解锁。");
        ImGui.SameLine();
        return;

        static void DrawWarning(StringBuilder sb, uint color, Vector2 size, string suffix, string good)
        {
            using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale);
            if (sb.Length > 0)
            {
                sb.Append(suffix);
                using (_ = ImRaii.PushFont(UiBuilder.IconFont))
                {
                    ImGuiUtil.DrawTextButton(FontAwesomeIcon.ExclamationCircle.ToIconString(), size, color);
                }

                ImGuiUtil.HoverTooltip(sb.ToString());
            }
            else
            {
                ImGuiUtil.DrawTextButton(string.Empty, size, 0);
                ImGuiUtil.HoverTooltip(good);
            }
        }
    }

    private void DrawDragDrop(AutoDesignSet set, int index)
    {
        const string dragDropLabel = "DesignDragDrop";
        using (var target = ImRaii.DragDropTarget())
        {
            if (target.Success && ImGuiUtil.IsDropping(dragDropLabel))
            {
                if (_dragIndex >= 0)
                {
                    var idx = _dragIndex;
                    _endAction = () => _manager.MoveDesign(set, idx, index);
                }

                _dragIndex = -1;
            }
        }

        using (var source = ImRaii.DragDropSource())
        {
            if (source)
            {
                ImGui.TextUnformatted($"移动角色设计 #{index + 1:D2}...");
                if (ImGui.SetDragDropPayload(dragDropLabel, nint.Zero, 0))
                {
                    _dragIndex                 = index;
                    _selector._dragDesignIndex = index;
                }
            }
        }
    }

    private void DrawApplicationTypeBoxes(AutoDesignSet set, AutoDesign design, int autoDesignIndex, bool singleLine)
    {
        using var style      = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, new Vector2(2 * ImGuiHelpers.GlobalScale));
        var       newType    = design.Type;
        var       newTypeInt = (uint)newType;
        style.Push(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale);
        using (_ = ImRaii.PushColor(ImGuiCol.Border, ColorId.FolderLine.Value()))
        {
            if (ImGui.CheckboxFlags("##all", ref newTypeInt, (uint)ApplicationType.All))
                newType = (ApplicationType)newTypeInt;
        }

        style.Pop();
        ImGuiUtil.HoverTooltip("一键开关");
        if (_config.ShowAllAutomatedApplicationRules)
        {
            void Box(int idx)
            {
                var (type, description) = ApplicationTypeExtensions.Types[idx];
                var value = design.Type.HasFlag(type);
                if (ImGui.Checkbox($"##{(byte)type}", ref value))
                    newType = value ? newType | type : newType & ~type;
                ImGuiUtil.HoverTooltip(description);
            }

            ImGui.SameLine();
            Box(0);
            ImGui.SameLine();
            Box(1);
            if (singleLine)
                ImGui.SameLine();

            Box(2);
            ImGui.SameLine();
            Box(3);
            ImGui.SameLine();
            Box(4);
        }

        _manager.ChangeApplicationType(set, autoDesignIndex, newType);
    }

    private void DrawIdentifierSelection(int setIndex)
    {
        using var id = ImUtf8.PushId("Identifiers"u8);
        _identifierDrawer.DrawWorld(130);
        ImGui.SameLine();
        _identifierDrawer.DrawName(200 - ImGui.GetStyle().ItemSpacing.X);
        _identifierDrawer.DrawNpcs(330);
        var buttonWidth = new Vector2(90 * ImGuiHelpers.GlobalScale - ImGui.GetStyle().ItemSpacing.X / 2, 0);
        if (ImUtf8.ButtonEx("分配给玩家"u8, string.Empty, buttonWidth, !_identifierDrawer.CanSetPlayer))
            _manager.ChangeIdentifier(setIndex, _identifierDrawer.PlayerIdentifier);
        ImGui.SameLine();
        if (ImUtf8.ButtonEx("分配给NPC"u8, string.Empty, buttonWidth, !_identifierDrawer.CanSetNpc))
            _manager.ChangeIdentifier(setIndex, _identifierDrawer.NpcIdentifier);
        ImGui.SameLine();
        if (ImUtf8.ButtonEx("分配给雇员"u8, string.Empty, buttonWidth, !_identifierDrawer.CanSetRetainer))
            _manager.ChangeIdentifier(setIndex, _identifierDrawer.RetainerIdentifier);
        ImGui.SameLine();
        if (ImUtf8.ButtonEx("分配给服装模特"u8, string.Empty, buttonWidth, !_identifierDrawer.CanSetRetainer))
            _manager.ChangeIdentifier(setIndex, _identifierDrawer.MannequinIdentifier);
        ImGui.SameLine();
        if (ImUtf8.ButtonEx("分配给所属NPC"u8, string.Empty, buttonWidth, !_identifierDrawer.CanSetOwned))
            _manager.ChangeIdentifier(setIndex, _identifierDrawer.OwnedIdentifier);
    }

    private sealed class JobGroupCombo(AutoDesignManager manager, JobService jobs, Logger log)
        : FilterComboCache<JobGroup>(() => jobs.JobGroups.Values.ToList(), MouseWheelType.None, log)
    {
        public void Draw(AutoDesignSet set, AutoDesign design, int autoDesignIndex)
        {
            CurrentSelection    = design.Jobs;
            CurrentSelectionIdx = jobs.JobGroups.Values.IndexOf(j => j.Id == design.Jobs.Id);
            if (Draw("##JobGroups", design.Jobs.Name,
                    "选择应该将此设计应用于哪些职业。\n按住键盘Ctrl键+鼠标右键点击此处设置为所有职业。",
                    ImGui.GetContentRegionAvail().X, ImGui.GetTextLineHeightWithSpacing())
             && CurrentSelectionIdx >= 0)
                manager.ChangeJobCondition(set, autoDesignIndex, CurrentSelection);
            else if (ImGui.GetIO().KeyCtrl && ImGui.IsItemClicked(ImGuiMouseButton.Right))
                manager.ChangeJobCondition(set, autoDesignIndex, jobs.JobGroups[1]);
        }

        protected override string ToString(JobGroup obj)
            => obj.Name;
    }
}
