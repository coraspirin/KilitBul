using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Text;

namespace KilitBul.Services;

public enum LockingAppType
{
    Unknown = 0,
    MainWindow = 1,
    OtherWindow = 2,
    Service = 3,
    Explorer = 4,
    Console = 5,
    Critical = 1000,
}

public sealed record RmLockInfo(int ProcessId, DateTime StartTime, string AppName, string ServiceName, LockingAppType AppType);

/// <summary>
/// Windows Restart Manager API (rstrtmgr.dll) sarmalayıcısı.
/// Verilen dosyaları açık tutan işlemleri döndürür; yönetici yetkisi gerektirmez.
/// </summary>
internal static class RestartManager
{
    private const int RmRebootReasonNone = 0;
    private const int CchRmMaxAppName = 255;
    private const int CchRmMaxSvcName = 63;
    private const int CchRmSessionKey = 32;
    private const int ErrorMoreData = 234;

    // Tek bir RmRegisterResources çağrısında verilecek en fazla dosya sayısı.
    public const int BatchSize = 1000;

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public System.Runtime.InteropServices.ComTypes.FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxAppName + 1)]
        public string strAppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CchRmMaxSvcName + 1)]
        public string strServiceShortName;

        public LockingAppType ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, StringBuilder strSessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint pSessionHandle,
        uint nFiles, string[]? rgsFilenames,
        uint nApplications, RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices, string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
        ref uint lpdwRebootReasons);

    /// <summary>Verilen dosyaları kilitleyen işlemleri döndürür (tek oturum, en fazla <see cref="BatchSize"/> dosya önerilir).</summary>
    public static IReadOnlyList<RmLockInfo> GetLockingProcesses(IReadOnlyCollection<string> paths)
    {
        if (paths.Count == 0)
            return Array.Empty<RmLockInfo>();

        // strSessionKey bir çıktı tamponudur; RM oturum anahtarını buraya yazar.
        var key = new StringBuilder(CchRmSessionKey + 1);
        int rc = RmStartSession(out uint handle, 0, key);
        if (rc != 0)
            throw new Win32Exception(rc, "Restart Manager oturumu başlatılamadı.");

        try
        {
            var files = paths.ToArray();
            rc = RmRegisterResources(handle, (uint)files.Length, files, 0, null, 0, null);
            if (rc != 0)
                throw new Win32Exception(rc, "Dosyalar Restart Manager'a kaydedilemedi.");

            RM_PROCESS_INFO[]? infos = null;
            uint count = 0;
            uint reasons = RmRebootReasonNone;

            // Liste çağrılar arasında büyüyebilir; ERROR_MORE_DATA geldikçe tekrar dene.
            for (int attempt = 0; attempt < 5; attempt++)
            {
                rc = RmGetList(handle, out uint needed, ref count, infos, ref reasons);
                if (rc == 0)
                    break;
                if (rc != ErrorMoreData)
                    throw new Win32Exception(rc, "Kilitleyen işlemler listelenemedi.");
                count = needed;
                infos = new RM_PROCESS_INFO[needed];
            }
            if (rc != 0)
                throw new Win32Exception(rc, "Kilitleyen işlemler listelenemedi.");

            var result = new List<RmLockInfo>((int)count);
            for (int i = 0; i < count; i++)
            {
                var p = infos![i];
                long ft = ((long)p.Process.ProcessStartTime.dwHighDateTime << 32) | (uint)p.Process.ProcessStartTime.dwLowDateTime;
                var start = ft > 0 ? DateTime.FromFileTime(ft) : DateTime.MinValue;
                result.Add(new RmLockInfo(p.Process.dwProcessId, start, p.strAppName ?? "", p.strServiceShortName ?? "", p.ApplicationType));
            }
            return result;
        }
        finally
        {
            RmEndSession(handle);
        }
    }
}
