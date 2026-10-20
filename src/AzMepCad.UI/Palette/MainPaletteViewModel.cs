namespace AzMepCad.UI.Palette;

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using AzMepCad.Core.Interfaces;
using AzMepCad.UI.Resources;

/// <summary>
/// Binds catalog discipline categories for the MEP palette (az-AZ labels from resx).
/// </summary>
public sealed class MainPaletteViewModel : INotifyPropertyChanged
{
    private readonly IMepCatalog? _catalog;

    public MainPaletteViewModel(IMepCatalog? catalog = null)
    {
        _catalog = catalog;
        Strings.Culture = CultureInfo.GetCultureInfo("az-AZ");
        Title = Strings.PaletteTitle;
        StatusMessage = Strings.StatusReady;
        LoadCategories();
    }

    public string Title { get; }

    public string StatusMessage
    {
        get => _statusMessage;
        private set
        {
            _statusMessage = value;
            OnPropertyChanged();
        }
    }

    private string _statusMessage = string.Empty;

    public ObservableCollection<CatalogCategoryItem> Categories { get; } = new();

    public async Task RefreshCatalogAsync(CancellationToken cancellationToken = default)
    {
        if (_catalog is null)
        {
            StatusMessage = Strings.StatusReady;
            return;
        }

        await _catalog.InitializeAsync(cancellationToken).ConfigureAwait(true);
        StatusMessage = $"{Strings.StatusReady} — kataloq yükləndi";
        OnPropertyChanged(nameof(Categories));
    }

    private void LoadCategories()
    {
        Categories.Clear();
        Categories.Add(new CatalogCategoryItem(Strings.CategoryHvac, MepDiscipline.Ventilyasiya, MepDiscipline.Istilik));
        Categories.Add(new CatalogCategoryItem(Strings.CategoryPipe, MepDiscipline.Santexnika));
        Categories.Add(new CatalogCategoryItem(Strings.CategoryElectrical, MepDiscipline.Elektrik));
        Categories.Add(new CatalogCategoryItem(Strings.CategoryArchitecture, MepDiscipline.Naməlum));
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    private void OnPropertyChanged([CallerMemberName] string? name = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

public sealed record CatalogCategoryItem(string DisplayName, params MepDiscipline[] Disciplines);
