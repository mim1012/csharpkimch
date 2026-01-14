#!/bin/bash
# 긴급 배포 스크립트 - EC2에서 직접 실행
# 사용법: curl -fsSL https://raw.githubusercontent.com/mim1012/csharpkimch/develop/deploy/EMERGENCY_DEPLOY.sh | bash

set -e

echo "=========================================="
echo "KimchiHedge AuthServer 긴급 배포"
echo "=========================================="

# Git 및 .NET 설치
sudo dnf install -y git dotnet-sdk-8.0

# 소스 clone
cd /tmp
rm -rf csharpkimch
git clone https://github.com/mim1012/csharpkimch.git
cd csharpkimch
git checkout develop

# 빌드
sudo dotnet publish src/KimchiHedge.AuthServer/KimchiHedge.AuthServer.csproj \
  -c Release \
  -o /opt/authserver

# Systemd 서비스
sudo bash -c 'cat > /etc/systemd/system/authserver.service << "EOF"
[Unit]
Description=KimchiHedge AuthServer
After=network.target

[Service]
Type=simple
Restart=always
RestartSec=10
WorkingDirectory=/opt/authserver
Environment="ASPNETCORE_URLS=http://0.0.0.0:80"
Environment="ASPNETCORE_ENVIRONMENT=Production"
Environment="ConnectionStrings__AuthDb=Host=aws-1-ap-northeast-1.pooler.supabase.com;Port=5432;Database=postgres;Username=postgres.mnfbkggcdakljrrwshdo;Password=rlawlgns2233@"
Environment="Jwt__Secret=KimchiHedge2024SecretKeyAutoTrading1234567890"
Environment="Jwt__Issuer=KimchiHedge.AuthServer"
Environment="Jwt__Audience=KimchiHedge.Client"
Environment="Jwt__AccessTokenExpiresHours=24"
Environment="Jwt__RefreshTokenExpiresDays=30"
Environment="Admin__Email=admin@kimchihedge.com"
Environment="Admin__Password=Admin123456"
Environment="KimchiPremium__LambdaApiUrl=https://ogh80p7tqk.execute-api.ap-northeast-2.amazonaws.com/prod/premium"
Environment="KimchiPremium__PollingIntervalMs=1000"
Environment="KimchiPremium__PollingEnabled=true"
ExecStart=/usr/bin/dotnet /opt/authserver/KimchiHedge.AuthServer.dll

[Install]
WantedBy=multi-user.target
EOF'

sudo systemctl daemon-reload
sudo systemctl enable authserver
sudo systemctl start authserver

sleep 3
curl http://localhost/health

echo "배포 완료!"
