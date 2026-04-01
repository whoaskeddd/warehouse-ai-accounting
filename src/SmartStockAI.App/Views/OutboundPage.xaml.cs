using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SmartStockAI.App.Models;

namespace SmartStockAI.App.Views;

public partial class OutboundPage : Page, INotifyPropertyChanged
{
    private readonly List<DocumentListItem> _allDocuments;
    private readonly List<MovementHistoryItem> _allHistory;
    private string _documentFilterText = string.Empty;
    private string _historyFilterText = string.Empty;
    private DocumentListItem? _selectedDocument;
    private ProductLookupItem? _selectedProduct;
    private DocumentLineItem? _selectedLine;
    private string _documentNumber = string.Empty;
    private string _recipientName = string.Empty;
    private string _documentComment = string.Empty;
    private string _documentStatus = "Черновик";

    public OutboundPage()
    {
        InitializeComponent();
        DataContext = this;

        Products =
        [
            new ProductLookupItem { Id = 1, Sku = "MILK-1L", Name = "Молоко 1л", Unit = "шт", AvailableStock = 12 },
            new ProductLookupItem { Id = 2, Sku = "COF-250", Name = "Кофе 250г", Unit = "шт", AvailableStock = 5 },
            new ProductLookupItem { Id = 3, Sku = "SUG-1KG", Name = "Сахар 1кг", Unit = "шт", AvailableStock = 22 }
        ];

        _allDocuments =
        [
            new DocumentListItem { Id = 1, Number = "OUT-1018", CounterpartyName = "Магазин Арбат", Status = "Черновик", CreatedAt = DateTime.Today, LinesCount = 2, TotalQuantity = 18, HasWarnings = true },
            new DocumentListItem { Id = 2, Number = "OUT-1017", CounterpartyName = "Заказ кафе №14", Status = "Проведен", CreatedAt = DateTime.Today.AddDays(-1), LinesCount = 3, TotalQuantity = 14, HasWarnings = false },
            new DocumentListItem { Id = 3, Number = "OUT-1016", CounterpartyName = "Перемещение в зал", Status = "Проведен", CreatedAt = DateTime.Today.AddDays(-2), LinesCount = 1, TotalQuantity = 6, HasWarnings = false }
        ];

        _allHistory =
        [
            new MovementHistoryItem { OccurredAt = DateTime.Now.AddHours(-4), ProductName = "Молоко 1л", Sku = "MILK-1L", MovementType = "Расход", DocumentNumber = "OUT-1017", Quantity = 6, BalanceAfter = 12, Comment = "Кафе" },
            new MovementHistoryItem { OccurredAt = DateTime.Now.AddHours(-2), ProductName = "Кофе 250г", Sku = "COF-250", MovementType = "Расход", DocumentNumber = "OUT-1018", Quantity = 8, BalanceAfter = 5, Comment = "Черновик" },
            new MovementHistoryItem { OccurredAt = DateTime.Now.AddMinutes(-40), ProductName = "Сахар 1кг", Sku = "SUG-1KG", MovementType = "Расход", DocumentNumber = "OUT-1018", Quantity = 10, BalanceAfter = 22, Comment = "Комплектация" }
        ];

        FilteredDocuments = [];
        FilteredHistory = [];
        Lines = [];

        SelectedProduct = Products.FirstOrDefault();
        NewDocument();
        RefreshDocuments();
        RefreshHistory();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DocumentListItem> FilteredDocuments { get; }

    public ObservableCollection<MovementHistoryItem> FilteredHistory { get; }

    public ObservableCollection<DocumentLineItem> Lines { get; }

    public IReadOnlyList<ProductLookupItem> Products { get; }

    public DocumentListItem? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetField(ref _selectedDocument, value) && value is not null)
            {
                DocumentNumber = value.Number;
                RecipientName = value.CounterpartyName;
                DocumentStatus = value.Status;
                DocumentComment = $"Подготовка расхода по документу {value.Number}.";
                LoadDocumentLines(value.Id);
                OnPropertyChanged(nameof(QueueHeadline));
            }
        }
    }

    public ProductLookupItem? SelectedProduct
    {
        get => _selectedProduct;
        set
        {
            if (SetField(ref _selectedProduct, value))
            {
                OnPropertyChanged(nameof(SelectedProductStockText));
                UpdateSelectedValidation();
            }
        }
    }

    public DocumentLineItem? SelectedLine
    {
        get => _selectedLine;
        set => SetField(ref _selectedLine, value);
    }

    public string DocumentNumber
    {
        get => _documentNumber;
        set => SetField(ref _documentNumber, value);
    }

    public string RecipientName
    {
        get => _recipientName;
        set => SetField(ref _recipientName, value);
    }

    public string DocumentComment
    {
        get => _documentComment;
        set => SetField(ref _documentComment, value);
    }

    public string DocumentStatus
    {
        get => _documentStatus;
        set
        {
            if (SetField(ref _documentStatus, value))
            {
                OnPropertyChanged(nameof(EditorTitle));
            }
        }
    }

    public string DocumentFilterText
    {
        get => _documentFilterText;
        set
        {
            if (SetField(ref _documentFilterText, value))
            {
                RefreshDocuments();
            }
        }
    }

    public string HistoryFilterText
    {
        get => _historyFilterText;
        set
        {
            if (SetField(ref _historyFilterText, value))
            {
                RefreshHistory();
            }
        }
    }

    public string QueueHeadline => SelectedDocument is null ? "Список документов" : $"Активный: {SelectedDocument.Number}";

    public string QueueSummary => $"{FilteredDocuments.Count} документов";

    public string EditorTitle => $"{DocumentStatus} · {DocumentNumber}";

    public string TotalsText => $"Строк: {Lines.Count} · К выдаче: {Lines.Sum(x => x.Quantity).ToString("0.##", CultureInfo.InvariantCulture)}";

    public string HistorySummary => $"{FilteredHistory.Count} движений";

    public string ShortageSummary =>
        Lines.Any(x => x.HasShortage)
            ? "Есть строки, которые нельзя провести из-за нехватки остатка. Исправь количество или замени товар."
            : "Нехватки по текущему документу не обнаружено.";

    public string WarningBadgeText => $"{Lines.Count(x => x.HasShortage)} проблем";

    public string SelectedProductStockText => SelectedProduct is null
        ? "Выбери товар"
        : $"{SelectedProduct.AvailableStock:0.##} {SelectedProduct.Unit}";

    public string SelectedValidationText
    {
        get
        {
            if (SelectedProduct is null)
            {
                return "Нет проверки";
            }

            if (!TryParseDecimal(LineQuantityTextBox?.Text, out var quantity))
            {
                return "Введи количество";
            }

            return quantity <= SelectedProduct.AvailableStock
                ? "Остатка достаточно"
                : $"Нехватка {quantity - SelectedProduct.AvailableStock:0.##} {SelectedProduct.Unit}";
        }
    }

    public Brush SelectedValidationBackground =>
        SelectedValidationText.StartsWith("Нехватка", StringComparison.Ordinal)
            ? new SolidColorBrush(Color.FromRgb(254, 242, 242))
            : new SolidColorBrush(Color.FromRgb(240, 253, 244));

    public Brush SelectedValidationBorder =>
        SelectedValidationText.StartsWith("Нехватка", StringComparison.Ordinal)
            ? new SolidColorBrush(Color.FromRgb(254, 205, 211))
            : new SolidColorBrush(Color.FromRgb(187, 247, 208));

    public Brush SelectedValidationForeground =>
        SelectedValidationText.StartsWith("Нехватка", StringComparison.Ordinal)
            ? new SolidColorBrush(Color.FromRgb(180, 35, 24))
            : new SolidColorBrush(Color.FromRgb(21, 128, 61));

    private void NewDocumentButton_OnClick(object sender, RoutedEventArgs e)
    {
        NewDocument();
    }

    private void AddLineButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedProduct is null)
        {
            MessageBox.Show("Выбери товар для строки расхода.", "Расход", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(LineQuantityTextBox.Text, out var quantity))
        {
            MessageBox.Show("Количество должно быть числом.", "Расход", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (quantity <= 0)
        {
            MessageBox.Show("Количество должно быть больше нуля.", "Расход", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        var shortage = quantity > SelectedProduct.AvailableStock;
        Lines.Add(new DocumentLineItem
        {
            LineNo = Lines.Count + 1,
            Sku = SelectedProduct.Sku,
            ProductName = SelectedProduct.Name,
            Quantity = quantity,
            Unit = SelectedProduct.Unit,
            AvailableStock = SelectedProduct.AvailableStock,
            Comment = LineCommentTextBox.Text.Trim(),
            HasShortage = shortage,
            ValidationMessage = shortage ? $"Нехватка {quantity - SelectedProduct.AvailableStock:0.##} {SelectedProduct.Unit}" : "OK"
        });

        LineQuantityTextBox.Text = string.Empty;
        LineCommentTextBox.Text = string.Empty;
        OnPropertyChanged(nameof(TotalsText));
        OnPropertyChanged(nameof(ShortageSummary));
        OnPropertyChanged(nameof(WarningBadgeText));
        UpdateSelectedValidation();
    }

    private void RemoveLineButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedLine is null)
        {
            return;
        }

        Lines.Remove(SelectedLine);
        ResequenceLines();
        OnPropertyChanged(nameof(TotalsText));
        OnPropertyChanged(nameof(ShortageSummary));
        OnPropertyChanged(nameof(WarningBadgeText));
    }

    private void PostDocumentButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Lines.Count == 0)
        {
            MessageBox.Show("Добавь хотя бы одну строку перед проведением.", "Расход", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (Lines.Any(x => x.HasShortage))
        {
            MessageBox.Show("В документе есть строки с нехваткой остатка. Проведение заблокировано на уровне UI.", "Расход", MessageBoxButton.OK, MessageBoxImage.Error);
            return;
        }

        DocumentStatus = "Проведен";
        MessageBox.Show("UI-демо: документ помечен как проведенный. Реальное списание подключится из backend.", "Расход", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void LineQuantityTextBox_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        UpdateSelectedValidation();
    }

    private void RefreshDocuments()
    {
        var filtered = _allDocuments
            .Where(x => string.IsNullOrWhiteSpace(DocumentFilterText)
                || x.Number.Contains(DocumentFilterText, StringComparison.OrdinalIgnoreCase)
                || x.CounterpartyName.Contains(DocumentFilterText, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToList();

        FilteredDocuments.Clear();
        foreach (var item in filtered)
        {
            FilteredDocuments.Add(item);
        }

        OnPropertyChanged(nameof(QueueSummary));
    }

    private void RefreshHistory()
    {
        var filtered = _allHistory
            .Where(x => string.IsNullOrWhiteSpace(HistoryFilterText)
                || x.ProductName.Contains(HistoryFilterText, StringComparison.OrdinalIgnoreCase)
                || x.Sku.Contains(HistoryFilterText, StringComparison.OrdinalIgnoreCase)
                || x.DocumentNumber.Contains(HistoryFilterText, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => x.OccurredAt)
            .ToList();

        FilteredHistory.Clear();
        foreach (var item in filtered)
        {
            FilteredHistory.Add(item);
        }

        OnPropertyChanged(nameof(HistorySummary));
    }

    private void LoadDocumentLines(int documentId)
    {
        Lines.Clear();

        List<DocumentLineItem> seed = documentId switch
        {
            1 =>
            [
                new DocumentLineItem { LineNo = 1, Sku = "COF-250", ProductName = "Кофе 250г", Quantity = 8, Unit = "шт", AvailableStock = 5, HasShortage = true, ValidationMessage = "Нехватка 3 шт" },
                new DocumentLineItem { LineNo = 2, Sku = "MILK-1L", ProductName = "Молоко 1л", Quantity = 10, Unit = "шт", AvailableStock = 12, HasShortage = false, ValidationMessage = "OK" }
            ],
            2 =>
            [
                new DocumentLineItem { LineNo = 1, Sku = "MILK-1L", ProductName = "Молоко 1л", Quantity = 4, Unit = "шт", AvailableStock = 12, HasShortage = false, ValidationMessage = "OK" },
                new DocumentLineItem { LineNo = 2, Sku = "SUG-1KG", ProductName = "Сахар 1кг", Quantity = 5, Unit = "шт", AvailableStock = 22, HasShortage = false, ValidationMessage = "OK" },
                new DocumentLineItem { LineNo = 3, Sku = "COF-250", ProductName = "Кофе 250г", Quantity = 5, Unit = "шт", AvailableStock = 5, HasShortage = false, ValidationMessage = "OK" }
            ],
            _ =>
            [
                new DocumentLineItem { LineNo = 1, Sku = "SUG-1KG", ProductName = "Сахар 1кг", Quantity = 6, Unit = "шт", AvailableStock = 22, HasShortage = false, ValidationMessage = "OK" }
            ]
        };

        foreach (var line in seed)
        {
            Lines.Add(line);
        }

        OnPropertyChanged(nameof(TotalsText));
        OnPropertyChanged(nameof(ShortageSummary));
        OnPropertyChanged(nameof(WarningBadgeText));
    }

    private void NewDocument()
    {
        SelectedDocument = null;
        DocumentNumber = $"OUT-{DateTime.Now:HHmmss}";
        RecipientName = "Ручная выдача";
        DocumentStatus = "Черновик";
        DocumentComment = string.Empty;
        Lines.Clear();
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(QueueHeadline));
        OnPropertyChanged(nameof(TotalsText));
        OnPropertyChanged(nameof(ShortageSummary));
        OnPropertyChanged(nameof(WarningBadgeText));
        UpdateSelectedValidation();
    }

    private void UpdateSelectedValidation()
    {
        OnPropertyChanged(nameof(SelectedValidationText));
        OnPropertyChanged(nameof(SelectedValidationBackground));
        OnPropertyChanged(nameof(SelectedValidationBorder));
        OnPropertyChanged(nameof(SelectedValidationForeground));
    }

    private void ResequenceLines()
    {
        for (var i = 0; i < Lines.Count; i++)
        {
            Lines[i].LineNo = i + 1;
        }

        LinesDataGrid.Items.Refresh();
    }

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out value);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
    {
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
