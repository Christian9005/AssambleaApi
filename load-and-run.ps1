# ============================================
# Script de Activación - Sistema de Voto Electrónico
# Coordinación General de Participación Ciudadana
# Plataforma: Windows
# ============================================

$ErrorActionPreference = "Stop"

# Colores para mensajes
function Write-ColorOutput($ForegroundColor) {
    $fc = $host.UI.RawUI.ForegroundColor
    $host.UI.RawUI.ForegroundColor = $ForegroundColor
    if ($args) {
        Write-Output $args
    }
    $host.UI.RawUI.ForegroundColor = $fc
}

Write-Host "============================================" -ForegroundColor Cyan
Write-Host "Sistema de Voto Electrónico" -ForegroundColor Cyan
Write-Host "Coordinación General de Participación Ciudadana" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

# Verificar Docker
Write-Host "Verificando Docker..." -ForegroundColor Yellow
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Docker no está instalado o no está en el PATH" -ForegroundColor Red
    Write-Host "Por favor instale Docker Desktop desde: https://www.docker.com/products/docker-desktop" -ForegroundColor Red
    exit 1
}

# Verificar Docker Compose
if (-not (Get-Command docker -ErrorAction SilentlyContinue)) {
    Write-Host "ERROR: Docker Compose no está disponible" -ForegroundColor Red
    exit 1
}

Write-Host "Docker version:" -ForegroundColor Green
docker --version
docker compose version
Write-Host ""

# Crear red si no existe
Write-Host "Creando red Docker..." -ForegroundColor Yellow
docker network create assamblea-network 2>$null
if ($LASTEXITCODE -eq 0) {
    Write-Host "Red 'assamblea-network' creada correctamente" -ForegroundColor Green
} else {
    Write-Host "Red 'assamblea-network' ya existe" -ForegroundColor Yellow
}
Write-Host ""

# Detener contenedores existentes
Write-Host "Deteniendo contenedores existentes..." -ForegroundColor Yellow
docker compose -f docker-compose.backend.yml down 2>$null
docker compose -f docker-compose.frontend.yml down 2>$null
Write-Host ""

# ==================== BACKEND (Base de Datos + API) ====================
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "BACKEND: Construyendo e iniciando..." -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

docker compose -f docker-compose.backend.yml up -d --build

if ($LASTEXITCODE -ne 0) {
    Write-Host "ERROR: Falló la construcción del backend" -ForegroundColor Red
    exit 1
}

Write-Host "Backend iniciado correctamente" -ForegroundColor Green
Write-Host ""

# Esperar a que el API esté disponible
Write-Host "Esperando a que el API esté disponible..." -ForegroundColor Yellow
$maxAttempts = 30
$attempt = 0
$apiReady = $false

while ($attempt -lt $maxAttempts) {
    $attempt++
    try {
        $response = Invoke-WebRequest -Uri "http://localhost:8080/swagger/index.html" -UseBasicParsing -TimeoutSec 2 -ErrorAction SilentlyContinue
        if ($response.StatusCode -eq 200) {
            Write-Host "API está disponible!" -ForegroundColor Green
            $apiReady = $true
            break
        }
    } catch {
        Write-Host "Intento $attempt de $maxAttempts - Esperando..." -ForegroundColor Gray
        Start-Sleep -Seconds 2
    }
}

if (-not $apiReady) {
    Write-Host "ADVERTENCIA: El API no respondió después de $maxAttempts intentos" -ForegroundColor Yellow
    Write-Host "Revise los logs con: docker compose -f docker-compose.backend.yml logs -f api" -ForegroundColor Yellow
}
Write-Host ""

# ==================== FRONTEND ====================
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "FRONTEND: Construyendo e iniciando..." -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan

# Verificar que exista la carpeta del frontend
$frontendPath = "..\dashboard-asamblea"
if (-not (Test-Path $frontendPath)) {
    Write-Host "ADVERTENCIA: No se encontró la carpeta del frontend en: $frontendPath" -ForegroundColor Yellow
    Write-Host "Omitiendo despliegue del frontend" -ForegroundColor Yellow
} else {
    docker compose -f docker-compose.frontend.yml up -d --build

    if ($LASTEXITCODE -ne 0) {
        Write-Host "ERROR: Falló la construcción del frontend" -ForegroundColor Red
    } else {
        Write-Host "Frontend iniciado correctamente" -ForegroundColor Green
    }
}
Write-Host ""

# ==================== VERIFICACIÓN ====================
Write-Host "============================================" -ForegroundColor Cyan
Write-Host "ESTADO DE LOS SERVICIOS" -ForegroundColor Cyan
Write-Host "============================================" -ForegroundColor Cyan
Write-Host ""

docker ps --filter "name=assamblea" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
Write-Host ""

# ==================== INFORMACIÓN ====================
Write-Host "============================================" -ForegroundColor Green
Write-Host "SISTEMA INICIADO CORRECTAMENTE" -ForegroundColor Green
Write-Host "============================================" -ForegroundColor Green
Write-Host ""
Write-Host "URLs de acceso:" -ForegroundColor Cyan
Write-Host "  - API (Swagger):  http://localhost:8080/swagger" -ForegroundColor White
Write-Host "  - Frontend:       http://localhost:3000" -ForegroundColor White
Write-Host "  - Base de Datos:  localhost:5433" -ForegroundColor White
Write-Host ""
Write-Host "Credenciales de PostgreSQL:" -ForegroundColor Cyan
Write-Host "  - Usuario:        postgres" -ForegroundColor White
Write-Host "  - Contraseña:     postgrespw" -ForegroundColor White
Write-Host "  - Base de Datos:  assamblea" -ForegroundColor White
Write-Host ""
Write-Host "Comandos útiles:" -ForegroundColor Cyan
Write-Host "  Ver logs del backend:" -ForegroundColor White
Write-Host "    docker compose -f docker-compose.backend.yml logs -f" -ForegroundColor Gray
Write-Host ""
Write-Host "  Ver logs del frontend:" -ForegroundColor White
Write-Host "    docker compose -f docker-compose.frontend.yml logs -f" -ForegroundColor Gray
Write-Host ""
Write-Host "  Detener todos los servicios:" -ForegroundColor White
Write-Host "    .\stop.ps1" -ForegroundColor Gray
Write-Host ""
Write-Host "============================================" -ForegroundColor Green