﻿using Glamourer.Designs;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Widgets;

namespace Glamourer.Gui.Tabs.DesignTab;

public sealed class DesignColorCombo(DesignColors _designColors, bool _skipAutomatic) :
    FilterComboCache<string>(_skipAutomatic
            ? _designColors.Keys.OrderBy(k => k)
            : _designColors.Keys.OrderBy(k => k).Prepend(DesignColors.AutomaticName),
        MouseWheelType.Control, Glamourer.Log)
{
    protected override void OnMouseWheel(string preview, ref int current, int steps)
    {
        if (CurrentSelectionIdx < 0)
            CurrentSelectionIdx = Items.IndexOf(preview);
        base.OnMouseWheel(preview, ref current, steps);
    }

    protected override bool DrawSelectable(int globalIdx, bool selected)
    {
        var       isAutomatic = !_skipAutomatic && globalIdx == 0;
        var       key         = Items[globalIdx];
        var       color       = isAutomatic ? 0 : _designColors[key];
        using var c           = ImRaii.PushColor(ImGuiCol.Text, color, color != 0);
        var       ret         = base.DrawSelectable(globalIdx, selected);
        if (isAutomatic)
            ImGuiUtil.HoverTooltip(
                "自动配色将按照配色设置中常规颜色的定义，依据设计状态进行设置。");
        return ret;
    }
}
