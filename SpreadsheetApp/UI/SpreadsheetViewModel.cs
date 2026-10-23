using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows.Input;
using SpreadsheetApp.Model;
using SpreadsheetApp.Storage;

namespace SpreadsheetApp.UI;

/// <summary>Main page view model: table editing, files and storage switching.</summary>
public sealed class SpreadsheetViewModel : INotifyPropertyChanged
{
    private const string AppTitle = "Електронні таблиці";
    private const string DefaultFileName = "Таблиця";
    private const string FileTypeDescription = "Електронна таблиця (JSON)";

    private Spreadsheet _sheet;
    private IFileStorage _storage;
    private readonly SpreadsheetSerializer _serializer;
    private readonly LocalFileStorage _localStorage;
    private readonly GoogleDriveStorage _driveStorage;
    private readonly IDialogService _dialogs;
    private readonly IFilePickerService _filePicker;

    private DisplayMode _mode = DisplayMode.Value;
    private string _currentFileName = string.Empty;
    private IFileStorage? _currentFileStorage;
    private string _errorMessage = string.Empty;
    private bool _isModified;
    private bool _isBusy;
    private CellAddress? _selected;
    private string _selectedExpression = string.Empty;

    public SpreadsheetViewModel(
        LocalFileStorage localStorage,
        GoogleDriveStorage driveStorage,
        SpreadsheetSerializer serializer,
        IDialogService dialogs,
        IFilePickerService filePicker)
    {
        _sheet = new Spreadsheet();
        _localStorage = localStorage;
        _driveStorage = driveStorage;
        _storage = localStorage;
        _serializer = serializer;
        _dialogs = dialogs;
        _filePicker = filePicker;

        NewCommand = new Command(async () => await RunAsync(NewAsync), () => !IsBusy);
        OpenCommand = new Command(async () => await RunAsync(OpenWithDialogAsync), () => !IsBusy);
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

    public string DocumentInfo => $"{StorageName} · {RowCount} × {ColumnCount}";

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

    public string Title =>
        $"{(CurrentFileName.Length > 0 ? Path.GetFileName(CurrentFileName) : "Нова таблиця")}{(IsModified ? " *" : "")} — {AppTitle}";

    // Storage switch: local disk or Google Drive
    public bool UseGoogleDrive
    {
        get => _storage == _driveStorage;
        set
        {
            if (value == UseGoogleDrive)
                return;
            if (value)
                _ = RunAsync(ConnectGoogleDriveAsync);
            else
                SwitchStorage(_localStorage);
        }
    }

    public string StorageName => _storage.DisplayName;

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

    // ---------- Files ----------

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
        await WriteFileAsync(CurrentFileName);
    }

    private async Task SaveAsAsync()
    {
        string suggested = CurrentFileName.Length > 0
            ? Path.GetFileNameWithoutExtension(CurrentFileName)
            : DefaultFileName;

        // Local disk: Explorer save dialog; Google Drive: name prompt
        if (_storage == _localStorage)
        {
            string? path = await _filePicker.PickSaveFileAsync(
                suggested, FileTypeDescription, SpreadsheetSerializer.FileExtension);
            if (path is not null)
                await WriteFileAsync(path);
            return;
        }

        string? input = await _dialogs.PromptAsync("Зберегти таблицю",
            $"Ім'я файлу (сховище: {StorageName}):", suggested);
        if (string.IsNullOrWhiteSpace(input))
            return;

        string fileName = NormalizeFileName(input);
        var existing = await _storage.ListFilesAsync();
        if (existing.Contains(fileName, StringComparer.OrdinalIgnoreCase) &&
            !fileName.Equals(CurrentFileName, StringComparison.OrdinalIgnoreCase) &&
            !await _dialogs.ConfirmAsync("Файл уже існує", $"Файл «{fileName}» уже існує. Замінити його?"))
            return;

        await WriteFileAsync(fileName);
    }

    private async Task WriteFileAsync(string fileName)
    {
        await _storage.SaveAsync(fileName, _serializer.Serialize(_sheet));
        CurrentFileName = fileName;
        _currentFileStorage = _storage;
        IsModified = false;
        ErrorMessage = string.Empty;
    }

    private async Task OpenWithDialogAsync()
    {
        if (!await ConfirmDiscardChangesAsync())
            return;

        // Local disk: Explorer open dialog; Google Drive: list of files
        if (_storage == _localStorage)
        {
            string? path = await _filePicker.PickOpenFileAsync(SpreadsheetSerializer.FileExtension);
            if (path is not null)
                await OpenAsync(path);
            return;
        }

        var files = await _storage.ListFilesAsync();
        if (files.Count == 0)
        {
            await _dialogs.AlertAsync("Відкрити таблицю", $"У сховищі «{StorageName}» ще немає збережених таблиць.");
            return;
        }

        string? fileName = await _dialogs.ChooseAsync($"Відкрити таблицю ({StorageName})", files);
        if (fileName is not null)
            await OpenAsync(fileName);
    }

    public async Task OpenAsync(string fileName)
    {
        byte[] data = await _storage.LoadAsync(fileName);
        Spreadsheet sheet = _serializer.Deserialize(data);
        LoadSheet(sheet, fileName, _storage);
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

    private static string NormalizeFileName(string input)
    {
        string name = input.Trim();
        foreach (char c in Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        if (!name.EndsWith(SpreadsheetSerializer.FileExtension, StringComparison.OrdinalIgnoreCase))
            name += SpreadsheetSerializer.FileExtension;
        return name;
    }

    // ---------- Storage ----------

    // On failure or cancel the switch flips back to local disk
    private async Task ConnectGoogleDriveAsync()
    {
        try
        {
            if (!_driveStorage.HasClientSecrets && !await ImportClientSecretsAsync())
            {
                OnPropertyChanged(nameof(UseGoogleDrive));
                return;
            }

            await _driveStorage.AuthorizeAsync();
            SwitchStorage(_driveStorage);
        }
        catch
        {
            OnPropertyChanged(nameof(UseGoogleDrive));
            throw;
        }
    }

    // Asks the user to pick the Google OAuth client JSON file
    private async Task<bool> ImportClientSecretsAsync()
    {
        bool choose = await _dialogs.ConfirmAsync("Підключення Google Drive",
            "Для входу в Google Drive програмі потрібен файл OAuth-клієнта Google (тип «Desktop app»), " +
            "який завантажується з Google Cloud Console. Як його створити, описано в «Довідці».\n\n" +
            "Вибрати цей JSON-файл зараз?",
            "Вибрати файл", "Скасувати");
        if (!choose)
            return false;

        string? path = await _filePicker.PickOpenFileAsync(".json");
        if (path is null)
            return false;

        await _driveStorage.ImportClientSecretsAsync(path);
        return true;
    }

    private void SwitchStorage(IFileStorage storage)
    {
        _storage = storage;
        OnPropertyChanged(nameof(UseGoogleDrive));
        OnPropertyChanged(nameof(StorageName));
        OnPropertyChanged(nameof(DocumentInfo));
    }

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
        string message = ex switch
        {
            OperationCanceledException => "Операцію скасовано або перевищено час очікування.",
            Google.GoogleApiException api => $"Помилка Google Drive: {api.Error?.Message ?? api.Message}",
            System.Net.Http.HttpRequestException => "Немає з'єднання з інтернетом або сервер недоступний.",
            _ => ex.Message
        };
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
