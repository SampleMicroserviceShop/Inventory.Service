## Inventory.Microservice
Sample Microservice Shop Inventory microservice.


## Create and publish package
```powershell
$version="1.0.2"
$owner="SampleMicroserviceShop"
dotnet pack --configuration Release -p:PackageVersion=$version -o ..\..\packages\$owner
```

 ## Add the GitHub package source
```powershell
$owner="SampleMicroserviceShop"
$gh_pat="[PAT HERE]"
dotnet nuget add source --username USERNAME --password $gh_pat --store-password-in-clear-text --name github https://nuget.pkg.github.com/$owner/index.json
```
 ## Push Package to GitHub
```powershell
$version="1.0.2"
$gh_pat="[PAT HERE]"
$owner="SampleMicroserviceShop"
dotnet nuget push ..\..\packages\$owner\Inventory.Service.$version.nupkg --api-key $gh_pat --source "github"
or
dotnet nuget push ..\..\packages\$owner\Inventory.Contracts.$version.nupkg --api-key $gh_pat --source "github"
```
