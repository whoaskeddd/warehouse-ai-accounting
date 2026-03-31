using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.Core.Contracts.Categories;
using SmartStockAI.Core.Contracts.Products;

namespace SmartStockAI.App.Views;

public partial class CategoriesPage : Page
{
    private readonly ICategoryService _categoryService;
    private readonly IProductService _productService;
    private readonly ObservableCollection<CategoryListItem> _categories = [];
    private int? _selectedCategoryId;

    public CategoriesPage(ICategoryService categoryService, IProductService productService)
    {
        _categoryService = categoryService;
        _productService = productService;
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (CategoriesDataGrid.ItemsSource is null)
        {
            CategoriesDataGrid.ItemsSource = _categories;
        }

        await LoadCategoriesAsync();
        ResetEditor();
    }

    private async Task LoadCategoriesAsync()
    {
        var categories = await _categoryService.GetAllAsync();
        var products = await _productService.GetAllAsync();
        var productCounts = products
            .Where(x => x.CategoryId.HasValue)
            .GroupBy(x => x.CategoryId!.Value)
            .ToDictionary(x => x.Key, x => x.Count());

        var lookup = categories
            .OrderBy(x => x.Name)
            .Select(x => new LookupItem { Id = x.Id, Name = x.Name })
            .ToList();

        ParentCategoryComboBox.ItemsSource = CreateLookupOptions("Корневая категория", lookup);
        if (!_selectedCategoryId.HasValue)
        {
            ParentCategoryComboBox.SelectedIndex = 0;
        }

        var items = categories
            .OrderBy(x => x.Name)
            .Select(x => new CategoryListItem
            {
                Id = x.Id,
                Name = x.Name,
                ParentName = x.ParentCategoryName ?? "Корень",
                ProductCount = productCounts.GetValueOrDefault(x.Id)
            })
            .ToList();

        _categories.Clear();
        foreach (var item in items)
        {
            _categories.Add(item);
        }
    }

    private async void CategoriesDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoriesDataGrid.SelectedItem is not CategoryListItem item)
        {
            return;
        }

        var category = await _categoryService.GetByIdAsync(item.Id);
        if (category is null)
        {
            return;
        }

        _selectedCategoryId = category.Id;
        CategoryEditorTitleText.Text = category.Name;
        CategoryNameTextBox.Text = category.Name;
        ParentCategoryComboBox.SelectedValue = category.ParentCategoryId;
    }

    private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CategoryNameTextBox.Text))
        {
            MessageBox.Show("У категории должно быть название.", "Категории", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            CategoryDto? category;
            if (_selectedCategoryId.HasValue)
            {
                category = await _categoryService.UpdateAsync(_selectedCategoryId.Value, new UpdateCategoryRequest
                {
                    Name = CategoryNameTextBox.Text.Trim(),
                    ParentCategoryId = ParentCategoryComboBox.SelectedValue as int?
                });
            }
            else
            {
                category = await _categoryService.CreateAsync(new CreateCategoryRequest
                {
                    Name = CategoryNameTextBox.Text.Trim(),
                    ParentCategoryId = ParentCategoryComboBox.SelectedValue as int?
                });
            }

            await LoadCategoriesAsync();
            if (category is not null)
            {
                SelectCategory(category.Id);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Категории", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void SelectCategory(int categoryId)
    {
        var item = _categories.FirstOrDefault(x => x.Id == categoryId);
        if (item is not null)
        {
            CategoriesDataGrid.SelectedItem = item;
            CategoriesDataGrid.ScrollIntoView(item);
        }
    }

    private void ResetButton_OnClick(object sender, RoutedEventArgs e)
    {
        ResetEditor();
        CategoriesDataGrid.UnselectAll();
    }

    private async void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        await LoadCategoriesAsync();
    }

    private void ResetEditor()
    {
        _selectedCategoryId = null;
        CategoryEditorTitleText.Text = "Новая категория";
        CategoryNameTextBox.Text = string.Empty;
        if (ParentCategoryComboBox.Items.Count > 0)
        {
            ParentCategoryComboBox.SelectedIndex = 0;
        }
    }

    private static List<LookupItem> CreateLookupOptions(string emptyTitle, IEnumerable<LookupItem> items)
    {
        return [new LookupItem { Id = null, Name = emptyTitle }, .. items];
    }
}
