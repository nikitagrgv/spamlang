namespace Spamlang;

public interface IFileSystem
{
    string ResolveToFullPath(string anyPath);
    string ReadAllText(string fullPath);

    FileStream OpenWrite(string path);

    void CreateDir(string path);
    void RemoveDir(string path);
    string CreateTempDir(string name);
}