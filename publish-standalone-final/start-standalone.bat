@echo off
echo Starting SseApi Standalone...
echo.
echo Application will start on:
echo - HTTP:  http://localhost:5000
echo - HTTPS: https://localhost:5001
echo.
echo Test pages available at:
echo - http://localhost:5000/sse-test-page.html
echo - http://localhost:5000/sse-send.html
echo - http://localhost:5000/sse-recv.html
echo.
SseApi.exe
pause