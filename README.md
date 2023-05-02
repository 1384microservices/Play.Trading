### Publish service container image
```powershell
# Create docker image
docker-compose build

$imageVersion="1.0.7"
docker tag "play.trading:latest" "play.trading:${imageVersion}"

$appName="playeconomy1384"
$repositoryUrl="${appName}.azurecr.io"

docker tag "play.trading:latest" "${repositoryUrl}/play.trading:${imageVersion}"

az acr login --name $appName
docker push "${repositoryUrl}/play.trading:${imageVersion}"
```

### Create pod managed identity and grant Key Vault access
```powershell
# Create azure Identity
$appName="playeconomy1384"
$k8sNS="trading"

az identity create --resource-group $appName --name $k8sNS

# Create pod managed identity
$identityResourceId = az identity show --resource-group $appName --name $k8sNS --query id -otsv
az aks pod-identity add --resource-group $appName --cluster-name $appName --namespace $k8sNS --name $k8sNS --identity-resource-id $identityResourceId

# Grant pod Key Vault access
$identityClientId = az identity show --resource-group $appName --name $k8sNS --query clientId -otsv
az keyvault set-policy -n $appName --secret-permissions get list --spn $identityClientId

# Set federated identity credentials
$aksOIDIssuer=az aks show -n $appName -g $appName --query "oidcIssuerProfile.issuerUrl" -otsv
az identity federated-credential create --name $k8sNS --identity-name $k8sNS --resource-group $appName --issuer $aksOIDIssuer --subject "system:serviceaccount:${k8sNS}:${k8sNS}-serviceaccount"
```

### Create K8S pod
```powershell
$appName="playeconomy1384"
$k8sNS="trading"
kubectl create namespace $k8sNS
kubectl apply -f kubernetes\trading.yaml -n $k8sNS
```