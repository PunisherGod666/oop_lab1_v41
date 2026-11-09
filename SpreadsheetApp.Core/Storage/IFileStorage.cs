namespace SpreadsheetApp.Storage;

/// <summary>Place where spreadsheet files are saved and loaded.</summary>
public interface IFileStorage
{
    string DisplayName { get; }

    // true: file names are full paths picked in the system file dialog;
    // false: plain names inside the storage, listed by ListFilesAsync
    bool UsesSystemFileDialog { get; }

    Task SaveAsync(string fileName, byte[] data);
    Task<byte[]> LoadAsync(string fileName);
    Task<IReadOnlyList<string>> ListFilesAsync();
}

/// <summary>Storage that needs the user to sign in before use (cloud services).</summary>
public interface IConnectableStorage : IFileStorage
{
    bool IsConnected { get; }

    // Returns false if the user cancelled
    Task<bool> ConnectAsync(ICredentialsProvider credentials);
}

/// <summary>Asks the user for a credentials file (e.g. an OAuth client JSON); null if cancelled.</summary>
public interface ICredentialsProvider
{
    Task<string?> RequestCredentialsFileAsync(string storageName);
}
