namespace AzMepCad.UI.Palette;

using System.Globalization;
using System.Windows;
using AzMepCad.UI.Resources;

public partial class MepPaletteWindow : Window
{
    public MepPaletteWindow()
    {
        InitializeComponent();
        Strings.Culture = CultureInfo.GetCultureInfo("az-AZ");
        Title = Strings.PaletteTitle;
    }
}
