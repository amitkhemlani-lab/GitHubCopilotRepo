# Deployment Guide

Step-by-step deployment instructions for DotNetSample on various platforms.

## Prerequisites

- **.NET 8 SDK** installed (for building)
- **Database server** (for production: PostgreSQL, SQL Server, MySQL)
- **Application hosting environment** (IIS, Docker, Azure, AWS, Linux server)

---

## Local Development Deployment

See [WORKSPACE.md](WORKSPACE.md) for local setup instructions.

**Quick start:**

```bash
cd /path/to/DotNetSample
dotnet build
dotnet run --project Api
```

API available at: `http://localhost:5181`

---

## Docker Deployment

### Dockerfile

Create `Dockerfile` in the repository root:

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copy solution and project files
COPY DotNetSample.sln ./
COPY Api/Api.csproj Api/
COPY Core/Core.csproj Core/
COPY Infrastructure/Infrastructure.csproj Infrastructure/
COPY Worker/Worker.csproj Worker/

# Restore dependencies
RUN dotnet restore

# Copy source code
COPY . .

# Build and publish
WORKDIR /src/Api
RUN dotnet publish -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
COPY --from=build /app/publish .

# Expose ports
EXPOSE 8080
EXPOSE 8081

# Set environment variables
ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

# Entry point
ENTRYPOINT ["dotnet", "Api.dll"]
```

---

### Build and Run

```bash
# Build image
docker build -t dotnetsample-api:latest .

# Run container
docker run -d \
  -p 5181:8080 \
  --name dotnetsample \
  -e ConnectionStrings__Default="Data Source=/data/app.db" \
  -v $(pwd)/data:/data \
  dotnetsample-api:latest

# View logs
docker logs -f dotnetsample

# Stop container
docker stop dotnetsample
docker rm dotnetsample
```

---

### Docker Compose

Create `docker-compose.yml`:

```yaml
version: '3.8'

services:
  api:
    build: .
    ports:
      - "5181:8080"
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - ConnectionStrings__Default=Data Source=/data/app.db
    volumes:
      - ./data:/data
    restart: unless-stopped

  # Optional: Add PostgreSQL for production
  postgres:
    image: postgres:16
    environment:
      - POSTGRES_USER=dotnetsample
      - POSTGRES_PASSWORD=yourpassword
      - POSTGRES_DB=dotnetsample
    ports:
      - "5432:5432"
    volumes:
      - postgres-data:/var/lib/postgresql/data

volumes:
  postgres-data:
```

**With PostgreSQL:**

Update `docker-compose.yml` api service:

```yaml
api:
  build: .
  ports:
    - "5181:8080"
  environment:
    - ASPNETCORE_ENVIRONMENT=Production
    - ConnectionStrings__Default=Host=postgres;Database=dotnetsample;Username=dotnetsample;Password=yourpassword
  depends_on:
    - postgres
  restart: unless-stopped
```

**Run:**

```bash
docker-compose up -d
docker-compose logs -f
```

---

## Azure App Service Deployment

### Option 1: Azure CLI

**1. Login:**

```bash
az login
```

**2. Create resource group:**

```bash
az group create \
  --name DotNetSampleRG \
  --location eastus
```

**3. Create App Service plan:**

```bash
az appservice plan create \
  --name DotNetSamplePlan \
  --resource-group DotNetSampleRG \
  --sku B1 \
  --is-linux
```

**4. Create Web App:**

```bash
az webapp create \
  --name dotnetsample-api \
  --resource-group DotNetSampleRG \
  --plan DotNetSamplePlan \
  --runtime "DOTNET|8.0"
```

**5. Configure connection string:**

```bash
az webapp config connection-string set \
  --name dotnetsample-api \
  --resource-group DotNetSampleRG \
  --connection-string-type SQLAzure \
  --settings Default="Server=tcp:yourserver.database.windows.net;Database=dotnetsample;User Id=admin;Password=yourpassword;"
```

**6. Deploy:**

```bash
# Publish locally
dotnet publish -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../deploy.zip .
cd ..

# Deploy to Azure
az webapp deployment source config-zip \
  --resource-group DotNetSampleRG \
  --name dotnetsample-api \
  --src deploy.zip
```

**7. Browse:**

```bash
az webapp browse \
  --name dotnetsample-api \
  --resource-group DotNetSampleRG
```

---

### Option 2: Visual Studio Publish

1. Right-click `Api` project → **Publish**
2. Target: **Azure**
3. Specific target: **Azure App Service (Linux)**
4. Select or create App Service instance
5. Click **Publish**

---

### Option 3: GitHub Actions

Create `.github/workflows/azure-deploy.yml`:

```yaml
name: Deploy to Azure

on:
  push:
    branches:
      - main

jobs:
  build-and-deploy:
    runs-on: ubuntu-latest

    steps:
    - uses: actions/checkout@v3

    - name: Setup .NET
      uses: actions/setup-dotnet@v3
      with:
        dotnet-version: '8.0.x'

    - name: Restore dependencies
      run: dotnet restore

    - name: Build
      run: dotnet build --configuration Release --no-restore

    - name: Test
      run: dotnet test --no-build --verbosity normal

    - name: Publish
      run: dotnet publish Api/Api.csproj -c Release -o ./publish

    - name: Deploy to Azure Web App
      uses: azure/webapps-deploy@v2
      with:
        app-name: 'dotnetsample-api'
        publish-profile: ${{ secrets.AZURE_WEBAPP_PUBLISH_PROFILE }}
        package: ./publish
```

**Setup:**
1. Download publish profile from Azure Portal
2. Add as secret `AZURE_WEBAPP_PUBLISH_PROFILE` in GitHub
3. Push to `main` branch to trigger deployment

---

## AWS Elastic Beanstalk Deployment

**1. Install EB CLI:**

```bash
pip install awsebcli
```

**2. Initialize:**

```bash
eb init -p "64bit Amazon Linux 2023 v3.1.0 running .NET 8" dotnetsample --region us-east-1
```

**3. Create environment:**

```bash
eb create dotnetsample-env --instance-type t3.small
```

**4. Deploy:**

```bash
# Publish
dotnet publish Api/Api.csproj -c Release -o ./publish

# Create deployment package
cd publish
zip -r ../deploy.zip .
cd ..

# Deploy to EB
eb deploy
```

**5. Set environment variables:**

```bash
eb setenv ASPNETCORE_ENVIRONMENT=Production
eb setenv ConnectionStrings__Default="Server=yourdb.rds.amazonaws.com;Database=dotnetsample;User Id=admin;Password=yourpassword"
```

**6. Open application:**

```bash
eb open
```

---

### AWS RDS (PostgreSQL)

**1. Create RDS instance:**

```bash
aws rds create-db-instance \
  --db-instance-identifier dotnetsample-db \
  --db-instance-class db.t3.micro \
  --engine postgres \
  --master-username admin \
  --master-user-password yourpassword \
  --allocated-storage 20
```

**2. Configure connection string in EB:**

```bash
eb setenv ConnectionStrings__Default="Host=dotnetsample-db.xxxxx.us-east-1.rds.amazonaws.com;Database=dotnetsample;Username=admin;Password=yourpassword"
```

---

## Linux Server (Ubuntu) Deployment

### 1. Install .NET Runtime

```bash
# Add Microsoft package repository
wget https://packages.microsoft.com/config/ubuntu/22.04/packages-microsoft-prod.deb
sudo dpkg -i packages-microsoft-prod.deb
sudo apt-get update

# Install ASP.NET Core runtime
sudo apt-get install -y aspnetcore-runtime-8.0
```

---

### 2. Copy Application

**On local machine:**

```bash
dotnet publish Api/Api.csproj -c Release -o ./publish
scp -r publish/* user@yourserver:/var/www/dotnetsample/
```

**On server:**

```bash
sudo mkdir -p /var/www/dotnetsample
sudo chown -R www-data:www-data /var/www/dotnetsample
```

---

### 3. Create systemd Service

**File:** `/etc/systemd/system/dotnetsample.service`

```ini
[Unit]
Description=DotNetSample API
After=network.target

[Service]
WorkingDirectory=/var/www/dotnetsample
ExecStart=/usr/bin/dotnet /var/www/dotnetsample/Api.dll
Restart=always
RestartSec=10
SyslogIdentifier=dotnetsample
User=www-data
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=DOTNET_PRINT_TELEMETRY_MESSAGE=false
Environment=ASPNETCORE_URLS=http://localhost:5000
Environment=ConnectionStrings__Default="Host=localhost;Database=dotnetsample;Username=dbuser;Password=dbpass"

[Install]
WantedBy=multi-user.target
```

---

### 4. Enable and Start Service

```bash
# Reload systemd
sudo systemctl daemon-reload

# Enable service (start on boot)
sudo systemctl enable dotnetsample

# Start service
sudo systemctl start dotnetsample

# Check status
sudo systemctl status dotnetsample

# View logs
sudo journalctl -u dotnetsample -f
```

---

### 5. Configure Nginx Reverse Proxy

**Install Nginx:**

```bash
sudo apt-get install -y nginx
```

**File:** `/etc/nginx/sites-available/dotnetsample`

```nginx
server {
    listen 80;
    listen [::]:80;
    server_name yourdomain.com www.yourdomain.com;

    location / {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_cache_bypass $http_upgrade;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
    }
}
```

**Enable site:**

```bash
# Create symlink
sudo ln -s /etc/nginx/sites-available/dotnetsample /etc/nginx/sites-enabled/

# Test configuration
sudo nginx -t

# Restart Nginx
sudo systemctl restart nginx
```

---

### 6. SSL with Let's Encrypt (Optional)

```bash
# Install Certbot
sudo apt-get install -y certbot python3-certbot-nginx

# Obtain certificate
sudo certbot --nginx -d yourdomain.com -d www.yourdomain.com

# Auto-renewal is configured automatically
```

---

## Configuration for Production

### 1. Update Connection String

**File:** `Api/appsettings.Production.json`

```json
{
  "ConnectionStrings": {
    "Default": "Host=prod-db-server.example.com;Database=dotnetsample;Username=dbuser;Password=securepassword;SslMode=Require"
  },
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning"
    }
  },
  "AllowedHosts": "*"
}
```

---

### 2. Use Environment Variables

**Best practice:** Store secrets in environment variables, not config files.

**Linux (systemd):**

```ini
Environment=ConnectionStrings__Default="Host=..."
```

**Docker:**

```bash
docker run -e ConnectionStrings__Default="Host=..." dotnetsample-api
```

**Azure:**

```bash
az webapp config appsettings set \
  --name dotnetsample-api \
  --settings ConnectionStrings__Default="Host=..."
```

**Reading in code:**

```csharp
var connectionString = builder.Configuration.GetConnectionString("Default");
```

---

### 3. Apply Migrations in Production

**Option 1: Auto-migration at startup (already configured):**

File: `Api/Program.cs`

```csharp
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();  // Applies pending migrations
}
```

**Option 2: Manual migration:**

```bash
# SSH into production server
dotnet ef database update \
  --project Infrastructure \
  --startup-project Api \
  --connection "Host=proddb;Database=dotnetsample;..."
```

**Recommendation:** Use auto-migration for simplicity, or manual for control in sensitive environments.

---

## Health Checks

Add health check endpoint for monitoring.

**File:** `Api/Program.cs`

```csharp
// Add health checks
builder.Services.AddHealthChecks()
    .AddDbContextCheck<AppDbContext>();

// Map health endpoint
app.MapHealthChecks("/health");
```

**Test:**

```bash
curl http://localhost:5181/health
```

**Response:**

```
Healthy
```

**Azure/AWS:** Configure load balancer to ping `/health` for availability checks.

---

## Monitoring and Logging

### Application Insights (Azure)

**1. Add NuGet package:**

```bash
dotnet add Api package Microsoft.ApplicationInsights.AspNetCore
```

**2. Configure in `Program.cs`:**

```csharp
builder.Services.AddApplicationInsightsTelemetry();
```

**3. Add instrumentation key to `appsettings.json`:**

```json
{
  "ApplicationInsights": {
    "InstrumentationKey": "your-instrumentation-key"
  }
}
```

**4. View telemetry in Azure Portal → Application Insights**

---

### Serilog (Structured Logging)

**1. Add NuGet packages:**

```bash
dotnet add Api package Serilog.AspNetCore
dotnet add Api package Serilog.Sinks.Console
dotnet add Api package Serilog.Sinks.File
```

**2. Configure in `Program.cs`:**

```csharp
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, config) =>
{
    config
        .ReadFrom.Configuration(context.Configuration)
        .Enrich.FromLogContext()
        .WriteTo.Console()
        .WriteTo.File("logs/log-.txt", rollingInterval: RollingInterval.Day);
});
```

**3. Add to `appsettings.json`:**

```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft": "Warning",
        "System": "Warning"
      }
    }
  }
}
```

---

## Troubleshooting

### API Won't Start

**Check port availability:**

```bash
netstat -an | grep 5181
lsof -i :5181
```

**Verify .NET runtime:**

```bash
dotnet --info
dotnet --list-runtimes
```

**Check systemd logs:**

```bash
sudo journalctl -u dotnetsample -n 50
```

---

### Database Connection Errors

**Verify connection string:**

```bash
# Check environment variable
echo $ConnectionStrings__Default

# Test connection (PostgreSQL)
psql -h hostname -U username -d database

# Test connection (SQL Server)
sqlcmd -S server -U username -P password
```

**Check firewall rules:**

```bash
# Allow PostgreSQL port
sudo ufw allow 5432/tcp

# Check if database server is accessible
telnet db-server 5432
```

**Verify migrations applied:**

```bash
dotnet ef migrations list --project Infrastructure --startup-project Api
```

---

### Worker Not Processing Orders

**Check if Worker is running:**

```bash
# View logs for worker activity
sudo journalctl -u dotnetsample | grep "Processing order"
```

**Verify IOrderProcessor registration:**

File: `Api/Program.cs`

```csharp
builder.Services.AddScoped<IOrderProcessor, OrderProcessor>();
builder.Services.AddHostedService<OrderWorker>();
```

**Check for pending orders:**

```bash
sqlite3 Api/app.db "SELECT * FROM Orders WHERE Status = 0;"
```

---

### High Memory Usage

**Profile memory:**

```bash
dotnet-counters monitor --process-id <pid>
```

**Optimize DbContext:**
- Ensure DbContext instances are disposed
- Use `.AsNoTracking()` for read-only queries
- Avoid loading unnecessary related entities

---

### Slow API Responses

**Enable detailed logging:**

Set `Logging:LogLevel:Default` to `Debug` in `appsettings.json`

**Add database query logging:**

```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite("...")
           .EnableSensitiveDataLogging()
           .EnableDetailedErrors());
```

**Profile with Application Insights** or use `dotnet-trace` for CPU profiling.

---

## Production Checklist

- [ ] Update connection string to production database
- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Use HTTPS (SSL certificate)
- [ ] Configure health checks
- [ ] Enable logging (Serilog or Application Insights)
- [ ] Set up monitoring and alerts
- [ ] Configure auto-scaling (if using cloud)
- [ ] Back up database regularly
- [ ] Apply migrations
- [ ] Test API endpoints after deployment
- [ ] Configure CORS if needed
- [ ] Review security settings (disable Swagger in prod, use auth)

---

## Related Documentation

- See [DATABASE.md](DATABASE.md) for switching from SQLite to PostgreSQL/SQL Server
- See [ARCHITECTURE.md](ARCHITECTURE.md) for Worker deployment options
- See [WORKSPACE.md](WORKSPACE.md) for local development setup
