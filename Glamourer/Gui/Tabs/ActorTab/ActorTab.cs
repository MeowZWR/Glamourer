using Dalamud.Interface.Utility;
using ImGuiNET;
using OtterGui.Widgets;

namespace Glamourer.Gui.Tabs.ActorTab;

public class ActorTab(ActorSelector selector, ActorPanel panel) : ITab
{
    public ReadOnlySpan<byte> Label
        => "附近角色"u8;

    public void DrawContent()
    {
        selector.Draw(200 * ImGuiHelpers.GlobalScale);
        ImGui.SameLine();
        panel.Draw();
    }
}
