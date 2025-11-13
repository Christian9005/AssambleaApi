# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

# Copia la solución y el/los csproj para cachear restore
COPY *.sln ./
# Si tu proyecto se llama AssambleaApi.csproj (está en la misma carpeta que el Dockerfile)
COPY AssambleaApi.csproj ./

# Restaura solo el proyecto (más rápido que copiar todo)
RUN dotnet restore "AssambleaApi.csproj"

# Copia todo y publica
COPY . .
RUN dotnet publish "AssambleaApi.csproj" -c Release -o /app/publish

# Runtime
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS runtime
WORKDIR /app
ENV ASPNETCORE_URLS=http://+:80
EXPOSE 80
COPY --from=build /app/publish .
ENTRYPOINT ["dotnet", "AssambleaApi.dll"]