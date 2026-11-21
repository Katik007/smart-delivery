@echo off
cd /d C:\Users\User\Desktop\web
start http://localhost:8002/docs
python -m uvicorn src.catalog_service.main:app --host 0.0.0.0 --port 8002 --reload
pause
