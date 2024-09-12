using ImGuiNET;

namespace Glamourer.Gui;

public enum ColorId
{
    NormalDesign,
    CustomizationDesign,
    StateDesign,
    EquipmentDesign,
    ActorAvailable,
    ActorUnavailable,
    FolderExpanded,
    FolderCollapsed,
    FolderLine,
    EnabledAutoSet,
    DisabledAutoSet,
    AutomationActorAvailable,
    AutomationActorUnavailable,
    HeaderButtons,
    FavoriteStarOn,
    FavoriteStarHovered,
    FavoriteStarOff,
    QuickDesignButton,
    QuickDesignFrame,
    QuickDesignBg,
    TriStateCheck,
    TriStateCross,
    TriStateNeutral,
    BattleNpc,
    EventNpc,
}

public static class Colors
{
    public const uint SelectedRed = 0xFF2020D0;

    public static (uint DefaultColor, string Name, string Description) Data(this ColorId color)
        => color switch
        {
            // @formatter:off
            ColorId.NormalDesign               => (0xFFFFFFFF, "普通设计",                           "没有特殊规则设置的设计。"                                                                         ),
            ColorId.CustomizationDesign        => (0xFFC000C0, "外貌设计",                           "仅修改角色外貌的设计。"                                                 ),
            ColorId.StateDesign                => (0xFF00C0C0, "状态设计",                           "不修改角色外貌或装备的设计。"                                 ),
            ColorId.EquipmentDesign            => (0xFF00C000, "装备设计",                           "只修改角色装备的设计。"                                                      ),
            ColorId.ActorAvailable             => (0xFF18C018, "角色可用",                           "如果在游戏世界中此角色至少存在过一次，附近角色选项卡中的角色标题会显示为此颜色。" ),
            ColorId.ActorUnavailable           => (0xFF1818C0, "角色不可用",                         "如果在游戏世界中此角色当前不存在，附近角色选项卡中的角色标题会显示为此颜色。"),
            ColorId.FolderExpanded             => (0xFFFFF0C0, "展开的设计折叠组",                   "当前展开的设计折叠组，标题会显示为此颜色。"                                                               ),
            ColorId.FolderCollapsed            => (0xFFFFF0C0, "收起的设计折叠组",                   "当前收起的设计折叠组，标题会显示为此颜色。"),
            ColorId.FolderLine                 => (0xFFFFF0C0, "展开设计折叠组竖线",                 "表示哪些子设计隶属于展开的折叠组，用于标识树形目录结构的竖线会显示为此颜色。"                                ),
            ColorId.EnabledAutoSet             => (0xFFA0F0A0, "已启用的自动执行集",                 "当前已启用的自动化执行集. 每个角色只能启用一个。"     ),
            ColorId.DisabledAutoSet            => (0xFF808080, "已禁用的自动执行集",                 "当前已禁用的自动化执行集"),
            ColorId.AutomationActorAvailable   => (0xFFFFFFFF, "自动执行关联角色存在",               "与自动执行集关联的角色当前存在。"                          ),
            ColorId.AutomationActorUnavailable => (0xFF808080, "自动执行关联角色不存在",             "与自动执行集关联的角色当前不存在。"),
            ColorId.HeaderButtons              => (0xFFFFF0C0, "标题按钮",                           "标题处按钮的文本和边框颜色。比如匿名开关按钮。"                            ),
            ColorId.FavoriteStarOn             => (0xFF40D0D0, "收藏物品",                           "收藏物品的五角星和已解锁选项卡总览模式中的边框的颜色。。"                     ),
            ColorId.FavoriteStarHovered        => (0xFFD040D0, "收藏五角星悬停",                     "鼠标在收藏物品五角星按钮上悬停时的颜色"                                               ),
            ColorId.FavoriteStarOff            => (0x20808080, "收藏五角星轮廓",                     "收藏品五角星的默认颜色"                              ),
            ColorId.QuickDesignButton          => (0x900A0A0A, "快速设计栏按钮背景",                 "快速设计栏中按钮框体的颜色。"),
            ColorId.QuickDesignFrame           => (0x90383838, "快速设计栏选择器背景",               "快速设计栏中设计选择器的背景颜色。"),
            ColorId.QuickDesignBg              => (0x00F0F0F0, "快速设计栏窗口背景",                 "快速设计栏中窗口的背景颜色。"),
            ColorId.TriStateCheck              => (0xFF00D000, "三态复选框√（打勾）",                "复选框中表示选中的符号的颜色。"                            ),
            ColorId.TriStateCross              => (0xFF0000D0, "三态复选框×（打叉）",                "复选框中表示反选的符号的颜色。"),
            ColorId.TriStateNeutral            => (0xFFD0D0D0, "三态复选框●（点选）",                "复选框中表示保持原样的符号的颜色。"                                        ),
            ColorId.BattleNpc                  => (0xFFFFFFFF, "NPC选项卡中的战斗NPC",               "NPC选项卡中没有指定其他颜色的战斗NPC名称的颜色。"),
            ColorId.EventNpc                   => (0xFFFFFFFF, "NPC选项卡中的事件NPC",               "NPC选项卡中没有指定其他颜色的事件NPC名称的颜色。"),
            _                                  => (0x00000000, string.Empty,                         string.Empty                                                                                                ),
            // @formatter:on
        };

    private static IReadOnlyDictionary<ColorId, uint> _colors = new Dictionary<ColorId, uint>();

    /// <summary> Obtain the configured value for a color. </summary>
    public static uint Value(this ColorId color)
        => _colors.TryGetValue(color, out var value) ? value : color.Data().DefaultColor;

    /// <summary> Set the configurable colors dictionary to a value. </summary>
    public static void SetColors(Configuration config)
        => _colors = config.Colors;
}
