using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;

namespace SmartStockAI.App.Views;

public partial class InboundPage : Page, INotifyPropertyChanged
{
    private readonly List<DocumentListItem> _allDocuments;
    private readonly List<MovementHistoryItem> _allHistory;
    private string _documentFilterText = string.Empty;
    private string _historyFilterText = string.Empty;
    private DocumentListItem? _selectedDocument;
    private ProductLookupItem? _selectedProduct;
    private DocumentLineItem? _selectedLine;
    private LookupItem? _selectedSupplier;
    private string _documentNumber = string.Empty;
    private string _documentComment = string.Empty;
    private string _documentStatus = "Черновик";

    public InboundPage()
    {
        InitializeComponent();
        DataContext = this;

        Suppliers =
        [
            new LookupItem { Id = 1, Name = "Northwind Foods" },
            new LookupItem { Id = 2, Name = "Volga Retail Group" },
            new LookupItem { Id = 3, Name = "Local Import Partner" }
        ];

        Products =
        [
            new ProductLookupItem { Id = 1, Sku = "MILK-1L", Name = "Молоко 1л", Unit = "шт", AvailableStock = 42 },
            new ProductLookupItem { Id = 2, Sku = "COF-250", Name = "Кофе 250г", Unit = "шт", AvailableStock = 18 },
            new ProductLookupItem { Id = 3, Sku = "SUG-1KG", Name = "Сахар 1кг", Unit = "шт", AvailableStock = 67 }
        ];

        _allDocuments =
        [
            new DocumentListItem { Id = 1, Number = "IN-24031", CounterpartyName = "Northwind Foods", Status = "Проведен", CreatedAt = DateTime.Today.AddDays(-1), LinesCount = 3, TotalQuantity = 96 },
            new DocumentListItem { Id = 2, Number = "IN-24032", CounterpartyName = "Volga Retail Group", Status = "Черновик", CreatedAt = DateTime.Today, LinesCount = 2, TotalQuantity = 34 },
            new DocumentListItem { Id = 3, Number = "IN-24033", CounterpartyName = "Local Import Partner", Status = "Черновик", CreatedAt = DateTime.Today, LinesCount = 1, TotalQuantity = 12 }
        ];

        _allHistory =
        [
            new MovementHistoryItem { OccurredAt = DateTime.Now.AddHours(-5), ProductName = "Молоко 1л", Sku = "MILK-1L", MovementType = "Приход", DocumentNumber = "IN-24031", Quantity = 24, BalanceAfter = 42, Comment = "Утренний прием" },
            new MovementHistoryItem { OccurredAt = DateTime.Now.AddHours(-3), ProductName = "Кофе 250г", Sku = "COF-250", MovementType = "Приход", DocumentNumber = "IN-24031", Quantity = 16, BalanceAfter = 18, Comment = "Дозавоз" },
            new MovementHistoryItem { OccurredAt = DateTime.Now.AddHours(-1), ProductName = "Сахар 1кг", Sku = "SUG-1KG", MovementType = "Приход", DocumentNumber = "IN-24032", Quantity = 10, BalanceAfter = 67, Comment = "Черновик поставки" }
        ];

        FilteredDocuments = [];
        FilteredHistory = [];
        Lines = [];

        SelectedSupplier = Suppliers.FirstOrDefault();
        SelectedProduct = Products.FirstOrDefault();
        NewDocument();
        RefreshDocuments();
        RefreshHistory();
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DocumentListItem> FilteredDocuments { get; }

    public ObservableCollection<MovementHistoryItem> FilteredHistory { get; }

    public ObservableCollection<DocumentLineItem> Lines { get; }

    public IReadOnlyList<LookupItem> Suppliers { get; }

    public IReadOnlyList<ProductLookupItem> Products { get; }

    public DocumentListItem? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetField(ref _selectedDocument, value) && value is not null)
            {
                DocumentNumber = value.Number;
                DocumentStatus = value.Status;
                SelectedSupplier = Suppliers.FirstOrDefault(x => x.Name == value.CounterpartyName) ?? Suppliers.FirstOrDefault();
                DocumentComment = $"Документ {value.Number} подготовлен для приемки партии.";
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
            }
        }
    }

    public DocumentLineItem? SelectedLine
    {
        get => _selectedLine;
        set => SetField(ref _selectedLine, value);
    }

    public LookupItem? SelectedSupplier
    {
        get => _selectedSupplier;
        set => SetField(ref _selectedSupplier, value);
    }

    public string DocumentNumber
    {
        get => _documentNumber;
        set => SetField(ref _documentNumber, value);
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

    public string TotalsText => $"Строк: {Lines.Count} · Кол-во: {Lines.Sum(x => x.Quantity).ToString("0.##", CultureInfo.InvariantCulture)}";

    public string HistorySummary => $"{FilteredHistory.Count} движений";

    public string SelectedProductStockText => SelectedProduct is null
        ? "Выбери товар"
        : $"{SelectedProduct.AvailableStock:0.##} {SelectedProduct.Unit}";

    private void NewDocumentButton_OnClick(object sender, RoutedEventArgs e)
    {
        NewDocument();
    }

    private void AddLineButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedProduct is null)
        {
            MessageBox.Show("Выбери товар для строки документа.", "Приход", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!decimal.TryParse(LineQuantityTextBox.Text, NumberStyles.Any, CultureInfo.InvariantCulture, out var quantity) &&
            !decimal.TryParse(LineQuantityTextBox.Text, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out quantity))
        {
            MessageBox.Show("Количество должно быть числом.", "Приход", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (quantity <= 0)
        {
            MessageBox.Show("Количество должно быть больше нуля.", "Приход", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        Lines.Add(new DocumentLineItem
        {
            LineNo = Lines.Count + 1,
            Sku = SelectedProduct.Sku,
            ProductName = SelectedProduct.Name,
            Quantity = quantity,
            AvailableStock = SelectedProduct.AvailableStock,
            Unit = SelectedProduct.Unit,
            Comment = LineCommentTextBox.Text.Trim()
        });

        LineQuantityTextBox.Text = string.Empty;
        LineCommentTextBox.Text = string.Empty;
        OnPropertyChanged(nameof(TotalsText));
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
    }

    private void PostDocumentButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Lines.Count == 0)
        {
            MessageBox.Show("Добавь хотя бы одну строку перед проведением.", "Приход", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        DocumentStatus = "Проведен";
        MessageBox.Show("UI-демо: документ помечен как проведенный. Логику проведения подключит backend.", "Приход", MessageBoxButton.OK, MessageBoxImage.Information);
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
                new DocumentLineItem { LineNo = 1, Sku = "MILK-1L", ProductName = "Молоко 1л", Quantity = 24, Unit = "шт", Comment = "Паллет 1" },
                new DocumentLineItem { LineNo = 2, Sku = "COF-250", ProductName = "Кофе 250г", Quantity = 16, Unit = "шт", Comment = "Секция B" },
                new DocumentLineItem { LineNo = 3, Sku = "SUG-1KG", ProductName = "Сахар 1кг", Quantity = 56, Unit = "шт", Comment = "Основной запас" }
            ],
            2 =>
            [
                new DocumentLineItem { LineNo = 1, Sku = "SUG-1KG", ProductName = "Сахар 1кг", Quantity = 10, Unit = "шт", Comment = "Черновик" },
                new DocumentLineItem { LineNo = 2, Sku = "MILK-1L", ProductName = "Молоко 1л", Quantity = 24, Unit = "шт", Comment = "Допоставка" }
            ],
            _ =>
            [
                new DocumentLineItem { LineNo = 1, Sku = "COF-250", ProductName = "Кофе 250г", Quantity = 12, Unit = "шт", Comment = "Новая партия" }
            ]
        };

        foreach (var line in seed)
        {
            Lines.Add(line);
        }

        OnPropertyChanged(nameof(TotalsText));
    }

    private void NewDocument()
    {
        SelectedDocument = null;
        DocumentNumber = $"IN-{DateTime.Now:HHmmss}";
        DocumentStatus = "Черновик";
        DocumentComment = string.Empty;
        SelectedSupplier = Suppliers.FirstOrDefault();
        Lines.Clear();
        OnPropertyChanged(nameof(EditorTitle));
        OnPropertyChanged(nameof(QueueHeadline));
        OnPropertyChanged(nameof(TotalsText));
    }

    private void ResequenceLines()
    {
        for (var i = 0; i < Lines.Count; i++)
        {
            Lines[i].LineNo = i + 1;
        }

        LinesDataGrid.Items.Refresh();
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
