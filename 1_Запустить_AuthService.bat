@echo off
cd /d C:\Users\User\Desktop\web
start http://localhost:8001/docs
python -m uvicorn src.auth_service.main:app --host 0.0.0.0 --port 8001 --reload
pause
