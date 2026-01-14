#!/bin/bash
# KimchiHedge AuthServer - Ubuntu 22.04 배포 스크립트

set -e

echo "=========================================="
echo "KimchiHedge AuthServer 배포 (Ubuntu)"
echo "=========================================="

# Git 및 .NET 설치
echo "[1/5] Git 및 .NET SDK 설치 중..."
sudo apt-get update
sudo apt-get install -y wget git

# .NET SDK 설치 (Ubuntu 22.04)
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
rm packages-microsoft-prod.deb

sudo apt-get update
sudo apt-get install -y dotnet-sdk-8.0

echo "✓ 설치 완료"

# 소스 clone
echo "[2/5] GitHub 소스코드 clone 중..."
cd /tmp
rm -rf csharpkimch
git clone https://github.com/mim1012/csharpkimch.git
cd csharpkimch
git checkout develop
echo "✓ Clone 완료"

# 빌드
echo "[3/5] dotnet publish 빌드 중..."
sudo dotnet publish src/KimchiHedge.AuthServer/KimchiHedge.AuthServer.csproj \
  -c Release \
  -o /opt/authserver
echo "✓ 빌드 완료"

# Systemd 서비스
echo "[4/5] Systemd 서비스 설정 중..."
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
echo "✓ 서비스 설정 완료"

# 서비스 시작
echo "[5/5] AuthServer 시작 중..."
sudo systemctl enable authserver
sudo systemctl start authserver

sleep 5

# Health Check
echo "=========================================="
echo "Health Check 중..."
echo "=========================================="

HEALTH=$(curl -s http://localhost/health || echo "FAILED")

if [[ "$HEALTH" == *"Healthy"* ]]; then
    echo "✅ 배포 성공!"
    echo ""
    echo "응답:"
    echo "$HEALTH"
    echo ""
    echo "AuthServer URL: http://13.209.241.123"
else
    echo "⚠️ Health Check 실패"
    echo "로그 확인:"
    sudo systemctl status authserver
    sudo journalctl -u authserver -n 30
fi

echo ""
echo "배포 완료!"
