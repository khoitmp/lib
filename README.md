# Lib
- [Kernel.Lib](./Kernel.Lib/) (published)
- [Exception.Lib](./Exception.Lib/) (published)
- [GenericRepository.Lib](./GenericRepository.Lib/) (published)
- [DynamicSearch.Lib](./DynamicSearch.Lib/) (published - moved to separate repository)
- [UserContext.Lib](./UserContext.Lib/) (published)
- [RateLimit.Lib](./RateLimit.Lib/)

### Push
```sh
dotnet build
dotnet pack
dotnet nuget push --source nuget.org --api-key <api_key> <path>/<file_name>.nupkg
```