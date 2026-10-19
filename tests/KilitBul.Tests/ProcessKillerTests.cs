using KilitBul.Services;
using Xunit;

namespace KilitBul.Tests;

public class ProcessKillerTests
{
    private static LockingProcess Make(int pid, string name, LockingAppType type = LockingAppType.Unknown)
        => new() { ProcessId = pid, Name = name, AppType = type };

    [Theory]
    [InlineData(4, "System")]
    [InlineData(1234, "csrss.exe")]
    [InlineData(1234, "lsass.exe")]
    [InlineData(1234, "winlogon.exe")]
    public void SystemProcesses_AreProtected(int pid, string name)
    {
        Assert.True(Make(pid, name).IsProtected);
        Assert.Equal(KillResult.Protected, ProcessKiller.Kill(Make(pid, name)));
    }

    [Fact]
    public void CriticalAppType_IsProtected()
        => Assert.True(Make(1234, "foo.exe", LockingAppType.Critical).IsProtected);

    [Fact]
    public void CurrentProcess_IsProtectedEvenIfCritical()
    {
        Assert.True(Make(Environment.ProcessId, "KilitBul.exe").IsProtected);
        Assert.True(Make(Environment.ProcessId, "KilitBul.exe", LockingAppType.Critical).IsProtected);
    }

    [Fact]
    public void NormalProcess_IsNotProtected()
        => Assert.False(Make(1234, "WINWORD.EXE", LockingAppType.MainWindow).IsProtected);

    [Fact]
    public void Explorer_IsDetected()
        => Assert.True(ProcessKiller.IsExplorer(Make(1234, "explorer.exe")));

    [Fact]
    public void ExitedProcess_ReturnsAlreadyExited()
    {
        // Var olmayan bir PID (Windows PID'leri 4'ün katıdır).
        Assert.Equal(KillResult.AlreadyExited, ProcessKiller.Kill(Make(int.MaxValue - 3, "gone.exe")));
    }

    [Fact]
    public void ExecutablePath_OfCurrentProcess_IsResolved()
    {
        var path = ProcessKiller.TryGetExecutablePath(Environment.ProcessId);
        Assert.False(string.IsNullOrEmpty(path));
        Assert.True(System.IO.File.Exists(path));
    }

    [Theory]
    [InlineData()]
    [InlineData("--kill")]
    [InlineData("--kill", "abc", "0")]
    [InlineData("--kill", "1234", "xyz")]
    public void KillArguments_Invalid_ReturnsFailed(params string[] args)
        => Assert.Equal(KillResult.Failed, App.KillFromArguments(args));

    [Fact]
    public void KillArguments_ExitedProcess_ReturnsAlreadyExited()
        => Assert.Equal(KillResult.AlreadyExited,
            App.KillFromArguments(new[] { "--kill", (int.MaxValue - 3).ToString(), "0" }));
}
