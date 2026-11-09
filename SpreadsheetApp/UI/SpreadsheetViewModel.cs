using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SpreadsheetApp.Model;
using SpreadsheetApp.Storage;

namespace SpreadsheetApp.UI;

/// <summary>Main page view model: table editing, display mode, current document and storage.</summary>
public sealed class SpreadsheetViewModel : INotifyPropertyChanged
{
    private const string AppTitle = "Електронні таблиці";

    private readonly IDocumentFileService _files;
    private readonly ICredentialsProvider _credentials;
    private readonly IDialogService _dialogs;

    private Spreadsheet _sheet = new();
    private IFileStorage _storage;
    private IFileStorage? _currentFileStorage;
    private DisplayMode _mode = DisplayMode.Value;
    private string _currentFileName = string.Empty;
    private string _errorMessage = string.Empty;
    private bool _isModified;
    private bool _isBusy;
    private CellAddress? _selected;
    private string _selectedExpression = string.Empty;

    public SpreadsheetViewModel(
        IEnumerable<IFileStorage> storages,
        IDocumentFileService files,
        ICredentialsProvider credentials,
        IDialogService dialogs)
    {
        Storages = storages.ToList();
        if (Storages.Count == 0)
            throw new ArgumentException("At least one storage is required.", nameof(storages));
        _storage = Storages[0];
        _files = files;
        _credentials = credentials;
        _dialogs = dialogs;

        NewCommand = new Command(async () => await RunAsync(NewAsync), () => !IsBusy);
        OpenCommand = new Command(async () => await RunAsync(OpenAsync), () => !IsBusy);
        SaveCommand = new Command(async () => await RunAsync(SaveAsync), () => !IsBusy);
        SaveAsCommand = new Command(async () => await RunAsync(SaveAsAsync), () => !IsBusy);
        AddRowCommand = new Command(AddRow);
        RemoveRowCommand = new Command(async () => await RunAsync(RemoveRowAsync));
        AddColumnCommand = new Command(AddColumn);
        RemoveColumnCommand = new Command(async () => await RunAsync(RemoveColumnAsync));
        CommitFormulaCommand = new Command(CommitFormula);
        ShowValuesCommand = new Command(() => Mode = DisplayMode.Value);
        ShowExpressionsCommand = new Command(() => Mode = DisplayMode.Expression);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    // Cell contents changed; table size is the same
    public event EventHandler? CellsChanged;

    // Row/column count changed or another table was loaded
    public event EventHandler? StructureChanged;

    public DisplayMode Mode
    {
        get => _mode;
        set
        {
            if (SetField(ref _mode, value))
            {
                OnPropertyChanged(nameof(IsValueMode));
                OnPropertyChanged(nameof(IsExpressionMode));
                CellsChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public bool IsValueMode => Mode == DisplayMode.Value;
    public bool IsExpressionMode => Mode == DisplayMode.Expression;

    public string CurrentFileName
    {
        get => _currentFileName;
        private set
        {
            if (SetField(ref _currentFileName, value))
            {
                OnPropertyChanged(nameof(Title));
                OnPropertyChanged(nameof(DocumentName));
            }
        }
    }

    // Header above the table: file name, storage and size
    public string DocumentName =>
        CurrentFileName.Length > 0 ? Path.GetFileNameWithoutExtension(CurrentFileName) : "Нова таблиця";

    public string DocumentInfo => $"{_storage.DisplayName} · {RowCount} × {ColumnCount}";

    public string Title =>
        $"{(CurrentFileName.Length > 0 ? Path.GetFileName(CurrentFileName) : "Нова таблиця")}{(IsModified ? " *" : "")} — {AppTitle}";

    public string ErrorMessage
    {
        get => _errorMessage;
        private set
        {
            if (SetField(ref _errorMessage, value))
                OnPropertyChanged(nameof(HasErrorMessage));
        }
    }

    public bool HasErrorMessage => ErrorMessage.Length > 0;

    public bool IsModified
    {
        get => _isModified;
        private set
        {
            if (SetField(ref _isModified, value))
                OnPropertyChanged(nameof(Title));
        }
    }

    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (SetField(ref _isBusy, value))
            {
                foreach (var command in new[] { NewCommand, OpenCommand, SaveCommand, SaveAsCommand })
                    ((Command)command).ChangeCanExecute();
            }
        }
    }

    // All registered storages; a new one only needs a DI registration
    public IReadOnlyList<IFileStorage> Storages { get; }

    // Storages that need sign-in are connected first; on cancel or error the previous one stays selected
    public IFileStorage SelectedStorage
    {
        get => _storage;
        set
        {
            if (value is null || value == _storage)
                return;
            if (IsBusy)
                RevertStorageSelection();
            else if (value is IConnectableStorage { IsConnected: false } connectable)
                _ = RunAsync(() => ConnectAsync(connectable));
            else
                SwitchStorage(value);
        }
    }

    public int RowCount => _sheet.RowCount;
    public int ColumnCount => _sheet.ColumnCount;

    public string SelectedCellName => _selected?.ToString() ?? "—";

    public string SelectedExpression
    {
        get => _selectedExpression;
        set => SetField(ref _selectedExpression, value ?? string.Empty);
    }

    public ICommand NewCommand { get; }
    public ICommand OpenCommand { get; }
    public ICommand SaveCommand { get; }
    public ICommand SaveAsCommand { get; }
    public ICommand AddRowCommand { get; }
    public ICommand RemoveRowCommand { get; }
    public ICommand AddColumnCommand { get; }
    public ICommand RemoveColumnCommand { get; }
    public ICommand CommitFormulaCommand { get; }
    public ICommand ShowValuesCommand { get; }
    public ICommand ShowExpressionsCommand { get; }

    // ---------- Cells ----------

    public void EditCell(int row, int column, string text)
    {
        var address = new CellAddress(row, column);
        if (!_sheet.Contains(address))
            return;

        text = text?.Trim() ?? string.Empty;
        if (_sheet.GetCell(address).Expression == text)
            return;

        _sheet.SetExpression(address, text);
        IsModified = true;

        if (_selected == address)
            SelectedExpression = text;
        UpdateErrorMessage(address);
        CellsChanged?.Invoke(this, EventArgs.Empty);
    }

    public string GetDisplayText(int row, int column)
    {
        var cell = _sheet.GetCell(new CellAddress(row, column));
        if (Mode == DisplayMode.Expression)
            return cell.Expression;
        if (cell.Error is { } error)
            return error.Code;
        return cell.Value?.ToString() ?? string.Empty;
    }

    public string GetExpression(int row, int column) =>
        _sheet.GetCell(new CellAddress(row, column)).Expression;

    public bool HasError(int row, int column) =>
        _sheet.GetCell(new CellAddress(row, column)).HasError;

    // Tooltip: error details or "expression = value"
    public string GetToolTip(int row, int column)
    {
        var cell = _sheet.GetCell(new CellAddress(row, column));
        if (cell.Error is { } error)
            return error.DetailedMessage;
        if (cell.Expression.Length == 0)
            return string.Empty;
        return $"{cell.Expression}  =  {cell.Value}";
    }

    public void SelectCell(int row, int column)
    {
        var address = new CellAddress(row, column);
        _selected = address;
        OnPropertyChanged(nameof(SelectedCellName));
        SelectedExpression = _sheet.GetCell(address).Expression;
        UpdateErrorMessage(address);
    }

    private void CommitFormula()
    {
        if (_selected is { } address)
            EditCell(address.Row, address.Column, SelectedExpression);
    }

    private void UpdateErrorMessage(CellAddress address)
    {
        var error = _sheet.GetCell(address).Error;
        ErrorMessage = error is null ? string.Empty : $"{address}: {error.DetailedMessage}";
    }

    // ---------- Rows and columns ----------

    private void AddRow()
    {
        _sheet.AddRow();
        IsModified = true;
        RaiseStructureChanged();
    }

    private void AddColumn()
    {
        _sheet.AddColumn();
        IsModified = true;
        RaiseStructureChanged();
    }

    private async Task RemoveRowAsync()
    {
        if (_sheet.RowCount <= 1)
        {
            ErrorMessage = "Таблиця повинна містити хоча б один рядок.";
            return;
        }

        int row = _sheet.RowCount - 1;
        if (_sheet.IsRowInUse(row) &&
            !await _dialogs.ConfirmAsync("Видалення рядка",
                $"Рядок {row + 1} містить дані або на нього посилаються інші клітинки. " +
                "Такі посилання стануть помилкою #REF!. Видалити рядок?"))
            return;

        _sheet.RemoveRow();
        IsModified = true;
        RaiseStructureChanged();
    }

    private async Task RemoveColumnAsync()
    {
        if (_sheet.ColumnCount <= 1)
        {
            ErrorMessage = "Таблиця повинна містити хоча б один стовпець.";
            return;
        }

        int column = _sheet.ColumnCount - 1;
        if (_sheet.IsColumnInUse(column) &&
            !await _dialogs.ConfirmAsync("Видалення стовпця",
                $"Стовпець {CellAddress.ColumnName(column)} містить дані або на нього посилаються інші клітинки. " +
                "Такі посилання стануть помилкою #REF!. Видалити стовпець?"))
            return;

        _sheet.RemoveColumn();
        IsModified = true;
        RaiseStructureChanged();
    }

    // ---------- Document ----------

    private async Task NewAsync()
    {
        if (!await ConfirmDiscardChangesAsync())
            return;
        LoadSheet(new Spreadsheet(), string.Empty, null);
    }

    public async Task SaveAsync()
    {
        if (CurrentFileName.Length == 0 || _currentFileStorage != _storage)
        {
            await SaveAsAsync();
            return;
        }
        await _files.SaveAsync(_sheet, _storage, CurrentFileName);
        MarkSaved(CurrentFileName);
    }

    private async Task SaveAsAsync()
    {
        string? fileName = await _files.SaveAsAsync(_sheet, _storage, CurrentFileName);
        if (fileName is not null)
            MarkSaved(fileName);
    }

    private async Task OpenAsync()
    {
        if (!await ConfirmDiscardChangesAsync())
            return;

        var document = await _files.OpenAsync(_storage);
        if (document is not null)
            LoadSheet(document.Sheet, document.FileName, _storage);
    }

    // Offers to save unsaved changes; false means the user cancelled
    public async Task<bool> ConfirmDiscardChangesAsync()
    {
        if (!IsModified)
            return true;

        const string save = "Зберегти";
        const string discard = "Не зберігати";
        string? choice = await _dialogs.ChooseAsync("У таблиці є незбережені зміни. Зберегти їх?", new[] { save, discard });
        if (choice == discard)
            return true;
        if (choice != save)
            return false;

        try
        {
            await SaveAsync();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
            return false;
        }
        return !IsModified;
    }

    private void MarkSaved(string fileName)
    {
        CurrentFileName = fileName;
        _currentFileStorage = _storage;
        IsModified = false;
        ErrorMessage = string.Empty;
    }

    private void LoadSheet(Spreadsheet sheet, string fileName, IFileStorage? storage)
    {
        _sheet = sheet;
        CurrentFileName = fileName;
        _currentFileStorage = storage;
        IsModified = false;
        _selected = null;
        OnPropertyChanged(nameof(SelectedCellName));
        SelectedExpression = string.Empty;
        ErrorMessage = string.Empty;
        RaiseStructureChanged();
    }

    // ---------- Storage ----------

    private async Task ConnectAsync(IConnectableStorage storage)
    {
        try
        {
            if (await storage.ConnectAsync(_credentials))
                SwitchStorage(storage);
        }
        finally
        {
            OnPropertyChanged(nameof(SelectedStorage)); // reverts the picker if not switched
        }
    }

    private void SwitchStorage(IFileStorage storage)
    {
        _storage = storage;
        OnPropertyChanged(nameof(SelectedStorage));
        OnPropertyChanged(nameof(DocumentInfo));
    }

    // The picker ignores a change raised inside its own setter, so revert on the next tick
    private void RevertStorageSelection() =>
        Dispatcher.GetForCurrentThread()?.Dispatch(() => OnPropertyChanged(nameof(SelectedStorage)));

    // ---------- Helpers ----------

    // Runs an async action with a busy indicator and shows any error to the user
    private async Task RunAsync(Func<Task> action)
    {
        if (IsBusy)
            return;
        IsBusy = true;
        try
        {
            await action();
        }
        catch (Exception ex)
        {
            await ShowErrorAsync(ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task ShowErrorAsync(Exception ex)
    {
        string message = ex is OperationCanceledException
            ? "Операцію скасовано або перевищено час очікування."
            : ex.Message;
        ErrorMessage = message;
        await _dialogs.AlertAsync("Помилка", message);
    }

    private void RaiseStructureChanged()
    {
        OnPropertyChanged(nameof(RowCount));
        OnPropertyChanged(nameof(ColumnCount));
        OnPropertyChanged(nameof(DocumentInfo));
        StructureChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
            return false;
        field = value;
        OnPropertyChanged(propertyName);
        return true;
    }

    private void OnPropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}
