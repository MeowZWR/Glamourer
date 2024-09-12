using Dalamud.Interface;
using Dalamud.Interface.ImGuiNotification;
using FFXIVClientStructs.FFXIV.Client.Game.Object;
using Glamourer.Designs;
using Glamourer.Gui.Customization;
using Glamourer.Gui.Equipment;
using Glamourer.Gui.Tabs.DesignTab;
using Glamourer.Interop;
using Glamourer.State;
using ImGuiNET;
using OtterGui;
using OtterGui.Classes;
using OtterGui.Raii;
using Penumbra.GameData.Enums;
using static Glamourer.Gui.Tabs.HeaderDrawer;

namespace Glamourer.Gui.Tabs.NpcTab;

public class NpcPanel
{
    private readonly DesignColorCombo       _colorCombo;
    private          string                 _newName = string.Empty;
    private          DesignBase?            _newDesign;
    private readonly NpcSelector            _selector;
    private readonly LocalNpcAppearanceData _favorites;
    private readonly CustomizationDrawer    _customizeDrawer;
    private readonly EquipmentDrawer        _equipDrawer;
    private readonly DesignConverter        _converter;
    private readonly DesignManager          _designManager;
    private readonly StateManager           _state;
    private readonly ObjectManager          _objects;
    private readonly DesignColors           _colors;
    private readonly Button[]              _leftButtons;
    private readonly Button[]              _rightButtons;

    public NpcPanel(NpcSelector selector,
        LocalNpcAppearanceData favorites,
        CustomizationDrawer customizeDrawer,
        EquipmentDrawer equipDrawer,
        DesignConverter converter,
        DesignManager designManager,
        StateManager state,
        ObjectManager objects,
        DesignColors colors)
    {
        _selector        = selector;
        _favorites       = favorites;
        _customizeDrawer = customizeDrawer;
        _equipDrawer     = equipDrawer;
        _converter       = converter;
        _designManager   = designManager;
        _state           = state;
        _objects         = objects;
        _colors          = colors;
        _colorCombo      = new DesignColorCombo(colors, true);
        _leftButtons =
        [
            new ExportToClipboardButton(this),
            new SaveAsDesignButton(this),
        ];
        _rightButtons =
        [
            new FavoriteButton(this),
        ];
    }

    public void Draw()
    {
        using var group = ImRaii.Group();

        DrawHeader();
        DrawPanel();
    }

    private void DrawHeader()
    {
        HeaderDrawer.Draw(_selector.HasSelection ? _selector.Selection.Name : "未选择", ColorId.NormalDesign.Value(),
            ImGui.GetColorU32(ImGuiCol.FrameBg), _leftButtons, _rightButtons);
        SaveDesignDrawPopup();
    }

    private sealed class FavoriteButton(NpcPanel panel) : Button
    {
        protected override string Description
            => panel._favorites.IsFavorite(panel._selector.Selection)
                ? "从你的收藏中移除此NPC外观。"
                : "将此NPC外观添加到你的收藏中。";

        protected override uint TextColor
            => panel._favorites.IsFavorite(panel._selector.Selection)
                ? ColorId.FavoriteStarOn.Value()
                : 0x80000000;

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Star;

        public override bool Visible
            => panel._selector.HasSelection;

        protected override void OnClick()
            => panel._favorites.ToggleFavorite(panel._selector.Selection);
    }

    private void SaveDesignDrawPopup()
    {
        if (!ImGuiUtil.OpenNameField("保存为设计", ref _newName))
            return;

        if (_newDesign != null && _newName.Length > 0)
            _designManager.CreateClone(_newDesign, _newName, true);
        _newDesign = null;
        _newName   = string.Empty;
    }

    private void DrawPanel()
    {
        using var child = ImRaii.Child("##Panel", -Vector2.One, true);
        if (!child || !_selector.HasSelection)
            return;

        DrawButtonRow();
        DrawCustomization();
        DrawEquipment();
        DrawAppearanceInfo();
    }

    private void DrawButtonRow()
    {
        DrawApplyToSelf();
        ImGui.SameLine();
        DrawApplyToTarget();
    }

    private void DrawCustomization()
    {
        var header = _selector.Selection.ModelId == 0
            ? "外貌"
            : $"外貌（模型Id #{_selector.Selection.ModelId}）###Customization";
        using var h = ImRaii.CollapsingHeader(header);
        if (!h)
            return;

        _customizeDrawer.Draw(_selector.Selection.Customize, true, true);
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
    }

    private void DrawEquipment()
    {
        using var h = ImRaii.CollapsingHeader("装备");
        if (!h)
            return;

        _equipDrawer.Prepare();
        var designData = ToDesignData();

        foreach (var slot in EquipSlotExtensions.EqdpSlots)
        {
            var data = new EquipDrawData(slot, designData) { Locked = true };
            _equipDrawer.DrawEquip(data);
        }

        var mainhandData = new EquipDrawData(EquipSlot.MainHand, designData) { Locked = true };
        var offhandData  = new EquipDrawData(EquipSlot.OffHand,  designData) { Locked = true };
        _equipDrawer.DrawWeapons(mainhandData, offhandData, false);

        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
        EquipmentDrawer.DrawMetaToggle(ToggleDrawData.FromValue(MetaIndex.VisorState, _selector.Selection.VisorToggled));
        ImGui.Dummy(new Vector2(ImGui.GetTextLineHeight() / 2));
    }

    private DesignData ToDesignData()
    {
        var selection  = _selector.Selection;
        var items      = _converter.FromDrawData(selection.Equip.ToArray(), selection.Mainhand, selection.Offhand, true).ToArray();
        var designData = new DesignData { Customize = selection.Customize };
        foreach (var (slot, item, stain) in items)
        {
            designData.SetItem(slot, item);
            designData.SetStain(slot, stain);
        }

        return designData;
    }

    private void DrawApplyToSelf()
    {
        var (id, data) = _objects.PlayerData;
        if (!ImGuiUtil.DrawDisabledButton("应用到自己", Vector2.Zero,
                "将当前NPC外观应用于你的角色。\n按住Ctrl仅应用装备。\n按住Shift仅应用外貌。",
                !data.Valid))
            return;

        if (_state.GetOrCreate(id, data.Objects[0], out var state))
        {
            var design = _converter.Convert(ToDesignData(), new StateMaterialManager(), ApplicationRules.NpcFromModifiers());
            _state.ApplyDesign(state, design, ApplySettings.Manual);
        }
    }

    private void DrawApplyToTarget()
    {
        var (id, data) = _objects.TargetData;
        var tt = id.IsValid
            ? data.Valid
                ? "将当前NPC外观应用于你的目标。\n按住Ctrl仅应用装备。\n按住Shift仅应用外貌。"
                : "当前目标无法操作。"
            : "未选择有效的目标。";
        if (!ImGuiUtil.DrawDisabledButton("应用到目标", Vector2.Zero, tt, !data.Valid))
            return;

        if (_state.GetOrCreate(id, data.Objects[0], out var state))
        {
            var design = _converter.Convert(ToDesignData(), new StateMaterialManager(), ApplicationRules.NpcFromModifiers());
            _state.ApplyDesign(state, design, ApplySettings.Manual);
        }
    }


    private void DrawAppearanceInfo()
    {
        using var h = ImRaii.CollapsingHeader("外观详情");
        if (!h)
            return;

        using var table = ImRaii.Table("Details", 2);
        if (!table)
            return;

        using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));
        ImGui.TableSetupColumn("Type", ImGuiTableColumnFlags.WidthFixed, ImGui.CalcTextSize("Last Update Datem").X);
        ImGui.TableSetupColumn("Data", ImGuiTableColumnFlags.WidthStretch);

        var selection = _selector.Selection;
        CopyButton("NPC名称", selection.Name);
        CopyButton("NPC ID",   selection.Id.Id.ToString());
        ImGuiUtil.DrawFrameColumn("NPC类型");
        ImGui.TableNextColumn();
        var width = ImGui.GetContentRegionAvail().X;
        ImGuiUtil.DrawTextButton(selection.Kind is ObjectKind.BattleNpc ? "战斗NPC" : "事件NPC", new Vector2(width, 0),
            ImGui.GetColorU32(ImGuiCol.FrameBg));

        ImGuiUtil.DrawFrameColumn("配色");
        var color     = _favorites.GetColor(selection);
        var colorName = color.Length == 0 ? DesignColors.AutomaticName : color;
        ImGui.TableNextColumn();
        if (_colorCombo.Draw("##colorCombo", colorName,
                "将颜色与此NPC外观相关联。\n"
              + "右键单击可恢复为自动配色。\n"
              + "按住Ctrl并滚动鼠标滚轮进行滚动选择。",
                width - ImGui.GetStyle().ItemSpacing.X - ImGui.GetFrameHeight(), ImGui.GetTextLineHeight())
         && _colorCombo.CurrentSelection != null)
        {
            color = _colorCombo.CurrentSelection is DesignColors.AutomaticName ? string.Empty : _colorCombo.CurrentSelection;
            _favorites.SetColor(selection, color);
        }

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right))
        {
            _favorites.SetColor(selection, string.Empty);
            color = string.Empty;
        }

        if (_colors.TryGetValue(color, out var currentColor))
        {
            ImGui.SameLine();
            if (DesignColorUi.DrawColorButton($"Color associated with {color}", currentColor, out var newColor))
                _colors.SetColor(color, newColor);
        }
        else if (color.Length != 0)
        {
            ImGui.SameLine();
            var       size = new Vector2(ImGui.GetFrameHeight());
            using var font = ImRaii.PushFont(UiBuilder.IconFont);
            ImGuiUtil.DrawTextButton(FontAwesomeIcon.ExclamationCircle.ToIconString(), size, 0, _colors.MissingColor);
            ImGuiUtil.HoverTooltip("与此设计相关联的颜色不存在。");
        }

        return;

        static void CopyButton(string label, string text)
        {
            ImGuiUtil.DrawFrameColumn(label);
            ImGui.TableNextColumn();
            if (ImGui.Button(text, new Vector2(ImGui.GetContentRegionAvail().X, 0)))
                ImGui.SetClipboardText(text);
            ImGuiUtil.HoverTooltip("Click to copy to clipboard.");
        }
    }

    private sealed class ExportToClipboardButton(NpcPanel panel) : Button
    {
        protected override string Description
            => "将当前NPC外观复制到剪贴板。\n按住Ctrl禁止复制外貌。\n按住Shift可禁止复制装备。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Copy;

        public override bool Visible
            => panel._selector.HasSelection;

        protected override void OnClick()
        {
            try
            {
                var data = panel.ToDesignData();
                var text = panel._converter.ShareBase64(data, new StateMaterialManager(), ApplicationRules.NpcFromModifiers());
                ImGui.SetClipboardText(text);
            }
            catch (Exception ex)
            {
                Glamourer.Messager.NotificationMessage(ex, $"无法复制{panel._selector.Selection.Name}的数据到剪贴板。",
                    $"无法从NPC外观{panel._selector.Selection.Kind} {panel._selector.Selection.Id.Id}复制数据到剪贴板。",
                    NotificationType.Error);
            }
        }
    }

    private sealed class SaveAsDesignButton(NpcPanel panel) : Button
    {
        protected override string Description
            => "将此NPC外观保存为设计。\n按住Ctrl禁止保存外貌。\n按住Shift可禁止保存装备。";

        protected override FontAwesomeIcon Icon
            => FontAwesomeIcon.Save;

        public override bool Visible
            => panel._selector.HasSelection;

        protected override void OnClick()
        {
            ImGui.OpenPopup("保存为设计");
            panel._newName = panel._selector.Selection.Name;
            var data = panel.ToDesignData();
            panel._newDesign = panel._converter.Convert(data, new StateMaterialManager(), ApplicationRules.NpcFromModifiers());
        }
    }
}
