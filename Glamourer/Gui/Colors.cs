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
            ColorId.NormalDesign               => (0xFFFFFFFF, "��ͨ���",                           "û������������õ���ơ�"                                                                         ),
            ColorId.CustomizationDesign        => (0xFFC000C0, "��ò���",                           "���޸Ľ�ɫ��ò����ơ�"                                                 ),
            ColorId.StateDesign                => (0xFF00C0C0, "״̬���",                           "���޸Ľ�ɫ��ò��װ������ơ�"                                 ),
            ColorId.EquipmentDesign            => (0xFF00C000, "װ�����",                           "ֻ�޸Ľ�ɫװ������ơ�"                                                      ),
            ColorId.ActorAvailable             => (0xFF18C018, "��ɫ����",                           "�������Ϸ�����д˽�ɫ���ٴ��ڹ�һ�Σ�������ɫѡ��еĽ�ɫ�������ʾΪ����ɫ��" ),
            ColorId.ActorUnavailable           => (0xFF1818C0, "��ɫ������",                         "�������Ϸ�����д˽�ɫ��ǰ�����ڣ�������ɫѡ��еĽ�ɫ�������ʾΪ����ɫ��"),
            ColorId.FolderExpanded             => (0xFFFFF0C0, "չ��������۵���",                   "��ǰչ��������۵��飬�������ʾΪ����ɫ��"                                                               ),
            ColorId.FolderCollapsed            => (0xFFFFF0C0, "���������۵���",                   "��ǰ���������۵��飬�������ʾΪ����ɫ��"),
            ColorId.FolderLine                 => (0xFFFFF0C0, "չ������۵�������",                 "��ʾ��Щ�����������չ�����۵��飬���ڱ�ʶ����Ŀ¼�ṹ�����߻���ʾΪ����ɫ��"                                ),
            ColorId.EnabledAutoSet             => (0xFFA0F0A0, "�����õ��Զ�ִ�м�",                 "��ǰ�����õ��Զ���ִ�м�. ÿ����ɫֻ������һ����"     ),
            ColorId.DisabledAutoSet            => (0xFF808080, "�ѽ��õ��Զ�ִ�м�",                 "��ǰ�ѽ��õ��Զ���ִ�м�"),
            ColorId.AutomationActorAvailable   => (0xFFFFFFFF, "�Զ�ִ�й�����ɫ����",               "���Զ�ִ�м������Ľ�ɫ��ǰ���ڡ�"                          ),
            ColorId.AutomationActorUnavailable => (0xFF808080, "�Զ�ִ�й�����ɫ������",             "���Զ�ִ�м������Ľ�ɫ��ǰ�����ڡ�"),
            ColorId.HeaderButtons              => (0xFFFFF0C0, "���ⰴť",                           "���⴦��ť���ı��ͱ߿���ɫ�������������ذ�ť��"                            ),
            ColorId.FavoriteStarOn             => (0xFF40D0D0, "�ղ���Ʒ",                           "�ղ���Ʒ������Ǻ��ѽ���ѡ�����ģʽ�еı߿����ɫ����"                     ),
            ColorId.FavoriteStarHovered        => (0xFFD040D0, "�ղ��������ͣ",                     "������ղ���Ʒ����ǰ�ť����ͣʱ����ɫ"                                               ),
            ColorId.FavoriteStarOff            => (0x20808080, "�ղ����������",                     "�ղ�Ʒ����ǵ�Ĭ����ɫ"                              ),
            ColorId.QuickDesignButton          => (0x900A0A0A, "�����������ť����",                 "����������а�ť�������ɫ��"),
            ColorId.QuickDesignFrame           => (0x90383838, "���������ѡ��������",               "��������������ѡ�����ı�����ɫ��"),
            ColorId.QuickDesignBg              => (0x00F0F0F0, "������������ڱ���",                 "����������д��ڵı�����ɫ��"),
            ColorId.TriStateCheck              => (0xFF00D000, "��̬��ѡ��̣��򹴣�",                       "��ѡ���б�ʾѡ�еķ��ŵ���ɫ��"                            ),
            ColorId.TriStateCross              => (0xFF0000D0, "��̬��ѡ�������棩",                       "��ѡ���б�ʾ��ѡ�ķ��ŵ���ɫ��"),
            ColorId.TriStateNeutral            => (0xFFD0D0D0, "��̬��ѡ��񣨵�ѡ��",                       "��ѡ���б�ʾ����ԭ���ķ��ŵ���ɫ��"                                        ),
            ColorId.BattleNpc                  => (0xFFFFFFFF, "NPCѡ��е�ս��NPC",                "NPCѡ���û��ָ��������ɫ��ս��NPC���Ƶ���ɫ��"),
            ColorId.EventNpc                   => (0xFFFFFFFF, "NPCѡ��е��¼�NPC",                "NPCѡ���û��ָ��������ɫ���¼�NPC���Ƶ���ɫ��"),
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
