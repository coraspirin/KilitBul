# KilitBul

"Bu dosya başka bir programda açık olduğundan işlem tamamlanamıyor" hatası aldığınızda,
dosyayı **hangi uygulamanın kullandığını** gösterir ve isterseniz o uygulamayı durdurur.

- **Windows 11** içindir; Dosya Gezgini'nin modern sağ tık menüsüne asma kilit ikonlu **"Kilit Bul"** öğesi ekler.
- Dosya ve klasörlerde çalışır (klasörde alt klasörlerdeki tüm dosyalar taranır).
- Küçük, sade bir pencere: kilitleyen uygulamaların listesi ve tek bir **"Seçilen uygulamayı durdur"** butonu.

## Kurulum

1. `KilitBul-Setup-<sürüm>.exe` dosyasını çalıştırın ve yönetici onayı verin.
   Program `C:\Program Files\Kilit Bul\` altına kurulur; .NET kurulumu gerekmez.
2. Kurulum Başlat menüsüne veya masaüstüne kısayol koymaz. Uygulama yalnızca sağ tık menüsünden açılır:
   dosya veya klasöre sağ tık → 🔒 **Kilit Bul**. (Hemen görünmezse Dosya Gezgini'ni kapatıp açın.)
3. Kaldırmak için: **Ayarlar → Uygulamalar** → **Kilit Bul** → Kaldır.

> Kurulum dosyası dijital olarak imzalı olmadığından Windows SmartScreen uyarı verebilir:
> **Daha fazla bilgi → Yine de çalıştır**.

### Kurulum ne yapar?

Windows 11'in modern menüsüne yalnızca imzalı bir uygulama paketi öğe ekleyebilir. Bu yüzden kurulum:

- `KilitBul.exe`, menü eklentisi `KilitBulShell.dll` ve küçük bir **sparse MSIX paketini** Program Files'a kopyalar,
- paketi imzalayan, her sürümde yeniden üretilen **kendinden imzalı "CN=KilitBul" sertifikasını** bu bilgisayarın
  *Güvenilir Kişiler* deposuna ekler (kaldırırken siler; sertifikanın özel anahtarı hiçbir yerde saklanmaz),
- paketi kurulumu yapan Windows kullanıcısı için kaydeder.

## Kullanım

1. Silinemeyen dosya/klasöre sağ tıklayın → **Kilit Bul**.
2. Açılan pencerede o öğeyi kullanan uygulamalar ikonlarıyla listelenir.
3. Bir uygulamayı seçip **Seçilen uygulamayı durdur**'a basın (veya `Delete`), onaylayın → uygulama kapatılır
   ve liste yenilenir. Liste boşalınca dosyayı silebilir veya taşıyabilirsiniz.

İpuçları: `F5` listeyi yeniler, `Esc` onayı veya pencereyi kapatır. Başka bir kullanıcının ya da yönetici olarak
çalışan bir uygulamayı durdurmak gerekiyorsa Kilit Bul yalnızca o işlem için yönetici izni (UAC) ister.
Sistem için kritik işlemler (System, csrss, lsass, winlogon vb.) soluk gösterilir ve seçilemez.

## Nasıl çalışır?

Windows'un resmi **Restart Manager API**'si (`rstrtmgr.dll`, Windows Installer'ın "dosyalar kullanımda"
penceresinde kullandığı mekanizma) ile dosyaları açık tutan işlemler sorgulanır. Klasörlerde dosyalar
1000'erli gruplar halinde sorgulanır; sonuç dönen gruplar ikiye bölünerek hangi dosyanın hangi işlem
tarafından kilitlendiği bulunur. Ayrıca klasör altından exe/dll yüklemiş işlemler de listelenir.

## Bilinen kısıtlar

- Yalnızca Windows 11 (64 bit) desteklenir.
- Menü öğesi kurulumu yapan Windows kullanıcısı için kaydedilir; aynı bilgisayardaki başka kullanıcı hesaplarında görünmez.
- Bir klasörü yalnızca **çalışma dizini** olarak tutan işlemler (ör. o klasörde açık bir `cmd`/PowerShell)
  Restart Manager tarafından raporlanmaz; klasör taramasında liste boş çıksa bile silme başarısız olabilir.
- Erişim izni olmayan alt klasörlerdeki dosyalar taramada atlanır.
- Çekirdek sürücülerinin tuttuğu kilitler (ör. antivirüs filtreleri) görünmez.

## Geliştirme

Gereksinim: .NET 8 SDK.

```
dotnet build
dotnet test                                                  # yalnızca Windows'ta
dotnet publish src/KilitBul -c Release -r win-x64 -o dist     # tek exe
```

Kurulum dosyası Windows SDK (makeappx, signtool) ve MSVC gerektirdiği için GitHub Actions'ta üretilir
(`.github/workflows/build.yml`): menü eklentisi DLL'i derlenir, sertifika üretilip sparse paket imzalanır,
paket runner'da deneme amaçlı kaydedilip kaldırılır, ardından NSIS ile `KilitBul-Setup-<sürüm>.exe` oluşturulur.

İkon `installer/make-icon.py` ile üretilir (`python3 installer/make-icon.py`; exe ikonu, pencere görseli ve MSIX logoları).

Proje yapısı:

| Dosya | Görev |
|---|---|
| `src/KilitBul/Services/RestartManager.cs` | Restart Manager P/Invoke sarmalayıcısı |
| `src/KilitBul/Services/LockFinder.cs` | Dosya/klasör tarama, kilit sahibini bulma |
| `src/KilitBul/Services/ProcessKiller.cs` | Kapatma / zorla sonlandırma, korumalı işlemler |
| `src/KilitBul/Services/ShellRegistration.cs` | Klasik sağ tık menüsü kaydı (`--register`, geliştirme için) |
| `src/KilitBul/Services/ElevationHelper.cs` | Yalnızca seçilen işlemi yönetici olarak sonlandırma (`--kill`) |
| `src/KilitBul/ViewModels/MainViewModel.cs` | Arayüz mantığı (CommunityToolkit.Mvvm) |
| `shell/KilitBulShell.cpp` | Windows 11 modern menü eklentisi (IExplorerCommand, C++) |
| `installer/msix/AppxManifest.xml` | Menü eklentisini tanıtan sparse paket manifesti |
| `installer/KilitBul.nsi` | NSIS kurulum betiği |

GitHub Actions (`.github/workflows/build.yml`) her push'ta Windows üzerinde testleri çalıştırır,
`KilitBul-Setup-*.exe` dosyasını artefakt olarak yükler.
