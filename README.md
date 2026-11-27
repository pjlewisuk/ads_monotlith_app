# Retail Monolith App

A lightweight ASP.NET Core 9 Razor Pages application that simulates a retail monolith before decomposition.  
It includes product listing, shopping cart, checkout, and inventory management — built to demonstrate modernisation and refactoring patterns.

---

## Features

- ASP.NET Core 9 (Razor Pages)
- Entity Framework Core (SQL Server LocalDB)
- Dependency Injection with modular services:
  - `CartService`
  - `CheckoutService`
  - `MockPaymentGateway`
- 50 sample seeded products with random inventory
- End-to-end retail flow:
  - Products → Cart → Checkout → Orders
- Minimal APIs:
  - `POST /api/checkout`
  - `GET /api/orders/{id}`
- Health-check endpoint at `/health`
- **Comprehensive Observability:**
  - Application Insights integration
  - OpenTelemetry distributed tracing
  - Custom metrics and events
  - Structured logging
  - Database health checks
- **Production-Ready Containerization:**
  - Multi-stage Docker build optimized for .NET 9
  - Docker Compose for local development with SQL Server
  - Kubernetes manifests for AKS deployment
  - CI/CD pipeline with GitHub Actions
  - Security scanning with Trivy
  - Non-root container execution
- Ready for decomposition into microservices

---

## 🏠 Home Page
![Home Page Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/HomePage.jpg)

## 🛍 Products
![Products Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/Products.jpg)

## 🧺 Cart
![Cart Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/Cart.jpg)

## 💳 Checkout
![Checkout Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/CheckOut.jpg)

## 📦 Orders
![Orders Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/Orders.jpg)

## 📦 Order Details
![Orders Screenshot](https://github.com/lavann/ads_monotlith_app/blob/main/Images/OrderDetails.jpg)

---

## Development Setup

You can run and edit this application in four different ways:

### 1. Local Development Environment

Run the application directly on your local machine with your preferred IDE or editor.

**Prerequisites:**
- .NET 9 SDK installed ([download](https://dotnet.microsoft.com/download/dotnet/9.0))
- SQL Server LocalDB (included with Visual Studio) or SQL Server instance
- Your favorite code editor (Visual Studio, VS Code, Rider, etc.)

**Steps:**
```bash
git clone https://github.com/lavann/ads_monotlith_app.git
cd ads_monotlith_app
dotnet restore
dotnet ef database update
dotnet run
```

### 2. Docker-Hosted Dev Container

Use a Docker container with a pre-configured development environment. This ensures consistency across different machines without installing dependencies locally.

**Prerequisites:**
- Docker Desktop installed and running
- Visual Studio Code with the Dev Containers extension

**Steps:**
1. Clone the repository
2. Open the folder in VS Code
3. When prompted, click "Reopen in Container" (or use Command Palette: `Dev Containers: Reopen in Container`)
4. VS Code will build and start the dev container with all dependencies pre-installed
5. Run `dotnet ef database update` and `dotnet run` inside the container terminal

### 3. GitHub Codespaces

Develop entirely in the cloud with zero local setup. Codespaces provides a full VS Code environment in your browser.

**Prerequisites:**
- GitHub account with Codespaces access

**Steps:**
1. Navigate to the repository on GitHub
2. Click the green "Code" button
3. Select the "Codespaces" tab
4. Click "Create codespace on main"
5. Wait for the environment to initialize
6. Run `dotnet ef database update` and `dotnet run` in the integrated terminal

### 4. Docker & Docker Compose

Run the entire application stack (web app + SQL Server) in containers with Docker Compose.

**Prerequisites:**
- Docker Desktop installed and running
- Docker Compose v3.8 or later

**Steps:**
1. Clone the repository:
   ```bash
   git clone https://github.com/pjlewisuk/ads_monotlith_app.git
   cd ads_monotlith_app
   ```

2. Start the application:
   ```bash
   docker-compose up -d
   ```

3. Access the application at `http://localhost:8080`

4. View logs:
   ```bash
   docker-compose logs -f web
   ```

5. Stop the application:
   ```bash
   docker-compose down
   ```

For more details on containerization, see [DEPLOYMENT.md](DEPLOYMENT.md).

All four environments provide a consistent development experience with the .NET SDK, C# extension, and all necessary tools pre-configured.

---

## Database & Migrations

### Apply existing migrations
dotnet ef database update

### Create a new migration

- If you modify models:
	- `dotnet ef migrations add <MigrationName>`
	- `dotnet ef database update`

- EF Core uses DesignTimeDbContextFactory (Data/DesignTimeDbContextFactory.cs)
with the connection string:
	- `Server=(localdb)\MSSQLLocalDB;Database=RetailMonolith;Trusted_Connection=True;MultipleActiveResultSets=true`

### Seeding Sample Data

At startup, the app automatically runs `await AppDbContext.SeedAsync(db);` which seeds 50 sample products with random categories, prices, and inventory.

To reseed manually:
```bash
dotnet ef database drop -f
dotnet ef database update
dotnet run
```

---

## Running the Application

Start the application:
```bash
dotnet run
```

Access the app at `https://localhost:5001` or `http://localhost:5000`.

### Available Endpoints

| Path               | Description           |
| ------------------ | --------------------- |
| `/`                | Home Page             |
| `/Products`        | Product catalogue     |
| `/Cart`            | Shopping cart         |
| `/Checkout`        | Checkout page         |
| `/Orders`          | Order history         |
| `/Orders/Details`  | Order details         |
| `/api/checkout`    | Checkout API          |
| `/api/orders/{id}` | Order details API     |
| `/health`          | Health check endpoint |

---

## Environment Variables (optional)
You can override the default connection string by setting the `ConnectionStrings__DefaultConnection` environment variable.
| Variable                               | Description                | Default          |
| -------------------------------------- | -------------------------- | ---------------- |
| `ConnectionStrings__DefaultConnection` | Database connection string | LocalDB instance |
| `ASPNETCORE_ENVIRONMENT`               | Environment mode           | `Development`    |
| `ApplicationInsights__ConnectionString`| Application Insights connection string | Empty (optional) |

---

## Observability

The application includes comprehensive observability features using **Application Insights** and **OpenTelemetry**.

### Application Insights Configuration

To enable Application Insights telemetry:

1. Create an Application Insights resource in Azure
2. Copy the connection string from the Azure portal
3. Add it to `appsettings.json` or set the environment variable:

```json
{
  "ApplicationInsights": {
    "ConnectionString": "InstrumentationKey=...;IngestionEndpoint=https://..."
  }
}
```

Or via environment variable:
```bash
export ApplicationInsights__ConnectionString="InstrumentationKey=...;IngestionEndpoint=https://..."
```

### Telemetry Features

The application automatically collects:

**Automatic Instrumentation:**
- All HTTP requests and responses
- Database queries (Entity Framework Core)
- Dependencies and external calls
- Exceptions and errors

**Custom Business Events:**
- `ProductsViewed` - When the product catalog is viewed
- `ProductViewed` - When a specific product is viewed
- `AddedToCart` - When a product is added to cart
- `CartViewed` - When the shopping cart is viewed
- `CheckoutInitiated` - When checkout process starts
- `PaymentProcessed` - When payment is processed
- `OrderCompleted` - When an order is successfully completed

**Custom Metrics:**
- `OrderValue` - Tracks the value of completed orders
- `PaymentSuccess` - Counter for successful payments
- `PaymentFailure` - Counter for failed payments

**Distributed Tracing:**
- End-to-end tracing across the shopping flow
- Custom spans for critical operations:
  - `Checkout` - Overall checkout process
  - `ReserveInventory` - Inventory management
  - `ProcessPayment` - Payment processing
  - `AddToCart` - Cart operations

### Health Checks

The `/health` endpoint provides:
- Overall application health status
- Database connectivity check
- Memory usage monitoring

Access the health check at: `https://localhost:5001/health`

### Monitoring Queries

Use these Kusto queries in Application Insights to analyze telemetry:

**Request Performance:**
```kusto
requests
| where timestamp > ago(1d)
| summarize avg(duration), percentile(duration, 95) by name
| order by avg_duration desc
```

**Checkout Funnel:**
```kusto
customEvents
| where timestamp > ago(7d)
| where name in ("ProductViewed", "AddedToCart", "CheckoutInitiated", "OrderCompleted")
| summarize count() by name
```

**Failed Requests:**
```kusto
requests
| where timestamp > ago(1d)
| where success == false
| summarize count() by resultCode, name
```

**Payment Success Rate:**
```kusto
customEvents
| where timestamp > ago(1d)
| where name == "PaymentProcessed"
| summarize 
    Total = count(),
    Successful = countif(customDimensions.Success == "true")
| extend SuccessRate = (Successful * 100.0) / Total
```

### Logging

The application uses structured logging with context:
- Correlation IDs are automatically tracked across operations
- Log levels are configured in `appsettings.json`
- Logs are sent to Application Insights when configured

**Log Levels:**
- `Information` - Business events and normal operations
- `Warning` - Potential issues (e.g., out of stock)
- `Error` - Errors and exceptions

---

## 🚀 Deployment

The application is containerized and ready for deployment to Azure using modern container platforms.

### Container Deployment Options

#### 🐳 Docker
The application includes a production-optimized multi-stage Dockerfile:
- **Build Stage:** Uses `mcr.microsoft.com/dotnet/sdk:9.0` for compilation
- **Runtime Stage:** Uses `mcr.microsoft.com/dotnet/aspnet:9.0-alpine` for minimal size
- **Security:** Runs as non-root user with restricted capabilities
- **Health Checks:** Built-in health check endpoint at `/health`

Build and run locally:
```bash
docker build -t retailmonolith:latest .
docker run -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Your-Connection-String" \
  retailmonolith:latest
```

#### 📦 Azure Container Apps
Deploy to Azure Container Apps for serverless container execution:
```bash
# Build and push to Azure Container Registry
az acr build --registry yourregistry --image retailmonolith:latest .

# Deploy to Container Apps
az containerapp create \
  --name retailmonolith-app \
  --resource-group your-rg \
  --environment your-env \
  --image yourregistry.azurecr.io/retailmonolith:latest \
  --target-port 8080 \
  --ingress external \
  --env-vars ConnectionStrings__DefaultConnection="Your-Connection-String"
```

#### ☸️ Azure Kubernetes Service (AKS)
Deploy to AKS for full Kubernetes orchestration capabilities:
```bash
# Apply Kubernetes manifests
kubectl apply -f k8s/

# Check deployment status
kubectl get pods -n retailmonolith
```

The `k8s/` directory includes:
- **Deployment** - Application pods with health checks and resource limits
- **Service** - LoadBalancer for external access
- **HPA** - Horizontal Pod Autoscaler for auto-scaling
- **Ingress** - HTTPS ingress with cert-manager support
- **ConfigMap & Secrets** - Configuration and sensitive data management

### CI/CD Pipeline

A GitHub Actions workflow (`.github/workflows/docker-build.yml`) provides:
- ✅ Automated Docker image builds
- ✅ Security scanning with Trivy
- ✅ Push to GitHub Container Registry (GHCR)
- ✅ Multi-platform support
- ✅ Automated tagging (branch, PR, SHA, semver)

The workflow runs on:
- Every push to `main` or `develop` branches
- Every pull request to `main`
- Manual workflow dispatch

### Detailed Deployment Guide

For comprehensive deployment instructions, including:
- Step-by-step Azure setup
- Database configuration
- Environment variables
- Monitoring setup
- Troubleshooting

See the **[DEPLOYMENT.md](DEPLOYMENT.md)** documentation.

---
