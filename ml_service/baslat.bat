@echo off
cd /d "%~dp0"
echo ML Servisi baslatiliyor...
uvicorn main:app --reload --port 8000
pause
