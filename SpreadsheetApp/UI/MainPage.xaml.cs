using System.ComponentModel;
using SpreadsheetApp.Model;

namespace SpreadsheetApp.UI;

/// <summary>Main page: builds the cell grid and forwards user input to the view model.</summary>
public partial class MainPage : ContentPage
{
    private const double RowHeaderWidth = 48;
    private const double ColumnWidth = 120;
    private const double HeaderHeight = 32;
    private const double RowHeight = 38;

    // Grid colors (match Resources/Styles/Colors.xaml)
    private static readonly Color ErrorColor = Color.FromArgb("#C62828");
    private static readonly Color CellTextColor = Color.FromArgb("#0B132B");
    private static readonly Color CellColor = Color.FromArgb("#F1F5F9");
    private static readonly Color CellAltColor = Color.FromArgb("#E2E8F0");
    private static readonly Color HeaderColor = Color.FromArgb("#2B3A55");
    private static readonly Color HeaderTextColor = Color.FromArgb("#F4B942");

    private readonly SpreadsheetViewModel _viewModel;
    private Entry[,] _cells = new Entry[0, 0];

    public MainPage(SpreadsheetViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        viewModel.CellsChanged += (_, _) => RefreshCells();
        viewModel.StructureChanged += (_, _) => BuildGrid();
        viewModel.PropertyChanged += OnViewModelPropertyChanged;

        BuildGrid();
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        UpdateWindowTitle();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SpreadsheetViewModel.Title))
            UpdateWindowTitle();
    }

    private void UpdateWindowTitle()
    {
        if (Window is not null)
            Window.Title = _viewModel.Title;
    }

    // ---------- Event handlers ----------

    // Enter commits the cell and moves to the cell below
    private void OnCellCompleted(object? sender, EventArgs e)
    {
        if (sender is not Entry entry)
            return;

        var (row, column) = GetPosition(entry);
        _viewModel.EditCell(row, column, entry.Text ?? string.Empty);

        if (row + 1 < _viewModel.RowCount)
            _cells[row + 1, column].Focus();
        else
            entry.Unfocus();
    }

    private async void OnHelpClicked(object? sender, EventArgs e)
    {
        await Navigation.PushAsync(new HelpPage());
    }

    // An edited cell always shows its expression
    private void OnCellFocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry)
            return;

        var (row, column) = GetPosition(entry);
        entry.Text = _viewModel.GetExpression(row, column);
        entry.TextColor = CellTextColor;
        _viewModel.SelectCell(row, column);
    }

    private void OnCellUnfocused(object? sender, FocusEventArgs e)
    {
        if (sender is not Entry entry)
            return;

        var (row, column) = GetPosition(entry);
        if (row >= _viewModel.RowCount || column >= _viewModel.ColumnCount)
            return; // cell was removed with its row/column

        _viewModel.EditCell(row, column, entry.Text ?? string.Empty);
        RefreshCell(entry, row, column);
    }

    // Mirrors typing into the formula bar
    private void OnCellTextChanged(object? sender, TextChangedEventArgs e)
    {
        if (sender is Entry { IsFocused: true })
            _viewModel.SelectedExpression = e.NewTextValue ?? string.Empty;
    }

    // ---------- Grid building and refresh ----------

    // Rebuilds headers and cell entries for the current table size
    private void BuildGrid()
    {
        int rows = _viewModel.RowCount;
        int columns = _viewModel.ColumnCount;

        SheetGrid.Children.Clear();
        SheetGrid.RowDefinitions.Clear();
        SheetGrid.ColumnDefinitions.Clear();

        SheetGrid.ColumnDefinitions.Add(new ColumnDefinition(RowHeaderWidth));
        for (int c = 0; c < columns; c++)
            SheetGrid.ColumnDefinitions.Add(new ColumnDefinition(ColumnWidth));

        SheetGrid.RowDefinitions.Add(new RowDefinition(HeaderHeight));
        for (int r = 0; r < rows; r++)
            SheetGrid.RowDefinitions.Add(new RowDefinition(RowHeight));

        SheetGrid.Add(CreateHeader(string.Empty), 0, 0);
        for (int c = 0; c < columns; c++)
            SheetGrid.Add(CreateHeader(CellAddress.ColumnName(c)), c + 1, 0);

        _cells = new Entry[rows, columns];
        for (int r = 0; r < rows; r++)
        {
            SheetGrid.Add(CreateHeader((r + 1).ToString()), 0, r + 1);
            for (int c = 0; c < columns; c++)
            {
                var entry = CreateCellEntry(r % 2 == 0 ? CellColor : CellAltColor);
                _cells[r, c] = entry;
                SheetGrid.Add(entry, c + 1, r + 1);
            }
        }

        RefreshCells();
    }

    private Entry CreateCellEntry(Color background)
    {
        var entry = new Entry
        {
            BackgroundColor = background,
            TextColor = CellTextColor,
            FontSize = 14,
            Margin = 0,
            MinimumHeightRequest = RowHeight,
            HeightRequest = RowHeight,
            ReturnType = ReturnType.Next,
            IsSpellCheckEnabled = false,
            IsTextPredictionEnabled = false,
        };
        entry.Completed += OnCellCompleted;
        entry.Focused += OnCellFocused;
        entry.Unfocused += OnCellUnfocused;
        entry.TextChanged += OnCellTextChanged;
        return entry;
    }

    private static Label CreateHeader(string text)
    {
        var label = new Label
        {
            Text = text,
            FontAttributes = FontAttributes.Bold,
            HorizontalTextAlignment = TextAlignment.Center,
            VerticalTextAlignment = TextAlignment.Center,
            BackgroundColor = HeaderColor,
            TextColor = HeaderTextColor,
        };
        return label;
    }

    private void RefreshCells()
    {
        for (int r = 0; r < _cells.GetLength(0); r++)
            for (int c = 0; c < _cells.GetLength(1); c++)
            {
                var entry = _cells[r, c];
                if (!entry.IsFocused)
                    RefreshCell(entry, r, c);
            }
    }

    private void RefreshCell(Entry entry, int row, int column)
    {
        entry.Text = _viewModel.GetDisplayText(row, column);
        entry.TextColor = _viewModel.HasError(row, column) ? ErrorColor : CellTextColor;
        // No tooltip for empty cells (Windows would show an empty box)
        string toolTip = _viewModel.GetToolTip(row, column);
        if (toolTip.Length > 0)
            ToolTipProperties.SetText(entry, toolTip);
        else
            entry.ClearValue(ToolTipProperties.TextProperty);
    }

    private static (int Row, int Column) GetPosition(Entry entry) =>
        (Grid.GetRow(entry) - 1, Grid.GetColumn(entry) - 1);
}
