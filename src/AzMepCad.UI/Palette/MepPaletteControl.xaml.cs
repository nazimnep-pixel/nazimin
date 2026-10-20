namespace AzMepCad.UI.Palette;

using System.Globalization;
using System.Windows.Controls;
using AzMepCad.UI.Resources;

public partial class MepPaletteControl : UserControl
{
    public MepPaletteControl()
    {
        InitializeComponent();
        ApplyLocalizedStrings();
    }

    private void ApplyLocalizedStrings()
    {
        Strings.Culture = CultureInfo.GetCultureInfo("az-AZ");
        TitleText.Text = Strings.PaletteTitle;
        RoutingTab.Header = Strings.TabRouting;
        CatalogTab.Header = Strings.TabCatalog;
        BoqTab.Header = Strings.TabBoq;
        DiameterLabel.Text = Strings.LabelDiameter;
        RouteButton.Content = Strings.ButtonRoute;
        StatusText.Text = Strings.StatusReady;
    }
}
