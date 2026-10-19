; Kilit Bul kurulum betiği (NSIS 3)
;
; Derleme (depo kökünden):
;   dotnet publish src/KilitBul/KilitBul.csproj -c Release -r win-x64 -o dist
;   makensis -DVERSION=1.1.0 installer/KilitBul.nsi
;
; Kurulum tüm kullanıcılar içindir: Program Files'a kopyalar ve sağ tık menüsünü HKLM'ye yazar.
; Başlat menüsü veya masaüstü kısayolu oluşturmaz; uygulama yalnızca sağ tık menüsünden açılır.

Unicode true
ManifestDPIAware true

!ifndef VERSION
  !define VERSION "1.1.0"
!endif
!ifndef SRC_EXE
  !define SRC_EXE "..\dist\KilitBul.exe"
!endif

!define APP_NAME     "Kilit Bul"
!define APP_EXE      "KilitBul.exe"
!define VERB_KEY     "KilitBul"
!define PUBLISHER    "coraspirin"
!define UNINST_KEY   "Software\Microsoft\Windows\CurrentVersion\Uninstall\KilitBul"
!define ICON_FILE    "..\src\KilitBul\Assets\KilitBul.ico"

!include "MUI2.nsh"
!include "x64.nsh"
!include "FileFunc.nsh"

Name "${APP_NAME}"
OutFile "..\dist\KilitBul-Setup-${VERSION}.exe"
InstallDir "$PROGRAMFILES64\${APP_NAME}"
InstallDirRegKey HKLM "${UNINST_KEY}" "InstallLocation"
RequestExecutionLevel admin
SetCompressor /SOLID lzma
BrandingText "${APP_NAME} ${VERSION}"

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

!define MUI_WELCOMEPAGE_TEXT "Bu sihirbaz ${APP_NAME} ${VERSION} sürümünü bilgisayarınıza kuracak.$\r$\n$\r$\nKilit Bul, silinemeyen veya taşınamayan bir dosya ya da klasörü hangi programın kullandığını gösterir ve isterseniz o programı kapatır.$\r$\n$\r$\nDevam etmek için İleri'ye tıklayın."
!define MUI_FINISHPAGE_TITLE "Kurulum tamamlandı"
!define MUI_FINISHPAGE_TEXT "${APP_NAME} kuruldu.$\r$\n$\r$\nKullanmak için bir dosyaya veya klasöre sağ tıklayıp $\"${APP_NAME}$\" seçeneğini seçin.$\r$\n$\r$\nWindows 11'de bu seçenek $\"Daha fazla seçenek göster$\" altında (veya Shift + sağ tık ile) görünür."

!insertmacro MUI_PAGE_WELCOME
!insertmacro MUI_PAGE_DIRECTORY
!insertmacro MUI_PAGE_INSTFILES
!insertmacro MUI_PAGE_FINISH

!insertmacro MUI_UNPAGE_CONFIRM
!insertmacro MUI_UNPAGE_INSTFILES

!insertmacro MUI_LANGUAGE "Turkish"

; Sağ tık menüsü kaydı: CLS = hedef sınıf ("*" veya "Directory")
!macro WriteVerb CLS
  WriteRegStr HKLM "Software\Classes\${CLS}\shell\${VERB_KEY}" "" "${APP_NAME}"
  WriteRegStr HKLM "Software\Classes\${CLS}\shell\${VERB_KEY}" "Icon" '"$INSTDIR\${APP_EXE}",0'
  WriteRegStr HKLM "Software\Classes\${CLS}\shell\${VERB_KEY}\command" "" '"$INSTDIR\${APP_EXE}" "%1"'
!macroend

; Önceki sürümlerin (ve uygulama içi "sağ tık menüsüne ekle" butonunun) HKCU kayıtları HKLM'yi gölgeler; temizle.
!macro RemoveUserVerbs
  DeleteRegKey HKCU "Software\Classes\*\shell\${VERB_KEY}"
  DeleteRegKey HKCU "Software\Classes\Directory\shell\${VERB_KEY}"
  DeleteRegKey HKCU "Software\Classes\Drive\shell\${VERB_KEY}"
!macroend

!macro NotifyShell
  ; SHCNE_ASSOCCHANGED: Explorer'ın menü önbelleğini yenilemesi için
  System::Call 'shell32::SHChangeNotify(i 0x08000000, i 0, p 0, p 0)'
!macroend

Function .onInit
  ${IfNot} ${RunningX64}
    MessageBox MB_ICONSTOP "${APP_NAME} yalnızca 64 bit Windows'ta çalışır."
    Abort
  ${EndIf}
  SetRegView 64
FunctionEnd

Function un.onInit
  SetRegView 64
FunctionEnd

Section "Kilit Bul" SecMain
  SectionIn RO

  ; Güncelleme sırasında çalışan bir kopya varsa exe üzerine yazılamaz.
  nsExec::Exec '"$SYSDIR\taskkill.exe" /F /IM ${APP_EXE}'
  Pop $0

  SetOutPath "$INSTDIR"
  File "/oname=${APP_EXE}" "${SRC_EXE}"
  WriteUninstaller "$INSTDIR\Uninstall.exe"

  !insertmacro RemoveUserVerbs
  !insertmacro WriteVerb "*"
  !insertmacro WriteVerb "Directory"

  ; Programlar ve Özellikler / Uygulamalar listesi
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayName"     "${APP_NAME}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayVersion"  "${VERSION}"
  WriteRegStr   HKLM "${UNINST_KEY}" "Publisher"       "${PUBLISHER}"
  WriteRegStr   HKLM "${UNINST_KEY}" "DisplayIcon"     '"$INSTDIR\${APP_EXE}",0'
  WriteRegStr   HKLM "${UNINST_KEY}" "InstallLocation" "$INSTDIR"
  WriteRegStr   HKLM "${UNINST_KEY}" "UninstallString" '"$INSTDIR\Uninstall.exe"'
  WriteRegStr   HKLM "${UNINST_KEY}" "QuietUninstallString" '"$INSTDIR\Uninstall.exe" /S'
  WriteRegStr   HKLM "${UNINST_KEY}" "URLInfoAbout"    "https://github.com/coraspirin/KilitBul"
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoModify" 1
  WriteRegDWORD HKLM "${UNINST_KEY}" "NoRepair" 1
  ${GetSize} "$INSTDIR" "/S=0K" $0 $1 $2
  IntFmt $0 "0x%08X" $0
  WriteRegDWORD HKLM "${UNINST_KEY}" "EstimatedSize" "$0"

  !insertmacro NotifyShell
SectionEnd

Section "Uninstall"
  nsExec::Exec '"$SYSDIR\taskkill.exe" /F /IM ${APP_EXE}'
  Pop $0

  DeleteRegKey HKLM "Software\Classes\*\shell\${VERB_KEY}"
  DeleteRegKey HKLM "Software\Classes\Directory\shell\${VERB_KEY}"
  !insertmacro RemoveUserVerbs
  DeleteRegKey HKLM "${UNINST_KEY}"

  Delete "$INSTDIR\${APP_EXE}"
  Delete "$INSTDIR\Uninstall.exe"
  RMDir "$INSTDIR"

  !insertmacro NotifyShell
SectionEnd
