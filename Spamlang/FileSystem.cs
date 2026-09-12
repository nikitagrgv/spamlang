namespace Spamlang;

public class FileSystem : IFileSystem
{
    private readonly string _currentDirectory;

    public FileSystem(string currentDirectory)
    {
        _currentDirectory = currentDirectory;
    }

    public string ResolveToFullPath(string anyPath)
    {
        if (Path.IsPathFullyQualified(anyPath))
        {
            return anyPath;
        }

        return Path.GetFullPath(anyPath, _currentDirectory);
    }

    public string ReadAllText(string fullPath)
    {
        return File.ReadAllText(fullPath);
    }
}