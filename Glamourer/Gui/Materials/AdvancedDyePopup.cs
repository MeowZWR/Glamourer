using Dalamud.Interface;
using Dalamud.Interface.Utility;
using FFXIVClientStructs.FFXIV.Client.Graphics.Kernel;
using FFXIVClientStructs.FFXIV.Client.System.Resource.Handle;
using FFXIVClientStructs.Interop;
using Glamourer.Designs;
using Glamourer.Interop.Material;
using Glamourer.State;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Interop;
using Penumbra.String;

namespace Glamourer.Gui.Materials;

public sealed unsafe class AdvancedDyePopup(
    Configuration config,
    StateManager stateManager,
    LiveColorTablePreviewer preview,
    DirectXService directX) : IService
{
    private MaterialValueIndex? _drawIndex;
    private ActorState          _state = null!;
    private Actor               _actor;
    private byte                _selectedMaterial = byte.MaxValue;
    private bool                _anyChanged;

    private bool ShouldBeDrawn()
    {
        if (!config.UseAdvancedDyes)
            return false;

        if (_drawIndex is not { Valid: true })
            return false;

        if (!_actor.IsCharacter || !_state.ModelData.IsHuman || !_actor.Model.IsHuman)
            return false;

        return true;
    }

    public void DrawButton(EquipSlot slot)
        => DrawButton(MaterialValueIndex.FromSlot(slot));

    private void DrawButton(MaterialValueIndex index)
    {
        if (!config.UseAdvancedDyes)
            return;

        ImGui.SameLine();
        using var id     = ImRaii.PushId(index.SlotIndex | ((int)index.DrawObject << 8));
        var       isOpen = index == _drawIndex;

        using (ImRaii.PushColor(ImGuiCol.Button, ImGui.GetColorU32(ImGuiCol.ButtonActive), isOpen)
                   .Push(ImGuiCol.Text,   ColorId.HeaderButtons.Value(), isOpen)
                   .Push(ImGuiCol.Border, ColorId.HeaderButtons.Value(), isOpen))
        {
            using var frame = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, 2 * ImGuiHelpers.GlobalScale, isOpen);
            if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Palette.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
                    string.Empty, false, true))
            {
                _selectedMaterial = byte.MaxValue;
                _drawIndex        = isOpen ? null : index;
            }
        }

        ImGuiUtil.HoverTooltip("打开此插槽的高级染色窗口。");
    }

    private (string Path, string GamePath) ResourceName(MaterialValueIndex index)
    {
        var materialHandle = (MaterialResourceHandle*)_actor.Model.AsCharacterBase->MaterialsSpan[
            index.MaterialIndex + index.SlotIndex * MaterialService.MaterialsPerModel].Value;
        var model       = _actor.Model.AsCharacterBase->ModelsSpan[index.SlotIndex].Value;
        var modelHandle = model == null ? null : model->ModelResourceHandle;
        var path = materialHandle == null
            ? string.Empty
            : ByteString.FromSpanUnsafe(materialHandle->ResourceHandle.FileName.AsSpan(), true).ToString();
        var gamePath = modelHandle == null
            ? string.Empty
            : modelHandle->GetMaterialFileNameBySlotAsString(index.MaterialIndex);
        return (path, gamePath);
    }

    private void DrawTabBar(ReadOnlySpan<Pointer<Texture>> textures, ref bool firstAvailable)
    {
        using var bar = ImRaii.TabBar("tabs");
        if (!bar)
            return;

        for (byte i = 0; i < MaterialService.MaterialsPerModel; ++i)
        {
            var index     = _drawIndex!.Value with { MaterialIndex = i };
            var available = index.TryGetTexture(textures, out var texture) && directX.TryGetColorTable(*texture, out var table);

            if (index == preview.LastValueIndex with { RowIndex = 0 })
                table = preview.LastOriginalColorTable;

            using var disable = ImRaii.Disabled(!available);
            var select = available && firstAvailable && _selectedMaterial == byte.MaxValue
                ? ImGuiTabItemFlags.SetSelected
                : ImGuiTabItemFlags.None;

            if (available)
                firstAvailable = false;

            using var tab = _label.TabItem(i, select);
            if (ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            {
                using var enabled = ImRaii.Enabled();
                var (path, gamePath) = ResourceName(index);
                if (gamePath.Length == 0 || path.Length == 0)
                    ImGui.SetTooltip("此材质不存在。");
                else if (!available)
                    ImGui.SetTooltip($"此材质没有关联的颜色集。\n\n{gamePath}\n{path}");
                else
                    ImGui.SetTooltip($"{gamePath}\n{path}");
            }

            if ((tab.Success || select is ImGuiTabItemFlags.SetSelected) && available)
            {
                _selectedMaterial = i;
                DrawTable(index, table);
            }
        }

        using (ImRaii.PushFont(UiBuilder.IconFont))
        {
            if (ImGui.TabItemButton($"{FontAwesomeIcon.Times.ToIconString()} ", ImGuiTabItemFlags.NoTooltip))
                _drawIndex = null;
        }

        ImGuiUtil.HoverTooltip("关闭高级染色窗口。");
    }

    private void DrawContent(ReadOnlySpan<Pointer<Texture>> textures)
    {
        var firstAvailable = true;
        DrawTabBar(textures, ref firstAvailable);

        if (firstAvailable)
            ImGui.TextUnformatted("没有可编辑的材质。");
    }

    private void DrawWindow(ReadOnlySpan<Pointer<Texture>> textures)
    {
        var flags = ImGuiWindowFlags.NoFocusOnAppearing
          | ImGuiWindowFlags.NoCollapse
          | ImGuiWindowFlags.NoDecoration
          | ImGuiWindowFlags.NoResize;

        // Set position to the right of the main window when attached
        // The downwards offset is implicit through child position.
        if (config.KeepAdvancedDyesAttached)
        {
            var position = ImGui.GetWindowPos();
            position.X += ImGui.GetWindowSize().X + ImGui.GetStyle().WindowPadding.X;
            ImGui.SetNextWindowPos(position);
            flags |= ImGuiWindowFlags.NoMove;
        }

        var size = new Vector2(7 * ImGui.GetFrameHeight() + 3 * ImGui.GetStyle().ItemInnerSpacing.X + 300 * ImGuiHelpers.GlobalScale,
            18 * ImGui.GetFrameHeightWithSpacing() + ImGui.GetStyle().WindowPadding.Y + 2 * ImGui.GetStyle().ItemSpacing.Y);
        ImGui.SetNextWindowSize(size);

        var window = ImGui.Begin("###Glamourer Advanced Dyes", flags);
        try
        {
            if (window)
                DrawContent(textures);
        }
        finally
        {
            ImGui.End();
        }
    }

    public void Draw(Actor actor, ActorState state)
    {
        _actor = actor;
        _state = state;
        if (!ShouldBeDrawn())
            return;

        if (_drawIndex!.Value.TryGetTextures(actor, out var textures))
            DrawWindow(textures);
    }

    private void DrawTable(MaterialValueIndex materialIndex, in LegacyColorTable table)
    {
        using var disabled = ImRaii.Disabled(_state.IsLocked);
        _anyChanged = false;
        for (byte i = 0; i < LegacyColorTable.NumUsedRows; ++i)
        {
            var     index = materialIndex with { RowIndex = i };
            ref var row   = ref table[i];
            DrawRow(ref row, index, table);
        }

        ImGui.Separator();
        DrawAllRow(materialIndex, table);
    }

    private void DrawAllRow(MaterialValueIndex materialIndex, in LegacyColorTable table)
    {
        using var id         = ImRaii.PushId(100);
        var       buttonSize = new Vector2(ImGui.GetFrameHeight());
        ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Crosshairs.ToIconString(), buttonSize, "高亮显示角色身上所有受影响的颜色。",
            false, true);
        if (ImGui.IsItemHovered())
            preview.OnHover(materialIndex with { RowIndex = byte.MaxValue }, _actor.Index, table);
        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushFont(UiBuilder.MonoFont))
        {
            ImGui.TextUnformatted("所有颜色行");
        }

        var spacing = ImGui.GetStyle().ItemInnerSpacing.X;
        ImGui.SameLine(ImGui.GetWindowSize().X - 3 * buttonSize.X - 3 * spacing);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Clipboard.ToIconString(), buttonSize, "将此颜色集表导出到剪贴板。", false,
                true))
            ColorRowClipboard.Table = table;
        ImGui.SameLine(0, spacing);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Paste.ToIconString(), buttonSize,
                "将导出的表从剪贴板导入到此表。", !ColorRowClipboard.IsTableSet, true))
            foreach (var (row, idx) in ColorRowClipboard.Table.WithIndex())
            {
                var internalRow = new ColorRow(row);
                var slot        = materialIndex.ToEquipSlot();
                var weapon = slot is EquipSlot.MainHand or EquipSlot.OffHand
                    ? _state.ModelData.Weapon(slot)
                    : _state.ModelData.Armor(slot).ToWeapon(0);
                var value = new MaterialValueState(internalRow, internalRow, weapon, StateSource.Manual);
                stateManager.ChangeMaterialValue(_state, materialIndex with { RowIndex = (byte)idx }, value, ApplySettings.Manual);
            }

        ImGui.SameLine(0, spacing);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.UndoAlt.ToIconString(), buttonSize, "将此表重置为游戏状态。", !_anyChanged,
                true))
            for (byte i = 0; i < LegacyColorTable.NumUsedRows; ++i)
                stateManager.ResetMaterialValue(_state, materialIndex with { RowIndex = i }, ApplySettings.Game);
    }

    private void DrawRow(ref LegacyColorTable.Row row, MaterialValueIndex index, in LegacyColorTable table)
    {
        using var id      = ImRaii.PushId(index.RowIndex);
        var       changed = _state.Materials.TryGetValue(index, out var value);
        if (!changed)
        {
            var internalRow = new ColorRow(row);
            var slot        = index.ToEquipSlot();
            var weapon = slot is EquipSlot.MainHand or EquipSlot.OffHand
                ? _state.ModelData.Weapon(slot)
                : _state.ModelData.Armor(slot).ToWeapon(0);
            value = new MaterialValueState(internalRow, internalRow, weapon, StateSource.Manual);
        }
        else
        {
            _anyChanged = true;
            value       = new MaterialValueState(value.Game, value.Model, value.DrawData, StateSource.Manual);
        }

        var buttonSize = new Vector2(ImGui.GetFrameHeight());
        ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Crosshairs.ToIconString(), buttonSize, "高亮显示角色身上受影响的颜色。",
            false, true);
        if (ImGui.IsItemHovered())
            preview.OnHover(index, _actor.Index, table);

        ImGui.SameLine();
        ImGui.AlignTextToFramePadding();
        using (ImRaii.PushFont(UiBuilder.MonoFont))
        {
            ImGui.TextUnformatted($"行#{index.RowIndex + 1:D2}");
        }

        ImGui.SameLine(0, ImGui.GetStyle().ItemSpacing.X * 2);
        var applied = ImGuiUtil.ColorPicker("##diffuse", "更改此行的漫反射值。", value.Model.Diffuse,
            v => value.Model.Diffuse = v, "D");

        var spacing = ImGui.GetStyle().ItemInnerSpacing;
        ImGui.SameLine(0, spacing.X);
        applied |= ImGuiUtil.ColorPicker("##specular", "更改此行的镜面反射值。", value.Model.Specular,
            v => value.Model.Specular = v, "S");
        ImGui.SameLine(0, spacing.X);
        applied |= ImGuiUtil.ColorPicker("##emissive", "更改此行的发光值。", value.Model.Emissive,
            v => value.Model.Emissive = v, "E");
        ImGui.SameLine(0, spacing.X);
        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        applied |= ImGui.DragFloat("##Gloss", ref value.Model.GlossStrength, 0.01f, 0.001f, float.MaxValue, "%.3f G")
         && value.Model.GlossStrength > 0;
        ImGuiUtil.HoverTooltip("更改此行的光泽强度。");
        ImGui.SameLine(0, spacing.X);
        ImGui.SetNextItemWidth(100 * ImGuiHelpers.GlobalScale);
        applied |= ImGui.DragFloat("##Specular Strength", ref value.Model.SpecularStrength, 0.01f, float.MinValue, float.MaxValue, "%.3f SS");
        ImGuiUtil.HoverTooltip("更改此行的镜面反射强度。");
        ImGui.SameLine(0, spacing.X);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Clipboard.ToIconString(), buttonSize, "将此行导出到剪贴板。", false,
                true))
            ColorRowClipboard.Row = value.Model;
        ImGui.SameLine(0, spacing.X);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Paste.ToIconString(), buttonSize,
                "将导出的行从剪贴板导入到此行。", !ColorRowClipboard.IsSet, true))
        {
            value.Model = ColorRowClipboard.Row;
            applied     = true;
        }

        ImGui.SameLine(0, spacing.X);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.UndoAlt.ToIconString(), buttonSize, "将此行重置为游戏状态。", !changed, true))
            stateManager.ResetMaterialValue(_state, index, ApplySettings.Game);

        if (applied)
            stateManager.ChangeMaterialValue(_state, index, value, ApplySettings.Manual);
    }

    private LabelStruct _label = new();

    private struct LabelStruct
    {
        private fixed byte _label[12];

        public ImRaii.IEndObject TabItem(byte materialIndex, ImGuiTabItemFlags flags)
        {
            _label[10] = (byte)('1' + materialIndex);
            fixed (byte* ptr = _label)
            {
                return ImRaii.TabItem(ptr, flags | ImGuiTabItemFlags.NoTooltip);
            }
        }

        public LabelStruct()
        {
            _label[0]  = (byte)'M';
            _label[1]  = (byte)'a';
            _label[2]  = (byte)'t';
            _label[3]  = (byte)'e';
            _label[4]  = (byte)'r';
            _label[5]  = (byte)'i';
            _label[6]  = (byte)'a';
            _label[7]  = (byte)'l';
            _label[8]  = (byte)' ';
            _label[9]  = (byte)'#';
            _label[11] = 0;
        }
    }
}
