@echo off
cd /d "%~dp0"

set PORT=5000
set "AVATAR_SOURCE=C:\Users\Admin\Pictures\Camera Roll\656826954_1346120630881813_365791499904025732_n.jpg"
set "AVATAR_DIR=wwwroot\images\avatars"
set "AVATAR_DEST=%AVATAR_DIR%\default-admin.jpg"

if not exist "%AVATAR_DIR%" mkdir "%AVATAR_DIR%"
if not exist "%AVATAR_DEST%" (
    if exist "%AVATAR_SOURCE%" (
        copy /Y "%AVATAR_SOURCE%" "%AVATAR_DEST%" >nul
        echo Da them anh avatar mac dinh cho admin/default.
    ) else (
        echo Khong tim thay anh avatar goc: "%AVATAR_SOURCE%"
    )
)

taskkill /FI "IMAGENAME eq LVDKMovie.exe" /F >nul 2>&1
for /f "tokens=2" %%p in ('tasklist /FI "IMAGENAME eq dotnet.exe" /FO LIST ^| findstr /B "PID:"') do (
    powershell -NoProfile -Command "$p = Get-CimInstance Win32_Process -Filter 'ProcessId=%%p'; if ($p.CommandLine -like '*LVDKMovie*') { Stop-Process -Id %%p -Force }" >nul 2>&1
)

net session >nul 2>&1
if %errorlevel%==0 (
    netsh advfirewall firewall add rule name="LVDKMovie %PORT%" dir=in action=allow protocol=TCP localport=%PORT% >nul 2>&1
)

echo.
echo LVDKMovie dang chay tren may nay:
echo   http://localhost:%PORT%
echo.
echo Neu xem tu may khac trong cung Wi-Fi/LAN, mo bang:
for /f "tokens=2 delims=:" %%a in ('ipconfig ^| findstr /c:"IPv4"') do (
    set "LAN_IP=%%a"
    call echo   http://%%LAN_IP: =%%:%PORT%
)
echo.

start "" http://localhost:5000

dotnet run --project "%~dp0LVDKMovie.csproj" --urls "http://0.0.0.0:%PORT%"

pause
