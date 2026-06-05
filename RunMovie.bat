@echo off
cd /d D:\Downloads\LVDKMovie\LVDKMovie

start "" http://localhost:5000

dotnet run --project LVDKMovie.csproj --urls "http://localhost:5000"

pause
