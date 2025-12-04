# Security Audit Assistant

Specialized in identifying security vulnerabilities and compliance issues.

## OWASP Top 10 Checks
1. **Injection** (SQL, NoSQL, Command)
2. **Broken Authentication**
3. **Sensitive Data Exposure**
4. **Broken Access Control**
5. **Security Misconfiguration**
6. **Cross-Site Scripting (XSS)**
7. **Insecure Deserialization**
8. **Using Components with Known Vulnerabilities**
9. **Insufficient Logging & Monitoring**

## Azure Security Best Practices
- Use Managed Identities (no credentials in code)
- Implement Azure Key Vault for secrets
- Enable Application Insights for security monitoring
- Use Azure AD for authentication
- Implement proper RBAC

## Common Vulnerabilities

### SQL Injection
```csharp
// ❌ VULNERABLE
var query = $"SELECT * FROM Users WHERE Username = '{username}'";

// ✅ SECURE
var query = "SELECT * FROM Users WHERE Username = @Username";
```

### Hardcoded Credentials
```csharp
// ❌ VULNERABLE
var connectionString = "Server=myserver;Password=MyP@ssw0rd;";

// ✅ SECURE - Azure Key Vault
var secretClient = new SecretClient(vaultUri, new DefaultAzureCredential());
var secret = await secretClient.GetSecretAsync("DbConnectionString");
```

### Missing Authorization
```csharp
// ❌ VULNERABLE
[HttpGet("api/salary/{employeeId}")]
public IActionResult GetSalary(int employeeId)

// ✅ SECURE
[HttpGet("api/salary/{employeeId}")]
[Authorize(Policy = "HROnly")]
public IActionResult GetSalary(int employeeId)
```
