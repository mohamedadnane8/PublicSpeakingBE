#!/bin/bash

set -euo pipefail

RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m'

APP_DIR="/var/www/publicspeaking"
COMPOSE_CMD="docker compose"

echo -e "${GREEN}Starting deployment update...${NC}"

if [ ! -d "$APP_DIR" ]; then
    echo -e "${RED}App directory not found: $APP_DIR${NC}"
    exit 1
fi

cd "$APP_DIR"

if ! command -v docker >/dev/null 2>&1; then
    echo -e "${RED}Docker is not installed${NC}"
    exit 1
fi

if ! docker compose version >/dev/null 2>&1; then
    echo -e "${RED}Docker Compose plugin is not installed${NC}"
    echo -e "${YELLOW}Install it with: sudo apt install docker-compose-plugin -y${NC}"
    exit 1
fi

if [ ! -d ".git" ]; then
    echo -e "${RED}This folder is not a git repository${NC}"
    exit 1
fi

echo -e "${GREEN}Pulling latest code...${NC}"
git pull origin main

if [ ! -f ".env" ]; then
    echo -e "${RED}.env file not found${NC}"
    exit 1
fi

echo -e "${GREEN}Stopping old containers...${NC}"
$COMPOSE_CMD down

echo -e "${GREEN}Cleaning unused images...${NC}"
docker image prune -f

echo -e "${GREEN}Building and starting containers...${NC}"
$COMPOSE_CMD up -d --build --force-recreate

# Run database migrations
echo -e "${GREEN}Running database migrations...${NC}"
$COMPOSE_CMD exec -T api dotnet ef database update --project MyApp.Infrastructure --startup-project MyApp.API || echo "Migration skipped or failed"

echo -e "${GREEN}Current status:${NC}"
$COMPOSE_CMD ps

echo -e "${GREEN}Last logs:${NC}"
$COMPOSE_CMD logs --tail=50

echo -e "${GREEN}Deployment complete.${NC}"