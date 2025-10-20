@echo off
echo ================================
echo    He thong quan ly LDA
echo ================================
echo.

echo Dang khoi dong API Server...
start "LDA API Server" cmd /k "cd /d d:\Doan_KLTN\QLTTTA_API\QLTTTA_API && dotnet run"

echo Cho API khoi dong trong 5 giay...
timeout /t 5 /nobreak > nul

echo Dang khoi dong Web Application...
start "LDA Web App" cmd /k "cd /d d:\Doan_KLTN\QLTTTA_API\QLTTTA_WEB && dotnet run"

echo.
echo ================================
echo  He thong da duoc khoi dong!
echo ================================
echo - API Server: http://localhost:5069
echo - Web App: http://localhost:5165  
echo.
echo Nhan phim bat ky de mo trinh duyet...
pause > nul

start http://localhost:5165

echo.
echo Nhan phim bat ky de dong script...
pause > nul