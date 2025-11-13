# Build stage
FROM mcr.microsoft.com/dotnet/sdk:6.0 AS build
WORKDIR /src

# Copy csproj and restore
COPY ["AssambleaApi.csproj", "./"]
RUN dotnet restore "AssambleaApi.csproj"

# Copy everything else and build
COPY . .
RUN dotnet publish "AssambleaApi.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:6.0 AS runtime
WORKDIR /app

# Install fonts for PdfSharpCore
RUN apt-get update && apt-get install -y fonts-dejavu-core && rm -rf /var/lib/apt/lists/*

COPY --from=build /app/publish .

EXPOSE 80
ENTRYPOINT ["dotnet", "AssambleaApi.dll"]