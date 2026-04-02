using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using SmartStockAI.App.Models;
using SmartStockAI.Core.Contracts.Products;
using SmartStockAI.Core.Contracts.Stock;
using SmartStockAI.Core.Contracts.Suppliers;
using SmartStockAI.Core.Enums;

namespace SmartStockAI.App.Views;

public partial class InboundPage : Page, INotifyPropertyChanged
{
    private readonly IStockService _stockService;
    private readonly IProductService _productService;
    private readonly ISupplierService _supplierService;
    private List<DocumentListItem> _allDocuments = [];
    private List<MovementHistoryItem> _allHistory = [];
    private List<StockDocumentDto> _documents = [];
    private string _documentFilterText = string.Empty;
    private string _historyFilterText = string.Empty;
    private DocumentListItem? _selectedDocument;
    private ProductLookupItem? _selectedProduct;
    private DocumentLineItem? _selectedLine;
    private LookupItem? _selectedSupplier;
    private string _documentNumber = string.Empty;
    private string _documentComment = string.Empty;
    private string _documentStatus = "Черновик";
    private int? _currentDocumentId;
    private bool _isRefreshingSelection;

    public InboundPage(IStockService stockService, IProductService productService, ISupplierService supplierService)
    {
        _stockService = stockService;
        _productService = productService;
        _supplierService = supplierService;

        InitializeComponent();
        DataContext = this;

        FilteredDocuments = [];
        FilteredHistory = [];
        Lines = [];
        Suppliers = [];
        Products = [];

        Loaded += OnLoaded;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public ObservableCollection<DocumentListItem> FilteredDocuments { get; }

    public ObservableCollection<MovementHistoryItem> FilteredHistory { get; }

    public ObservableCollection<DocumentLineItem> Lines { get; }

    public ObservableCollection<LookupItem> Suppliers { get; }

    public ObservableCollection<ProductLookupItem> Products { get; }

    public DocumentListItem? SelectedDocument
    {
        get => _selectedDocument;
        set
        {
            if (SetField(ref _selectedDocument, value) && !_isRefreshingSelection)
            {
                _ = LoadSelectedDocumentAsync(value);
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
        set
        {
            if (SetField(ref _documentNumber, value))
            {
                OnPropertyChanged(nameof(EditorTitle));
            }
        }
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

    private async void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;
        await LoadPageAsync();
    }

    private async Task LoadPageAsync()
    {
        await LoadSuppliersAsync();
        await LoadProductsAsync();
        await LoadDocumentsAsync();
        await LoadHistoryAsync();
        NewDocument();
    }

    private async Task LoadSuppliersAsync()
    {
        var suppliers = await _supplierService.GetAllAsync();
        Suppliers.Clear();
        foreach (var item in suppliers.OrderBy(x => x.Name))
        {
            Suppliers.Add(new LookupItem { Id = item.Id, Name = item.Name });
        }

        if (SelectedSupplier is null)
        {
            SelectedSupplier = Suppliers.FirstOrDefault();
        }
    }

    private async Task LoadProductsAsync()
    {
        var products = await _productService.GetAllAsync();
        Products.Clear();
        foreach (var item in products.OrderBy(x => x.Name))
        {
            Products.Add(new ProductLookupItem
            {
                Id = item.Id,
                Sku = item.Sku,
                Name = item.Name,
                Unit = item.Unit,
                AvailableStock = item.AvailableStock
            });
        }

        SelectedProduct ??= Products.FirstOrDefault();
        OnPropertyChanged(nameof(SelectedProductStockText));
    }

    private async Task LoadDocumentsAsync()
    {
        _documents = (await _stockService.GetDocumentsAsync(StockDocumentType.Receipt)).ToList();
        _allDocuments = _documents
            .Select(MapDocumentListItem)
            .OrderByDescending(x => x.CreatedAt)
            .ThenByDescending(x => x.Id)
            .ToList();

        RefreshDocuments();
    }

    private async Task LoadHistoryAsync()
    {
        _allHistory = (await _stockService.GetMovementsAsync())
            .Where(x => x.Type == StockMovementType.Receipt)
            .Select(MapMovementHistoryItem)
            .OrderByDescending(x => x.OccurredAt)
            .ToList();

        RefreshHistory();
    }

    private async Task LoadSelectedDocumentAsync(DocumentListItem? item)
    {
        if (item is null)
        {
            return;
        }

        var document = _documents.FirstOrDefault(x => x.Id == item.Id) ?? await _stockService.GetDocumentByIdAsync(item.Id);
        if (document is null)
        {
            return;
        }

        ApplyDocument(document);
    }

    private void NewDocumentButton_OnClick(object sender, RoutedEventArgs e)
    {
        NewDocument();
    }

    private async void AddLineButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedProduct is null)
        {
            MessageBox.Show("Выбери товар для строки документа.", "Приход", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        if (!TryParseDecimal(LineQuantityTextBox.Text, out var quantity))
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
            Comment = LineCommentTextBox.Text.Trim(),
            ValidationMessage = "OK"
        });

        LineQuantityTextBox.Text = string.Empty;
        LineCommentTextBox.Text = string.Empty;
        OnPropertyChanged(nameof(TotalsText));

        await SaveDraftAsync();
    }

    private async void RemoveLineButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (SelectedLine is null)
        {
            return;
        }

        Lines.Remove(SelectedLine);
        ResequenceLines();
        OnPropertyChanged(nameof(TotalsText));

        if (_currentDocumentId.HasValue)
        {
            await SaveDraftAsync();
        }
    }

    private async void PostDocumentButton_OnClick(object sender, RoutedEventArgs e)
    {
        if (Lines.Count == 0)
        {
            MessageBox.Show("Добавь хотя бы одну строку перед проведением.", "Приход", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            var document = await SaveDraftAsync();
            var posted = await _stockService.PostDocumentAsync(document.Id);
            if (posted is null)
            {
                return;
            }

            await ReloadAfterMutationAsync(posted.Id);
            MessageBox.Show("Документ прихода проведен.", "Приход", MessageBoxButton.OK, MessageBoxImage.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(ex.Message, "Приход", MessageBoxButton.OK, MessageBoxImage.Error);
        }
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

    private void NewDocument()
    {
        _currentDocumentId = null;
        _isRefreshingSelection = true;
        SelectedDocument = null;
        _isRefreshingSelection = false;
        DocumentNumber = CreateDefaultDocumentNumber("IN");
        DocumentStatus = "Черновик";
        DocumentComment = string.Empty;
        SelectedSupplier = Suppliers.FirstOrDefault();
        Lines.Clear();
        SelectedLine = null;
        OnPropertyChanged(nameof(QueueHeadline));
        OnPropertyChanged(nameof(TotalsText));
    }

    private void ApplyDocument(StockDocumentDto document)
    {
        _currentDocumentId = document.Id;
        DocumentNumber = document.Number;
        DocumentStatus = MapStatus(document.Status);
        DocumentComment = document.Comment ?? string.Empty;
        SelectedSupplier = Suppliers.FirstOrDefault(x => x.Id == document.SupplierId) ?? Suppliers.FirstOrDefault();

        Lines.Clear();
        var lineNo = 1;
        foreach (var line in document.Lines)
        {
            var product = Products.FirstOrDefault(x => x.Id == line.ProductId);
            Lines.Add(new DocumentLineItem
            {
                LineNo = lineNo++,
                Sku = line.ProductSku,
                ProductName = line.ProductName,
                Quantity = line.Quantity,
                Unit = product?.Unit ?? "шт",
                AvailableStock = product?.AvailableStock ?? 0,
                Comment = line.Comment ?? string.Empty,
                ValidationMessage = "OK"
            });
        }

        OnPropertyChanged(nameof(QueueHeadline));
        OnPropertyChanged(nameof(TotalsText));
    }

    private async Task<StockDocumentDto> SaveDraftAsync()
    {
        var requestLines = BuildRequestLines();
        if (_currentDocumentId.HasValue)
        {
            var updated = await _stockService.UpdateDocumentAsync(_currentDocumentId.Value, new UpdateStockDocumentRequest
            {
                SupplierId = SelectedSupplier?.Id,
                Comment = DocumentComment,
                Lines = requestLines
            });

            if (updated is null)
            {
                throw new InvalidOperationException("Черновик документа не найден.");
            }

            DocumentNumber = updated.Number;
            return updated;
        }

        var created = await _stockService.CreateDocumentAsync(new CreateStockDocumentRequest
        {
            Number = string.IsNullOrWhiteSpace(DocumentNumber) ? CreateDefaultDocumentNumber("IN") : DocumentNumber.Trim(),
            Type = StockDocumentType.Receipt,
            SupplierId = SelectedSupplier?.Id,
            Comment = DocumentComment,
            Lines = requestLines
        });

        _currentDocumentId = created.Id;
        DocumentNumber = created.Number;
        DocumentStatus = MapStatus(created.Status);
        await LoadDocumentsAsync();
        SelectDocumentInQueue(created.Id);
        return created;
    }

    private List<SaveStockDocumentLineRequest> BuildRequestLines()
    {
        var productMap = Products.ToDictionary(x => x.Sku, x => x.Id);
        return Lines.Select(x => new SaveStockDocumentLineRequest
        {
            ProductId = productMap[x.Sku],
            Quantity = x.Quantity,
            UnitPrice = 0,
            Comment = string.IsNullOrWhiteSpace(x.Comment) ? null : x.Comment.Trim()
        }).ToList();
    }

    private async Task ReloadAfterMutationAsync(int? selectedDocumentId = null)
    {
        await LoadProductsAsync();
        await LoadDocumentsAsync();
        await LoadHistoryAsync();

        if (selectedDocumentId.HasValue)
        {
            SelectDocumentInQueue(selectedDocumentId.Value);
            var document = await _stockService.GetDocumentByIdAsync(selectedDocumentId.Value);
            if (document is not null)
            {
                ApplyDocument(document);
                return;
            }
        }

        NewDocument();
    }

    private void SelectDocumentInQueue(int documentId)
    {
        var item = FilteredDocuments.FirstOrDefault(x => x.Id == documentId) ?? _allDocuments.FirstOrDefault(x => x.Id == documentId);
        if (item is null)
        {
            return;
        }

        _isRefreshingSelection = true;
        SelectedDocument = item;
        _isRefreshingSelection = false;
        OnPropertyChanged(nameof(QueueHeadline));
    }

    private static DocumentListItem MapDocumentListItem(StockDocumentDto document)
    {
        return new DocumentListItem
        {
            Id = document.Id,
            Number = document.Number,
            CounterpartyName = document.SupplierName ?? "Без поставщика",
            Status = MapStatus(document.Status),
            CreatedAt = document.CreatedAt.ToLocalTime(),
            LinesCount = document.TotalItems,
            TotalQuantity = document.TotalQuantity,
            HasWarnings = false
        };
    }

    private static MovementHistoryItem MapMovementHistoryItem(StockMovementDto movement)
    {
        return new MovementHistoryItem
        {
            OccurredAt = movement.CreatedAt.ToLocalTime(),
            ProductName = movement.ProductName,
            Sku = movement.ProductSku,
            MovementType = MapMovementType(movement.Type),
            DocumentNumber = movement.DocumentNumber ?? string.Empty,
            Quantity = movement.Quantity,
            BalanceAfter = movement.BalanceAfter,
            Comment = movement.Comment ?? string.Empty
        };
    }

    private static string MapStatus(StockDocumentStatus status) => status switch
    {
        StockDocumentStatus.Draft => "Черновик",
        StockDocumentStatus.Posted => "Проведен",
        StockDocumentStatus.Cancelled => "Отменен",
        _ => status.ToString()
    };

    private static string MapMovementType(StockMovementType type) => type switch
    {
        StockMovementType.Receipt => "Приход",
        StockMovementType.Issue => "Расход",
        StockMovementType.Reservation => "Резерв",
        StockMovementType.ReservationRelease => "Снятие резерва",
        StockMovementType.Adjustment => "Корректировка",
        _ => type.ToString()
    };

    private static bool TryParseDecimal(string? text, out decimal value)
    {
        return decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
            || decimal.TryParse(text, NumberStyles.Any, CultureInfo.GetCultureInfo("ru-RU"), out value);
    }

    private static string CreateDefaultDocumentNumber(string prefix)
    {
        return $"{prefix}-{DateTime.Now:yyyyMMdd-HH:mm}";
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
