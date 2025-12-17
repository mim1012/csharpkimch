@echo off
REM KimchiHedge AuthServer Startup Script
REM Development mode with Swagger UI

set ASPNETCORE_ENVIRONMENT=Development
set "KIMCHI_DATABASE_URL=Host=aws-1-ap-northeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.mnfbkggcdakljrrwshdo;Password=rlawlgns2233@"
set KIMCHI_JWT_SECRET=KimchiHedge2024SecretKeyAutoTrading1234567890
set KIMCHI_ADMIN_EMAIL=admin@kimchihedge.com
set KIMCHI_ADMIN_PASSWORD=Admin123456

cd /d D:\Project\Csharpkimchi\src\KimchiHedge.AuthServer
dotnet run --urls=http://localhost:5000
pause
