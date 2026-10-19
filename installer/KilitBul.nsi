; Kilit Bul kurulum betiği (NSIS 3) — Windows 11
;
; Gerekli dosyalar dist/ altında (CI üretir, bkz. .github/workflows/build.yml):
;   KilitBul.exe        WPF uygulaması (tek dosya)
;   KilitBulShell.dll   modern sağ tık menüsü eklentisi (shell/)
;   KilitBul.msix       imzalı sparse paket (installer/msix/AppxManifest.xml)
;   KilitBul.cer        paketi imzalayan sertifikanın açık anahtarı
;
; Derleme: makensis -DVERSION=1.2.0 -DCERT_THUMBPRINT=<parmak izi> installer/KilitBul.nsi
;
; Kurulum Program Files'a kopyalar, sertifikayı bu bilgisayarın "Güvenilir Kişiler" deposuna ekler ve
; paketi kurulumu yapan kullanıcı için kaydeder. Kısayol oluşturmaz; uygulama yalnızca sağ tık menüsünden açılır.

Unicode true
ManifestDPIAware true

!ifndef VERSION
  !define VERSION "1.2.0"
!endif
!ifndef CERT_THUMBPRINT
  !error "CERT_THUMBPRINT tanımlı değil (makensis -DCERT_THUMBPRINT=...)"
!endif
!ifndef DIST
  !define DIST "..\dist"
!endif

!define APP_NAME     "Kilit Bul"
!define APP_EXE      "KilitBul.exe"
!define SHELL_DLL    "KilitBulShell.dll"
!define PACKAGE_NAME "coraspirin.KilitBul"
!define PUBLISHER    "coraspirin"
!define UNINST_KEY   "Software\Microsoft\Windows\CurrentVersion\Uninstall\KilitBul"
!define ICON_FILE    "..\src\KilitBul\Assets\KilitBul.ico"
!define POWERSHELL   '"$SYSDIR\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -NonInteractive -ExecutionPolicy Bypass -Command'

!include "MUI2.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"
!include "LogicLib.nsh"

Name "${APP_NAME}"
OutFile "${DIST}\KilitBul-Setup-${VERSION}.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "${UNINST_KEY}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${APP_NAME} ${VERSION}"
ShowInstDetails show

VIProductVersion "${VERSION}.0"
VIAddVersionKey /LANG=1055 "ProductName" "${APP_NAME}"
VIAddVersionKey /LANG=1055 "FileDescription" "${APP_NAME} Kurulumu"
VIAddVersionKey /LANG=1055 "FileVersion" "${VERSION}"
VIAddVersionKey /LANG=1055 "ProductVersion" "${VERSION}"
VIAddVersionKey /LANG=1055 "CompanyName" "${PUBLISHER}"
VIAddVersionKey /LANG=1055 "LegalCopyright" "${PUBLISHER}"

!define MUI_ICON   "${ICON_FILE}"
!define MUI_UNICON "${ICON_FILE}"
!define MUI_ABORTWARNING

!define MUI_WELCOMEPAGE_TEXT "Bu sihirbaz ${APP_NAME} ${VERSION} sürümünü bilgisayarınıza kuracak.$\r$\n$\r$\nKilit Bul, silinemeyen veya taşınamayan bir dosya ya da klasörü hangi uygulamanın kullandığını gösterir ve isterseniz o uygulamayı durdurur.$\r$\n$\r$\nDevam etmek için İleri'ye tıklayın."
!define MUI_FINISHPAGE_TITLE "Kurulum tamamlandı"
!define MUI_FINISHPAGE_TEXT "${APP_NAME} kuruldu.$\r$\n$\r$\nKullanmak için bir dosyaya veya klasöre sağ tıklayıp $\"${APP_NAME}$\" seçeneğini seçin.$\r$\n$\r$\nMenüde hemen görünmezse Dosya Gezgini'ni kapatıp yeniden açın."

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Turkish"

; 1.1 sürümünün klasik menü kayıtları (HKLM) ve uygulama içi/--register kayıtları (HKCU).
!macro RemoveClassicVerbs
  DeleteRegKey HKLM "Software\Classes\*\shell\KilitBul"
  DeleteRegKey HKLM "Software\Classes\Directory\shell\KilitBul"
  DeleteRegKey HKCU "Software\Classes\*\shell\KilitBul"
  DeleteRegKey HKCU "Software\Classes\Directory\shell\KilitBul"
  DeleteRegKey HKCU "Software\Classes\Drive\shell\KilitBul"
!macroend

; Bu kullanıcı için kayıtlı Kilit Bul paketini kaldırır (yoksa sessizce geçer).
!macro RemovePackage
  nsExec::ExecToLog '${POWERSHELL} "Get-AppxPackage -Name ${PACKAGE_NAME} | Remove-AppxPackage"'
  Pop $0
!macroend

; Yüklü (Explorer'ın kullandığı) bir DLL silinemez ama yeniden adlandırılabilir; eskisi yeniden başlatmada silinir.
!macro ReleaseFile FILE
  Delete "${FILE}.old"
  ${If} ${FileExists} "${FILE}"
    Delete "${FILE}"
    ${If} ${FileExists} "${FILE}"
      Rename "${FILE}" "${FILE}.old"
      Delete /REBOOTOK "${FILE}.old"
    ${EndIf}
  ${EndIf}
!macroend

!macro NotifyShell
  ; SHCNE_ASSOCCHANGED: Explorer'ın menü önbelleğini yenilemesi için
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
!macroend

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "${APP_NAME} yalnızca 64 bit Windows 11'de çalışır."
    Abort
  ${EndIf}
  SetRegView 64
  ReadRegStr $0 HKLM "SOFTWARE\Microsoft\Windows NT\CurrentVersion" "CurrentBuildNumber"
  ${If} $0 < 22000
    MessageBox MB_ICONSTOP "${APP_NAME} yalnızca Windows 11'de çalışır.$\r$\n(Bu bilgisayarın Windows sürüm numarası: $0)"
    Abort
  ${EndIf}
FunctionEnd

Function un.onInit
  SetRegView 64
FunctionEnd

Section "Kilit Bul" SecMain
  SectionIn RO
  ${DisableX64FSRedirection} ; 64 bit PowerShell / certutil

  DetailPrint "Önceki sürüm kapatılıyor…"
  nsExec::Exec '"$SYSDIR\taskkill.exe" /F /IM ${APP_EXE}'
  Pop $0
  !insertmacro RemovePackage
  !insertmacro RemoveClassicVerbs

  ; Önceki sürümün sertifikası (her derleme yeni sertifika üretir)
  ReadRegStr $1 HKLM "${UNINST_KEY}" "CertThumbprint"
  ${If} $1 != ""
  ${AndIf} $1 != "${CERT_THUMBPRINT}"
    nsExec::ExecToLog '"$SYSDIR\certutil.exe" -delstore TrustedPeople $1'
    Pop $0
  ${EndIf}

  SetOutPath "$INSTDIR"
  !insertmacro ReleaseFile "$INSTDIR\${SHELL_DLL}"
  File "${DIST}\${APP_EXE}"
  File "${DIST}\${SHELL_DLL}"
  File "${DIST}\KilitBul.msix"
  File "${DIST}\KilitBul.cer"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  DetailPrint "Sertifika güvenilir kişilere ekleniyor…"
  nsExec::ExecToLog '"$SYSDIR\certutil.exe" -f -addstore TrustedPeople "$INSTDIR\KilitBul.cer"'
  Pop $0
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "Sertifika eklenemedi (certutil hata kodu $0).$\r$\nKurulum geri alınacak."
    Goto rollback
  ${EndIf}

  DetailPrint "Sağ tık menüsü kaydediliyor…"
  nsExec::ExecToLog `${POWERSHELL} "try { Add-AppxPackage -Path '$INSTDIR\KilitBul.msix' -ExternalLocation '$INSTDIR' -ErrorAction Stop; exit 0 } catch { Write-Output $$_.Exception.Message; exit 1 }"`
  Pop $0
  ${If} $0 != 0
    MessageBox MB_ICONSTOP "Sağ tık menüsü kaydedilemedi (ayrıntılar kurulum günlüğünde).$\r$\nKurulum geri alınacak."
    Goto rollback
  ${EndIf}

  ; Programlar ve Özellikler / Uygulamalar listesi
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayName"     "${APP_NAME}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayVersion"  "${VERSION}"
  WriteRegStr   HKLM "${UNINST_KEY}" "Publisher"       "${PUBLISHER}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayIcon"     '"$INSTDIR\${APP_EXE}",0'
  WriteRegStr   HKLM "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr   HKLM "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegStr   HKLM "${UNINST_KEY}" "URLInfoAbout"    "https://github.com/coraspirin/KilitBul"
  WriteRegStr   HKLM "${UNINST_KEY}" "CertThumbprint"  "${CERT_THUMBPRINT}"
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINST_KEY}" "EstimatedSize" "$0"

  !insertmacro NotifyShell
  ${EnableX64FSRedirection}
  Return

rollback:
  !insertmacro RemovePackage
  nsExec::ExecToLog '"$SYSDIR\certutil.exe" -delstore TrustedPeople ${CERT_THUMBPRINT}'
  Pop $0
  Delete "$INSTDIR\${APP_EXE}"
  Delete /REBOOTOK "$INSTDIR\${SHELL_DLL}"
  Delete "$INSTDIR\KilitBul.msix"
  Delete "$INSTDIR\KilitBul.cer"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"
  ${EnableX64FSRedirection}
  Abort "Kurulum tamamlanamadı."
SectionEnd

Section "Uninstall"
  ${DisableX64FSRedirection}

  nsExec::Exec '"$SYSDIR\taskkill.exe" /F /IM ${APP_EXE}'
  Pop $0
  !insertmacro RemovePackage
  !insertmacro RemoveClassicVerbs

  ReadRegStr $1 HKLM "${UNINST_KEY}" "CertThumbprint"
  ${If} $1 != ""
    nsExec::ExecToLog '"$SYSDIR\certutil.exe" -delstore TrustedPeople $1'
    Pop $0
  ${EndIf}
  DeleteRegKey HKLM "${UNINST_KEY}"

  Delete "$INSTDIR\${APP_EXE}"
  !insertmacro ReleaseFile "$INSTDIR\${SHELL_DLL}"
  Delete "$INSTDIR\KilitBul.msix"
  Delete "$INSTDIR\KilitBul.cer"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir /REBOOTOK "$INSTDIR"

  !insertmacro NotifyShell
  ${EnableX64FSRedirection}
SectionEnd
