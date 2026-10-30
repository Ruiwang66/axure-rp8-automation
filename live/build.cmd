@echo off
setlocal
set "RP8_ROOT=%~dp0..\.."
set "RP8_CSC=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\csc.exe"
set "RP8_WPF=%WINDIR%\Microsoft.NET\Framework\v4.0.30319\WPF"
"%RP8_CSC%" /nologo /target:winexe /platform:x86 /r:System.Web.Extensions.dll /r:System.Xaml.dll /r:"%RP8_WPF%\WindowsBase.dll" /r:"%RP8_WPF%\PresentationCore.dll" /r:"%RP8_WPF%\PresentationFramework.dll" /win32icon:"%RP8_ROOT%\App.ico" /out:"%RP8_ROOT%\RP8Live.next.exe" "%~dp0RP8Live.cs" "%~dp0RP8Interactions.cs" "%~dp0RP8Styling.cs"
if errorlevel 1 exit /b 1
copy /y "%RP8_ROOT%\AxureRP8.exe.config" "%RP8_ROOT%\RP8Live.next.exe.config" >nul
"%RP8_CSC%" /nologo /target:winexe /platform:x86 /r:System.Web.Extensions.dll /win32icon:"%RP8_ROOT%\App.ico" /out:"%RP8_ROOT%\打开独立RP8.exe" "%~dp0RP8LiveLauncher.cs"
exit /b %errorlevel%
