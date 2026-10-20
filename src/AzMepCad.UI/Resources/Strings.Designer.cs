namespace AzMepCad.UI.Resources;

using System.Globalization;
using System.Resources;

public static class Strings
{
    private static readonly ResourceManager ResourceManager =
        new("AzMepCad.UI.Resources.Strings", typeof(Strings).Assembly);

    private static CultureInfo? _culture;

    public static CultureInfo? Culture
    {
        get => _culture;
        set => _culture = value;
    }

    public static string PaletteTitle => Get("PaletteTitle");
    public static string TabRouting => Get("TabRouting");
    public static string TabCatalog => Get("TabCatalog");
    public static string TabBoq => Get("TabBoq");
    public static string LabelDiameter => Get("LabelDiameter");
    public static string ButtonRoute => Get("ButtonRoute");
    public static string ButtonExportBoq => Get("ButtonExportBoq");
    public static string StatusReady => Get("StatusReady");
    public static string CategoryHvac => Get("CategoryHvac");
    public static string CategoryPipe => Get("CategoryPipe");
    public static string CategoryElectrical => Get("CategoryElectrical");
    public static string CategoryArchitecture => Get("CategoryArchitecture");

    private static string Get(string name) =>
        ResourceManager.GetString(name, _culture ?? CultureInfo.CurrentUICulture) ?? name;
}
