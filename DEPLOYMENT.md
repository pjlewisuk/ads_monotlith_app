# Deployment Guide

This guide provides detailed instructions for deploying the Retail Monolith application using containerization technologies on Azure.

## Table of Contents

- [Local Development with Docker](#local-development-with-docker)
- [Azure Container Apps Deployment](#azure-container-apps-deployment)
- [Azure Kubernetes Service (AKS) Deployment](#azure-kubernetes-service-aks-deployment)
- [Azure Container Registry Setup](#azure-container-registry-setup)
- [Database Configuration](#database-configuration)
- [CI/CD Pipeline](#cicd-pipeline)

---

## Local Development with Docker

### Prerequisites

- Docker Desktop installed and running
- Docker Compose v3.8 or later
- At least 4GB of available RAM for containers

### Quick Start

1. **Clone the repository:**
   ```bash
   git clone https://github.com/pjlewisuk/ads_monotlith_app.git
   cd ads_monotlith_app
   ```

2. **Create environment file (optional):**
   ```bash
   cp .env.example .env
   # Edit .env with your configuration
   ```

3. **Start the application:**
   ```bash
   docker-compose up -d
   ```

4. **Access the application:**
   - Web Application: http://localhost:8080
   - Health Check: http://localhost:8080/health

5. **View logs:**
   ```bash
   docker-compose logs -f web
   ```

6. **Stop the application:**
   ```bash
   docker-compose down
   ```

### Building the Docker Image Manually

```bash
# Build the production Docker image
docker build -t retailmonolith:latest .

# Run the container
docker run -d \
  -p 8080:8080 \
  -e ConnectionStrings__DefaultConnection="Server=host.docker.internal,1433;Database=RetailMonolith;User Id=sa;Password=YourStrong@Passw0rd;TrustServerCertificate=True;MultipleActiveResultSets=true" \
  --name retailmonolith \
  retailmonolith:latest
```

---

## Azure Container Apps Deployment

Azure Container Apps provides a serverless container platform ideal for microservices and web applications.

### Prerequisites

- Azure CLI installed ([Install Azure CLI](https://docs.microsoft.com/en-us/cli/azure/install-azure-cli))
- Azure subscription
- Azure Container Registry (see [setup instructions](#azure-container-registry-setup))

### Step 1: Set Environment Variables

```bash
# Configuration variables
RESOURCE_GROUP="rg-retailmonolith"
LOCATION="eastus"
CONTAINER_APP_ENV="retailmonolith-env"
CONTAINER_APP_NAME="retailmonolith-app"
ACR_NAME="yourregistry"  # Replace with your ACR name
IMAGE_NAME="retailmonolith"
IMAGE_TAG="latest"
```

### Step 2: Create Resource Group

```bash
az group create \
  --name $RESOURCE_GROUP \
  --location $LOCATION
```

### Step 3: Create Azure SQL Database

```bash
# Create SQL Server
SQL_SERVER_NAME="retailmonolith-sql-$(openssl rand -hex 4)"
ADMIN_USER="sqladmin"
ADMIN_PASSWORD="YourStrong@Passw0rd123"

az sql server create \
  --name $SQL_SERVER_NAME \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION \
  --admin-user $ADMIN_USER \
  --admin-password $ADMIN_PASSWORD

# Create database
az sql db create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name RetailMonolith \
  --service-objective S0

# Allow Azure services to access
az sql server firewall-rule create \
  --resource-group $RESOURCE_GROUP \
  --server $SQL_SERVER_NAME \
  --name AllowAzureServices \
  --start-ip-address 0.0.0.0 \
  --end-ip-address 0.0.0.0

# Get connection string
SQL_CONNECTION_STRING="Server=tcp:${SQL_SERVER_NAME}.database.windows.net,1433;Database=RetailMonolith;User Id=${ADMIN_USER};Password=${ADMIN_PASSWORD};Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

### Step 4: Create Container Apps Environment

```bash
az containerapp env create \
  --name $CONTAINER_APP_ENV \
  --resource-group $RESOURCE_GROUP \
  --location $LOCATION
```

### Step 5: Deploy Container App

```bash
# Build and push image to ACR
az acr build \
  --registry $ACR_NAME \
  --image $IMAGE_NAME:$IMAGE_TAG \
  --file Dockerfile .

# Create the container app
az containerapp create \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --environment $CONTAINER_APP_ENV \
  --image ${ACR_NAME}.azurecr.io/${IMAGE_NAME}:${IMAGE_TAG} \
  --registry-server ${ACR_NAME}.azurecr.io \
  --target-port 8080 \
  --ingress 'external' \
  --min-replicas 1 \
  --max-replicas 5 \
  --cpu 0.5 \
  --memory 1.0Gi \
  --env-vars \
    "ConnectionStrings__DefaultConnection=${SQL_CONNECTION_STRING}" \
    "ASPNETCORE_ENVIRONMENT=Production" \
    "ASPNETCORE_URLS=http://+:8080"
```

### Step 6: Configure Autoscaling

```bash
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --scale-rule-name http-rule \
  --scale-rule-type http \
  --scale-rule-http-concurrency 10
```

### Step 7: Get Application URL

```bash
az containerapp show \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --query properties.configuration.ingress.fqdn \
  --output tsv
```

---

## Azure Kubernetes Service (AKS) Deployment

For more control and advanced orchestration, deploy to Azure Kubernetes Service.

### Prerequisites

- kubectl installed ([Install kubectl](https://kubernetes.io/docs/tasks/tools/))
- Helm (optional, for advanced scenarios)
- Azure CLI

### Step 1: Create AKS Cluster

```bash
# Configuration
AKS_NAME="retailmonolith-aks"
RESOURCE_GROUP="rg-retailmonolith"
NODE_COUNT=2
NODE_VM_SIZE="Standard_B2s"

# Create AKS cluster
az aks create \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_NAME \
  --node-count $NODE_COUNT \
  --node-vm-size $NODE_VM_SIZE \
  --enable-managed-identity \
  --generate-ssh-keys \
  --attach-acr $ACR_NAME

# Get credentials
az aks get-credentials \
  --resource-group $RESOURCE_GROUP \
  --name $AKS_NAME
```

### Step 2: Create Kubernetes Manifests

Create a directory for Kubernetes manifests:

```bash
mkdir -p k8s
```

**k8s/namespace.yaml:**
```yaml
apiVersion: v1
kind: Namespace
metadata:
  name: retailmonolith
```

**k8s/secret.yaml:**
```yaml
apiVersion: v1
kind: Secret
metadata:
  name: retailmonolith-secrets
  namespace: retailmonolith
type: Opaque
stringData:
  connection-string: "Server=tcp:yourserver.database.windows.net,1433;Database=RetailMonolith;User Id=sqladmin;Password=YourPassword;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;"
```

**k8s/deployment.yaml:**
```yaml
apiVersion: apps/v1
kind: Deployment
metadata:
  name: retailmonolith
  namespace: retailmonolith
spec:
  replicas: 2
  selector:
    matchLabels:
      app: retailmonolith
  template:
    metadata:
      labels:
        app: retailmonolith
    spec:
      containers:
      - name: retailmonolith
        image: yourregistry.azurecr.io/retailmonolith:latest
        ports:
        - containerPort: 8080
        env:
        - name: ASPNETCORE_ENVIRONMENT
          value: "Production"
        - name: ASPNETCORE_URLS
          value: "http://+:8080"
        - name: ConnectionStrings__DefaultConnection
          valueFrom:
            secretKeyRef:
              name: retailmonolith-secrets
              key: connection-string
        livenessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 30
          periodSeconds: 10
        readinessProbe:
          httpGet:
            path: /health
            port: 8080
          initialDelaySeconds: 5
          periodSeconds: 5
        resources:
          requests:
            memory: "256Mi"
            cpu: "250m"
          limits:
            memory: "512Mi"
            cpu: "500m"
```

**k8s/service.yaml:**
```yaml
apiVersion: v1
kind: Service
metadata:
  name: retailmonolith-service
  namespace: retailmonolith
spec:
  type: LoadBalancer
  ports:
  - port: 80
    targetPort: 8080
    protocol: TCP
  selector:
    app: retailmonolith
```

**k8s/hpa.yaml** (Horizontal Pod Autoscaler):
```yaml
apiVersion: autoscaling/v2
kind: HorizontalPodAutoscaler
metadata:
  name: retailmonolith-hpa
  namespace: retailmonolith
spec:
  scaleTargetRef:
    apiVersion: apps/v1
    kind: Deployment
    name: retailmonolith
  minReplicas: 2
  maxReplicas: 10
  metrics:
  - type: Resource
    resource:
      name: cpu
      target:
        type: Utilization
        averageUtilization: 70
  - type: Resource
    resource:
      name: memory
      target:
        type: Utilization
        averageUtilization: 80
```

### Step 3: Deploy to AKS

```bash
# Apply all manifests
kubectl apply -f k8s/

# Check deployment status
kubectl get deployments -n retailmonolith
kubectl get pods -n retailmonolith
kubectl get services -n retailmonolith

# Get external IP (may take a few minutes)
kubectl get service retailmonolith-service -n retailmonolith
```

### Step 4: Install Ingress Controller (Optional)

For HTTPS and advanced routing:

```bash
# Add NGINX Ingress Controller
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.1/deploy/static/provider/cloud/deploy.yaml

# Create ingress resource
kubectl apply -f k8s/ingress.yaml
```

---

## Azure Container Registry Setup

### Create Azure Container Registry

```bash
ACR_NAME="yourregistry"  # Must be globally unique
RESOURCE_GROUP="rg-retailmonolith"

# Create ACR
az acr create \
  --resource-group $RESOURCE_GROUP \
  --name $ACR_NAME \
  --sku Basic

# Login to ACR
az acr login --name $ACR_NAME
```

### Build and Push Image

```bash
# Tag the image
docker tag retailmonolith:latest ${ACR_NAME}.azurecr.io/retailmonolith:latest

# Push to ACR
docker push ${ACR_NAME}.azurecr.io/retailmonolith:latest

# Or use ACR build
az acr build \
  --registry $ACR_NAME \
  --image retailmonolith:latest \
  --file Dockerfile .
```

---

## Database Configuration

### Azure SQL Database

The application requires a SQL Server database. For production, use Azure SQL Database:

1. **Create Azure SQL Server and Database** (see Azure Container Apps section above)

2. **Configure Firewall Rules:**
   ```bash
   # Allow your IP
   az sql server firewall-rule create \
     --resource-group $RESOURCE_GROUP \
     --server $SQL_SERVER_NAME \
     --name AllowMyIP \
     --start-ip-address YOUR_IP \
     --end-ip-address YOUR_IP
   ```

3. **Connection String Format:**
   ```
   Server=tcp:yourserver.database.windows.net,1433;Database=RetailMonolith;User Id=username;Password=password;Encrypt=True;TrustServerCertificate=False;Connection Timeout=30;
   ```

### Database Migrations

The application automatically runs migrations on startup. For manual control:

```bash
# Run migrations manually
dotnet ef database update

# Or create a migration job in Kubernetes
kubectl apply -f k8s/migration-job.yaml
```

---

## CI/CD Pipeline

### GitHub Actions Workflow

The repository includes a GitHub Actions workflow (`.github/workflows/docker-build.yml`) that:

1. Builds the Docker image
2. Runs security scanning with Trivy
3. Pushes to GitHub Container Registry
4. Can be extended for automatic deployment

### Configure Secrets

Add these secrets to your GitHub repository:

1. **AZURE_CREDENTIALS** - For Azure CLI authentication
2. **ACR_USERNAME** - Azure Container Registry username
3. **ACR_PASSWORD** - Azure Container Registry password

### Extend for Continuous Deployment

Add deployment steps to the workflow:

```yaml
- name: Deploy to Azure Container Apps
  uses: azure/container-apps-deploy-action@v1
  with:
    containerAppName: ${{ env.CONTAINER_APP_NAME }}
    resourceGroup: ${{ env.RESOURCE_GROUP }}
    imageToDeploy: ${{ env.REGISTRY }}/${{ env.IMAGE_NAME }}:${{ github.sha }}
```

---

## Environment Variables Reference

| Variable | Description | Default | Required |
|----------|-------------|---------|----------|
| `ConnectionStrings__DefaultConnection` | Database connection string | LocalDB | Yes |
| `ASPNETCORE_ENVIRONMENT` | Environment mode | Production | Yes |
| `ASPNETCORE_URLS` | Application listening URLs | http://+:8080 | Yes |
| `ApplicationInsights__ConnectionString` | Application Insights connection | Empty | No |

---

## Troubleshooting

### Common Issues

**Container won't start:**
- Check logs: `docker logs <container-name>`
- Verify database connectivity
- Ensure environment variables are set correctly

**Database connection errors:**
- Verify connection string format
- Check firewall rules
- Ensure database exists and is accessible

**Health check failures:**
- Access `/health` endpoint directly
- Check database connectivity
- Review application logs

### Useful Commands

```bash
# Docker
docker ps                           # List running containers
docker logs -f <container>          # Follow logs
docker exec -it <container> sh      # Shell into container

# Docker Compose
docker-compose ps                   # List services
docker-compose logs -f web          # Follow web logs
docker-compose restart web          # Restart service

# Kubernetes
kubectl get all -n retailmonolith   # List all resources
kubectl logs -f <pod> -n retailmonolith  # Follow logs
kubectl describe pod <pod> -n retailmonolith  # Detailed info
kubectl exec -it <pod> -n retailmonolith -- sh  # Shell into pod
```

---

## Security Best Practices

1. **Use managed identities** for Azure resource access
2. **Store secrets** in Azure Key Vault, not in environment variables
3. **Enable HTTPS** with valid certificates
4. **Run containers as non-root** (already configured)
5. **Scan images** regularly for vulnerabilities
6. **Use private container registries**
7. **Implement network policies** in Kubernetes
8. **Enable Azure Security Center** for threat detection

---

## Monitoring and Observability

### Application Insights

Configure Application Insights for comprehensive telemetry:

```bash
# Create Application Insights
az monitor app-insights component create \
  --app retailmonolith-insights \
  --location $LOCATION \
  --resource-group $RESOURCE_GROUP \
  --application-type web

# Get instrumentation key
APP_INSIGHTS_KEY=$(az monitor app-insights component show \
  --app retailmonolith-insights \
  --resource-group $RESOURCE_GROUP \
  --query connectionString \
  --output tsv)

# Set in Container App
az containerapp update \
  --name $CONTAINER_APP_NAME \
  --resource-group $RESOURCE_GROUP \
  --set-env-vars "ApplicationInsights__ConnectionString=${APP_INSIGHTS_KEY}"
```

### Health Monitoring

- **Health Endpoint:** `/health`
- **Liveness Probe:** Configured in Kubernetes manifests
- **Readiness Probe:** Configured in Kubernetes manifests

---

## Cost Optimization

### Azure Container Apps
- Use consumption-based pricing
- Configure min replicas to 0 for non-production environments
- Use Azure SQL Database Basic tier for development

### AKS
- Use B-series VMs for development
- Enable cluster autoscaler
- Use spot instances for non-critical workloads
- Consider Azure Reservations for production

---

## Next Steps

1. **Microservices Decomposition:** Consider breaking down the monolith into:
   - Product Service
   - Cart Service
   - Checkout Service
   - Order Service

2. **API Gateway:** Implement Azure API Management or NGINX

3. **Service Mesh:** Consider Istio or Linkerd for advanced networking

4. **Observability:** Enhance with distributed tracing and metrics

5. **Resilience:** Add retry policies, circuit breakers, and rate limiting

---

## Support and Resources

- [Azure Container Apps Documentation](https://learn.microsoft.com/en-us/azure/container-apps/)
- [Azure Kubernetes Service Documentation](https://learn.microsoft.com/en-us/azure/aks/)
- [Docker Documentation](https://docs.docker.com/)
- [.NET on Docker](https://learn.microsoft.com/en-us/dotnet/core/docker/introduction)
