FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY src/RecipeManager.Api/RecipeManager.Api.csproj src/RecipeManager.Api/
RUN dotnet restore src/RecipeManager.Api/RecipeManager.Api.csproj

COPY src/RecipeManager.Api/ src/RecipeManager.Api/
RUN dotnet publish src/RecipeManager.Api/RecipeManager.Api.csproj -c Release -o /app/publish /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

ENV ASPNETCORE_URLS=http://+:8080
ENV ASPNETCORE_ENVIRONMENT=Production

COPY --from=build /app/publish ./

EXPOSE 8080
ENTRYPOINT ["dotnet", "RecipeManager.Api.dll"]
