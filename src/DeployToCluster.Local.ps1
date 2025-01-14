# Create Namespaces
Write-Host ""
Write-Host "Creating Namespaces"
kubectl create namespace local
kubectl create namespace otel

# Update Kubernetes
Write-Host ""
Write-Host "Updating cluster"

# Delete Deployments
Write-Host ""
Write-Host "Deleting Deployments"
kubectl delete deployment --all --namespace=local

# Delete Services
Write-Host ""
Write-Host "Deleting Services"
kubectl delete service --all --namespace=local

# Change working directory to K8s yaml
Write-Host ""
Write-Host "Changing working directory"
Set-Location C:\Projects\sports-data-config\00_local

# Apply Deployments
Write-Host ""
Write-Host "Applying Deployments"
kubectl apply -f Deployments

# Apply Services
Write-Host ""
Write-Host "Applying Services"
kubectl apply -f Services

# Apply Prometheus Configuration
Write-Host ""
Write-Host "Applying Prometheus Configuration"
kubectl apply -f ./deployments/prometheus/prometheus-config.yml -n local

Write-Host ""
Read-Host -Prompt "Deploy Complete. Press <Enter> to exit"