### Publish service container image
```powershell
# Create docker image
$imageVersion="1.0.5"
docker-compose build
docker tag "play.trading:latest" "play.trading:${imageVersion}"

$appName="playeconomy1384"
$repositoryUrl="${appName}.azurecr.io"

docker tag "play.trading:latest" "${repositoryUrl}/play.trading:${imageVersion}"

az acr login --name $appName
docker push "${repositoryUrl}/play.trading:${imageVersion}"
```