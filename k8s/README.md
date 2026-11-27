# Kubernetes Manifests for Retail Monolith

This directory contains Kubernetes manifests for deploying the Retail Monolith application to Azure Kubernetes Service (AKS) or any Kubernetes cluster.

## Manifests Overview

- **namespace.yaml** - Creates the `retailmonolith` namespace
- **configmap.yaml** - Non-sensitive configuration values
- **secret.yaml** - Sensitive data like database connection strings (template)
- **deployment.yaml** - Main application deployment with 2 replicas
- **service.yaml** - LoadBalancer service for external access
- **hpa.yaml** - Horizontal Pod Autoscaler for automatic scaling
- **ingress.yaml** - Ingress configuration for HTTPS (requires NGINX Ingress Controller)

## Prerequisites

1. Kubernetes cluster (AKS, EKS, GKE, or local cluster)
2. `kubectl` configured to connect to your cluster
3. Azure Container Registry with the application image
4. Azure SQL Database (or other SQL Server instance)

## Quick Deployment

### 1. Update Configuration

Before deploying, update the following values:

**k8s/secret.yaml:**
```yaml
stringData:
  connection-string: "Server=tcp:yourserver.database.windows.net,..."
```

**k8s/deployment.yaml:**
```yaml
image: yourregistry.azurecr.io/retailmonolith:latest
```

**k8s/ingress.yaml (optional):**
```yaml
spec:
  tls:
  - hosts:
    - retailmonolith.yourdomain.com
```

### 2. Deploy All Manifests

```bash
# Apply all manifests in order
kubectl apply -f namespace.yaml
kubectl apply -f configmap.yaml
kubectl apply -f secret.yaml
kubectl apply -f deployment.yaml
kubectl apply -f service.yaml
kubectl apply -f hpa.yaml
kubectl apply -f ingress.yaml  # Optional
```

Or apply all at once:
```bash
kubectl apply -f .
```

### 3. Verify Deployment

```bash
# Check all resources
kubectl get all -n retailmonolith

# Check pod status
kubectl get pods -n retailmonolith

# Check service
kubectl get svc -n retailmonolith

# Get external IP (for LoadBalancer service)
kubectl get svc retailmonolith-service -n retailmonolith -o jsonpath='{.status.loadBalancer.ingress[0].ip}'
```

### 4. View Logs

```bash
# Get logs from all pods
kubectl logs -l app=retailmonolith -n retailmonolith

# Follow logs
kubectl logs -f -l app=retailmonolith -n retailmonolith

# Logs from specific pod
kubectl logs <pod-name> -n retailmonolith
```

## Configuration Details

### Security Context

The deployment includes security best practices:
- Runs as non-root user (UID 1000)
- Drops all Linux capabilities
- Prevents privilege escalation
- Uses seccomp profile

### Health Checks

**Liveness Probe:**
- Endpoint: `/health`
- Initial Delay: 30 seconds
- Period: 10 seconds

**Readiness Probe:**
- Endpoint: `/health`
- Initial Delay: 5 seconds
- Period: 5 seconds

### Resource Limits

**Requests:**
- CPU: 250m
- Memory: 256Mi

**Limits:**
- CPU: 500m
- Memory: 512Mi

Adjust these based on your workload requirements.

### Horizontal Pod Autoscaler

The HPA automatically scales the deployment based on:
- CPU utilization (target: 70%)
- Memory utilization (target: 80%)

**Scaling Range:**
- Minimum replicas: 2
- Maximum replicas: 10

## Using Ingress (HTTPS)

To use the ingress for HTTPS access:

### 1. Install NGINX Ingress Controller

```bash
kubectl apply -f https://raw.githubusercontent.com/kubernetes/ingress-nginx/controller-v1.8.1/deploy/static/provider/cloud/deploy.yaml
```

### 2. Install cert-manager (for automatic SSL certificates)

```bash
kubectl apply -f https://github.com/cert-manager/cert-manager/releases/download/v1.13.2/cert-manager.yaml
```

### 3. Create ClusterIssuer for Let's Encrypt

```yaml
apiVersion: cert-manager.io/v1
kind: ClusterIssuer
metadata:
  name: letsencrypt-prod
spec:
  acme:
    server: https://acme-v02.api.letsencrypt.org/directory
    email: your-email@example.com
    privateKeySecretRef:
      name: letsencrypt-prod
    solvers:
    - http01:
        ingress:
          class: nginx
```

### 4. Apply Ingress

Update `ingress.yaml` with your domain and apply:
```bash
kubectl apply -f ingress.yaml
```

## Secrets Management (Production)

For production environments, consider using Azure Key Vault with the Secrets Store CSI Driver:

### 1. Install CSI Driver

```bash
helm repo add csi-secrets-store-provider-azure https://azure.github.io/secrets-store-csi-driver-provider-azure/charts
helm install csi csi-secrets-store-provider-azure/csi-secrets-store-provider-azure
```

### 2. Create SecretProviderClass

```yaml
apiVersion: secrets-store.csi.x-k8s.io/v1
kind: SecretProviderClass
metadata:
  name: azure-retailmonolith-secrets
  namespace: retailmonolith
spec:
  provider: azure
  parameters:
    usePodIdentity: "false"
    useVMManagedIdentity: "true"
    userAssignedIdentityID: <your-identity-id>
    keyvaultName: <your-keyvault-name>
    objects: |
      array:
        - |
          objectName: db-connection-string
          objectType: secret
    tenantId: <your-tenant-id>
```

### 3. Mount Secrets in Deployment

Add volume mount to deployment:
```yaml
volumeMounts:
- name: secrets-store
  mountPath: "/mnt/secrets"
  readOnly: true
volumes:
- name: secrets-store
  csi:
    driver: secrets-store.csi.k8s.io
    readOnly: true
    volumeAttributes:
      secretProviderClass: azure-retailmonolith-secrets
```

## Monitoring

### View Metrics

```bash
# CPU and memory usage
kubectl top pods -n retailmonolith

# HPA status
kubectl get hpa -n retailmonolith
```

### Access Application

```bash
# Port forward for local access
kubectl port-forward svc/retailmonolith-service 8080:80 -n retailmonolith

# Access at http://localhost:8080
```

## Troubleshooting

### Pods not starting

```bash
# Describe pod for events
kubectl describe pod <pod-name> -n retailmonolith

# Check logs
kubectl logs <pod-name> -n retailmonolith

# Check events
kubectl get events -n retailmonolith --sort-by='.lastTimestamp'
```

### Database connection issues

```bash
# Test connection from pod
kubectl exec -it <pod-name> -n retailmonolith -- sh
# Inside pod, check connection string

# Verify secret
kubectl get secret retailmonolith-secrets -n retailmonolith -o yaml
```

### Service not accessible

```bash
# Check service endpoints
kubectl get endpoints -n retailmonolith

# Check service
kubectl describe svc retailmonolith-service -n retailmonolith

# Check network policies
kubectl get networkpolicies -n retailmonolith
```

## Cleanup

To remove all resources:

```bash
# Delete namespace (removes all resources)
kubectl delete namespace retailmonolith

# Or delete individual resources
kubectl delete -f .
```

## Customization

### Multiple Environments

Create separate manifests for each environment:

```
k8s/
├── base/
│   ├── deployment.yaml
│   └── service.yaml
├── overlays/
│   ├── dev/
│   │   └── kustomization.yaml
│   ├── staging/
│   │   └── kustomization.yaml
│   └── prod/
│       └── kustomization.yaml
```

Use Kustomize for environment-specific configurations:

```bash
kubectl apply -k overlays/prod/
```

## Additional Resources

- [Kubernetes Documentation](https://kubernetes.io/docs/)
- [Azure Kubernetes Service](https://learn.microsoft.com/en-us/azure/aks/)
- [kubectl Cheat Sheet](https://kubernetes.io/docs/reference/kubectl/cheatsheet/)
- [NGINX Ingress Controller](https://kubernetes.github.io/ingress-nginx/)
- [cert-manager Documentation](https://cert-manager.io/docs/)
