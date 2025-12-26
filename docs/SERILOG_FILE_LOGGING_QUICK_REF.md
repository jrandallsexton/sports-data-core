# Serilog File Logging - Quick Reference

## ? What Was Fixed

### Problem
- Original path `../../logs/` works on Windows but fails in Linux Docker containers
- Container permissions prevent writing to `/logs/`
- Logs not persisting across container restarts

### Solution
Environment-aware log path resolution:
- **Windows Dev**: `C:\Projects\sports-data\logs\{AppName}-{Date}.log`
- **Linux/Docker**: `/app/logs/{AppName}-{Date}.log`
- **Fallback**: `/tmp/logs/{AppName}-{Date}.log`

---

## ?? Quick Start

### Local Development (Windows)
**No changes needed!** Logs automatically write to:
```
C:\Projects\sports-data\logs\SportsData.Api-20251226.log
C:\Projects\sports-data\logs\SportsData.Producer-20251226.log
C:\Projects\sports-data\logs\SportsData.Provider-20251226.log
```

**View logs:**
```powershell
# See all log files
Get-ChildItem C:\Projects\sports-data\logs

# Tail real-time
Get-Content C:\Projects\sports-data\logs\SportsData.Api-20251226.log -Wait -Tail 20
```

### Docker Deployment
**Add volume mount to docker-compose.yml:**
```yaml
services:
  api:
    image: sportsdata.api:latest
    volumes:
      - ./logs:/app/logs  # Mount host ./logs to container /app/logs
```

**Run:**
```bash
docker-compose up -d

# View logs from host
tail -f ./logs/SportsData.Api-*.log
```

---

## ??? Configuration Options

### Option 1: Environment Variable (Recommended for Production)
```bash
docker run \
  -e LOG_PATH=/app/logs/{AppName}-.log \
  sportsdata.api:latest
```

### Option 2: appsettings.json
```json
{
  "Logging": {
    "FilePath": "/custom/path/{AppName}-.log"
  }
}
```

### Option 3: Auto-Detection (Default)
Uses OS detection + environment to determine path automatically.

---

## ?? Current Log Format

```
2025-12-26 04:58:25.052 -05:00 [INF] SportsData.Api.Program Heartbeat published
```

**Template:**
```
{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {SourceContext} {Message}{NewLine}{Exception}
```

**Fields:**
- `Timestamp`: ISO 8601 with timezone
- `Level`: INF, WRN, ERR, etc.
- `SourceContext`: Namespace/class generating log
- `Message`: Log message
- `Exception`: Stack trace (if present)

---

## ?? Priority Order

1. **`LOG_PATH` environment variable** (highest)
2. **`Logging:FilePath` in appsettings**
3. **Auto-detected based on OS + environment**

---

## ?? Docker-Compose Example

```yaml
version: '3.8'

services:
  api:
    build: ./src/SportsData.Api
    volumes:
      - ./logs:/app/logs
    environment:
      - ASPNETCORE_ENVIRONMENT=Production
      - LOG_PATH=/app/logs/{AppName}-.log

  producer:
    build: ./src/SportsData.Producer
    volumes:
      - ./logs:/app/logs

  provider:
    build: ./src/SportsData.Provider
    volumes:
      - ./logs:/app/logs
```

---

## ?? Testing

### Verify Logging Works

**Windows:**
```powershell
# Start application
dotnet run --project src\SportsData.Api

# Check log file created
Test-Path C:\Projects\sports-data\logs\SportsData.Api-*.log

# View last 10 lines
Get-Content C:\Projects\sports-data\logs\SportsData.Api-*.log -Tail 10
```

**Docker:**
```bash
# Start container
docker-compose up -d api

# Verify log directory mounted
docker exec api ls -la /app/logs

# Check logs created
ls -lh ./logs/

# Tail logs
tail -f ./logs/SportsData.Api-*.log
```

---

## ?? Troubleshooting

### "Permission denied" in Docker

**Cause**: Container user lacks write permissions to `/app/logs`

**Fix 1**: Set directory permissions before mounting
```bash
chmod -R 777 ./logs
```

**Fix 2**: Update Dockerfile
```dockerfile
# Add before ENTRYPOINT
RUN mkdir -p /app/logs && chown -R $APP_UID:$APP_UID /app/logs
```

### Logs not persisting after container restart

**Cause**: Missing volume mount

**Fix**: Add to docker-compose.yml
```yaml
volumes:
  - ./logs:/app/logs  # ? Persists to host
```

### Wrong log path in Kubernetes

**Cause**: Default path doesn't match your volume mount

**Fix**: Override with environment variable
```yaml
env:
  - name: LOG_PATH
    value: /mnt/shared-storage/logs/{AppName}-.log
```

---

## ?? Related Files

- **Configuration**: `src/SportsData.Core/DependencyInjection/AppConfiguration.cs`
- **Detailed Docs**: `docs/DOCKER_LOGGING_CONFIGURATION.md`
- **Dockerfiles**: `src/SportsData.*/Dockerfile`

---

## ?? Summary

? **Works on Windows** (Development)  
? **Works on Linux** (Docker/Production)  
? **Automatic path detection**  
? **Environment variable override**  
? **Volume mount support**  
? **Log rotation** (daily, keeps 3 days)  
? **Ready for Copilot feedback loops**

**Your logs ARE working - just need volume mount for Docker persistence!**
