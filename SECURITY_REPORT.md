# Security Vulnerability Report

**Project:** DotNetSample
**Scan Date:** 2025-11-30
**Scanner:** Security Agent (SAST, DAST, Dependency Scan)
**Status:** 🔴 **CRITICAL ISSUES FOUND**

---

## Executive Summary

**Total Vulnerabilities Found:** 11
- 🔴 **Critical:** 3
- 🟠 **High:** 4
- 🟡 **Medium:** 3
- 🔵 **Low:** 1

**Risk Level:** HIGH - Immediate action required before production deployment

---

## Critical Vulnerabilities

### [CRITICAL] Vulnerable Dependencies - System.Text.Json & Microsoft.Extensions.Caching.Memory

**Type:** Dependency Scan
**Severity:** CRITICAL
**CVE:** Multiple high-severity advisories

**Affected Projects:**
- Api (net8.0)
- Infrastructure (net8.0)
- Worker (net8.0)

**Vulnerable Packages:**
```
Microsoft.Extensions.Caching.Memory 8.0.0
  └─ Advisory: GHSA-qj66-m88j-hmgj
  └─ Severity: High

System.Text.Json 8.0.0
  └─ Advisory: GHSA-hh2w-p6rv-4g7w (High)
  └─ Advisory: GHSA-8g4q-xg66-9fp4 (High)
```

**Impact:**
- Potential for denial of service attacks
- Information disclosure
- Memory corruption vulnerabilities
- Remote code execution in worst case scenarios

**Remediation:**
Update all .NET 8.0 packages to latest stable version (10.0.0):

```bash
# Update all packages
dotnet add Api package Microsoft.AspNetCore.OpenApi --version 10.0.0
dotnet add Api package Microsoft.EntityFrameworkCore.Design --version 10.0.0
dotnet add Api package Swashbuckle.AspNetCore --version 10.0.1

dotnet add Infrastructure package Microsoft.EntityFrameworkCore --version 10.0.0
dotnet add Infrastructure package Microsoft.EntityFrameworkCore.Design --version 10.0.0
dotnet add Infrastructure package Microsoft.EntityFrameworkCore.Sqlite --version 10.0.0
dotnet add Infrastructure package Microsoft.Extensions.Logging.Abstractions --version 10.0.0

dotnet add Worker package Microsoft.Extensions.Hosting --version 10.0.0

# Verify no vulnerabilities remain
dotnet list package --vulnerable --include-transitive
```

**References:**
- https://github.com/advisories/GHSA-qj66-m88j-hmgj
- https://github.com/advisories/GHSA-hh2w-p6rv-4g7w
- https://github.com/advisories/GHSA-8g4q-xg66-9fp4

---

### [CRITICAL] No Authentication or Authorization

**Type:** SAST
**Severity:** CRITICAL
**Location:** All API endpoints in `Api/Controllers/*.cs`

**Description:**
No authentication or authorization is implemented on any API endpoints. All endpoints are publicly accessible without any access controls.

**Impact:**
- Anyone can access, create, modify, or delete all data
- No user identity tracking
- No access control or permissions
- Complete data breach risk
- Compliance violations (GDPR, HIPAA, PCI-DSS)

**Vulnerable Endpoints:**
```
GET    /api/customers
POST   /api/customers
PUT    /api/customers/{id}
DELETE /api/customers/{id}

GET    /api/products
POST   /api/products
PUT    /api/products/{id}
DELETE /api/products/{id}

GET    /api/orders
POST   /api/orders
DELETE /api/orders/{id}
POST   /api/orders/reprocess-pending
```

**Proof of Concept:**
```bash
# Anyone can delete all customers
curl -X DELETE http://localhost:5181/api/customers/{any-id}

# Anyone can create orders for any customer
curl -X POST http://localhost:5181/api/orders \
  -H "Content-Type: application/json" \
  -d '{"customerId":"any-guid","items":[...]}'
```

**Remediation:**

**Step 1:** Add Authentication (JWT Bearer)

File: `Api/Program.cs`
```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;

// Add authentication
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
        };
    });

builder.Services.AddAuthorization();

// After app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();
```

**Step 2:** Add [Authorize] attributes

File: `Api/Controllers/OrdersController.cs` (example)
```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]  // Require authentication for all endpoints
public class OrdersController : ControllerBase
{
    // Endpoints now require valid JWT token
}
```

**Step 3:** Verify user ownership

```csharp
[HttpGet("{id}")]
[Authorize]
public async Task<ActionResult<Order>> Get(Guid id)
{
    var order = await _orderRepo.GetAsync(id);
    if (order == null) return NotFound();

    // Verify user owns this order
    var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
    if (order.CustomerId.ToString() != userId)
        return Forbid();

    return order;
}
```

**References:**
- [OWASP A01:2021 – Broken Access Control](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- [CWE-862: Missing Authorization](https://cwe.mitre.org/data/definitions/862.html)

---

### [CRITICAL] Insecure Direct Object References (IDOR)

**Type:** SAST
**Severity:** CRITICAL
**Location:** All GET/PUT/DELETE endpoints with {id} parameter

**Description:**
All endpoints accept arbitrary GUIDs without verifying ownership. Users can access or modify resources belonging to other users.

**Impact:**
- Unauthorized data access
- Data manipulation by unauthorized users
- Privacy violations
- Compliance violations

**Vulnerable Code:**

File: `Api/Controllers/CustomersController.cs:60-66`
```csharp
[HttpGet("{id}")]
public async Task<ActionResult<Customer>> Get(Guid id)
{
    var c = await _repo.GetAsync(id);
    if (c == null) return NotFound();
    return c;  // No ownership check!
}
```

**Proof of Concept:**
```bash
# User A can access User B's customer data
curl http://localhost:5181/api/customers/{user-b-guid}

# User A can modify User B's order
curl -X PUT http://localhost:5181/api/orders/{user-b-order-id}
```

**Remediation:**
See authentication section above - implement [Authorize] and ownership checks on all resource endpoints.

**References:**
- [OWASP A01:2021 – Broken Access Control](https://owasp.org/Top10/A01_2021-Broken_Access_Control/)
- [CWE-639: Authorization Bypass Through User-Controlled Key](https://cwe.mitre.org/data/definitions/639.html)

---

## High Severity Vulnerabilities

### [HIGH] Missing Security Headers

**Type:** SAST
**Severity:** HIGH
**Location:** `Api/Program.cs`

**Description:**
No security headers are configured. Application is vulnerable to clickjacking, MIME sniffing, and other client-side attacks.

**Missing Headers:**
- `X-Content-Type-Options: nosniff`
- `X-Frame-Options: DENY`
- `X-XSS-Protection: 1; mode=block`
- `Referrer-Policy: no-referrer`
- `Content-Security-Policy`
- `Strict-Transport-Security` (HSTS)

**Impact:**
- Clickjacking attacks
- MIME type sniffing attacks
- Cross-site scripting (XSS) in older browsers
- Information leakage via Referer header
- Man-in-the-middle attacks (no HSTS)

**Remediation:**

File: `Api/Program.cs` (add after `var app = builder.Build();`)
```csharp
// Add security headers middleware
app.Use(async (context, next) =>
{
    context.Response.Headers.Add("X-Content-Type-Options", "nosniff");
    context.Response.Headers.Add("X-Frame-Options", "DENY");
    context.Response.Headers.Add("X-XSS-Protection", "1; mode=block");
    context.Response.Headers.Add("Referrer-Policy", "no-referrer");
    context.Response.Headers.Add("Content-Security-Policy",
        "default-src 'self'; frame-ancestors 'none'");

    if (context.Request.IsHttps)
    {
        context.Response.Headers.Add("Strict-Transport-Security",
            "max-age=31536000; includeSubDomains; preload");
    }

    await next();
});
```

**References:**
- [OWASP Secure Headers Project](https://owasp.org/www-project-secure-headers/)
- [CWE-693: Protection Mechanism Failure](https://cwe.mitre.org/data/definitions/693.html)

---

### [HIGH] No HTTPS Enforcement

**Type:** SAST
**Severity:** HIGH
**Location:** `Api/Program.cs`

**Description:**
Application does not enforce HTTPS. HTTP traffic is accepted, exposing data to man-in-the-middle attacks.

**Impact:**
- Credentials transmitted in cleartext
- Session tokens intercepted
- Data manipulation in transit
- Man-in-the-middle attacks

**Remediation:**

File: `Api/Program.cs` (add after `var app = builder.Build();`)
```csharp
// Force HTTPS redirection
app.UseHttpsRedirection();

// Add HSTS (after UseHttpsRedirection)
app.UseHsts();
```

File: `Api/appsettings.Production.json`
```json
{
  "Kestrel": {
    "EndpointDefaults": {
      "Protocols": "Http2"
    }
  },
  "AllowedHosts": "*",
  "ForceHttps": true
}
```

**References:**
- [OWASP A02:2021 – Cryptographic Failures](https://owasp.org/Top10/A02_2021-Cryptographic_Failures/)
- [CWE-319: Cleartext Transmission of Sensitive Information](https://cwe.mitre.org/data/definitions/319.html)

---

### [HIGH] No Input Validation

**Type:** SAST
**Severity:** HIGH
**Location:** All controller endpoints

**Description:**
No input validation attributes or model validation checks are implemented. Application accepts any input without validation.

**Impact:**
- Mass assignment vulnerabilities
- Database constraint violations
- Application crashes
- Business logic bypass
- Data integrity issues

**Vulnerable Code:**
```csharp
// No validation on input
[HttpPost]
public async Task<ActionResult> Post(Customer customer)
{
    customer.Id = Guid.NewGuid();
    await _repo.AddAsync(customer);  // No validation!
    return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer);
}
```

**Remediation:**

**Step 1:** Create DTOs with validation

File: `Core/DTOs/CreateCustomerRequest.cs`
```csharp
using System.ComponentModel.DataAnnotations;

public class CreateCustomerRequest
{
    [Required(ErrorMessage = "Name is required")]
    [StringLength(100, MinimumLength = 2)]
    public string Name { get; set; }

    [Required(ErrorMessage = "Email is required")]
    [EmailAddress(ErrorMessage = "Invalid email format")]
    [StringLength(255)]
    public string Email { get; set; }
}
```

**Step 2:** Use DTOs in controllers

```csharp
[HttpPost]
public async Task<ActionResult> Post(CreateCustomerRequest request)
{
    if (!ModelState.IsValid)
        return BadRequest(ModelState);

    var customer = new Customer
    {
        Id = Guid.NewGuid(),
        Name = request.Name,
        Email = request.Email
    };

    await _repo.AddAsync(customer);
    return CreatedAtAction(nameof(Get), new { id = customer.Id }, customer);
}
```

**References:**
- [OWASP A03:2021 – Injection](https://owasp.org/Top10/A03_2021-Injection/)
- [CWE-20: Improper Input Validation](https://cwe.mitre.org/data/definitions/20.html)

---

### [HIGH] No Rate Limiting

**Type:** SAST
**Severity:** HIGH
**Location:** `Api/Program.cs`

**Description:**
No rate limiting is configured. API is vulnerable to brute force, credential stuffing, and denial of service attacks.

**Impact:**
- Brute force attacks on authentication endpoints
- Credential stuffing
- API abuse
- Denial of service (resource exhaustion)
- Increased infrastructure costs

**Remediation:**

File: `Api/Program.cs`
```csharp
using System.Threading.RateLimiting;

// Add rate limiting
builder.Services.AddRateLimiter(options =>
{
    options.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
        RateLimitPartition.GetFixedWindowLimiter(
            partitionKey: context.User.Identity?.Name ?? context.Request.Headers.Host.ToString(),
            factory: partition => new FixedWindowRateLimiterOptions
            {
                AutoReplenishment = true,
                PermitLimit = 100,
                QueueLimit = 0,
                Window = TimeSpan.FromMinutes(1)
            }));

    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        await context.HttpContext.Response.WriteAsync(
            "Too many requests. Please try again later.", token);
    };
});

// Use rate limiter (after app.UseAuthorization();)
app.UseRateLimiter();
```

**References:**
- [OWASP API Security Top 10 - API4:2023 Unrestricted Resource Consumption](https://owasp.org/API-Security/editions/2023/en/0xa4-unrestricted-resource-consumption/)
- [CWE-770: Allocation of Resources Without Limits](https://cwe.mitre.org/data/definitions/770.html)

---

## Medium Severity Vulnerabilities

### [MEDIUM] Swagger Exposed in Production

**Type:** SAST
**Severity:** MEDIUM
**Location:** `Api/Program.cs:37-41`

**Status:** ✅ CORRECTLY CONFIGURED (Swagger only enabled in Development)

**Code Review:**
```csharp
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

**Recommendation:**
Current configuration is secure. Swagger is properly restricted to Development environment only.

**Additional Hardening:**
Consider adding explicit production check:
```csharp
if (!app.Environment.IsProduction())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
```

---

### [MEDIUM] Outdated Dependencies

**Type:** Dependency Scan
**Severity:** MEDIUM
**Location:** All .csproj files

**Description:**
Multiple packages are 2+ major versions behind latest stable releases.

**Outdated Packages:**
```
Api:
  Microsoft.AspNetCore.OpenApi: 8.0.0 → 10.0.0
  Microsoft.EntityFrameworkCore.Design: 8.0.0 → 10.0.0
  Swashbuckle.AspNetCore: 6.5.0 → 10.0.1

Infrastructure:
  Microsoft.EntityFrameworkCore: 8.0.0 → 10.0.0
  Microsoft.EntityFrameworkCore.Sqlite: 8.0.0 → 10.0.0

Tests:
  coverlet.collector: 6.0.2 → 6.0.4
  Microsoft.NET.Test.Sdk: 17.12.0 → 18.0.1
  xunit: 2.9.2 → 2.9.3
  xunit.runner.visualstudio: 2.8.2 → 3.1.5
```

**Impact:**
- Missing security patches
- Missing performance improvements
- Potential compatibility issues
- Increased technical debt

**Remediation:**
Update all packages to latest stable versions (see Critical vulnerability remediation above).

---

### [MEDIUM] SQLite in Production

**Type:** SAST / Configuration
**Severity:** MEDIUM
**Location:** `Infrastructure/AppDbContext.cs`, `Api/appsettings.json`

**Description:**
SQLite is configured as the database. While acceptable for development, SQLite has limitations for production workloads.

**Limitations:**
- Single writer (no concurrent writes)
- No network access
- Limited scalability
- No built-in replication
- File-based only

**Impact:**
- Performance degradation under load
- Data loss risk (single file)
- Scalability limitations
- High availability challenges

**Remediation:**
For production, migrate to PostgreSQL, SQL Server, or MySQL:

```csharp
// Api/Program.cs - Production configuration
#if RELEASE
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Default")));
#else
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseSqlite(builder.Configuration.GetConnectionString("Default") ?? "Data Source=app.db"));
#endif
```

**References:**
- See `docs/DATABASE.md` for migration guide

---

## Low Severity Vulnerabilities

### [LOW] AllowedHosts Too Permissive

**Type:** SAST / Configuration
**Severity:** LOW
**Location:** `Api/appsettings.json:8`

**Description:**
`AllowedHosts` is set to `"*"` which allows any host header.

**Code:**
```json
{
  "AllowedHosts": "*"
}
```

**Impact:**
- Host header injection
- Cache poisoning
- Password reset poisoning

**Remediation:**

File: `Api/appsettings.Production.json`
```json
{
  "AllowedHosts": "yourdomain.com;www.yourdomain.com;api.yourdomain.com"
}
```

**References:**
- [CWE-644: Improper Neutralization of HTTP Headers for Scripting Syntax](https://cwe.mitre.org/data/definitions/644.html)

---

## Positive Security Findings ✅

The following security best practices were observed:

1. ✅ **No Raw SQL Queries** - All database access uses EF Core LINQ (prevents SQL injection)
2. ✅ **No Hard-Coded Secrets** - No passwords, API keys, or secrets in source code
3. ✅ **No Insecure CORS** - No `AllowAnyOrigin()` configuration
4. ✅ **No Sensitive Data Logging** - Logger statements don't expose passwords or tokens
5. ✅ **Swagger Properly Restricted** - Only enabled in Development environment
6. ✅ **Clean Architecture** - Good separation of concerns
7. ✅ **Parameterized Queries** - EF Core uses parameterized queries by default

---

## Remediation Priority

### Immediate Action Required (Before Production)

1. 🔴 **Update all vulnerable dependencies** (30 minutes)
2. 🔴 **Implement authentication & authorization** (4-8 hours)
3. 🔴 **Add IDOR protection with ownership checks** (2-4 hours)
4. 🟠 **Add security headers** (15 minutes)
5. 🟠 **Enable HTTPS enforcement** (15 minutes)
6. 🟠 **Implement input validation with DTOs** (2-4 hours)
7. 🟠 **Add rate limiting** (30 minutes)

### Medium Priority (Production Readiness)

8. 🟡 **Migrate to production database** (PostgreSQL/SQL Server)
9. 🟡 **Configure specific AllowedHosts**
10. 🟡 **Set up monitoring and logging**
11. 🟡 **Implement proper error handling**

---

## Security Testing Recommendations

### Automated Testing (CI/CD)

Add to `.github/workflows/security.yml`:
```yaml
- name: Dependency Scan
  run: dotnet list package --vulnerable --include-transitive

- name: SAST
  run: |
    dotnet add Api package SecurityCodeScan.VS2019
    dotnet build /p:TreatWarningsAsErrors=true

- name: Security Headers Check
  run: |
    dotnet run --project Api &
    sleep 5
    curl -I http://localhost:5181/api/customers | grep -E "X-Frame-Options|X-Content-Type-Options"
```

### Manual Testing

1. **OWASP ZAP Scan:**
```bash
docker run -t owasp/zap2docker-stable zap-baseline.py -t http://localhost:5181
```

2. **Authentication Testing:**
```bash
# Try accessing protected endpoints without token
curl http://localhost:5181/api/orders

# Try accessing other users' resources
curl http://localhost:5181/api/customers/{other-user-id}
```

3. **Input Validation Testing:**
```bash
# Test XSS
curl -X POST http://localhost:5181/api/customers \
  -d '{"name":"<script>alert(1)</script>","email":"test@test.com"}'

# Test SQL injection (should fail safely with EF Core)
curl http://localhost:5181/api/customers/1%27%20OR%20%271%27%3D%271
```

---

## Compliance Impact

### GDPR Compliance Issues
- ❌ No access controls (Article 32: Security of processing)
- ❌ No data encryption in transit (Article 32)
- ❌ No audit logging (Article 30: Records of processing)

### PCI-DSS Compliance Issues
- ❌ No authentication (Requirement 7: Restrict access)
- ❌ No encryption (Requirement 4: Encrypt transmission)
- ❌ No logging (Requirement 10: Track access)

### HIPAA Compliance Issues
- ❌ No access controls (§164.312(a)(1))
- ❌ No encryption (§164.312(e)(1))
- ❌ No audit controls (§164.312(b))

---

## Conclusion

The DotNetSample application has **critical security vulnerabilities** that must be addressed before any production deployment. While the code follows good architectural patterns and avoids some common pitfalls (SQL injection, hard-coded secrets), it lacks fundamental security controls.

**Estimated Remediation Time:** 12-20 hours of development + testing

**Next Steps:**
1. Review this report with the development team
2. Prioritize critical and high severity fixes
3. Implement fixes following the remediation guidance
4. Re-scan after fixes are applied
5. Conduct penetration testing before production release

---

**Report Generated By:** Security Agent v1.0
**Contact:** security@dotnetsample.com
**Next Scan:** Schedule after remediation
