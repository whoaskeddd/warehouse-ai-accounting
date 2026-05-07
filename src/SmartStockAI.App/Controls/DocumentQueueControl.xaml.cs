using System.Collections;
using System.Windows;
using System.Windows.Controls;

namespace SmartStockAI.App.Controls;

public partial class DocumentQueueControl : UserControl
{
    public static readonly DependencyProperty TitleProperty =
        DependencyProperty.Register(nameof(Title), typeof(string), typeof(DocumentQueueControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SelectedDocumentTitleProperty =
        DependencyProperty.Register(nameof(SelectedDocumentTitle), typeof(string), typeof(DocumentQueueControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SummaryTextProperty =
        DependencyProperty.Register(nameof(SummaryText), typeof(string), typeof(DocumentQueueControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SearchTextProperty =
        DependencyProperty.Register(nameof(SearchText), typeof(string), typeof(DocumentQueueControl), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ItemsSourceProperty =
        DependencyProperty.Register(nameof(ItemsSource), typeof(IEnumerable), typeof(DocumentQueueControl), new PropertyMetadata(null));

    public static readonly DependencyProperty SelectedItemProperty =
        DependencyProperty.Register(nameof(SelectedItem), typeof(object), typeof(DocumentQueueControl), new PropertyMetadata(null));

    public DocumentQueueControl()
    {
        InitializeComponent();
    }

    public string Title
    {
        get => (string)GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string SelectedDocumentTitle
    {
        get => (string)GetValue(SelectedDocumentTitleProperty);
        set => SetValue(SelectedDocumentTitleProperty, value);
    }

    public string SummaryText
    {
        get => (string)GetValue(SummaryTextProperty);
        set => SetValue(SummaryTextProperty, value);
    }

    public string SearchText
    {
        get => (string)GetValue(SearchTextProperty);
        set => SetValue(SearchTextProperty, value);
    }

    public IEnumerable? ItemsSource
    {
        get => (IEnumerable?)GetValue(ItemsSourceProperty);
        set => SetValue(ItemsSourceProperty, value);
    }

    public object? SelectedItem
    {
        get => GetValue(SelectedItemProperty);
        set => SetValue(SelectedItemProperty, value);
    }
}
