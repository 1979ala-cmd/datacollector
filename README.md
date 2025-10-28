# Data Collector Platform - Enterprise Multi-Tenant API

A production-ready .NET 8 LTS backend API for an enterprise multi-tenant data collector platform with per-tenant database isolation, comprehensive approval workflows, and enterprise-grade security.

## 🏗️ Architecture Overview

```
┌─────────────────────────────────────────────────────────────────┐
│                         API Gateway / Load Balancer              │
└────────────────────────────┬────────────────────────────────────┘
                             │
             ┌───────────────┴───────────────┐
             │   DataCollector.API (NET 8)   │
             │  - Controllers                 │
             │  - JWT Auth Middleware         │
             │  - Tenant Resolution           │
             └───────────────┬───────────────┘
                             │
        ┌────────────────────┼────────────────────┐
        │                    │                    │
   ┌────▼────┐         ┌────▼────┐         ┌────▼────┐
   │ Domain  │         │  App    │         │ Infra   │
   │ Entities│◄────────│ Services│◄────────│Postgres │
   │ & Logic │         │ & DTOs  │         │ Multi-DB│
   └─────────┘         └─────────┘         └────┬────┘
                                                 │
                  ┌──────────────────────────────┴───────────┐
                  │                                          │
            ┌─────▼──────┐              ┌────────▼────────┐
            │  Shared DB  │              │ Tenant DBs      │
            │  - Auth     │              │ - tenant_acme   │
            │  - Tenants  │              │ - tenant_contoso│
            │  - Users    │              │ (Auto-created)  │
            └─────────────┘              └─────────────────┘
```

### Multi-Tenant Database Strategy
- **Shared Database**: Stores authentication, users, roles, and tenant metadata
- **Per-Tenant Databases**: Each tenant gets a separate PostgreSQL database created automatically on onboarding
- **Tenant Resolution**: Via JWT claim, HTTP header (`X-Tenant-ID`), or subdomain
- **Data Isolation**: Complete separation ensuring no cross-tenant data access

## 🚀 Quick Start

### Prerequisites
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
- [Docker Desktop](https://www.docker.com/products/docker-desktop)
- [PostgreSQL Client](https://www.postgresql.org/download/) (optional, for debugging)

### Local Development with Docker

1. **Clone and build**:
```bash
git clone <repository-url>
cd DataCollectorPlatform
docker-compose up --build
```

2. **Access the API**:
   - API: http://localhost:5000
   - Swagger UI: http://localhost:5000/swagger
   - Health Check: http://localhost:5000/health

3. **Database Access**:
   - Host: localhost:5432
   - User: postgres
   - Password: postgres123
   - Shared DB: datacollector_shared

### Manual Local Development

1. **Start PostgreSQL**:
```bash
docker run --name postgres-dc -e POSTGRES_PASSWORD=postgres123 -p 5432:5432 -d postgres:16
```

2. **Update connection strings** in `appsettings.Development.json`

3. **Run migrations**:
```bash
cd src/DataCollector.API
dotnet ef database update --context SharedDbContext
```

4. **Run the API**:
```bash
dotnet run --project src/DataCollector.API
```

## 📋 Complete Workflow Example

### 1. Onboard a New Tenant

```bash
curl -X POST http://localhost:5000/api/tenants \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Acme Corporation",
    "adminEmail": "admin@acme.com",
    "adminPassword": "SecurePass123!",
    "slug": "acme"
  }'
```

**Response**:
```json
{
  "tenantId": "550e8400-e29b-41d4-a716-446655440000",
  "name": "Acme Corporation",
  "slug": "acme",
  "databaseName": "tenant_acme",
  "status": "Active",
  "adminUserId": "660e8400-e29b-41d4-a716-446655440001",
  "createdAt": "2024-10-28T10:30:00Z"
}
```

### 2. Login as Tenant Admin

```bash
curl -X POST http://localhost:5000/api/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "admin@acme.com",
    "password": "SecurePass123!"
  }'
```

**Response**:
```json
{
  "accessToken": "eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...",
  "refreshToken": "8f7d9a2b-1c3e-4f5a-9b8c-7d6e5f4a3b2c",
  "expiresIn": 3600,
  "tokenType": "Bearer",
  "user": {
    "id": "660e8400-e29b-41d4-a716-446655440001",
    "email": "admin@acme.com",
    "roles": ["Admin"]
  }
}
```

### 3. Create a DataSource

```bash
curl -X POST http://localhost:5000/api/datasources \
  -H "Authorization: Bearer {token}" \
  -H "X-Tenant-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "CRM REST API",
    "description": "Customer Relationship Management System API",
    "type": "REST",
    "protocol": "REST",
    "endpoint": "https://api.crm.example.com/v1",
    "authType": "ApiKey",
    "authConfig": {
      "apiKey": "sk_live_xxx",
      "location": "header",
      "parameterName": "X-API-Key"
    },
    "category": "CRM",
    "tags": ["crm", "customers"]
  }'
```

### 4. Create a DataCollector with Pipelines

```bash
curl -X POST http://localhost:5000/api/collectors \
  -H "Authorization: Bearer {token}" \
  -H "X-Tenant-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Content-Type: application/json" \
  -d '{
    "name": "Customer Data Collector",
    "description": "Collects customer data from CRM",
    "pipelines": [
      {
        "name": "Customer Sync Pipeline",
        "dataSourceId": "{datasource-id}",
        "apiPath": "/customers",
        "method": "GET",
        "processingSteps": [
          {
            "type": "api-call",
            "name": "Fetch Customers",
            "enabled": true
          },
          {
            "type": "pagination",
            "name": "Paginate Results",
            "enabled": true,
            "config": {
              "type": "offset",
              "pageSize": 100,
              "maxPages": 10
            }
          },
          {
            "type": "transform",
            "name": "Transform Data",
            "enabled": true
          },
          {
            "type": "store-database",
            "name": "Store to DB",
            "enabled": true
          }
        ]
      }
    ]
  }'
```

### 5. Submit for Approval

```bash
curl -X POST http://localhost:5000/api/collectors/{collector-id}/submit \
  -H "Authorization: Bearer {token}" \
  -H "X-Tenant-ID: 550e8400-e29b-41d4-a716-446655440000"
```

### 6. Approve to Production (as Approver role)

```bash
curl -X POST http://localhost:5000/api/approvals/{collector-id}/approve \
  -H "Authorization: Bearer {approver-token}" \
  -H "X-Tenant-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Content-Type: application/json" \
  -d '{
    "targetStage": "PRODUCTION",
    "comment": "All security checks passed. Approved for production deployment."
  }'
```

### 7. Execute a Pipeline

```bash
curl -X POST http://localhost:5000/api/collectors/{collector-id}/execute \
  -H "Authorization: Bearer {token}" \
  -H "X-Tenant-ID: 550e8400-e29b-41d4-a716-446655440000" \
  -H "Content-Type: application/json" \
  -d '{
    "pipelineId": "{pipeline-id}",
    "parameters": {}
  }'
```

## 🔐 Authentication & Authorization

### Roles
- **Admin**: Full system access, tenant management
- **ProductOwner**: Create/manage collectors and datasources
- **Approver**: Approve/reject collector promotions
- **Developer**: Create and test collectors in dev environment
- **Collector**: Execute data collection jobs
- **Reader**: Read-only access to collectors and data

### JWT Token Structure
```json
{
  "sub": "user-id",
  "email": "user@example.com",
  "tenantId": "tenant-id",
  "roles": ["Admin", "ProductOwner"],
  "exp": 1698504000,
  "iss": "DataCollectorPlatform",
  "aud": "DataCollectorPlatform"
}
```

### Refresh Token Flow
1. Access token expires after 1 hour
2. Use refresh token to get new access token
3. Refresh tokens valid for 7 days
4. Automatic rotation on each refresh

## 🗄️ Database Schema

### Shared Database Tables
- `tenants` - Tenant information and metadata
- `users` - User accounts across all tenants
- `user_roles` - User-role assignments
- `roles` - Available system roles
- `refresh_tokens` - Active refresh tokens
- `audit_logs` - System-wide audit trail

### Tenant-Specific Tables (per tenant DB)
- `data_sources` - Data source configurations
- `data_collectors` - Collector definitions
- `pipelines` - Pipeline configurations
- `processing_steps` - Step definitions
- `approval_workflows` - Approval state and history
- `approval_templates` - Approval requirement templates
- `execution_logs` - Pipeline execution history
- `collected_data` - Actual collected data

### Enterprise Security Constraints Applied
- **Row-level timestamps**: created_at, updated_at, created_by, updated_by
- **Soft deletes**: deleted_at, deleted_by for audit trail
- **Check constraints**: Valid email format, state transitions, enum values
- **Foreign key constraints**: Referential integrity across all relations
- **Unique constraints**: Email uniqueness, slug uniqueness
- **Indexes**: Optimized queries on tenant_id, user_id, status fields
- **Encrypted columns**: Passwords (bcrypt), API keys, secrets

## 🧪 Testing

### Run All Tests
```bash
dotnet test
```

### Run Unit Tests Only
```bash
dotnet test tests/DataCollector.Tests.Unit
```

### Run Integration Tests
```bash
# Start test database
docker-compose -f docker-compose.test.yml up -d

# Run tests
dotnet test tests/DataCollector.Tests.Integration

# Cleanup
docker-compose -f docker-compose.test.yml down -v
```

### Test Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutputFormat=opencover
```

## 📦 Deployment

### Kubernetes Deployment

1. **Using kubectl**:
```bash
# Create namespace
kubectl create namespace datacollector

# Apply manifests
kubectl apply -f k8s/namespace.yaml
kubectl apply -f k8s/configmap.yaml
kubectl apply -f k8s/secrets.yaml
kubectl apply -f k8s/postgres-statefulset.yaml
kubectl apply -f k8s/api-deployment.yaml
kubectl apply -f k8s/service.yaml
kubectl apply -f k8s/ingress.yaml
```

2. **Using Helm**:
```bash
# Install with default values
helm install datacollector ./helm-chart

# Install with custom values
helm install datacollector ./helm-chart -f values-production.yaml

# Upgrade
helm upgrade datacollector ./helm-chart
```

### Environment Variables (Production)

Required environment variables:
```bash
# Database
ConnectionStrings__SharedDatabase=Host=postgres;Database=datacollector_shared;Username=postgres;Password=<secret>

# JWT
Jwt__Secret=<256-bit-secret>
Jwt__Issuer=DataCollectorPlatform
Jwt__Audience=DataCollectorPlatform
Jwt__ExpiryMinutes=60

# Multi-Tenancy
MultiTenancy__DatabasePrefix=tenant_
MultiTenancy__AutoCreateDatabase=true

# Logging
Serilog__MinimumLevel__Default=Information
Serilog__MinimumLevel__Override__Microsoft=Warning

# Security
Security__PasswordHashIterations=10000
Security__RequireHttps=true
Security__EnableAuditLogging=true
```

## 📊 Monitoring & Health Checks

### Health Endpoints
- `/health` - Overall health status
- `/health/ready` - Readiness probe (for K8s)
- `/health/live` - Liveness probe (for K8s)

### Metrics
- Request duration histograms
- Error rate counters
- Active connection gauges
- Database query performance

### Logging
- Structured logging with Serilog
- Log levels: Trace, Debug, Information, Warning, Error, Critical
- Output sinks: Console, File, Seq (optional)
- Sensitive data sanitization

## 🔧 Configuration

### appsettings.json Structure
```json
{
  "ConnectionStrings": {
    "SharedDatabase": "..."
  },
  "Jwt": {
    "Secret": "...",
    "Issuer": "...",
    "Audience": "...",
    "ExpiryMinutes": 60,
    "RefreshTokenExpiryDays": 7
  },
  "MultiTenancy": {
    "DatabasePrefix": "tenant_",
    "AutoCreateDatabase": true,
    "IsolationStrategy": "DatabasePerTenant"
  },
  "Security": {
    "PasswordMinLength": 8,
    "RequireUppercase": true,
    "RequireDigit": true,
    "RequireSpecialChar": true,
    "PasswordHashIterations": 10000
  }
}
```

## 🏛️ Project Structure

```
DataCollectorPlatform/
├── src/
│   ├── DataCollector.API/              # Web API layer
│   │   ├── Controllers/                # API endpoints
│   │   ├── Middleware/                 # Custom middleware
│   │   ├── Filters/                    # Action filters
│   │   └── Program.cs                  # App entry point
│   ├── DataCollector.Application/      # Application logic
│   │   ├── Services/                   # Business services
│   │   ├── DTOs/                       # Data transfer objects
│   │   ├── Validators/                 # Input validation
│   │   └── Interfaces/                 # Service contracts
│   ├── DataCollector.Domain/           # Domain models
│   │   ├── Entities/                   # Domain entities
│   │   ├── Enums/                      # Enumerations
│   │   ├── ValueObjects/               # Value objects
│   │   └── Interfaces/                 # Repository contracts
│   └── DataCollector.Infrastructure/   # Infrastructure
│       ├── Persistence/                # EF Core, repositories
│       ├── Security/                   # Auth, encryption
│       ├── MultiTenancy/               # Tenant resolution
│       └── Services/                   # External services
├── tests/
│   ├── DataCollector.Tests.Unit/       # Unit tests
│   └── DataCollector.Tests.Integration/ # Integration tests
├── k8s/                                 # Kubernetes manifests
├── helm-chart/                          # Helm chart
├── docs/                                # Documentation
├── postman/                             # API collections
├── docker-compose.yml                   # Local development
└── README.md                            # This file
```

## 🚨 Troubleshooting

### Database Connection Issues
```bash
# Check PostgreSQL is running
docker ps | grep postgres

# Test connection
psql -h localhost -U postgres -d datacollector_shared

# View logs
docker logs postgres-dc
```

### Migration Issues
```bash
# Reset migrations
dotnet ef database drop --context SharedDbContext --force
dotnet ef database update --context SharedDbContext

# Create new migration
dotnet ef migrations add MigrationName --context SharedDbContext
```

### Tenant Database Not Created
- Check logs: `docker logs datacollector-api`
- Verify `MultiTenancy__AutoCreateDatabase=true`
- Ensure PostgreSQL user has `CREATEDB` privilege

## 📚 Additional Resources

- [API Documentation (Swagger)](http://localhost:5000/swagger)
- [Architecture Diagrams](./docs/architecture)
- [Migration Guide](./docs/migrations.md)
- [Security Best Practices](./docs/security.md)
- [Contributing Guidelines](./CONTRIBUTING.md)

## 📝 License

Copyright © 2024 DataCollector Platform. All rights reserved.

## 🤝 Support

For issues and questions:
- GitHub Issues: <repository-url>/issues
- Email: support@datacollector.example.com
- Documentation: https://docs.datacollector.example.com
