using System.Diagnostics;
using System.IO;
using KilitBul.Services;
using Xunit;

namespace KilitBul.Tests;

public sealed class LockFinderTests : IDisposable
{
    private readonly string _dir = Path.Combine(Path.GetTempPath(), "KilitBulTests_" + Guid.NewGuid().ToString("N"));

    public LockFinderTests() => Directory.CreateDirectory(_dir);

    public void Dispose()
    {
        try { Directory.Delete(_dir, recursive: true); } catch { }
    }

    private string CreateFile(string relative)
    {
        var path = Path.Combine(_dir, relative);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, "test");
        return path;
    }

    [Fact]
    public void UnlockedFile_ReturnsEmpty()
    {
        var file = CreateFile("free.txt");
        Assert.Empty(LockFinder.Find(file));
    }

    [Fact]
    public void LockedFile_ReturnsCurrentProcess()
    {
        var file = CreateFile("locked.txt");
        using var stream = new FileStream(file, FileMode.Open, FileAccess.ReadWrite, FileShare.None);

        var result = LockFinder.Find(file);

        var self = Assert.Single(result, p => p.ProcessId == Environment.ProcessId);
        Assert.Contains(file, self.LockedPaths, StringComparer.OrdinalIgnoreCase);
        // KilitBul kendi işlemini asla sonlandırmamalı.
        Assert.True(self.IsSelf);
        Assert.True(self.IsProtected);
        Assert.Equal(KillResult.Protected, ProcessKiller.Kill(self));
    }

    [Theory]
    [InlineData(@"C:\", true)]
    [InlineData(@"\\sunucu\paylasim", true)]
    [InlineData(@"\\sunucu\paylasim\", true)]
    [InlineData(@"C:\Windows", false)]
    [InlineData(@"\\sunucu\paylasim\klasor", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsRootPath_DetectsDriveAndShareRoots(string? path, bool expected)
        => Assert.Equal(expected, LockFinder.IsRootPath(path));

    [Fact]
    public void Folder_FindsLockedFileInSubdirectoryAmongMany()
    {
        // Bölerek arama mantığını test etmek için RM parti boyutundan fazla dosya oluştur.
        for (int i = 0; i < RestartManager.BatchSize + 50; i++)
            CreateFile($"many/file{i}.txt");
        var locked = CreateFile(@"many\deep\nested\target.bin");

        using var stream = new FileStream(locked, FileMode.Open, FileAccess.Read, FileShare.None);

        var result = LockFinder.Find(_dir);

        var self = Assert.Single(result, p => p.ProcessId == Environment.ProcessId);
        Assert.Equal(new[] { locked }, self.LockedPaths, StringComparer.OrdinalIgnoreCase);
    }

    [Fact]
    public void LockedByChildProcess_IsFoundAndCanBeKilled()
    {
        var file = CreateFile("child.txt");
        // Dosyayı açık tutan ayrı bir işlem: cmd, dosyayı stdin olarak açıp beklemede kalır.
        var psi = new ProcessStartInfo("cmd.exe", $"/c ping -n 30 127.0.0.1 > nul < \"{file}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
        };
        using var child = Process.Start(psi)!;
        try
        {
            LockingProcess? found = null;
            for (int i = 0; i < 20 && found is null; i++)
            {
                found = LockFinder.Find(file).FirstOrDefault(p => p.ProcessId == child.Id);
                if (found is null) Thread.Sleep(100);
            }

            Assert.NotNull(found);
            Assert.Equal(KillResult.Success, ProcessKiller.Kill(found!, entireTree: true));
            Assert.True(child.HasExited);
            Assert.DoesNotContain(LockFinder.Find(file), p => p.ProcessId == child.Id);
        }
        finally
        {
            if (!child.HasExited) child.Kill(true);
        }
    }

    [Fact]
    public void MissingPath_Throws()
    {
        Assert.Throws<FileNotFoundException>(() => LockFinder.Find(Path.Combine(_dir, "yok.txt")));
    }
}
