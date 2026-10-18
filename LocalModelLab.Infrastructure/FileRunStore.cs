using System.Text.Json;
using LocalModelLab.Core;

namespace LocalModelLab.Infrastructure;

public sealed class FileRunStore : IRunStore
{
    public string Location { get; }

    public FileRunStore(string outputRoot)
    {
        Location = Path.GetFullPath(Path.Combine(outputRoot, $"{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}"));
        Directory.CreateDirectory(Location);
    }

    public void WriteJson<T>(string relativePath, T value) => WriteBytes(relativePath, JsonSerializer.SerializeToUtf8Bytes(value, LabJson.Options));

    public void WriteBytes(string relativePath, byte[] bytes) => AtomicWrite(Resolve(relativePath), bytes);

    public Stream OpenRaw(string relativePath)
    {
        var path = Resolve(relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        return new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read, 4096, FileOptions.WriteThrough);
    }

    public static void AtomicWrite(string path, byte[] bytes)
    {
        path = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.WriteThrough))
            {
                stream.Write(bytes);
                stream.Flush(flushToDisk: true);
            }
            File.Move(temporary, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    private string Resolve(string relativePath)
    {
        var path = Path.GetFullPath(Path.Combine(Location, relativePath));
        if (!path.StartsWith(Location + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException("Artifact path must stay inside the run directory.");
        return path;
    }
}
