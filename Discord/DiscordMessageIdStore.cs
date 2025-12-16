using System.Text;

namespace StatusImageCard.Discord;

public sealed class DiscordMessageIdStore
{
    private readonly string _path;

    public DiscordMessageIdStore(string path)
    {
        _path = path;
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);
    }

    public bool TryRead(out string messageId)
    {
        messageId = "";
        if (!File.Exists(_path)) return false;

        messageId = File.ReadAllText(_path, Encoding.UTF8).Trim();
        return !string.IsNullOrWhiteSpace(messageId);
    }

    public void Write(string messageId)
    {
        File.WriteAllText(_path, messageId.Trim(), Encoding.UTF8);
    }

    public void DeleteIfExists()
    {
        if (File.Exists(_path)) File.Delete(_path);
    }
}
