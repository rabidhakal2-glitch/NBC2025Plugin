@echo off
title NBC2025 Plugin Builder
color 1F
echo.
echo  =============================================
echo   NBC 105:2025 ETABS Plugin - DLL Builder
echo  =============================================
echo.

:: ── Find C# Compiler ─────────────────────────────────────────
set CSC=
set NETREF=

if exist "C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe" (
    set CSC=C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe
    set NETREF=C:\Windows\Microsoft.NET\Framework64\v4.0.30319
    echo [OK] Compiler found .NET 4.0 x64
    goto :find_etabs
)
if exist "C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v4.0.30319\csc.exe
    set NETREF=C:\Windows\Microsoft.NET\Framework\v4.0.30319
    echo [OK] Compiler found .NET 4.0 x86
    goto :find_etabs
)
if exist "C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe" (
    set CSC=C:\Windows\Microsoft.NET\Framework64\v3.5\csc.exe
    set NETREF=C:\Windows\Microsoft.NET\Framework64\v3.5
    echo [OK] Compiler found .NET 3.5 x64
    goto :find_etabs
)
if exist "C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe" (
    set CSC=C:\Windows\Microsoft.NET\Framework\v3.5\csc.exe
    set NETREF=C:\Windows\Microsoft.NET\Framework\v3.5
    echo [OK] Compiler found .NET 3.5 x86
    goto :find_etabs
)
echo [ERROR] No C# compiler found on this PC.
echo.
pause
exit /b 1

:: ── Find ETABSv1.dll ─────────────────────────────────────────
:find_etabs
set ETABS=
if exist "C:\Program Files\Computers and Structures\ETABS 19\ETABSv1.dll" (
    set ETABS=C:\Program Files\Computers and Structures\ETABS 19
    echo [OK] ETABS 19 found
    goto :compile
)
if exist "C:\Program Files (x86)\Computers and Structures\ETABS 19\ETABSv1.dll" (
    set ETABS=C:\Program Files (x86)\Computers and Structures\ETABS 19
    echo [OK] ETABS 19 found
    goto :compile
)
if exist "%~dp0ETABSv1.dll" (
    set ETABS=%~dp0
    echo [OK] ETABSv1.dll found in current folder
    goto :compile
)
echo [ERROR] ETABSv1.dll not found.
echo Copy ETABSv1.dll into same folder as this bat file.
echo.
pause
exit /b 1

:: ── Compile ──────────────────────────────────────────────────
:compile
set SRC=%~dp0
echo.
echo [..] Compiling - please wait...
echo.

"%CSC%" ^
  /target:library ^
  /optimize+ ^
  /out:"%SRC%NBC2025Plugin.dll" ^
  /reference:"%ETABS%\ETABSv1.dll" ^
  /reference:"%NETREF%\System.Windows.Forms.dll" ^
  /reference:"%NETREF%\System.Drawing.dll" ^
  /reference:"%NETREF%\System.dll" ^
  /reference:"%NETREF%\mscorlib.dll" ^
  "%SRC%NBC2025PluginSingle.cs"

:: ── Check result ─────────────────────────────────────────────
if not exist "%SRC%NBC2025Plugin.dll" (
    echo.
    echo [FAILED] See error above.
    echo.
    echo Make sure:
    echo  1. Right-click this bat - Run as Administrator
    echo  2. NBC2025PluginSingle.cs is in same folder as this bat
    echo  3. ETABSv1.dll is in same folder as this bat
    echo.
    pause
    exit /b 1
)

echo.
echo [OK] NBC2025Plugin.dll created!
echo.

:: ── Copy to ETABS Plugins folder ─────────────────────────────
if not exist "%ETABS%\Plugins" mkdir "%ETABS%\Plugins"
copy /Y "%SRC%NBC2025Plugin.dll" "%ETABS%\Plugins\NBC2025Plugin.dll"

if exist "%ETABS%\Plugins\NBC2025Plugin.dll" (
    echo [OK] Installed to ETABS Plugins folder!
    echo      %ETABS%\Plugins\
) else (
    echo [!] Please manually copy NBC2025Plugin.dll to:
    echo     %ETABS%\Plugins\
)

echo.
echo  =============================================
echo   DONE!
echo   Open ETABS 19
echo   Tools - Plug-ins - NBC 105:2025 Seismic
echo  =============================================
echo.
pause
