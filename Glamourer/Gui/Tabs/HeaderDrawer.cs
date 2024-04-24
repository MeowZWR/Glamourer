﻿using Dalamud.Interface;
using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;

namespace Glamourer.Gui.Tabs;

public static class HeaderDrawer
{
    public abstract class Button
    {
        protected abstract void OnClick();

        protected virtual string Description
            => string.Empty;

        protected virtual uint BorderColor
            => ColorId.HeaderButtons.Value();

        protected virtual uint TextColor
            => ColorId.HeaderButtons.Value();

        protected virtual FontAwesomeIcon Icon
            => FontAwesomeIcon.None;

        protected virtual bool Disabled
            => false;

        public virtual bool Visible
            => true;

        public void Draw(float width)
        {
            if (!Visible)
                return;

            using var color = ImRaii.PushColor(ImGuiCol.Border, BorderColor)
                .Push(ImGuiCol.Text, TextColor, TextColor != 0);
            if (ImGuiUtil.DrawDisabledButton(Icon.ToIconString(), new Vector2(width, ImGui.GetFrameHeight()), string.Empty, Disabled, true))
                OnClick();
            color.Pop();
            ImGuiUtil.HoverTooltip(Description);
        }
    }

    public sealed class IncognitoButton(EphemeralConfig config) : Button
    {
        protected override string Description
            => config.IncognitoMode
                ? "关闭匿名模式。"
                : "开启匿名模式。";

        protected override FontAwesomeIcon Icon
            => config.IncognitoMode
                ? FontAwesomeIcon.EyeSlash
                : FontAwesomeIcon.Eye;

        protected override void OnClick()
        {
            config.IncognitoMode = !config.IncognitoMode;
            config.Save();
        }
    }

    public static void Draw(string text, uint textColor, uint frameColor, Button[] leftButtons, Button[] rightButtons)
    {
        var width = ImGui.GetFrameHeightWithSpacing();
        using var style = ImRaii.PushStyle(ImGuiStyleVar.ItemSpacing, Vector2.Zero)
            .Push(ImGuiStyleVar.FrameRounding,   0)
            .Push(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale);

        var leftButtonSize = 0f;
        foreach (var button in leftButtons.Where(b => b.Visible))
        {
            button.Draw(width);
            ImGui.SameLine();
            leftButtonSize += width;
        }

        var rightButtonSize = rightButtons.Count(b => b.Visible) * width;
        var midSize         = ImGui.GetContentRegionAvail().X - rightButtonSize - ImGuiHelpers.GlobalScale;

        style.Pop();
        style.Push(ImGuiStyleVar.ButtonTextAlign, new Vector2(0.5f + (rightButtonSize - leftButtonSize) / midSize, 0.5f));
        if (textColor != 0)
            ImGuiUtil.DrawTextButton(text, new Vector2(midSize, ImGui.GetFrameHeight()), frameColor, textColor);
        else
            ImGuiUtil.DrawTextButton(text, new Vector2(midSize, ImGui.GetFrameHeight()), frameColor);
        style.Pop();
        style.Push(ImGuiStyleVar.FrameBorderSize, ImGuiHelpers.GlobalScale);

        foreach (var button in rightButtons.Where(b => b.Visible))
        {
            ImGui.SameLine();
            button.Draw(width);
        }
    }
}
