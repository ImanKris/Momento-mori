# Deployment Readiness

## Current Status
Project is ready for deployment. Build succeeds locally.

## Requirements for Somee.com Hosting

### Database
- **Connection**: SQL Server via Entity Framework Core
- **Connection string**: Configured in `appsettings.json` / environment variables
- **Migration**: Run `dotnet ef database update` after deployment
- **Note**: Full SQL BACKUP DATABASE command is NOT available on shared hosting

### Application
- **Framework**: ASP.NET Core 8.0 MVC
- **Target**: Self-contained or framework-dependent (shared hosting typically uses framework-dependent)
- **Output**: `RoboStore.dll` as entry point

### Required Files for Deployment
```
/RoboStore
  /bin/Release/net8.0/publish/
    - *.dll (application binaries)
    - *.json (appsettings)
    - wwwroot/ (static files)
    - Views/ (MVC views)
```

## Domain Setup (when ready)

### Option 1: Somee Subdomain (free)
- Default: `yourapp.somee.com`
- No additional configuration needed

### Option 2: Custom Domain
1. Register domain at any registrar
2. Configure DNS:
   - A record: Points to Somee server IP
   - CNAME: `www` -> `@` or Somee forwarding
3. Update Somee hosting settings with custom domain
4. Enable HTTPS (usually via Let's Encrypt on Somee)

### DNS Propagation
- Allow 24-48 hours for full propagation
- Use `dig yourdomain.com` or online DNS checkers to verify

## Pre-Deployment Checklist

- [ ] Set `ASPNETCORE_ENVIRONMENT=Production`
- [ ] Configure production connection string
- [ ] Run database migrations
- [ ] Test locally with production-like settings
- [ ] Verify HTTPS works on Somee
- [ ] Set up monitoring/logging for production errors

## Current Limitations
- No direct SQL Server backup (use JSON export in /Admin/Backup instead)
- No SSH access (shared hosting constraint)
- No custom background services