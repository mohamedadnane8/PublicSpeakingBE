# DigitalOcean Deployment Guide

This guide explains how to deploy the PublicSpeaking API to DigitalOcean using Docker.

## Prerequisites

- A DigitalOcean Droplet (Ubuntu 22.04 or 24.04 recommended)
- A DigitalOcean Managed PostgreSQL database (or self-hosted)
- Domain name (optional but recommended)

## Server Setup

### 1. Create Droplet

- **Image:** Ubuntu 24.04 (LTS) x64
- **Plan:** Basic, $6-12/month (1GB RAM minimum, 2GB recommended)
- **Datacenter:** Choose closest to your users
- **Authentication:** SSH keys (recommended)

### 2. Install Docker

SSH into your droplet and run:

```bash
# Update system
sudo apt update && sudo apt upgrade -y

# Install Docker
sudo apt install -y apt-transport-https ca-certificates curl software-properties-common
curl -fsSL https://download.docker.com/linux/ubuntu/gpg | sudo apt-key add -
sudo add-apt-repository "deb [arch=amd64] https://download.docker.com/linux/ubuntu $(lsb_release -cs) stable"
sudo apt update
sudo apt install -y docker-ce docker-ce-cli containerd.io docker-compose-plugin

# Add user to docker group (logout and login after this)
sudo usermod -aG docker $USER
```

Logout and SSH back in.

### 3. Setup App Directory

```bash
# Create app directory
sudo mkdir -p /var/www/publicspeaking
sudo chown $USER:$USER /var/www/publicspeaking
cd /var/www/publicspeaking

# Clone your repository
git clone https://github.com/YOUR_USERNAME/PublicSpeakingBE.git .
```

### 4. Configure Environment

```bash
cd /var/www/publicspeaking

# Copy example environment file
cp .env.example .env

# Edit with your values
nano .env
```

Fill in your actual values:

```env
CONNECTION_STRING=Host=your-db-host.db.ondigitalocean.com;Port=25060;Database=publicspeaking;Username=doadmin;Password=YOUR_PASSWORD;SslMode=Require
JWT_SECRET_KEY=your-64-character-secret-key-here
GOOGLE_CLIENT_ID=your-client-id.apps.googleusercontent.com
GOOGLE_CLIENT_SECRET=your-client-secret
GOOGLE_REDIRECT_URI=https://your-domain.com/api/auth/google/callback
FRONTEND_URL=https://your-frontend-domain.com
```

### 5. Deploy

```bash
# Run the update script
./updateContainer.sh
```

Or manually:

```bash
docker-compose up --build -d
```

### 6. Setup Nginx (Reverse Proxy)

Install Nginx:

```bash
sudo apt install -y nginx
```

Create config:

```bash
sudo nano /etc/nginx/sites-available/publicspeaking
```

Add this configuration:

```nginx
server {
    listen 80;
    server_name your-domain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
        
        # Increase timeout for long requests
        proxy_read_timeout 300s;
        proxy_connect_timeout 75s;
    }
}
```

Enable site:

```bash
sudo ln -s /etc/nginx/sites-available/publicspeaking /etc/nginx/sites-enabled/
sudo rm /etc/nginx/sites-enabled/default
sudo nginx -t
sudo systemctl restart nginx
```

### 7. HTTPS with Let's Encrypt

```bash
sudo apt install -y certbot python3-certbot-nginx
sudo certbot --nginx -d your-domain.com
```

Follow prompts. Certbot will auto-renew.

### 8. Update Google OAuth Redirect URI

Go to [Google Cloud Console](https://console.cloud.google.com/) → APIs & Services → Credentials:

Add authorized redirect URI:
- `https://your-domain.com/api/auth/google/callback`

## Updating the Application

After making changes to your code:

```bash
ssh root@your-droplet-ip
cd /var/www/publicspeaking
./updateContainer.sh
```

Or if you want to do it manually:

```bash
git pull origin main
docker-compose down
docker-compose up --build -d
```

## Viewing Logs

```bash
# View all logs
docker-compose logs

# Follow logs in real-time
docker-compose logs -f

# View specific service logs
docker-compose logs -f api
```

## Troubleshooting

### Container won't start

```bash
# Check logs
docker-compose logs api

# Check if port is already in use
sudo lsof -i :5000

# Rebuild from scratch
docker-compose down
docker system prune -a
docker-compose up --build
```

### Database connection issues

```bash
# Test database connection from container
docker-compose exec api bash
# Then inside container:
apt-get update && apt-get install -y postgresql-client
psql "YOUR_CONNECTION_STRING"
```

### SSL certificate issues

```bash
# Renew certificate manually
sudo certbot renew --force-renewal
sudo systemctl restart nginx
```

## Security Checklist

- [ ] Change all default passwords
- [ ] Configure UFW firewall (allow only 22, 80, 443)
- [ ] Enable automatic security updates
- [ ] Use strong JWT secret (64+ characters)
- [ ] Enable DigitalOcean backups
- [ ] Setup log monitoring (optional)

## Useful Commands

```bash
# Check container status
docker-compose ps

# Restart container
docker-compose restart api

# Execute commands in container
docker-compose exec api bash

# Database migrations (if needed)
docker-compose exec api dotnet ef database update

# Backup database
docker-compose exec api pg_dump "$CONNECTION_STRING" > backup.sql

# Monitor resources
docker stats
```

## Monitoring (Optional)

Setup basic monitoring with Docker:

```bash
# Install doctop (Docker top)
docker run --rm -v /var/run/docker.sock:/var/run/docker.sock \
  --name=doctop j-bennet/doctop
```

Or use DigitalOcean's built-in monitoring.

## Cost Optimization

- Use a $6/month droplet for small apps
- Enable DigitalOcean's monitoring alerts
- Set up log rotation to save disk space
- Use DigitalOcean Container Registry if building images frequently
