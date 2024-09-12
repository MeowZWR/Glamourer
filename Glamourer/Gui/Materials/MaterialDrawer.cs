using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Glamourer.Designs;
using Glamourer.Interop.Material;
using ImGuiNET;
using OtterGui;
using OtterGui.Services;
using OtterGui.Text;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Files.MaterialStructs;
using Penumbra.GameData.Gui;

namespace Glamourer.Gui.Materials;

public class MaterialDrawer(DesignManager _designManager, Configuration _config) : IService
{
    public const float GlossWidth            = 100;
    public const float SpecularStrengthWidth = 125;

    private EquipSlot          _newSlot = EquipSlot.Head;
    private int                _newMaterialIdx;
    private int                _newRowIdx;
    private MaterialValueIndex _newKey = MaterialValueIndex.FromSlot(EquipSlot.Head);

    private Vector2 _buttonSize;
    private float   _spacing;

    public void Draw(Design design)
    {
        var available = ImGui.GetContentRegionAvail().X;
        _spacing    = ImGui.GetStyle().ItemInnerSpacing.X;
        _buttonSize = new Vector2(ImGui.GetFrameHeight());
        var colorWidth = 4 * _buttonSize.X
          + (GlossWidth + SpecularStrengthWidth) * ImGuiHelpers.GlobalScale
          + 6 * _spacing
          + ImUtf8.CalcTextSize("Revert"u8).X;
        if (available > 1.95 * colorWidth)
            DrawSingleRow(design);
        else
            DrawTwoRow(design);
        DrawNew(design);
    }

    private void DrawName(MaterialValueIndex index)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));
        ImUtf8.TextFramed(index.ToString(), 0, new Vector2((GlossWidth + SpecularStrengthWidth) * ImGuiHelpers.GlobalScale + _spacing, 0),
            borderColor: ImGui.GetColorU32(ImGuiCol.Text));
    }

    private void DrawSingleRow(Design design)
    {
        for (var i = 0; i < design.Materials.Count; ++i)
        {
            using var id = ImRaii.PushId(i);
            var (idx, value) = design.Materials[i];
            var key = MaterialValueIndex.FromKey(idx);

            DrawName(key);
            ImGui.SameLine(0, _spacing);
            DeleteButton(design, key, ref i);
            ImGui.SameLine(0, _spacing);
            CopyButton(value.Value);
            ImGui.SameLine(0, _spacing);
            PasteButton(design, key);
            ImGui.SameLine(0, _spacing);
            using var disabled = ImRaii.Disabled(design.WriteProtected());
            EnabledToggle(design, key, value.Enabled);
            ImGui.SameLine(0, _spacing);
            DrawRow(design, key, value.Value, value.Revert);
            ImGui.SameLine(0, _spacing);
            RevertToggle(design, key, value.Revert);
        }
    }

    private void DrawTwoRow(Design design)
    {
        for (var i = 0; i < design.Materials.Count; ++i)
        {
            using var id = ImRaii.PushId(i);
            var (idx, value) = design.Materials[i];
            var key = MaterialValueIndex.FromKey(idx);

            DrawName(key);
            ImGui.SameLine(0, _spacing);
            DeleteButton(design, key, ref i);
            ImGui.SameLine(0, _spacing);
            CopyButton(value.Value);
            ImGui.SameLine(0, _spacing);
            PasteButton(design, key);
            ImGui.SameLine(0, _spacing);
            EnabledToggle(design, key, value.Enabled);


            DrawRow(design, key, value.Value, value.Revert);
            ImGui.SameLine(0, _spacing);
            RevertToggle(design, key, value.Revert);
            ImGui.Separator();
        }
    }

    private void DeleteButton(Design design, MaterialValueIndex index, ref int idx)
    {
        var deleteEnabled = _config.DeleteDesignModifier.IsActive();
        if (!ImUtf8.IconButton(FontAwesomeIcon.Trash,
                $"删除此行颜色集。{(deleteEnabled ? string.Empty : $"\n按住{_config.DeleteDesignModifier}来删除。")}", disabled:
                !deleteEnabled || design.WriteProtected()))
            return;

        _designManager.ChangeMaterialValue(design, index, null);
        --idx;
    }

    private void CopyButton(in ColorRow row)
    {
        if (ImUtf8.IconButton(FontAwesomeIcon.Clipboard, "将此行导出到剪贴板。"u8))
            ColorRowClipboard.Row = row;
    }

    private void PasteButton(Design design, MaterialValueIndex index)
    {
        if (ImUtf8.IconButton(FontAwesomeIcon.Paste, "将导出的高级染色数据从剪贴板导入到此行。"u8,
                disabled: !ColorRowClipboard.IsSet || design.WriteProtected()))
            _designManager.ChangeMaterialValue(design, index, ColorRowClipboard.Row);
    }

    private void EnabledToggle(Design design, MaterialValueIndex index, bool enabled)
    {
        if (ImUtf8.Checkbox("启用"u8, ref enabled))
            _designManager.ChangeApplyMaterialValue(design, index, enabled);
    }

    private void RevertToggle(Design design, MaterialValueIndex index, bool revert)
    {
        if (ImUtf8.Checkbox("还原"u8, ref revert))
            _designManager.ChangeMaterialRevert(design, index, revert);
        ImUtf8.HoverTooltip(
            "如果选中此项，Glamourer将尝试将此行高级染色恢复到其游戏状态，不应用此行。"u8);
    }

    public sealed class MaterialSlotCombo; 

    public void DrawNew(Design design)
    {
        if (EquipSlotCombo.Draw("##slot", "为高级染色选择一个装备类型。", ref _newSlot))
            _newKey = MaterialValueIndex.FromSlot(_newSlot) with
            {
                MaterialIndex = (byte)_newMaterialIdx,
                RowIndex = (byte)_newRowIdx,
            };
        ImUtf8.SameLineInner();
        DrawMaterialIdxDrag();
        ImUtf8.SameLineInner();
        DrawRowIdxDrag();
        ImUtf8.SameLineInner();
        var exists = design.GetMaterialDataRef().TryGetValue(_newKey, out _);
        if (ImUtf8.ButtonEx("添加一行"u8,
                exists ? "所选高级染色已存在"u8 : "添加一行高级染色。"u8, Vector2.Zero,
                exists || design.WriteProtected()))
            _designManager.ChangeMaterialValue(design, _newKey, ColorRow.Empty);
    }

    private void DrawMaterialIdxDrag()
    {
        ImGui.SetNextItemWidth(ImUtf8.CalcTextSize("Material AA"u8).X);
        var format = $"Material {(char)('A' + _newMaterialIdx)}";
        if (ImUtf8.DragScalar("##Material"u8, ref _newMaterialIdx, format, 0, MaterialService.MaterialsPerModel - 1, 0.01f))
        {
            _newMaterialIdx = Math.Clamp(_newMaterialIdx, 0, MaterialService.MaterialsPerModel - 1);
            _newKey         = _newKey with { MaterialIndex = (byte)_newMaterialIdx };
        }
    }

    private void DrawRowIdxDrag()
    {
        ImGui.SetNextItemWidth(ImUtf8.CalcTextSize("Row 0000"u8).X);
        var format = $"Row {_newRowIdx / 2 + 1}{(char)(_newRowIdx % 2 + 'A')}";
        if (ImUtf8.DragScalar("##Row"u8, ref _newRowIdx, format, 0, ColorTable.NumRows - 1, 0.01f))
        {
            _newRowIdx = Math.Clamp(_newRowIdx, 0, ColorTable.NumRows - 1);
            _newKey    = _newKey with { RowIndex = (byte)_newRowIdx };
        }
    }

    private void DrawRow(Design design, MaterialValueIndex index, in ColorRow row, bool disabled)
    {
        var tmp = row;
        using var _ = ImRaii.Disabled(disabled);
        var applied = ImGuiUtil.ColorPicker("##diffuse", "更改此行的漫反射值。", row.Diffuse, v => tmp.Diffuse = v, "D");
        ImUtf8.SameLineInner();
        applied |= ImGuiUtil.ColorPicker("##specular", "更改此行的镜面反射值。", row.Specular, v => tmp.Specular = v, "S");
        ImUtf8.SameLineInner();
        applied |= ImGuiUtil.ColorPicker("##emissive", "更改此行的发光值。", row.Emissive, v => tmp.Emissive = v, "E");
        ImUtf8.SameLineInner();
        ImGui.SetNextItemWidth(GlossWidth * ImGuiHelpers.GlobalScale);
        applied |= AdvancedDyePopup.DragGloss(ref tmp.GlossStrength);
        ImUtf8.HoverTooltip("更改此行的光泽强度。"u8);
        ImUtf8.SameLineInner();
        ImGui.SetNextItemWidth(SpecularStrengthWidth * ImGuiHelpers.GlobalScale);
        applied |= AdvancedDyePopup.DragSpecularStrength(ref tmp.SpecularStrength);
        ImUtf8.HoverTooltip("更改此行的镜面反射强度。"u8);
        if (applied)
            _designManager.ChangeMaterialValue(design, index, tmp);
    }
}
