using Glamourer.State;

namespace Glamourer.Designs;

public enum MetaIndex
{
    Wetness     = StateIndex.MetaWetness,
    HatState    = StateIndex.MetaHatState,
    VisorState  = StateIndex.MetaVisorState,
    WeaponState = StateIndex.MetaWeaponState,
    ModelId     = StateIndex.MetaModelId,
}

[Flags]
public enum MetaFlag : byte
{
    Wetness     = 0x01,
    HatState    = 0x02,
    VisorState  = 0x04,
    WeaponState = 0x08,
}

public static class MetaExtensions
{
    public static readonly IReadOnlyList<MetaIndex> AllRelevant =
        [MetaIndex.Wetness, MetaIndex.HatState, MetaIndex.VisorState, MetaIndex.WeaponState];

    public const MetaFlag All = MetaFlag.Wetness | MetaFlag.HatState | MetaFlag.VisorState | MetaFlag.WeaponState;

    public static MetaFlag ToFlag(this MetaIndex index)
        => index switch
        {
            MetaIndex.Wetness     => MetaFlag.Wetness,
            MetaIndex.HatState    => MetaFlag.HatState,
            MetaIndex.VisorState  => MetaFlag.VisorState,
            MetaIndex.WeaponState => MetaFlag.WeaponState,
            _                     => (MetaFlag)byte.MaxValue,
        };

    public static MetaIndex ToIndex(this MetaFlag index)
        => index switch
        {
            MetaFlag.Wetness     => MetaIndex.Wetness,
            MetaFlag.HatState    => MetaIndex.HatState,
            MetaFlag.VisorState  => MetaIndex.VisorState,
            MetaFlag.WeaponState => MetaIndex.WeaponState,
            _                    => (MetaIndex)byte.MaxValue,
        };

    public static string ToName(this MetaIndex index)
        => index switch
        {
            MetaIndex.HatState    => "显示头部装备",
            MetaIndex.VisorState  => "调整头部装备",
            MetaIndex.WeaponState => "收回武器时显示主手及副手",
            MetaIndex.Wetness     => "强制湿身",
            _                     => "未知元数据",
        };

    public static string ToTooltip(this MetaIndex index)
        => index switch
        {
            MetaIndex.HatState    => "隐藏或显示角色的头部装备。",
            MetaIndex.VisorState  => "切换角色头部装备的面罩状态。",
            MetaIndex.WeaponState => "未手持时隐藏或显示角色武器。",
            MetaIndex.Wetness     => "强制角色湿身或不湿身。",
            _                     => string.Empty,
        };
}
