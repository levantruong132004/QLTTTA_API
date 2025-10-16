@echo off
echo Đang khởi động hệ thống quản lý TTA...
echo.

echo Bước 1: Khởi động API Server...
start "QLTTTA API" cmd /k "cd /d d:\Doan_KLTN\QLTTTA_API\QLTTTA_API && dotnet run"

echo Bước 2: Đợi API khởi động...
timeout /t 5 /nobreak > nul

echo Bước 3: Khởi động Web Application...
start "QLTTTA WEB" cmd /k "cd /d d:\Doan_KLTN\QLTTTA_API\QLTTTA_WEB && dotnet run"

echo.
echo Hệ thống đã được khởi động!
echo - API Server: http://localhost:5069
echo - Web Application: http://localhost:5020
echo.
echo Nhấn phím bất kỳ để mở trình duyệt...
pause > nul

start http://localhost:5020

echo.
echo Nhấn phím bất kỳ để đóng script...
pause > nul