@echo off
echo Starting SseApi Standalone (HTTP Only)...
echo.
echo Application will start on:
echo - HTTP:  http://localhost:5000
echo.
echo Test pages available at:
echo - http://localhost:5000/
echo - http://localhost:5000/sse-test-page.html
echo - http://localhost:5000/sse-send.html
echo - http://localhost:5000/sse-recv.html
echo.
echo Note: Using HTTP only to avoid SSL certificate issues
echo.
set ASPNETCORE_URLS=http://localhost:5000
SseApi.exe
pause