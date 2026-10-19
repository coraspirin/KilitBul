using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace KilitBul.Services;

/// <summary>Exe dosyalarının ikonlarını (32 px) WPF görüntüsü olarak döndürür.</summary>
public static class IconCache
{
    private const uint SHGFI_ICON = 0x100;
    private const uint SHGFI_LARGEICON = 0x0;
    private const uint SHGFI_USEFILEATTRIBUTES = 0x10;
    private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    private static readonly ConcurrentDictionary<string, ImageSource?> Cache = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Exe'nin ikonu; yol yoksa genel uygulama ikonu.</summary>
    public static ImageSource? Get(string? exePath)
    {
        // Yol bilinmiyorsa (erişim yok) dosya özniteliklerinden genel .exe ikonu alınır.
        var key = string.IsNullOrEmpty(exePath) ? "*.exe" : exePath;
        return Cache.GetOrAdd(key, Load);
    }

    private static ImageSource? Load(string path)
    {
        var info = new SHFILEINFO();
        uint flags = SHGFI_ICON | SHGFI_LARGEICON;
        if (path == "*.exe")
        {
            path = "app.exe";
            flags |= SHGFI_USEFILEATTRIBUTES;
        }

        if (SHGetFileInfo(path, FILE_ATTRIBUTE_NORMAL, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags) == IntPtr.Zero
            || info.hIcon == IntPtr.Zero)
            return null;

        try
        {
            var source = Imaging.CreateBitmapSourceFromHIcon(info.hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        finally
        {
            DestroyIcon(info.hIcon);
        }
    }
}
