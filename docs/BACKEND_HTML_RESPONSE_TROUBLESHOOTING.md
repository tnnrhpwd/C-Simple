# Backend HTML Response Troubleshooting Guide

## Issue: "Server returned HTML instead of JSON"

When the C# MAUI client makes API requests to `https://www.sthopwood.com/api/data/`, it receives HTML content instead of the expected JSON response, causing `JsonException` errors.

## Root Cause Analysis

The backend API is properly implemented with:
- ✅ JWT authentication middleware (`protect`)
- ✅ JSON error responses for authentication failures
- ✅ Proper route handling for `/api/data/` endpoints
- ✅ CORS configuration for cross-origin requests

However, the web server configuration is likely serving the React frontend HTML instead of routing requests to the backend API.

## Common Causes

### 1. Web Server Routing Issue (Most Likely)
- **Problem**: nginx/Apache is configured to serve the frontend React app for all routes, including API routes
- **Result**: Instead of forwarding `/api/data/` requests to the backend server, it serves the React app's `index.html`

### 2. Backend Server Not Running
- **Problem**: The Node.js backend server may not be running or accessible
- **Result**: Web server falls back to serving static files (frontend)

### 3. Proxy Configuration
- **Problem**: Reverse proxy is not properly configured to forward API requests
- **Result**: API requests are handled by the frontend server instead of backend

## Backend Code Analysis

The backend correctly handles authentication errors:

```javascript
// In authMiddleware.js - Returns JSON errors
if (!token) { 
  console.log('No authorization token provided')
  res.status(401)
  res.json({ dataMessage: 'Not authorized, no token' });
}
```

```javascript
// In getHashData.js - Proper error handling
if (!req.user) {
  res.status(401);
  throw new Error('User not found');
}
```

## Solutions

### Solution 1: Fix Web Server Configuration (Recommended)

**For nginx:**
```nginx
server {
    listen 80;
    server_name www.sthopwood.com;
    
    # API routes - forward to backend
    location /api/ {
        proxy_pass http://localhost:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection 'upgrade';
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_cache_bypass $http_upgrade;
    }
    
    # Frontend routes - serve React app
    location / {
        root /var/www/frontend;
        try_files $uri $uri/ /index.html;
    }
}
```

**For Apache (.htaccess):**
```apache
# API routes - forward to backend
RewriteEngine On
RewriteRule ^api/(.*)$ http://localhost:5000/api/$1 [P,L]

# Frontend routes - serve React app
RewriteCond %{REQUEST_FILENAME} !-f
RewriteCond %{REQUEST_FILENAME} !-d
RewriteRule . /index.html [L]
```

### Solution 2: Verify Backend Server Status

1. **Check if backend is running:**
   ```bash
   curl -I https://www.sthopwood.com/health
   ```

2. **Check backend logs:**
   ```bash
   pm2 logs backend  # if using PM2
   # or
   tail -f /var/log/backend.log
   ```

3. **Test backend directly:**
   ```bash
   curl -X GET "https://www.sthopwood.com/api/data/?data={\"text\":\"Plan\"}" \
   -H "Authorization: Bearer YOUR_TOKEN" \
   -H "Content-Type: application/json"
   ```

### Solution 3: Update Backend Base URL (Temporary Fix)

If you have access to a direct backend port, update the C# client:

```csharp
// In DataService.cs
private const string BaseUrl = "https://www.sthopwood.com:5000/api/data/";
// OR if backend is on a different subdomain:
private const string BaseUrl = "https://api.sthopwood.com/api/data/";
```

### Solution 4: Docker Configuration Fix

If using Docker Compose, ensure proper networking:

```yaml
version: '3.8'
services:
  backend:
    build: ./backend
    ports:
      - "5000:5000"
    networks:
      - app-network
  
  frontend:
    build: ./frontend
    ports:
      - "80:80"
    depends_on:
      - backend
    networks:
      - app-network

networks:
  app-network:
    driver: bridge
```

## Client-Side Enhancements

The C# client now includes enhanced debugging to identify this issue:

```csharp
// Enhanced logging in DataService.cs
Debug.WriteLine($"Response Status Code: {response.StatusCode}");
Debug.WriteLine($"Response Content Type: {response.Content.Headers.ContentType?.MediaType}");

if (responseContent.TrimStart().StartsWith("<"))
{
    Debug.WriteLine("⚠️ BACKEND ISSUE: Server returned HTML instead of JSON!");
    Debug.WriteLine("This typically indicates:");
    Debug.WriteLine("1. Web server is serving frontend instead of routing to backend API");
    Debug.WriteLine("2. Backend API server is not running or not accessible");
    Debug.WriteLine("3. API route configuration is incorrect");
}
```

## Testing the Fix

1. **Check API routing:**
   ```bash
   curl -v https://www.sthopwood.com/api/data/health
   ```

2. **Test authentication endpoint:**
   ```bash
   curl -X POST https://www.sthopwood.com/api/data/login \
   -H "Content-Type: application/json" \
   -d '{"email":"test@example.com","password":"test"}'
   ```

3. **Verify response headers:**
   - Should return `Content-Type: application/json`
   - Should NOT return HTML content

## Prevention

1. **Separate domains:** Use `api.sthopwood.com` for backend and `www.sthopwood.com` for frontend
2. **Health checks:** Implement monitoring to detect when API returns HTML
3. **Integration tests:** Add tests that verify API responses are JSON
4. **Deployment verification:** Always test API endpoints after deployment

## Related Files

- **Backend API:** `portfolio-app/backend/routes/routeData.js`
- **Authentication:** `portfolio-app/backend/middleware/authMiddleware.js`
- **C# Client:** `C-Simple/src/CSimple/Services/DataService.cs`
- **Error Handling:** `C-Simple/src/CSimple/Pages/PlanPage.xaml.cs`

---

**Status:** The C# client now gracefully handles this backend configuration issue by detecting HTML responses and falling back to cached data while providing clear debugging information to identify the server-side problem.
