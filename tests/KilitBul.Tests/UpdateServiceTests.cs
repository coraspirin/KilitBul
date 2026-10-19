using KilitBul.Services;
using Xunit;

namespace KilitBul.Tests;

public class UpdateServiceTests
{
    private static string Release(string tag, string assetName = "KilitBul-Setup-1.4.0.exe", bool prerelease = false,
        string? digest = "sha256:ABCDEF") => $$"""
        {
          "tag_name": "{{tag}}",
          "draft": false,
          "prerelease": {{(prerelease ? "true" : "false")}},
          "html_url": "https://github.com/coraspirin/KilitBul/releases/tag/{{tag}}",
          "assets": [
            { "name": "notes.txt", "browser_download_url": "https://example/notes.txt" },
            { "name": "{{assetName}}", "browser_download_url": "https://example/{{assetName}}"
              {{(digest is null ? "" : $", \"digest\": \"{digest}\"")}} }
          ]
        }
        """;

    private static readonly Version Current = new(1, 3, 0);

    [Fact]
    public void NewerRelease_ReturnsSetupAsset()
    {
        var info = UpdateService.ParseRelease(Release("v1.4.0"), Current);

        Assert.NotNull(info);
        Assert.Equal(new Version(1, 4, 0), info!.Version);
        Assert.Equal("KilitBul-Setup-1.4.0.exe", info.FileName);
        Assert.Equal("https://example/KilitBul-Setup-1.4.0.exe", info.DownloadUrl);
        Assert.Equal("ABCDEF", info.Sha256);
    }

    [Theory]
    [InlineData("v1.3.0")]
    [InlineData("v1.2.9")]
    [InlineData("1.3")]
    [InlineData("sürüm-yok")]
    public void SameOlderOrInvalidVersion_ReturnsNull(string tag)
        => Assert.Null(UpdateService.ParseRelease(Release(tag), Current));

    [Fact]
    public void Prerelease_IsIgnored()
        => Assert.Null(UpdateService.ParseRelease(Release("v2.0.0", prerelease: true), Current));

    [Fact]
    public void MissingSetupAsset_ReturnsNull()
        => Assert.Null(UpdateService.ParseRelease(Release("v2.0.0", assetName: "KilitBul.zip"), Current));

    [Fact]
    public void MissingDigest_StillReturnsUpdate()
        => Assert.Null(UpdateService.ParseRelease(Release("v2.0.0", digest: null), Current)!.Sha256);
}
