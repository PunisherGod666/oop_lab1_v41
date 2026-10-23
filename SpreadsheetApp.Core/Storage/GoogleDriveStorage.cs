using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using Google.Apis.Util.Store;
using DriveFile = Google.Apis.Drive.v3.Data.File;

namespace SpreadsheetApp.Storage;

/// <summary>Stores spreadsheet files in a folder on the user's Google Drive (drive.file scope).</summary>
public sealed class GoogleDriveStorage : IFileStorage
{
    public const string ClientSecretsFileName = "client_secret.json";
    public const string FolderName = "SpreadsheetApp";

    private const string FolderMimeType = "application/vnd.google-apps.folder";
    private const string FileMimeType = "application/json";

    private readonly string _clientSecretsPath;
    private readonly string _tokenDirectory;
    private DriveService? _driveService;
    private string? _folderId;

    public GoogleDriveStorage(string clientSecretsPath, string tokenDirectory)
    {
        _clientSecretsPath = clientSecretsPath;
        _tokenDirectory = tokenDirectory;
    }

    public string DisplayName => "Google Drive";

    public bool HasClientSecrets => File.Exists(_clientSecretsPath);

    // Validates a "Desktop app" OAuth client file and copies it to the app's location
    public async Task ImportClientSecretsAsync(string sourcePath)
    {
        byte[] data = await File.ReadAllBytesAsync(sourcePath);
        try
        {
            using var json = System.Text.Json.JsonDocument.Parse(data);
            var root = json.RootElement;
            if (root.TryGetProperty("web", out _))
                throw new InvalidDataException(
                    "Це OAuth-клієнт типу «Web application». Для цієї програми потрібен клієнт типу «Desktop app».");
            if (!root.TryGetProperty("installed", out var installed) ||
                !installed.TryGetProperty("client_id", out _) ||
                !installed.TryGetProperty("client_secret", out _))
                throw new InvalidDataException(
                    "Файл не схожий на OAuth-клієнта Google (немає розділу «installed» з client_id і client_secret).");
        }
        catch (System.Text.Json.JsonException)
        {
            throw new InvalidDataException("Вибраний файл не є коректним JSON-файлом.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(_clientSecretsPath)!);
        await File.WriteAllBytesAsync(_clientSecretsPath, data);
    }

    // Signs in via the browser once; the token is cached in _tokenDirectory
    public async Task AuthorizeAsync()
    {
        if (_driveService is not null)
            return;

        if (!File.Exists(_clientSecretsPath))
            throw new FileNotFoundException(
                $"Не знайдено файл «{ClientSecretsFileName}» для входу в Google Drive. " +
                "Створіть OAuth-клієнта типу «Desktop app» у Google Cloud Console і виберіть його JSON-файл.",
                _clientSecretsPath);

        GoogleClientSecrets secrets = await GoogleClientSecrets.FromFileAsync(_clientSecretsPath);
        using var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3));
        UserCredential credential = await GoogleWebAuthorizationBroker.AuthorizeAsync(
            secrets.Secrets,
            new[] { DriveService.Scope.DriveFile },
            "user",
            timeout.Token,
            new FileDataStore(_tokenDirectory, fullPath: true));

        _driveService = new DriveService(new BaseClientService.Initializer
        {
            HttpClientInitializer = credential,
            ApplicationName = "SpreadsheetApp",
        });
    }

    // Creates the file or overwrites one with the same name
    public async Task SaveAsync(string fileName, byte[] data)
    {
        DriveService service = await GetServiceAsync();
        string folderId = await GetFolderIdAsync(service);
        string? existingId = await FindFileIdAsync(service, folderId, fileName);

        using var stream = new MemoryStream(data);
        IUploadProgress progress;
        if (existingId is null)
        {
            var metadata = new DriveFile { Name = fileName, Parents = new[] { folderId }, MimeType = FileMimeType };
            progress = await service.Files.Create(metadata, stream, FileMimeType).UploadAsync();
        }
        else
        {
            progress = await service.Files.Update(new DriveFile(), existingId, stream, FileMimeType).UploadAsync();
        }

        if (progress.Status != UploadStatus.Completed)
            throw new IOException("Не вдалося завантажити файл на Google Drive.", progress.Exception);
    }

    public async Task<byte[]> LoadAsync(string fileName)
    {
        DriveService service = await GetServiceAsync();
        string folderId = await GetFolderIdAsync(service);
        string fileId = await FindFileIdAsync(service, folderId, fileName)
            ?? throw new FileNotFoundException($"Файл «{fileName}» не знайдено на Google Drive.");

        using var stream = new MemoryStream();
        var progress = await service.Files.Get(fileId).DownloadAsync(stream);
        if (progress.Status != Google.Apis.Download.DownloadStatus.Completed)
            throw new IOException("Не вдалося отримати файл з Google Drive.", progress.Exception);
        return stream.ToArray();
    }

    public async Task<IReadOnlyList<string>> ListFilesAsync()
    {
        DriveService service = await GetServiceAsync();
        string folderId = await GetFolderIdAsync(service);

        var names = new List<string>();
        string? pageToken = null;
        do
        {
            var request = service.Files.List();
            request.Q = $"'{folderId}' in parents and trashed = false and mimeType != '{FolderMimeType}'";
            request.Fields = "nextPageToken, files(id, name)";
            request.Spaces = "drive";
            request.PageToken = pageToken;
            var result = await request.ExecuteAsync();
            names.AddRange(result.Files.Select(f => f.Name));
            pageToken = result.NextPageToken;
        } while (pageToken is not null);

        return names.Order(StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    private async Task<DriveService> GetServiceAsync()
    {
        await AuthorizeAsync();
        return _driveService!;
    }

    // Finds or creates the app folder on Drive
    private async Task<string> GetFolderIdAsync(DriveService service)
    {
        if (_folderId is not null)
            return _folderId;

        var request = service.Files.List();
        request.Q = $"name = '{Escape(FolderName)}' and mimeType = '{FolderMimeType}' and trashed = false";
        request.Fields = "files(id)";
        var result = await request.ExecuteAsync();

        if (result.Files.Count > 0)
            return _folderId = result.Files[0].Id;

        var folder = new DriveFile { Name = FolderName, MimeType = FolderMimeType };
        var create = service.Files.Create(folder);
        create.Fields = "id";
        return _folderId = (await create.ExecuteAsync()).Id;
    }

    private static async Task<string?> FindFileIdAsync(DriveService service, string folderId, string fileName)
    {
        var request = service.Files.List();
        request.Q = $"name = '{Escape(fileName)}' and '{folderId}' in parents and trashed = false";
        request.Fields = "files(id)";
        var result = await request.ExecuteAsync();
        return result.Files.FirstOrDefault()?.Id;
    }

    // Escapes quotes for Drive search queries
    private static string Escape(string value) => value.Replace("\\", "\\\\").Replace("'", "\\'");
}
