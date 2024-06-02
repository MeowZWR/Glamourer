using Dalamud.Interface;
using Dalamud.Interface.Utility;
using Dalamud.Interface.Utility.Raii;
using Glamourer.Designs;
using Glamourer.Interop.Material;
using ImGuiNET;
using OtterGui;
using OtterGui.Services;
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
          + ImGui.CalcTextSize("Revert").X;
        if (available > 1.95 * colorWidth)
            DrawSingleRow(design);
        else
            DrawTwoRow(design);
        DrawNew(design);
    }

    private void DrawName(MaterialValueIndex index)
    {
        using var style = ImRaii.PushStyle(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale).Push(ImGuiStyleVar.ButtonTextAlign, new Vector2(0, 0.5f));
        using var color = ImRaii.PushColor(ImGuiCol.Border, ImGui.GetColorU32(ImGuiCol.Text));
        ImGuiUtil.DrawTextButton(index.ToString(), new Vector2((GlossWidth + SpecularStrengthWidth) * ImGuiHelpers.GlobalScale + _spacing, 0), 0);
    }

    private void DrawSingleRow(Design design)
    {
        for (var i = 0; i < design.Materials.Count; ++i)
        {
            using var id = ImRaii.PushId(i);
            var (idx, value) = design.Materials[i];
            var       key = MaterialValueIndex.FromKey(idx);

            DrawName(key);
            ImGui.SameLine(0, _spacing);
            DeleteButton(design, key, ref i); 
            ImGui.SameLine(0, _spacing);
            CopyButton(value.Value);
            ImGui.SameLine(0, _spacing);
            PasteButton(design, key);
            ImGui.SameLine(0, _spacing);
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
        if (!ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Trash.ToIconString(), _buttonSize,
                $"删除此行颜色集。{(deleteEnabled ? string.Empty : $"\n按住{_config.DeleteDesignModifier}来删除。")}",
                !deleteEnabled, true))
            return;

        _designManager.ChangeMaterialValue(design, index, null);
        --idx;
    }

    private void CopyButton(in ColorRow row)
    {
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Clipboard.ToIconString(), _buttonSize, "将此行导出到剪贴板。",
                false,
                true))
            ColorRowClipboard.Row = row;
    }

    private void PasteButton(Design design, MaterialValueIndex index)
    {
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Paste.ToIconString(), _buttonSize,
                "将导出的高级染色数据从剪贴板导入到此行。", !ColorRowClipboard.IsSet, true))
            _designManager.ChangeMaterialValue(design, index, ColorRowClipboard.Row);
    }

    private void EnabledToggle(Design design, MaterialValueIndex index, bool enabled)
    {
        if (ImGui.Checkbox("启用", ref enabled))
            _designManager.ChangeApplyMaterialValue(design, index, enabled);
    }

    private void RevertToggle(Design design, MaterialValueIndex index, bool revert)
    {
        if (ImGui.Checkbox("还原", ref revert))
            _designManager.ChangeMaterialRevert(design, index, revert);
        ImGuiUtil.HoverTooltip(
            "如果选中此项，Glamourer将尝试将此行高级染色恢复到其游戏状态，不应用此行。");
    }

    public void DrawNew(Design design)
    {
        if (EquipSlotCombo.Draw("##slot", "为高级染色选择一个装备类型。", ref _newSlot))
            _newKey = MaterialValueIndex.FromSlot(_newSlot) with
            {
                MaterialIndex = (byte)_newMaterialIdx,
                RowIndex = (byte)_newRowIdx,
            };
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        DrawMaterialIdxDrag();
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        DrawRowIdxDrag();
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        var exists = design.GetMaterialDataRef().TryGetValue(_newKey, out _);
        if (ImGuiUtil.DrawDisabledButton("添加一行", Vector2.Zero, 
                exists ? "所选高级染色（执行集）已存在" : "添加一行高级染色（执行集）。", exists, false))
            _designManager.ChangeMaterialValue(design, _newKey, ColorRow.Empty);
    }

    private void DrawMaterialIdxDrag()
    {
        _newMaterialIdx += 1;
        ImGui.SetNextItemWidth(ImGui.CalcTextSize("Material #000").X);
        if (ImGui.DragInt("##Material", ref _newMaterialIdx, 0.01f, 1, MaterialService.MaterialsPerModel, "材质 #%i"))
        {
            _newMaterialIdx = Math.Clamp(_newMaterialIdx, 1, MaterialService.MaterialsPerModel);
            _newKey         = _newKey with { MaterialIndex = (byte)(_newMaterialIdx - 1) };
        }

        _newMaterialIdx -= 1;
    }

    private void DrawRowIdxDrag()
    {
        _newRowIdx += 1;
        ImGui.SetNextItemWidth(ImGui.CalcTextSize("Row #0000").X);
        if (ImGui.DragInt("##Row", ref _newRowIdx, 0.01f, 1, LegacyColorTable.NumUsedRows, "行 #%i"))
        {
            _newRowIdx = Math.Clamp(_newRowIdx, 1, LegacyColorTable.NumUsedRows);
            _newKey    = _newKey with { RowIndex = (byte)(_newRowIdx - 1) };
        }

        _newRowIdx -= 1;
    }

    private void DrawRow(Design design, MaterialValueIndex index, in ColorRow row, bool disabled)
    {
        var tmp = row;
        using var _ = ImRaii.Disabled(disabled);
        var applied = ImGuiUtil.ColorPicker("##diffuse", "更改此行的漫反射值。", row.Diffuse, v => tmp.Diffuse = v, "D");
        ImGui.SameLine(0, _spacing);
        applied |= ImGuiUtil.ColorPicker("##specular", "更改此行的镜面反射值。", row.Specular, v => tmp.Specular = v, "S");
        ImGui.SameLine(0, _spacing);
        applied |= ImGuiUtil.ColorPicker("##emissive", "更改此行的发光值。", row.Emissive, v => tmp.Emissive = v, "E");
        ImGui.SameLine(0, _spacing);
        ImGui.SetNextItemWidth(GlossWidth * ImGuiHelpers.GlobalScale);
        applied |= ImGui.DragFloat("##Gloss", ref tmp.GlossStrength, 0.01f, 0.001f, float.MaxValue, "%.3f G");
        ImGuiUtil.HoverTooltip("更改此行的光泽强度。");
        ImGui.SameLine(0, _spacing);
        ImGui.SetNextItemWidth(SpecularStrengthWidth * ImGuiHelpers.GlobalScale);
        applied |= ImGui.DragFloat("##Specular Strength", ref tmp.SpecularStrength, 0.01f, float.MinValue, float.MaxValue, "%.3f SS");
        ImGuiUtil.HoverTooltip("更改此行的镜面反射强度。");
        if (applied)
            _designManager.ChangeMaterialValue(design, index, tmp);
    }
}
