# Stage 1: Build Angular
FROM node:20-alpine AS frontend-build
WORKDIR /app/frontend
COPY frontend/package*.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

# Stage 2: Build .NET
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS backend-build
WORKDIR /app/backend
COPY backend/ ./
RUN dotnet restore
RUN dotnet publish src/RecipeManager.Api/RecipeManager.Api.csproj -c Release -o /app/publish

# Stage 3: Runtime
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
RUN mkdir -p /app/uploads
COPY --from=backend-build /app/publish .
COPY --from=frontend-build /app/frontend/dist/frontend/browser ./wwwroot
EXPOSE 5000
ENV ASPNETCORE_URLS=http://+:5000
ENTRYPOINT ["dotnet", "RecipeManager.Api.dll"]
