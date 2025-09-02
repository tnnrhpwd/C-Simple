# Quick Backend Setup Guide

## Overview
The C# MAUI app now connects to real backend APIs instead of using sample data. The app automatically switches between development and production environments based on build configuration.

## Environment Configuration

### Development (Debug Build)
- **Backend URL**: `http://localhost:5000/api/data/`
- **Uses**: Local Node.js backend
- **Requirements**: Backend must be running locally

### Production (Release Build)
- **Backend URL**: `https://mern-plan-web-service.onrender.com/api/data/`
- **Uses**: Render-hosted backend
- **Requirements**: Render deployment must be active

## Starting Local Backend (Development)

1. Open terminal in backend directory:
   ```powershell
   cd c:\Users\tanne\Documents\Github\portfolio-app\backend
   ```

2. Install dependencies (first time only):
   ```powershell
   npm install
   ```

3. Start the backend server:
   ```powershell
   npm start
   ```
   
   **Alternative (with auto-restart):**
   ```powershell
   npm run dev
   ```

4. Verify backend is running:
   - Open browser: http://localhost:5000/health
   - Should return JSON response like: `{"status":"OK","timestamp":"..."}`

## Testing the MAUI App

### Debug Mode (Local Backend)
1. Ensure local backend is running (steps above)
2. Build MAUI app in Debug configuration
3. App will automatically connect to `http://localhost:5000`

### Release Mode (Production Backend)  
1. Build MAUI app in Release configuration
2. App will automatically connect to Render backend
3. Verify Render deployment is active

## Troubleshooting

### "No plans loaded" Message
- **Development**: Check if local backend is running on port 5000
- **Production**: Verify Render deployment status and URL

### "Server returned HTML instead of JSON"
This typically indicates:
- **Development**: Local backend not running or wrong port
- **Production**: Render deployment issue or incorrect URL in `BackendConfigService.cs`

### Environment Verification
The app logs which environment and URL it's using:
```
[BackendConfig] Environment: Development
[BackendConfig] Base URL: http://localhost:5000/api/data/
```

## Backend URLs

### Current Configuration (BackendConfigService.cs)
- **Development**: `http://localhost:5000/api/data/`
- **Production**: `https://mern-plan-web-service.onrender.com/api/data/`

### To Update Production URL
1. Edit `c:\Users\tanne\Documents\Github\C-Simple\src\CSimple\Services\BackendConfigService.cs`
2. Update `ProductionBaseUrl` constant
3. Rebuild app

## Key Features

### Automatic Environment Detection
- Debug builds → Development environment → localhost:5000
- Release builds → Production environment → Render backend

### Comprehensive Error Handling  
- Connection timeouts
- HTML vs JSON detection
- User-friendly error messages with next steps

### Backend Connectivity Testing
- Built-in diagnostic tools
- Health checks with detailed feedback
- Environment-specific troubleshooting guidance

## Sample Plan Data Removed
- App no longer shows sample/demo plans
- All data now comes from real backend APIs
- Empty state shows appropriate messaging when backend unavailable
