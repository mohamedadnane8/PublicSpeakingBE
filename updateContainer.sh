#!/bin/bash

# ============================================
# PublicSpeaking API - Container Update Script
# Run this on your DigitalOcean droplet to update the deployment
# ============================================

set -e

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}🚀 Starting deployment update...${NC}"

# Navigate to app directory
APP_DIR="/var/www/publicspeaking"
if [ ! -d "$APP_DIR" ]; then
    echo -e "${YELLOW}⚠️  App directory not found at $APP_DIR${NC}"
    echo -e "${YELLOW}   Please clone your repository to this location${NC}"
    exit 1
fi

cd "$APP_DIR"

# Pull latest code
echo -e "${GREEN}📥 Pulling latest code...${NC}"
git pull origin main

# Ensure .env file exists
if [ ! -f ".env" ]; then
    echo -e "${RED}❌ .env file not found!${NC}"
    echo -e "${YELLOW}   Please create .env file from .env.example${NC}"
    exit 1
fi

# Load environment variables
export $(grep -v '^#' .env | xargs)

# Stop existing containers
echo -e "${GREEN}🛑 Stopping existing containers...${NC}"
docker-compose down

# Remove old images to free space
echo -e "${GREEN}🧹 Cleaning up old images...${NC}"
docker image prune -f

# Build and start new containers
echo -e "${GREEN}🏗️  Building and starting containers...${NC}"
docker-compose up --build -d

# Wait for container to be healthy
echo -e "${GREEN}⏳ Waiting for container to be healthy...${NC}"
sleep 5

# Check health
MAX_RETRIES=10
RETRY_COUNT=0

while [ $RETRY_COUNT -lt $MAX_RETRIES ]; do
    if docker-compose ps | grep -q "healthy"; then
        echo -e "${GREEN}✅ Container is healthy!${NC}"
        break
    fi
    
    RETRY_COUNT=$((RETRY_COUNT + 1))
    echo -e "${YELLOW}   Retry $RETRY_COUNT/$MAX_RETRIES...${NC}"
    sleep 3
done

if [ $RETRY_COUNT -eq $MAX_RETRIES ]; then
    echo -e "${RED}❌ Container failed to become healthy${NC}"
    echo -e "${YELLOW}   Check logs: docker-compose logs${NC}"
    exit 1
fi

# Show status
echo -e "${GREEN}📊 Current status:${NC}"
docker-compose ps

echo -e "${GREEN}✨ Deployment complete!${NC}"
echo -e "${YELLOW}   View logs: docker-compose logs -f${NC}"
