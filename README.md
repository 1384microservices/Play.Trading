### Publish service container image
```powershell
# Create docker image
$imageVersion="1.0.6"
docker-compose build
docker tag "play.trading:latest" "play.trading:${imageVersion}"

$appName="playeconomy1384"
$repositoryUrl="${appName}.azurecr.io"

docker tag "play.trading:latest" "${repositoryUrl}/play.trading:${imageVersion}"

az acr login --name $appName
docker push "${repositoryUrl}/play.trading:${imageVersion}"
```

### Creating the Azure Managed Identity and granting access to Key Vault secrets
```powershell
# Create azure Identity
$appName="playeconomy1384"
$k8sNS="trading"
az identity create --resource-group $appName --name $k8sNS

# Fetch Identity client id
$identityClientId = az identity show --resource-group $appName --name $k8sNS --query clientId -otsv

# Assign get and list permissions to client (Identity)
az keyvault set-policy -n $appName --secret-permissions get list --spn $identityClientId
```