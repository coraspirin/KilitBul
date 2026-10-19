// Kilit Bul — Windows 11 modern sağ tık menüsü eklentisi.
//
// Explorer bu DLL'i sparse MSIX paketindeki (installer/msix/AppxManifest.xml) kayıt üzerinden yükler.
// Menüde asma kilit ikonlu "Kilit Bul" öğesini gösterir; tıklanınca aynı klasördeki KilitBul.exe'yi
// seçilen dosya/klasör yoluyla başlatır.

#include <windows.h>
#include <shellapi.h>
#include <shlwapi.h>
#include <shobjidl.h>
#include <new>
#include <string>

// {17C08CFC-902C-40EF-A34B-D4984CEFB91E} — AppxManifest.xml'deki CLSID ile aynı olmalı.
static const CLSID CLSID_KilitBulCommand =
    { 0x17c08cfc, 0x902c, 0x40ef, { 0xa3, 0x4b, 0xd4, 0x98, 0x4c, 0xef, 0xb9, 0x1e } };

static HMODULE g_module = nullptr;
static long g_objects = 0;
static long g_locks = 0;

static const wchar_t kTitle[] = L"Kilit Bul";
static const wchar_t kExeName[] = L"KilitBul.exe";

// DLL ile aynı klasördeki KilitBul.exe'nin tam yolu.
static std::wstring ExePath()
{
    wchar_t buf[MAX_PATH * 4];
    DWORD len = GetModuleFileNameW(g_module, buf, ARRAYSIZE(buf));
    if (len == 0 || len >= ARRAYSIZE(buf))
        return kExeName;
    std::wstring path(buf, len);
    size_t slash = path.find_last_of(L'\\');
    return path.substr(0, slash + 1) + kExeName;
}

// Komut satırı için tırnaklar; sondaki ters eğik çizgiler kapanış tırnağını kaçırmasın diye ikilenir.
static std::wstring QuoteArg(const std::wstring& arg)
{
    std::wstring s = L"\"" + arg;
    size_t trailing = 0;
    for (auto it = arg.rbegin(); it != arg.rend() && *it == L'\\'; ++it)
        ++trailing;
    s.append(trailing, L'\\');
    s += L"\"";
    return s;
}

class KilitBulCommand final : public IExplorerCommand
{
public:
    KilitBulCommand() { InterlockedIncrement(&g_objects); }
    ~KilitBulCommand() { InterlockedDecrement(&g_objects); }

    // IUnknown
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv)
            return E_POINTER;
        if (riid == IID_IUnknown || riid == __uuidof(IExplorerCommand))
        {
            *ppv = static_cast<IExplorerCommand*>(this);
            AddRef();
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&m_refs); }
    IFACEMETHODIMP_(ULONG) Release() override
    {
        long refs = InterlockedDecrement(&m_refs);
        if (refs == 0)
            delete this;
        return refs;
    }

    // IExplorerCommand
    IFACEMETHODIMP GetTitle(IShellItemArray*, LPWSTR* name) override { return SHStrDupW(kTitle, name); }

    IFACEMETHODIMP GetIcon(IShellItemArray*, LPWSTR* icon) override
    {
        std::wstring res = ExePath() + L",0";
        return SHStrDupW(res.c_str(), icon);
    }

    IFACEMETHODIMP GetToolTip(IShellItemArray*, LPWSTR* tip) override
    {
        *tip = nullptr;
        return E_NOTIMPL;
    }

    IFACEMETHODIMP GetCanonicalName(GUID* guid) override
    {
        *guid = CLSID_KilitBulCommand;
        return S_OK;
    }

    IFACEMETHODIMP GetState(IShellItemArray*, BOOL, EXPCMDSTATE* state) override
    {
        *state = ECS_ENABLED;
        return S_OK;
    }

    IFACEMETHODIMP Invoke(IShellItemArray* items, IBindCtx*) override
    {
        if (!items)
            return E_INVALIDARG;

        DWORD count = 0;
        if (FAILED(items->GetCount(&count)) || count == 0)
            return E_INVALIDARG;

        IShellItem* item = nullptr;
        HRESULT hr = items->GetItemAt(0, &item);
        if (FAILED(hr))
            return hr;

        LPWSTR path = nullptr;
        hr = item->GetDisplayName(SIGDN_FILESYSPATH, &path);
        item->Release();
        if (FAILED(hr))
            return hr;

        std::wstring exe = ExePath();
        std::wstring args = QuoteArg(path);
        CoTaskMemFree(path);

        SHELLEXECUTEINFOW sei = {};
        sei.cbSize = sizeof(sei);
        sei.fMask = SEE_MASK_NOASYNC | SEE_MASK_FLAG_NO_UI;
        sei.lpFile = exe.c_str();
        sei.lpParameters = args.c_str();
        sei.nShow = SW_SHOWNORMAL;
        return ShellExecuteExW(&sei) ? S_OK : HRESULT_FROM_WIN32(GetLastError());
    }

    IFACEMETHODIMP GetFlags(EXPCMDFLAGS* flags) override
    {
        *flags = ECF_DEFAULT;
        return S_OK;
    }

    IFACEMETHODIMP EnumSubCommands(IEnumExplorerCommand** commands) override
    {
        *commands = nullptr;
        return E_NOTIMPL;
    }

private:
    long m_refs = 1;
};

class ClassFactory final : public IClassFactory
{
public:
    IFACEMETHODIMP QueryInterface(REFIID riid, void** ppv) override
    {
        if (!ppv)
            return E_POINTER;
        if (riid == IID_IUnknown || riid == IID_IClassFactory)
        {
            *ppv = static_cast<IClassFactory*>(this);
            AddRef();
            return S_OK;
        }
        *ppv = nullptr;
        return E_NOINTERFACE;
    }
    IFACEMETHODIMP_(ULONG) AddRef() override { return InterlockedIncrement(&m_refs); }
    IFACEMETHODIMP_(ULONG) Release() override
    {
        long refs = InterlockedDecrement(&m_refs);
        if (refs == 0)
            delete this;
        return refs;
    }

    IFACEMETHODIMP CreateInstance(IUnknown* outer, REFIID riid, void** ppv) override
    {
        if (!ppv)
            return E_POINTER;
        *ppv = nullptr;
        if (outer)
            return CLASS_E_NOAGGREGATION;
        auto* cmd = new (std::nothrow) KilitBulCommand();
        if (!cmd)
            return E_OUTOFMEMORY;
        HRESULT hr = cmd->QueryInterface(riid, ppv);
        cmd->Release();
        return hr;
    }

    IFACEMETHODIMP LockServer(BOOL lock) override
    {
        if (lock)
            InterlockedIncrement(&g_locks);
        else
            InterlockedDecrement(&g_locks);
        return S_OK;
    }

private:
    long m_refs = 1;
};

extern "C" BOOL WINAPI DllMain(HINSTANCE instance, DWORD reason, LPVOID)
{
    if (reason == DLL_PROCESS_ATTACH)
    {
        g_module = instance;
        DisableThreadLibraryCalls(instance);
    }
    return TRUE;
}

extern "C" HRESULT __stdcall DllGetClassObject(REFCLSID clsid, REFIID riid, void** ppv)
{
    if (!ppv)
        return E_POINTER;
    *ppv = nullptr;
    if (clsid != CLSID_KilitBulCommand)
        return CLASS_E_CLASSNOTAVAILABLE;

    auto* factory = new (std::nothrow) ClassFactory();
    if (!factory)
        return E_OUTOFMEMORY;
    HRESULT hr = factory->QueryInterface(riid, ppv);
    factory->Release();
    return hr;
}

extern "C" HRESULT __stdcall DllCanUnloadNow()
{
    return (g_objects == 0 && g_locks == 0) ? S_OK : S_FALSE;
}
