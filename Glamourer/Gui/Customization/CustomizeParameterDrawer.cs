using Dalamud.Interface;
using Glamourer.Designs;
using Glamourer.GameData;
using Glamourer.Interop.PalettePlus;
using Glamourer.State;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Services;

namespace Glamourer.Gui.Customization;

public class CustomizeParameterDrawer(Configuration config, PaletteImport import) : IService
{
    private readonly Dictionary<Design, CustomizeParameterData> _lastData    = [];
    private          string                                     _paletteName = string.Empty;
    private          CustomizeParameterData                     _data;
    private          CustomizeParameterFlag                     _flags;
    private          float                                      _width;
    private          CustomizeParameterValue?                   _copy;

    public void Draw(DesignManager designManager, Design design)
    {
        using var generalSize = EnsureSize();
        DrawPaletteImport(designManager, design);
        DrawConfig(true);

        using (_ = ImRaii.ItemWidth(_width - 2 * ImGui.GetFrameHeight() - 2 * ImGui.GetStyle().ItemInnerSpacing.X))
        {
            foreach (var flag in CustomizeParameterExtensions.RgbFlags)
                DrawColorInput3(CustomizeParameterDrawData.FromDesign(designManager, design, flag), true);

            foreach (var flag in CustomizeParameterExtensions.RgbaFlags)
                DrawColorInput4(CustomizeParameterDrawData.FromDesign(designManager, design, flag));
        }

        foreach (var flag in CustomizeParameterExtensions.PercentageFlags)
            DrawPercentageInput(CustomizeParameterDrawData.FromDesign(designManager, design, flag));

        foreach (var flag in CustomizeParameterExtensions.ValueFlags)
            DrawValueInput(CustomizeParameterDrawData.FromDesign(designManager, design, flag));
    }

    public void Draw(StateManager stateManager, ActorState state)
    {
        using var generalSize = EnsureSize();
        DrawConfig(false);
        using (_ = ImRaii.ItemWidth(_width - 2 * ImGui.GetFrameHeight() - 2 * ImGui.GetStyle().ItemInnerSpacing.X))
        {
            foreach (var flag in CustomizeParameterExtensions.RgbFlags)
                DrawColorInput3(CustomizeParameterDrawData.FromState(stateManager, state, flag), state.ModelData.Customize.Highlights);

            foreach (var flag in CustomizeParameterExtensions.RgbaFlags)
                DrawColorInput4(CustomizeParameterDrawData.FromState(stateManager, state, flag));
        }

        foreach (var flag in CustomizeParameterExtensions.PercentageFlags)
            DrawPercentageInput(CustomizeParameterDrawData.FromState(stateManager, state, flag));

        foreach (var flag in CustomizeParameterExtensions.ValueFlags)
            DrawValueInput(CustomizeParameterDrawData.FromState(stateManager, state, flag));
    }

    private void DrawPaletteCombo()
    {
        using var id    = ImRaii.PushId("Palettes");
        using var combo = ImRaii.Combo("##import", _paletteName.Length > 0 ? _paletteName : "选择Palette设置...");
        if (!combo)
            return;

        foreach (var (name, (palette, flags)) in import.Data)
        {
            if (!ImGui.Selectable(name, _paletteName == name))
                continue;

            _paletteName = name;
            _data        = palette;
            _flags       = flags;
        }
    }

    private void DrawPaletteImport(DesignManager manager, Design design)
    {
        if (!config.ShowPalettePlusImport)
            return;

        var spacing = ImGui.GetStyle().ItemInnerSpacing.X;

        DrawPaletteCombo();

        ImGui.SameLine(0, spacing);
        var value = true;
        if (ImGui.Checkbox("显示导入选项", ref value))
        {
            config.ShowPalettePlusImport = false;
            config.Save();
        }

        ImGuiUtil.HoverTooltip("在所有设计中隐藏Palette+导入栏。关闭后可以在Glamourer界面设置中重新启用。");

        var buttonWidth = new Vector2((_width - spacing) / 2, 0);
        var tt = _paletteName.Length > 0
            ? $"将从Palette+插件中的数据[{_paletteName}]导入到此设计。"
            : "请先选择一个Palette+数据。";
        if (ImGuiUtil.DrawDisabledButton("应用导入", buttonWidth, tt, _paletteName.Length == 0 || design.WriteProtected()))
        {
            _lastData[design] = design.DesignData.Parameters;
            foreach (var parameter in _flags.Iterate())
                manager.ChangeCustomizeParameter(design, parameter, _data[parameter]);
        }

        ImGui.SameLine(0, spacing);
        var enabled = _lastData.TryGetValue(design, out var oldData);
        tt = enabled
            ? $"还原[{design.Name}]到导入前的最后一组高级（外貌）参数。"
            : $"你尚未导入任何可以供[{design.Name}]还原的数据。";
        if (ImGuiUtil.DrawDisabledButton("还原导入", buttonWidth, tt, !enabled || design.WriteProtected()))
        {
            _lastData.Remove(design);
            foreach (var parameter in CustomizeParameterExtensions.AllFlags)
                manager.ChangeCustomizeParameter(design, parameter, oldData[parameter]);
        }
    }


    private void DrawConfig(bool withApply)
    {
        if (!config.ShowColorConfig)
            return;

        DrawColorDisplayOptions();
        DrawColorFormatOptions(withApply);
        var value = config.ShowColorConfig;
        ImGui.SameLine();
        if (ImGui.Checkbox("显示设置", ref value))
        {
            config.ShowColorConfig = value;
            config.Save();
        }

        ImGuiUtil.HoverTooltip(
            "隐藏“外貌（高级）”面板中的颜色配置选项。可以在Glamourer界面设置中重新启用。");
    }

    private void DrawColorDisplayOptions()
    {
        using var group = ImRaii.Group();
        if (ImGui.RadioButton("RGB", config.UseRgbForColors) && !config.UseRgbForColors)
        {
            config.UseRgbForColors = true;
            config.Save();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("HSV", !config.UseRgbForColors) && config.UseRgbForColors)
        {
            config.UseRgbForColors = false;
            config.Save();
        }
    }

    private void DrawColorFormatOptions(bool withApply)
    {
        var width = _width
          - (ImGui.CalcTextSize("浮点数").X
              + ImGui.CalcTextSize("整数").X
              + 2 * (ImGui.GetFrameHeight() + ImGui.GetStyle().ItemSpacing.X)
              + ImGui.GetStyle().ItemInnerSpacing.X
              + ImGui.GetItemRectSize().X);
        if (!withApply)
            width -= ImGui.GetFrameHeight() + ImGui.GetStyle().ItemInnerSpacing.X;

        ImGui.SameLine(0, width);
        if (ImGui.RadioButton("浮点数", config.UseFloatForColors) && !config.UseFloatForColors)
        {
            config.UseFloatForColors = true;
            config.Save();
        }

        ImGui.SameLine();
        if (ImGui.RadioButton("整数", !config.UseFloatForColors) && config.UseFloatForColors)
        {
            config.UseFloatForColors = false;
            config.Save();
        }
    }

    private void DrawColorInput3(in CustomizeParameterDrawData data, bool allowHighlights)
    {
        using var id           = ImRaii.PushId((int)data.Flag);
        var       value        = data.CurrentValue.InternalTriple;
        var       noHighlights = !allowHighlights && data.Flag is CustomizeParameterFlag.HairHighlight;
        DrawCopyPasteButtons(data, data.Locked || noHighlights);
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        using (_ = ImRaii.Disabled(data.Locked || noHighlights))
        {
            if (ImGui.ColorEdit3("##value", ref value, GetFlags()))
                data.ChangeParameter(new CustomizeParameterValue(value));
        }

        if (noHighlights)
            ImGuiUtil.HoverTooltip("挑染在“外貌”选项中被禁用，需要使用请去“外貌”中启用。", ImGuiHoveredFlags.AllowWhenDisabled);

        DrawRevert(data);

        DrawApplyAndLabel(data);
    }

    private void DrawColorInput4(in CustomizeParameterDrawData data)
    {
        using var id    = ImRaii.PushId((int)data.Flag);
        var       value = data.CurrentValue.InternalQuadruple;
        DrawCopyPasteButtons(data, data.Locked);
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        using (_ = ImRaii.Disabled(data.Locked))
        {
            if (ImGui.ColorEdit4("##value", ref value, GetFlags() | ImGuiColorEditFlags.AlphaPreviewHalf))
                data.ChangeParameter(new CustomizeParameterValue(value));
        }

        DrawRevert(data);

        DrawApplyAndLabel(data);
    }

    private void DrawValueInput(in CustomizeParameterDrawData data)
    {
        using var id    = ImRaii.PushId((int)data.Flag);
        var       value = data.CurrentValue[0];

        using (_ = ImRaii.Disabled(data.Locked))
        {
            if (ImGui.InputFloat("##value", ref value, 0.1f, 0.5f))
                data.ChangeParameter(new CustomizeParameterValue(value));
        }

        DrawRevert(data);

        DrawApplyAndLabel(data);
    }

    private void DrawPercentageInput(in CustomizeParameterDrawData data)
    {
        using var id    = ImRaii.PushId((int)data.Flag);
        var       value = data.CurrentValue[0] * 100f;

        using (_ = ImRaii.Disabled(data.Locked))
        {
            if (ImGui.SliderFloat("##value", ref value, -100f, 300, "%.2f"))
                data.ChangeParameter(new CustomizeParameterValue(value / 100f));
            ImGuiUtil.HoverTooltip("除了拖动滑块调整数值，还可以按住Ctrl单击此项手动输入任意值。");
        }

        DrawRevert(data);

        DrawApplyAndLabel(data);
    }

    private static void DrawRevert(in CustomizeParameterDrawData data)
    {
        if (data.Locked || !data.AllowRevert)
            return;

        if (ImGui.IsItemClicked(ImGuiMouseButton.Right) && ImGui.GetIO().KeyCtrl)
            data.ChangeParameter(data.GameValue);

        ImGuiUtil.HoverTooltip("按住Ctrl并单击右键可恢复到游戏值。");
    }

    private static void DrawApply(in CustomizeParameterDrawData data)
    {
        if (UiHelpers.DrawCheckbox("##apply", "当应用此设计时也应用此参数。", data.CurrentApply, out var enabled,
                data.Locked))
            data.ChangeApplyParameter(enabled);
    }

    private void DrawApplyAndLabel(in CustomizeParameterDrawData data)
    {
        if (data.DisplayApplication && !config.HideApplyCheckmarks)
        {
            ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
            DrawApply(data);
        }

        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        ImGui.TextUnformatted(data.Flag.ToName());
    }

    private ImGuiColorEditFlags GetFlags()
        => Format | Display | ImGuiColorEditFlags.HDR | ImGuiColorEditFlags.NoOptions;

    private ImGuiColorEditFlags Format
        => config.UseFloatForColors ? ImGuiColorEditFlags.Float : ImGuiColorEditFlags.Uint8;

    private ImGuiColorEditFlags Display
        => config.UseRgbForColors ? ImGuiColorEditFlags.DisplayRGB : ImGuiColorEditFlags.DisplayHSV;

    private ImRaii.IEndObject EnsureSize()
    {
        var iconSize = ImGui.GetTextLineHeight() * 2 + ImGui.GetStyle().ItemSpacing.Y + 4 * ImGui.GetStyle().FramePadding.Y;
        _width = 7 * iconSize + 4 * ImGui.GetStyle().ItemInnerSpacing.X;
        return ImRaii.ItemWidth(_width);
    }

    private void DrawCopyPasteButtons(in CustomizeParameterDrawData data, bool locked)
    {
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Copy.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
                "复制此颜色以备稍后使用。", false, true))
            _copy = data.CurrentValue;
        ImGui.SameLine(0, ImGui.GetStyle().ItemInnerSpacing.X);
        if (ImGuiUtil.DrawDisabledButton(FontAwesomeIcon.Paste.ToIconString(), new Vector2(ImGui.GetFrameHeight()),
                _copy.HasValue ? "粘贴当前复制的值。" : "尚未复制任何值。", locked || !_copy.HasValue, true))
            data.ChangeParameter(_copy!.Value);
    }
}
