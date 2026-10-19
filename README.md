# KilitBul

"Bu dosya başka bir programda açık olduğundan işlem tamamlanamıyor" hatası aldığınızda,
dosyayı **hangi uygulamanın kullandığını** gösterir ve isterseniz o uygulamayı kapatır/sonlandırır.

- Dosya, klasör ve sürücülerde çalışır (klasörde alt klasörlerdeki tüm dosyalar taranır).
- Explorer sağ tık menüsüne **"Kim kullanıyor? (KilitBul)"** ekler.
- Yönetici yetkisi gerektirmez; gerektiğinde tek tıkla yönetici olarak yeniden açılır.

## Kurulum

1. `KilitBul.exe` dosyasını kalıcı bir yere koyun (ör. `C:\Tools\KilitBul\`).
   Tek dosyadır, .NET kurulumu gerekmez.
2. Sağ tık menüsüne eklemek için ya uygulamayı açıp sağ alttaki **"Sağ tık menüsüne ekle"** butonuna basın,
   ya da komut satırında:
   ```
   KilitBul.exe --register
   ```
   Kaldırmak için: `KilitBul.exe --unregister` (exe'yi taşırsanız tekrar `--register` çalıştırın).

> Windows 11'de klasik menü öğeleri **"Daha fazla seçenek göster"** (veya Shift + sağ tık) altında görünür.

## Kullanım

1. Silinemeyen dosya/klasöre sağ tıklayın → **Kim kullanıyor? (KilitBul)**.
   (Ya da uygulamayı açıp dosyayı pencereye sürükleyin / "Dosya seç…", "Klasör seç…" kullanın.)
2. Listede kilidi tutan uygulamalar görünür: ad, PID, tür, kilitli dosya, exe yolu.
3. Bir veya birden fazla satır seçin (seçim yoksa tümüne uygulanır):
   - **Kapat** — uygulamaya normal kapanma isteği gönderir; kaydedilmemiş belge varsa uygulama sorar.
   - **Zorla sonlandır (Del)** — onay sonrası işlemi hemen öldürür.
4. Liste boşaldığında **🗑 Şimdi sil** ile öğeyi Geri Dönüşüm Kutusu'na taşıyabilirsiniz.

Sistem için kritik işlemler (System, csrss, lsass, winlogon vb.) gri gösterilir ve sonlandırılamaz.
`explorer.exe` sonlandırılmadan önce ayrıca uyarılırsınız.

## Nasıl çalışır?

Windows'un resmi **Restart Manager API**'si (`rstrtmgr.dll`, Windows Installer'ın "dosyalar kullanımda"
penceresinde kullandığı mekanizma) ile dosyaları açık tutan işlemler sorgulanır. Klasörlerde dosyalar
1000'erli gruplar halinde sorgulanır; sonuç dönen gruplar ikiye bölünerek hangi dosyanın hangi işlem
tarafından kilitlendiği bulunur. Ayrıca klasör altından exe/dll yüklemiş işlemler de listelenir.

## Bilinen kısıtlar

- Bir klasörü yalnızca **çalışma dizini** olarak tutan işlemler (ör. o klasörde açık bir `cmd`/PowerShell)
  Restart Manager tarafından raporlanmaz. Bu yüzden klasör taramasında liste boş çıksa bile silme başarısız olabilir;
  bu durumda KilitBul taramayı tekrarlar.
- Erişim izni olmayan alt klasörlerdeki dosyalar taramada atlanır.
- Sürücü kökleri taranabilir ama **Şimdi sil** ile silinemez.
- KilitBul yönetici olarak açıldığında, Windows güvenlik kısıtı (UIPI) nedeniyle normal Explorer penceresinden
  sürükle-bırak çalışmaz; **Dosya seç…/Klasör seç…** kullanın.
- KilitBul'un kendi işlemi listede görünebilir (ör. exe'sinin bulunduğu klasör tarandığında) ama sonlandırılamaz.
- Normal kullanıcı olarak başka kullanıcıların/servislerin işlemleri listelenebilir ama sonlandırılamayabilir;
  bu durumda **🛡 Yönetici olarak aç** kullanın.
- Çekirdek sürücülerinin tuttuğu kilitler (ör. antivirüs filtreleri) görünmez.

## Geliştirme

Gereksinim: .NET 8 SDK.

```
dotnet build
dotnet test                                                  # yalnızca Windows'ta
dotnet publish src/KilitBul -c Release -r win-x64 -o publish  # tek exe
```

Proje yapısı:

| Dosya | Görev |
|---|---|
| `src/KilitBul/Services/RestartManager.cs` | Restart Manager P/Invoke sarmalayıcısı |
| `src/KilitBul/Services/LockFinder.cs` | Dosya/klasör tarama, kilit sahibini bulma |
| `src/KilitBul/Services/ProcessKiller.cs` | Kapatma / zorla sonlandırma, korumalı işlemler |
| `src/KilitBul/Services/ShellRegistration.cs` | Sağ tık menüsü kaydı (HKCU) |
| `src/KilitBul/Services/ElevationHelper.cs` | Yönetici olarak yeniden başlatma |
| `src/KilitBul/ViewModels/MainViewModel.cs` | Arayüz mantığı (CommunityToolkit.Mvvm) |

GitHub Actions (`.github/workflows/build.yml`) her push'ta Windows üzerinde testleri çalıştırır ve
`KilitBul.exe`'yi artefakt olarak yükler.
