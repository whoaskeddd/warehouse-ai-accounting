using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using Microsoft.EntityFrameworkCore;
using SmartStockAI.App.Models;
using SmartStockAI.App.Services;
using SmartStockAI.Core.Entities;

namespace SmartStockAI.App.Views;

public partial class CategoriesPage : Page
{
    private readonly ObservableCollection<CategoryListItem> _categories = [];
    private int? _selectedCategoryId;

    public CategoriesPage()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        if (CategoriesDataGrid.ItemsSource is null)
        {
            CategoriesDataGrid.ItemsSource = _categories;
        }

        LoadCategories();
        ResetEditor();
    }

    private void LoadCategories()
    {
        using var db = AppDbContextProvider.Create();

        var lookup = db.Categories.AsNoTracking()
            .OrderBy(x => x.Name)
            .Select(x => new LookupItem { Id = x.Id, Name = x.Name })
            .ToList();

        ParentCategoryComboBox.ItemsSource = CreateLookupOptions("Корневая категория", lookup);
        if (!_selectedCategoryId.HasValue)
        {
            ParentCategoryComboBox.SelectedIndex = 0;
        }

        var items = db.Categories
            .AsNoTracking()
            .Include(x => x.ParentCategory)
            .Include(x => x.Products)
            .OrderBy(x => x.Name)
            .Select(x => new CategoryListItem
            {
                Id = x.Id,
                Name = x.Name,
                ParentName = x.ParentCategory != null ? x.ParentCategory.Name : "Корень",
                ProductCount = x.Products.Count
            })
            .ToList();

        _categories.Clear();
        foreach (var item in items)
        {
            _categories.Add(item);
        }
    }

    private void CategoriesDataGrid_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (CategoriesDataGrid.SelectedItem is not CategoryListItem item)
        {
            return;
        }

        using var db = AppDbContextProvider.Create();
        var category = db.Categories.AsNoTracking().FirstOrDefault(x => x.Id == item.Id);
        if (category is null)
        {
            return;
        }

        _selectedCategoryId = category.Id;
        CategoryEditorTitleText.Text = category.Name;
        CategoryNameTextBox.Text = category.Name;
        ParentCategoryComboBox.SelectedValue = category.ParentCategoryId;
    }

    private void SaveButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(CategoryNameTextBox.Text))
        {
            MessageBox.Show("У категории должно быть название.", "Категории", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        using var db = AppDbContextProvider.Create();
        Category category;
        if (_selectedCategoryId.HasValue)
        {
            category = db.Categories.First(x => x.Id == _selectedCategoryId.Value);
        }
        else
        {
            category = new Category();
            db.Categories.Add(category);
        }

        category.Name = CategoryNameTextBox.Text.Trim();
        category.ParentCategoryId = ParentCategoryComboBox.SelectedValue as int?;
        db.SaveChanges();

        LoadCategories();
        SelectCategory(category.Id);
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

    private void ReloadButton_OnClick(object sender, RoutedEventArgs e)
    {
        LoadCategories();
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
