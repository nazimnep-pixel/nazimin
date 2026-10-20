namespace AzMepCad.UI.Palette;

using System.Globalization;
using System.Windows.Controls;
using AzMepCad.UI.Resources;

public partial class MepPaletteControl : UserControl
{
    public MainPaletteViewModel ViewModel { get; }

    public MepPaletteControl()
    {
        ViewModel = new MainPaletteViewModel();
        InitializeComponent();
        DataContext = ViewModel;
        ApplyLocalizedStrings();
        CategoryList.ItemsSource = ViewModel.Categories;
    }

    private void ApplyLocalizedStrings()
    {
        Strings.Culture = CultureInfo.GetCultureInfo("az-AZ");
        TitleText.Text = ViewModel.Title;
        RoutingTab.Header = Strings.TabRouting;
        CatalogTab.Header = Strings.TabCatalog;
        BoqTab.Header = Strings.TabBoq;
        DiameterLabel.Text = Strings.LabelDiameter;
        RouteButton.Content = Strings.ButtonRoute;
        StatusText.Text = ViewModel.StatusMessage;
    }
}
