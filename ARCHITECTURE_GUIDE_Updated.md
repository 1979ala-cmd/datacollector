# Data Collector Platform - Updated Architecture Guide

## 📋 Overview

This document explains the updated architecture where **DataSources** store complete API configurations including functions, and **Collectors** reference these functions through pipelines.

---

## 🏗️ Architecture Components

### 1. DataSource (API Configuration Store)

A **DataSource** is a complete API configuration that includes:

- **Base URL and Protocol** (REST, GraphQL, SOAP)
- **Authentication Configuration** (API Key, OAuth2, Basic, Bearer, etc.)
- **Headers** (static and dynamic)
- **Functions** (API endpoints/operations)
- **Rate Limiting**
- **Retry Logic**
- **Caching**
- **Monitoring**
- **Circuit Breaker**

```
┌─────────────────────────────────────────────────────────┐
│              DataSource: "CRM REST API"                 │
├─────────────────────────────────────────────────────────┤
│ BaseUrl: https://api.crm.com/v1                        │
│ Protocol: REST                                          │
│ Auth: OAuth2 (with client credentials)                 │
│                                                          │
│ Functions:                                              │
│  ┌──────────────────────────────────────┐             │
│  │ Function ID: "func-001"              │             │
│  │ Name: getCustomers                   │             │
│  │ Method: GET                          │             │
│  │ Path: /customers                     │             │
│  │ Parameters:                          │             │
│  │   - status (query, optional)         │             │
│  │   - limit (query, optional)          │             │
│  │   - offset (query, optional)         │             │
│  └──────────────────────────────────────┘             │
│  ┌──────────────────────────────────────┐             │
│  │ Function ID: "func-002"              │             │
│  │ Name: getCustomerById                │             │
│  │ Method: GET                          │             │
│  │ Path: /customers/{id}                │             │
│  │ Parameters:                          │             │
│  │   - id (path, required)              │             │
│  └──────────────────────────────────────┘             │
│  ┌──────────────────────────────────────┐             │
│  │ Function ID: "func-003"              │             │
│  │ Name: getCustomerOrders              │             │
│  │ Method: GET                          │             │
│  │ Path: /customers/{id}/orders         │             │
│  │ Parameters:                          │             │
│  │   - id (path, required)              │             │
│  │   - status (query, optional)         │             │
│  └──────────────────────────────────────┘             │
└─────────────────────────────────────────────────────────┘
```

### 2. Collector (Workflow Definition)

A **Collector** references **ONE DataSource** but can have **MULTIPLE Pipelines**. Each pipeline references a function from the DataSource.

```
┌──────────────────────────────────────────────────────────┐
│         Collector: "Customer Data Collector"             │
├──────────────────────────────────────────────────────────┤
│ DataSource: "CRM REST API" (Single DataSource)          │
│                                                           │
│ Pipelines:                                               │
│  ┌────────────────────────────────────┐                 │
│  │ Pipeline 1: "Sync All Customers"   │                 │
│  │ FunctionId: "func-001"             │                 │
│  │ (Uses: getCustomers)               │                 │
│  │                                     │                 │
│  │ Processing Steps:                  │                 │
│  │  1. API Call                       │                 │
│  │  2. Pagination                     │                 │
│  │  3. For-Each Customer              │                 │
│  │     └─ Child Steps:                │                 │
│  │        └─ API Call (func-003)      │                 │
│  │        └─ Transform                │                 │
│  │  4. Store to Database              │                 │
│  └────────────────────────────────────┘                 │
│  ┌────────────────────────────────────┐                 │
│  │ Pipeline 2: "Get Customer Details" │                 │
│  │ FunctionId: "func-002"             │                 │
│  │ (Uses: getCustomerById)            │                 │
│  │                                     │                 │
│  │ Processing Steps:                  │                 │
│  │  1. API Call                       │                 │
│  │  2. Transform                      │                 │
│  │  3. Store to Database              │                 │
│  └────────────────────────────────────┘                 │
└──────────────────────────────────────────────────────────┘
```

### 3. Pipeline (Execution Unit)

A **Pipeline** is a specific execution flow that:

1. References a **Function** from the DataSource
2. Defines **Parameter Mappings** (how to get values from previous steps)
3. Defines **Static Parameters** (hardcoded values)
4. Contains **Processing Steps** (transformation logic)

---

## 📝 Complete Example Flow

### Step 1: Create DataSource

```json
POST /api/datasources/manual
{
  "name": "CRM REST API",
  "description": "Customer relationship management API",
  "version": "1.0.0",
  "protocol": "REST",
  "baseUrl": "https://api.crm.com/v1",
  
  "authConfig": {
    "type": "OAuth2",
    "details": {
      "tokenUrl": "https://api.crm.com/oauth/token",
      "clientId": "{config.clientId}",
      "clientSecret": "{config.clientSecret}",
      "scopes": ["read", "write"]
    },
    "requiresTLS": true,
    "tokenExpirationMinutes": 60,
    "refreshTokenSupported": true
  },
  
  "headers": [
    {
      "name": "Content-Type",
      "value": "application/json",
      "required": true,
      "isDynamic": false
    },
    {
      "name": "X-API-Version",
      "value": "2.1",
      "required": true,
      "isDynamic": false
    }
  ],
  
  "functions": [
    {
      "id": "func-get-customers",
      "name": "getCustomers",
      "description": "Get all customers",
      "method": "GET",
      "path": "/customers",
      "parameters": [
        {
          "name": "status",
          "type": "string",
          "location": "query",
          "required": false,
          "description": "Filter by customer status"
        },
        {
          "name": "limit",
          "type": "number",
          "location": "query",
          "required": false,
          "default": "100"
        },
        {
          "name": "offset",
          "type": "number",
          "location": "query",
          "required": false,
          "default": "0"
        }
      ],
      "response": {
        "expectedFormat": "application/json",
        "schema": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "id": { "type": "string" },
              "name": { "type": "string" },
              "email": { "type": "string" },
              "status": { "type": "string" }
            }
          }
        }
      },
      "requiresAuth": true
    },
    {
      "id": "func-get-customer-orders",
      "name": "getCustomerOrders",
      "description": "Get orders for a specific customer",
      "method": "GET",
      "path": "/customers/{customerId}/orders",
      "parameters": [
        {
          "name": "customerId",
          "type": "string",
          "location": "path",
          "required": true,
          "description": "Customer ID"
        },
        {
          "name": "status",
          "type": "string",
          "location": "query",
          "required": false,
          "description": "Filter by order status"
        }
      ],
      "response": {
        "expectedFormat": "application/json",
        "schema": {
          "type": "array",
          "items": {
            "type": "object",
            "properties": {
              "orderId": { "type": "string" },
              "amount": { "type": "number" },
              "status": { "type": "string" },
              "createdAt": { "type": "string" }
            }
          }
        }
      },
      "requiresAuth": true
    }
  ],
  
  "rateLimitConfig": {
    "requestsPerMinute": 100,
    "requestsPerHour": 5000,
    "strategy": "sliding_window"
  },
  
  "retryConfig": {
    "enabled": true,
    "maxAttempts": 3,
    "initialDelayMs": 1000,
    "maxDelayMs": 30000,
    "backoffStrategy": "exponential",
    "retryableStatusCodes": [429, 500, 502, 503, 504],
    "retryOnTimeout": true
  },
  
  "category": "CRM",
  "tags": ["crm", "customers", "orders"]
}
```

**Response:**
```json
{
  "id": "datasource-123",
  "name": "CRM REST API",
  "version": "1.0.0",
  "protocol": "REST",
  "baseUrl": "https://api.crm.com/v1",
  "functionCount": 2,
  "isActive": true,
  "createdAt": "2025-10-28T10:00:00Z"
}
```

---

### Step 2: Create Collector with Pipelines

Now create a collector that uses functions from the DataSource:

```json
POST /api/collectors
{
  "name": "Customer Data Collector",
  "description": "Collects customer data and their orders",
  "dataSourceId": "datasource-123",
  
  "pipelines": [
    {
      "name": "Main Customer Sync Pipeline",
      "description": "Syncs all customers and their orders",
      "functionId": "func-get-customers",
      
      "staticParameters": {
        "status": "active",
        "limit": 100
      },
      
      "dataIngestion": {
        "strategy": "full-sync",
        "syncMode": "full",
        "batchSize": 100,
        "parallelization": false,
        "conflictResolution": "latest"
      },
      
      "processingSteps": [
        {
          "name": "Fetch Customers",
          "type": "api-call",
          "enabled": true,
          "config": {}
        },
        {
          "name": "Paginate Results",
          "type": "pagination",
          "enabled": true,
          "config": {
            "type": "offset",
            "pageSize": 100,
            "maxPages": 10,
            "limitParam": "limit",
            "offsetParam": "offset"
          }
        },
        {
          "name": "Process Each Customer",
          "type": "for-each",
          "enabled": true,
          "config": {},
          "childSteps": [
            {
              "name": "Fetch Customer Orders",
              "type": "api-call",
              "enabled": true,
              "config": {
                "functionId": "func-get-customer-orders",
                "parameterMappings": {
                  "customerId": "$.currentItem.id"
                }
              }
            },
            {
              "name": "Transform Orders",
              "type": "transform",
              "enabled": true,
              "config": {
                "mapping": {
                  "customerId": "$.customer.id",
                  "customerName": "$.customer.name",
                  "orders": "$.orders"
                }
              }
            },
            {
              "name": "Filter Active Orders",
              "type": "filter",
              "enabled": true,
              "config": {
                "condition": "$.status == 'active'"
              }
            }
          ]
        },
        {
          "name": "Store to Database",
          "type": "store-database",
          "enabled": true,
          "config": {
            "table": "customers",
            "mode": "upsert",
            "keyField": "id"
          }
        }
      ]
    },
    {
      "name": "Customer Orders Pipeline",
      "description": "Gets orders for specific customer",
      "functionId": "func-get-customer-orders",
      
      "parameterMappings": {
        "customerId": "$.input.customerId"
      },
      
      "staticParameters": {
        "status": "pending"
      },
      
      "processingSteps": [
        {
          "name": "Fetch Orders",
          "type": "api-call",
          "enabled": true,
          "config": {}
        },
        {
          "name": "Transform Data",
          "type": "transform",
          "enabled": true,
          "config": {}
        },
        {
          "name": "Store to Database",
          "type": "store-database",
          "enabled": true,
          "config": {
            "table": "customer_orders"
          }
        }
      ]
    }
  ]
}
```

---

### Step 3: Execute Pipeline

```json
POST /api/collectors/{collector-id}/execute
{
  "pipelineId": "{pipeline-id}",
  "parameters": {
    "customerId": "cust-12345"
  },
  "dryRun": false
}
```

**What Happens During Execution:**

1. **Load DataSource Configuration**
   - Get BaseURL, Auth, Headers, Rate Limits
   - Get the Function definition for `func-get-customer-orders`

2. **Resolve Parameters**
   - Static Parameters: `status: "pending"`
   - Runtime Parameters: `customerId: "cust-12345"`
   - Parameter Mappings: Applied from previous steps

3. **Build API Request**
   ```
   GET https://api.crm.com/v1/customers/cust-12345/orders?status=pending
   Headers:
     Authorization: Bearer {oauth-token}
     Content-Type: application/json
     X-API-Version: 2.1
   ```

4. **Execute with DataSource Settings**
   - Apply Rate Limiting (100 req/min)
   - Enable Retry Logic (3 attempts, exponential backoff)
   - Apply Circuit Breaker if configured

5. **Process Response Through Steps**
   - API Call → Transform → Store

6. **Return Execution Result**

---

## 🔄 Key Workflows

### Workflow 1: Generate DataSource from Swagger

```json
POST /api/datasources/generate/swagger
{
  "sourceType": "SwaggerUrl",
  "sourceUrl": "https://api.example.com/swagger.json",
  "dataSourceName": "Example API",
  "description": "Generated from Swagger",
  "baseUrlOverride": "https://api.example.com/v2",
  "filterOperations": true,
  "includedOperations": [
    "getUsers",
    "getUserById",
    "createUser"
  ],
  "generateModels": true,
  "validateRequests": true,
  "defaultRateLimit": 100
}
```

**What Happens:**
1. System fetches Swagger spec from URL
2. Parses all operations (endpoints)
3. Filters to only included operations
4. Generates Function definitions for each operation
5. Extracts auth configuration from Swagger
6. Creates DataSource with all functions

---

### Workflow 2: Add Function to Existing DataSource

```json
POST /api/datasources/{id}/functions
{
  "id": "func-create-customer",
  "name": "createCustomer",
  "description": "Create a new customer",
  "method": "POST",
  "path": "/customers",
  "requestBody": {
    "schema": {
      "type": "object",
      "properties": {
        "name": { "type": "string" },
        "email": { "type": "string", "format": "email" },
        "phone": { "type": "string" }
      },
      "required": ["name", "email"]
    },
    "contentType": "application/json",
    "required": true
  },
  "response": {
    "expectedFormat": "application/json",
    "statusCodes": {
      "201": {
        "description": "Customer created successfully",
        "schema": {
          "type": "object",
          "properties": {
            "id": { "type": "string" },
            "name": { "type": "string" },
            "email": { "type": "string" }
          }
        }
      },
      "400": {
        "description": "Bad request"
      }
    }
  },
  "requiresAuth": true
}
```

---

### Workflow 3: Test DataSource Connection

```json
POST /api/datasources/{id}/test
{
  "functionId": "func-get-customers",
  "testParameters": {
    "limit": 5,
    "status": "active"
  }
}
```

**Response:**
```json
{
  "success": true,
  "message": "Connection test successful",
  "responseTime": 234,
  "statusCode": 200,
  "responseData": {
    "customers": [
      { "id": "1", "name": "John Doe" },
      { "id": "2", "name": "Jane Smith" }
    ]
  },
  "testedAt": "2025-10-28T10:30:00Z"
}
```

---

## 🎯 Key Benefits

### 1. **Reusability**
- Define API configuration once in DataSource
- Reuse across multiple collectors and pipelines
- Update DataSource auth/headers → all collectors get updated config

### 2. **Maintainability**
- Single source of truth for API configuration
- Easy to update rate limits, retry logic, auth
- Function definitions are version-controlled

### 3. **Validation**
- Validate function exists before creating pipeline
- Validate parameter mappings against function schema
- Type checking for parameters

### 4. **Flexibility**
- One DataSource → Many Pipelines
- Each pipeline can use different functions
- Override parameters per pipeline

### 5. **Monitoring**
- Track usage per function
- Monitor rate limits per DataSource
- Centralized error handling

---

## 🔒 Validation Rules

### DataSource Validation
1. ✅ At least one function must be defined
2. ✅ Function IDs must be unique within DataSource
3. ✅ Function paths must be valid URL paths
4. ✅ Required parameters must be marked
5. ✅ Auth configuration must be valid for auth type

### Collector Validation
1. ✅ Must reference exactly ONE DataSource
2. ✅ All pipelines must use functions from that DataSource
3. ✅ Function IDs must exist in the DataSource
4. ✅ Parameter mappings must match function parameters
5. ✅ Static parameters must match function parameter types

### Pipeline Validation
1. ✅ Function ID must exist in parent collector's DataSource
2. ✅ Required function parameters must have values (static or mapped)
3. ✅ Parameter types must match function definition
4. ✅ Processing steps must be valid for the function type

---

## 📊 Database Schema Changes

### DataSource Table
```sql
CREATE TABLE data_sources (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    name VARCHAR(200) NOT NULL,
    description TEXT,
    version VARCHAR(50),
    image_url VARCHAR(500),
    protocol INT NOT NULL, -- REST, GraphQL, SOAP, etc.
    type INT NOT NULL, -- Manual, SwaggerUrl, etc.
    source TEXT, -- URL or file content for generated sources
    base_url VARCHAR(500),
    
    -- JSON Configurations
    config_fields JSONB, -- List<ConfigField>
    auth_config JSONB, -- AuthConfiguration
    headers JSONB, -- List<HeaderDefinition>
    functions JSONB NOT NULL, -- List<FunctionDefinition> - CRITICAL
    rate_limit_config JSONB,
    cache_config JSONB,
    retry_config JSONB,
    monitoring_config JSONB,
    circuit_breaker_config JSONB,
    
    -- Metadata
    category VARCHAR(100),
    tags JSONB, -- List<string>
    metadata JSONB, -- Dictionary
    
    -- Status
    is_active BOOLEAN DEFAULT true,
    last_tested_at TIMESTAMP,
    last_test_result BOOLEAN,
    last_test_error TEXT,
    
    -- Audit
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP,
    created_by VARCHAR(100),
    updated_by VARCHAR(100),
    is_deleted BOOLEAN DEFAULT false,
    deleted_at TIMESTAMP,
    deleted_by VARCHAR(100)
);
```

### Pipeline Table
```sql
CREATE TABLE pipelines (
    id UUID PRIMARY KEY,
    tenant_id UUID NOT NULL,
    data_collector_id UUID NOT NULL,
    data_source_id UUID NOT NULL,
    
    name VARCHAR(200) NOT NULL,
    description TEXT,
    
    -- Function Reference - CRITICAL
    function_id VARCHAR(100) NOT NULL, -- References DataSource.Functions[].Id
    function_name VARCHAR(200), -- Override display name
    api_path VARCHAR(500), -- Override if needed
    method VARCHAR(10), -- Override if needed
    
    is_enabled BOOLEAN DEFAULT true,
    
    -- JSON Configurations
    parameter_mappings JSONB, -- Dictionary<string, string>
    static_parameters JSONB, -- Dictionary<string, object>
    data_ingestion JSONB, -- DataIngestionConfiguration
    
    -- Audit
    created_at TIMESTAMP NOT NULL,
    updated_at TIMESTAMP,
    created_by VARCHAR(100),
    updated_by VARCHAR(100),
    is_deleted BOOLEAN DEFAULT false,
    deleted_at TIMESTAMP,
    deleted_by VARCHAR(100),
    
    FOREIGN KEY (data_collector_id) REFERENCES data_collectors(id),
    FOREIGN KEY (data_source_id) REFERENCES data_sources(id)
);
```

---

## 🚀 Migration Path

### For Existing Systems

1. **Update DataSource Entity**
   - Add Functions JSON column
   - Migrate existing API paths to function definitions
   - Add new configuration columns

2. **Update Pipeline Entity**
   - Add FunctionId column
   - Migrate ApiPath to function references
   - Add parameter mappings support

3. **Update Services**
   - Implement function resolution logic
   - Add parameter mapping engine
   - Update execution engine

4. **Update Controllers**
   - Add function management endpoints
   - Update pipeline creation to require function IDs
   - Add validation endpoints

---

## ✅ Summary

### Before (Simple Approach)
- Pipeline stored API path directly
- Limited configuration
- Hard to maintain
- No reusability

### After (Function-Based Approach)
- DataSource stores complete API config + functions
- Pipelines reference functions by ID
- Easy to maintain and update
- Full reusability across collectors
- Complete validation support
- Comprehensive execution configuration

---

**End of Architecture Guide**
