using System.Collections.Generic;

namespace Glamourer.Gui;

public enum ColorId
{
    CustomizationDesign,
    StateDesign,
    EquipmentDesign,
    ActorAvailable,
    ActorUnavailable,
}

public static class Colors
{
    public const  uint DiscordColor     = 0xFFDA8972;
    public const  uint ReniColorButton  = 0xFFCC648D;
    public const  uint ReniColorHovered = 0xFFB070B0;
    public const  uint ReniColorActive  = 0xFF9070E0;

    public static (uint DefaultColor, string Name, string Description) Data(this ColorId color)
        => color switch
        {
            // @formatter:off
            ColorId.CustomizationDesign   => (0xFFC000C0, "Customization Design", "A design that only changes customizations on a character."                                                 ),
            ColorId.StateDesign           => (0xFF00C0C0, "State Design",         "A design that only changes meta state on a character."                                                     ),
            ColorId.EquipmentDesign       => (0xFF00C000, "Equipment Design",     "A design that only changes equipment on a character."                                                      ),
            ColorId.ActorAvailable        => (0xFF18C018, "Actor Available",      "The header in the Actor tab panel if the currently selected actor exists in the game world at least once." ),
            ColorId.ActorUnavailable      => (0xFF1818C0, "Actor Unavailable",    "The Header in the Actor tab panel if the currently selected actor does not exist in the game world."       ),
            _                             => (0x00000000, string.Empty,           string.Empty                                                ),
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