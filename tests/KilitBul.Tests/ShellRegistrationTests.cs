using KilitBul.Services;
using Xunit;

namespace KilitBul.Tests;

public class ShellRegistrationTests
{
    [Fact]
    public void RegisterAndUnregister_WritesAndRemovesCommands()
    {
        const string verb = "KilitBulTest";
        const string exe = @"C:\Test Klasörü\KilitBul.exe";
        try
        {
            ShellRegistration.Register(exe, verb);

            Assert.True(ShellRegistration.IsRegistered(exe, verb));
            foreach (var cls in new[] { "*", "Directory" })
                Assert.Equal($"\"{exe}\" \"%1\"", ShellRegistration.GetCommand(cls, verb));
            Assert.Null(ShellRegistration.GetCommand("Drive", verb));
        }
        finally
        {
            ShellRegistration.Unregister(verb);
        }

        Assert.False(ShellRegistration.IsRegistered(exe, verb));
        Assert.Null(ShellRegistration.GetCommand("Directory", verb));
    }

    [Fact]
    public void IsRegistered_FalseWhenOneClassIsMissing()
    {
        const string verb = "KilitBulTestPartial";
        const string exe = @"C:\Test\KilitBul.exe";
        try
        {
            ShellRegistration.Register(exe, verb);
            Microsoft.Win32.Registry.CurrentUser.DeleteSubKeyTree($@"Software\Classes\Directory\shell\{verb}");

            Assert.False(ShellRegistration.IsRegistered(exe, verb));
        }
        finally
        {
            ShellRegistration.Unregister(verb);
        }
    }

    [Fact]
    public void Unregister_RemovesLegacyDriveEntry()
    {
        const string verb = "KilitBulTestLegacy";
        using (var k = Microsoft.Win32.Registry.CurrentUser.CreateSubKey($@"Software\Classes\Drive\shell\{verb}\command"))
            k.SetValue("", "x");

        ShellRegistration.Unregister(verb);

        Assert.Null(ShellRegistration.GetCommand("Drive", verb));
    }

    [Theory]
    [InlineData("C:\"", @"C:\")]
    [InlineData("\"C:\\Users\\a b\\x.txt\"", @"C:\Users\a b\x.txt")]
    [InlineData(@"D:\folder", @"D:\folder")]
    public void NormalizeArgument_FixesExplorerQuirks(string input, string expected)
        => Assert.Equal(expected, App.NormalizeArgument(input));
}
