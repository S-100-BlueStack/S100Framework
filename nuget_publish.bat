dotnet nuget push ".\src\S100Framework.ArcGIS.Core\bin\x64\Debug\*.nupkg" --api-key %github-api-key% --source "github" --skip-duplicate
dotnet nuget push ".\src\S100Framework.Catalogues\bin\Debug\*.nupkg" --api-key %github-api-key% --source "github" --skip-duplicate
dotnet nuget push ".\src\S100Framework.Catalogues.ProductSpecifications\bin\Debug\*.nupkg" --api-key %github-api-key% --source "github" --skip-duplicate
dotnet nuget push ".\src\S100Framework.GML\bin\Debug\*.nupkg" --api-key %github-api-key% --source "github" --skip-duplicate
dotnet nuget push ".\src\S100Framework.Topology\bin\Debug\*.nupkg" --api-key %github-api-key% --source "github" --skip-duplicate
dotnet nuget push ".\src\S100Framework.YAML\bin\x64\Debug\*.nupkg" --api-key %github-api-key% --source "github" --skip-duplicate
dotnet nuget push ".\src\S100Framework.ProductCatalogue\bin\x64\Debug\*.nupkg" --api-key %github-api-key% --source "github" --skip-duplicate
dotnet nuget push ".\src\S100Framework.REST\bin\Debug\S100Framework.REST.1.0.0.nupkg" --api-key %github-api-key% --source "github" --skip-duplicate