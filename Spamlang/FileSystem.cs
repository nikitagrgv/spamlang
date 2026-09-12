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

    public FileStream OpenWrite(string path)
    {
        return new FileStream(path, FileMode.Create, FileAccess.Write);
    }

    public void CreateDir(string path)
    {
        Directory.CreateDirectory(path);
    }

    public void RemoveDir(string path)
    {
        Directory.Delete(path, recursive: true);
    }

    public string CreateTempDir(string name)
    {
        string dirPath = Path.Combine(Path.GetTempPath(), name);
        CreateDir(dirPath);
        return dirPath;
    }

    public void CopyFile(string from, string too)
    {
        File.Copy(from, too);
    }
}