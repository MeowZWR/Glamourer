using Dalamud.Interface;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using Penumbra.GameData.Enums;
using Penumbra.GameData.Structs;

namespace Glamourer.Gui.Customization;

public partial class CustomizationDrawer
{
    private void DrawRaceGenderSelector()
    {
        DrawGenderSelector();
        ImGui.SameLine();
        using var group = ImRaii.Group();
        DrawRaceCombo();
        if (_withApply)
        {
            using var disabled = ImRaii.Disabled(_locked);
            if (UiHelpers.DrawCheckbox("##applyGender", "应用此设计的性别。", ChangeApply.HasFlag(CustomizeFlag.Gender),
                    out var applyGender, _locked))
                ChangeApply = applyGender ? ChangeApply | CustomizeFlag.Gender : ChangeApply & ~CustomizeFlag.Gender;
            ImGui.SameLine();
            if (UiHelpers.DrawCheckbox("##applyClan", "应用此设计的部族。", ChangeApply.HasFlag(CustomizeFlag.Clan), out var applyClan,
                    _locked))
                ChangeApply = applyClan ? ChangeApply | CustomizeFlag.Clan : ChangeApply & ~CustomizeFlag.Clan;
            ImGui.SameLine();
        }

        ImGui.AlignTextToFramePadding();
        ImGui.TextUnformatted("性别 & 部族");
    }

    private void DrawGenderSelector()
    {
        using (ImRaii.Disabled(_locked || _lockedRedraw))
        {
            var icon = _customize.Gender switch
            {
                Gender.Male   => FontAwesomeIcon.Mars,
                Gender.Female => FontAwesomeIcon.Venus,
                _             => FontAwesomeIcon.Question,
            };

            if (ImGuiUtil.DrawDisabledButton(icon.ToIconString(), _framedIconSize, string.Empty,
                    icon is not FontAwesomeIcon.Mars and not FontAwesomeIcon.Venus, true))
                Changed |= _service.ChangeGender(ref _customize, icon is FontAwesomeIcon.Mars ? Gender.Female : Gender.Male);
        }

        if (_lockedRedraw && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip(
                "无法更改性别，这需要重绘角色，而此角色不支持这个操作。");
    }

    private void DrawRaceCombo()
    {
        using (ImRaii.Disabled(_locked || _lockedRedraw))
        {
            ImGui.SetNextItemWidth(_raceSelectorWidth);
            using (var combo = ImRaii.Combo("##subRaceCombo", _service.ClanName(_customize.Clan, _customize.Gender)))
            {
                if (combo)
                    foreach (var subRace in Enum.GetValues<SubRace>().Skip(1)) // Skip Unknown
                    {
                        if (ImGui.Selectable(_service.ClanName(subRace, _customize.Gender), subRace == _customize.Clan))
                            Changed |= _service.ChangeClan(ref _customize, subRace);
                    }
            }
        }

        if (_lockedRedraw && ImGui.IsItemHovered(ImGuiHoveredFlags.AllowWhenDisabled))
            ImGui.SetTooltip("无法更改种族，这需要重绘角色，而此角色不支持这个操作。");
    }

    private void DrawBodyType()
    {
        if (_customize.BodyType.Value == 1)
            return;

        var label = _lockedRedraw
            ? $"Body Type {_customize.BodyType.Value}"
            : $"Reset Body Type {_customize.BodyType.Value} to Default";
        if (!ImGuiUtil.DrawDisabledButton(label, new Vector2(_raceSelectorWidth + _framedIconSize.X + ImGui.GetStyle().ItemSpacing.X, 0),
                string.Empty, _lockedRedraw))
            return;

        Changed             |= CustomizeFlag.BodyType;
        _customize.BodyType =  (CustomizeValue)1;
    }
}
