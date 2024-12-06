using Dalamud.Game.ClientState.Keys;
using Dalamud.Interface;
using Dalamud.Interface.Components;
using Dalamud.Interface.Utility;
using Dalamud.Plugin.Services;
using Glamourer.Designs;
using Glamourer.Gui.Tabs.DesignTab;
using Glamourer.Interop;
using Glamourer.Interop.PalettePlus;
using ImGuiNET;
using OtterGui;
using OtterGui.Raii;
using OtterGui.Text;
using OtterGui.Widgets;

namespace Glamourer.Gui.Tabs.SettingsTab;

public class SettingsTab(
    Configuration config,
    DesignFileSystemSelector selector,
    ContextMenuService contextMenuService,
    IUiBuilder uiBuilder,
    GlamourerChangelog changelog,
    IKeyState keys,
    DesignColorUi designColorUi,
    PaletteImport paletteImport,
    PalettePlusChecker paletteChecker,
    CollectionOverrideDrawer overrides,
    CodeDrawer codeDrawer,
    Glamourer glamourer)
    : ITab
{
    private readonly VirtualKey[] _validKeys = keys.GetValidVirtualKeys().Prepend(VirtualKey.NO_KEY).ToArray();

    public ReadOnlySpan<byte> Label
        => "插件设置"u8;

    public void DrawContent()
    {
        using var child = ImRaii.Child("MainWindowChild");
        if (!child)
            return;

        Checkbox("启用自动执行",
            "启用关联角色的自动执行功能。",
            config.EnableAutoDesigns, v => config.EnableAutoDesigns = v);
        ImGui.NewLine();
        ImGui.NewLine();
        ImGui.NewLine();
        ImGui.NewLine();
        ImGui.NewLine();

        using (ImRaii.Child("SettingsChild"))
        {
            DrawBehaviorSettings();
            DrawDesignDefaultSettings();
            DrawInterfaceSettings();
            DrawColorSettings();
            overrides.Draw();
            codeDrawer.Draw();
        }

        MainWindow.DrawSupportButtons(glamourer, changelog.Changelog);
    }

    private void DrawBehaviorSettings()
    {
        if (!ImGui.CollapsingHeader("特性设置"))
            return;

        Checkbox("总是为主手应用整套武器",
            "当手动应用主手武器时，自动应用对应的副手武器。",
            config.ChangeEntireItem, v => config.ChangeEntireItem = v);
        Checkbox("自动替换不兼容种族和性别的装备",
            "当检测到某些项目不适用角色当前的种族和性别时，使用匹配种族和性别的模型。",
            config.UseRestrictedGearProtection, v => config.UseRestrictedGearProtection = v);
        Checkbox("不在自动执行集中使用未获得过的物品",
            "如果你希望“自动执行”中只使用你已经获取过一次的物品，不使用那些从未获取过的物品，就启用这个选项。",
            config.UnlockedItemMode, v => config.UnlockedItemMode = v);
        Checkbox("编辑自动化时尊重手动更改",
            "对当前任何处于活动状态的自动执行组进行更改，在重新应用修改后的自动执行时是否保留手动作出的更改。",
            config.RespectManualOnAutomationUpdate, v => config.RespectManualOnAutomationUpdate = v);
        Checkbox("启动节日彩蛋",
            "Glamourer也许会在一些特别的日子做一些有趣的事情。如果你觉得这会影响你的体验，请禁用此选项。",
            config.DisableFestivals == 0, v => config.DisableFestivals = v ? (byte)0 : (byte)2);
        Checkbox("自动重新加载装备",
            "在更改Penumbra模组选项时，自动在自己的角色身上重新加载装备部件。",
            config.AutoRedrawEquipOnChanges, v => config.AutoRedrawEquipOnChanges = v);
        Checkbox("在更换区域时撤销手动更改",
            "当你更换区域时，撤销你对角色进行的手动更改，恢复到游戏基础状态或自动执行状态。",
            config.RevertManualChangesOnZoneChange, v => config.RevertManualChangesOnZoneChange = v);
        Checkbox("启用外貌（高级）选项",
            "启用外貌（高级）选项（如调色板）的显示和编辑。",
            config.UseAdvancedParameters, paletteChecker.SetAdvancedParameters);
        PaletteImportButton();
        Checkbox("启用染色（高级）选项",
            "启用所有装备的高级染色（颜色集）的显示和编辑",
            config.UseAdvancedDyes, v => config.UseAdvancedDyes = v);
        Checkbox("始终应用关联的模组",
            "无论何时将设计应用于角色（包括自动执行）时，Glamourer都会尝试将设计相关的模组设置应用于当前与该角色相关的合集（如果可用）。\n\n"
          + "Glamourer不会自动还原这些应用的设置。这可能会打乱你的合集和配置。\n\n"
          + "如果你启用此设置，你应意识到任何由此产生的配置错误都是你自己造成的。。",
            config.AlwaysApplyAssociatedMods, v => config.AlwaysApplyAssociatedMods = v);
        ImGui.NewLine();
    }

    private void DrawDesignDefaultSettings()
    {
        if (!ImUtf8.CollapsingHeader("Design Defaults"))
            return;

        Checkbox("Show in Quick Design Bar", "Newly created designs will be shown in the quick design bar by default.",
            config.DefaultDesignSettings.ShowQuickDesignBar, v => config.DefaultDesignSettings.ShowQuickDesignBar = v);
        Checkbox("Reset Advanced Dyes", "Newly created designs will be configured to reset advanced dyes on application by default.",
            config.DefaultDesignSettings.ResetAdvancedDyes, v => config.DefaultDesignSettings.ResetAdvancedDyes = v);
        Checkbox("Always Force Redraw", "Newly created designs will be configured to force character redraws on application by default.",
            config.DefaultDesignSettings.AlwaysForceRedrawing, v => config.DefaultDesignSettings.AlwaysForceRedrawing = v);
    }

    private void DrawInterfaceSettings()
    {
        if (!ImGui.CollapsingHeader("界面设置"))
            return;

        EphemeralCheckbox("显示快速设计栏",
            "显示一个与主窗口分离的工具栏，允许您快速应用设计、恢复角色和目标。",
            config.Ephemeral.ShowDesignQuickBar, v => config.Ephemeral.ShowDesignQuickBar = v);
        EphemeralCheckbox("锁定快速设计栏", "防止快速设计栏被移动，将其锁定在当前位置。",
            config.Ephemeral.LockDesignQuickBar,
            v => config.Ephemeral.LockDesignQuickBar = v);
        if (Widget.ModifiableKeySelector("快速设计栏开关热键", "设置一个用于打开或关闭快速设计栏的热键。",
                100 * ImGuiHelpers.GlobalScale,
                config.ToggleQuickDesignBar, v => config.ToggleQuickDesignBar = v, _validKeys))
            config.Save();
        Checkbox("在主窗口中显示快速设计栏",
            "也在主窗口的选项卡的选择区域显示快速设计栏。",
            config.ShowQuickBarInTabs, v => config.ShowQuickBarInTabs = v);
        DrawQuickDesignBoxes();

        ImGui.Dummy(Vector2.Zero);
        ImGui.Separator();
        ImGui.Dummy(Vector2.Zero);

        Checkbox("启用游戏右键菜单", "在可装备物品的游戏右键菜单中增加一个Glamourer试穿按钮。",
            config.EnableGameContextMenu,     v =>
            {
                config.EnableGameContextMenu = v;
                if (v)
                    contextMenuService.Enable();
                else
                    contextMenuService.Disable();
            });
        Checkbox("在用户界面隐藏时显示窗口", "即使游戏UI隐藏，也可以显示Glamourer窗口。",
            config.ShowWindowWhenUiHidden,        v =>
            {
                config.ShowWindowWhenUiHidden = v;
                uiBuilder.DisableUserUiHide   = v;
            });
        Checkbox("在过场动画中隐藏窗口", "在进入过场动画时，Glamourer主窗口是否应该自动隐藏。",
            config.HideWindowInCutscene,
            v =>
            {
                config.HideWindowInCutscene     = v;
                uiBuilder.DisableCutsceneUiHide = !v;
            });
        EphemeralCheckbox("锁定主窗口", "防止主窗口被移动，将其锁定在当前位置。",
            config.Ephemeral.LockMainWindow,
            v => config.Ephemeral.LockMainWindow = v);
        Checkbox("在游戏开始时打开主窗口", "启动游戏后，Glamourer主窗口是打开还是关闭状态。",
            config.OpenWindowAtStart,              v => config.OpenWindowAtStart = v);
        ImGui.Dummy(Vector2.Zero);
        ImGui.Separator();
        ImGui.Dummy(Vector2.Zero);

        Checkbox("装备面板紧凑显示", "使用不显示装备图标、有小染色按钮的单行视图，取代两行视图。",
            config.SmallEquip,            v => config.SmallEquip = v);
        DrawHeightUnitSettings();
        Checkbox("显示应用复选框",
            "显示“角色设计”选项卡下外貌和装备面板中的应用生效复选框，而不是仅在“应用规则”面板中显示。",
            !config.HideApplyCheckmarks, v => config.HideApplyCheckmarks = !v);
        if (Widget.DoubleModifierSelector("删除组合键",
                "在所有的删除按钮上生效所需要的组合键。", 100 * ImGuiHelpers.GlobalScale,
                config.DeleteDesignModifier, v => config.DeleteDesignModifier = v))
            config.Save();
        DrawRenameSettings();
        Checkbox("自动展开角色设计折叠组",
            "登录游戏后，角色设计折叠组默认状态是打开还是关闭。", config.OpenFoldersByDefault,
            v => config.OpenFoldersByDefault = v);
        DrawFolderSortType();

        ImGui.Dummy(Vector2.Zero);
        ImGui.Separator();
        ImGui.Dummy(Vector2.Zero);

        Checkbox("允许双击应用设计",
            "在设计选择其中双击角色设计条目时，尝试将该设计应用于玩家的角色。",
            config.AllowDoubleClickToApply, v => config.AllowDoubleClickToApply = v);
        Checkbox("在执行集的“执行规则”中显示所有复选框",
            "在执行规则中显示多个单独规则的复选框，而不是只显示一个一键开关的复选框。",
            config.ShowAllAutomatedApplicationRules, v => config.ShowAllAutomatedApplicationRules = v);
        Checkbox("对获取物品显示警告",
            "无论你是否已解锁角色设计中的所有装备/外貌物品，在执行集中显示提示信息。",
            config.ShowUnlockedItemWarnings, v => config.ShowUnlockedItemWarnings = v);
        if (config.UseAdvancedParameters)
        {
            Checkbox("显示颜色显示设置", "在“外貌（高级）”面板中显示颜色显示设置选项。",
                config.ShowColorConfig,           v => config.ShowColorConfig = v);
            Checkbox("显示Palette+导入按钮",
                "在角色设计的“外貌（高级）”面板中显示一个导入按钮，该按钮允许你将调色板插件Palette++的设置导入到设计中。",
                config.ShowPalettePlusImport, v => config.ShowPalettePlusImport = v);
            using var id = ImRaii.PushId(1);
            PaletteImportButton();
        }

        if (config.UseAdvancedDyes)
            Checkbox("高级染色窗口吸附",
                "保持高级染色窗口与主窗口吸附，取消勾选使其可自由移动。",
                config.KeepAdvancedDyesAttached, v => config.KeepAdvancedDyesAttached = v);

        Checkbox("调试模式", "显示调试选项卡，仅对调试和进阶用法有帮助。一般不建议使用。", config.DebugMode,
            v => config.DebugMode = v);
        ImGui.NewLine();
    }

    private void DrawQuickDesignBoxes()
    {
        var showAuto     = config.EnableAutoDesigns;
        var showAdvanced = config.UseAdvancedParameters || config.UseAdvancedDyes;
        var numColumns   = 7 - (showAuto ? 0 : 2) - (showAdvanced ? 0 : 1);
        ImGui.NewLine();
        ImGui.TextUnformatted("在快速设计栏中显示以下按钮：");
        ImGui.Dummy(Vector2.Zero);
        using var table = ImRaii.Table("##tableQdb", numColumns,
            ImGuiTableFlags.SizingFixedFit | ImGuiTableFlags.Borders | ImGuiTableFlags.NoHostExtendX);
        if (!table)
            return;

        var columns = new[]
        {
            (" 应用设计 ", true, QdbButtons.ApplyDesign),
            (" 全部还原 ", true, QdbButtons.RevertAll),
            (" 恢复自动 ", showAuto, QdbButtons.RevertAutomation),
            (" 重新应用自动 ", showAuto, QdbButtons.ReapplyAutomation),
            (" 还原装备 ", true, QdbButtons.RevertEquip),
            (" 还原外貌 ", true, QdbButtons.RevertCustomize),
            (" 还原高级选项 ", showAdvanced, QdbButtons.RevertAdvanced),
        };

        foreach (var (label, _, _) in columns.Where(t => t.Item2))
        {
            ImGui.TableNextColumn();
            ImGui.TableHeader(label);
        }

        foreach (var (_, _, flag) in columns.Where(t => t.Item2))
        {
            using var id = ImRaii.PushId((int)flag);
            ImGui.TableNextColumn();
            var offset = (ImGui.GetContentRegionAvail().X - ImGui.GetFrameHeight()) / 2;
            ImGui.SetCursorPosX(ImGui.GetCursorPosX() + offset);
            var value = config.QdbButtons.HasFlag(flag);
            if (!ImGui.Checkbox(string.Empty, ref value))
                continue;

            var buttons = value ? config.QdbButtons | flag : config.QdbButtons & ~flag;
            if (buttons == config.QdbButtons)
                continue;

            config.QdbButtons = buttons;
            config.Save();
        }
    }

    private void PaletteImportButton()
    {
        if (!config.UseAdvancedParameters || !config.ShowPalettePlusImport)
            return;

        ImGui.SameLine();
        if (ImGui.Button("导入调色板插件Palette+的设计"))
            paletteImport.ImportDesigns();
        ImGuiUtil.HoverTooltip(
            $"从你的调色板插件Palette+的配置中导入所有存在的数据到角色设计选项卡下，目录结构为PalettePlus/[名称]（如果还无同名设计存在）。现有调色板为：\n\n\t - {string.Join("\n\t - ", paletteImport.Data.Keys)}");
    }

    /// <summary> Draw the entire Color subsection. </summary>
    private void DrawColorSettings()
    {
        if (!ImGui.CollapsingHeader("配色设置"))
            return;

        using (var tree = ImRaii.TreeNode("自定义设计颜色"))
        {
            if (tree)
                designColorUi.Draw();
        }

        using (var tree = ImRaii.TreeNode("配色"))
        {
            if (tree)
                foreach (var color in Enum.GetValues<ColorId>())
                {
                    var (defaultColor, name, description) = color.Data();
                    var currentColor = config.Colors.TryGetValue(color, out var current) ? current : defaultColor;
                    if (Widget.ColorPicker(name, description, currentColor, c => config.Colors[color] = c, defaultColor))
                        config.Save();
                }
        }

        ImGui.NewLine();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void Checkbox(string label, string tooltip, bool current, Action<bool> setter)
    {
        using var id  = ImRaii.PushId(label);
        var       tmp = current;
        if (ImGui.Checkbox(string.Empty, ref tmp) && tmp != current)
        {
            setter(tmp);
            config.Save();
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker(label, tooltip);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining | MethodImplOptions.AggressiveOptimization)]
    private void EphemeralCheckbox(string label, string tooltip, bool current, Action<bool> setter)
    {
        using var id  = ImRaii.PushId(label);
        var       tmp = current;
        if (ImGui.Checkbox(string.Empty, ref tmp) && tmp != current)
        {
            setter(tmp);
            config.Ephemeral.Save();
        }

        ImGui.SameLine();
        ImGuiUtil.LabeledHelpMarker(label, tooltip);
    }

    /// <summary> Different supported sort modes as a combo. </summary>
    private void DrawFolderSortType()
    {
        var sortMode = config.SortMode;
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo("##sortMode", sortMode.Name))
        {
            if (combo)
                foreach (var val in Configuration.Constants.ValidSortModes)
                {
                    if (ImGui.Selectable(val.Name, val.GetType() == sortMode.GetType()) && val.GetType() != sortMode.GetType())
                    {
                        config.SortMode = val;
                        selector.SetFilterDirty();
                        config.Save();
                    }

                    ImGuiUtil.HoverTooltip(val.Description);
                }
        }

        ImGuiUtil.LabeledHelpMarker("排序模式", "为角色设计选项卡下的设计选择器选择一个排序方式。");
    }

    private void DrawRenameSettings()
    {
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo("##renameSettings", config.ShowRename.GetData().Name))
        {
            if (combo)
                foreach (var value in Enum.GetValues<RenameField>())
                {
                    var (name, desc) = value.GetData();
                    if (ImGui.Selectable(name, config.ShowRename == value))
                    {
                        config.ShowRename = value;
                        selector.SetRenameSearchPath(value);
                        config.Save();
                    }

                    ImGuiUtil.HoverTooltip(desc);
                }
        }

        ImGui.SameLine();
        const string tt =
            "选择在打开设计选择器中设计右键上下文菜单时可见的两个重命名输入字段中的哪一个。";
        ImGuiComponents.HelpMarker(tt);
        ImGui.SameLine();
        ImGui.TextUnformatted("设计上下文菜单中的重命名字段");
        ImGuiUtil.HoverTooltip(tt);
    }

    private void DrawHeightUnitSettings()
    {
        ImGui.SetNextItemWidth(300 * ImGuiHelpers.GlobalScale);
        using (var combo = ImRaii.Combo("##heightUnit", HeightDisplayTypeName(config.HeightDisplayType)))
        {
            if (combo)
                foreach (var type in Enum.GetValues<HeightDisplayType>())
                {
                    if (ImGui.Selectable(HeightDisplayTypeName(type), type == config.HeightDisplayType) && type != config.HeightDisplayType)
                    {
                        config.HeightDisplayType = type;
                        config.Save();
                    }
                }
        }

        ImGui.SameLine();
        const string tt = "选择如何以真实世界单位显示角色的身高。";
        ImGuiComponents.HelpMarker(tt);
        ImGui.SameLine();
        ImGui.TextUnformatted("人物身高显示单位");
        ImGuiUtil.HoverTooltip(tt);
    }

    private static string HeightDisplayTypeName(HeightDisplayType type)
        => type switch
        {
            HeightDisplayType.None       => "不显示",
            HeightDisplayType.Centimetre => "厘米（000.0 cm）",
            HeightDisplayType.Metre      => "米（0.00 m）",
            HeightDisplayType.Wrong      => "英寸（00.0 in）",
            HeightDisplayType.WrongFoot  => "英尺（0'00''）",
            HeightDisplayType.Corgi       => "柯基（0.0柯基）",
            HeightDisplayType.OlympicPool => "奥林匹克标准游泳池（0.000个游泳池）",
            _                             => string.Empty,
        };
}
