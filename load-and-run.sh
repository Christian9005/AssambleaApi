#!/bin/bash

# ============================================
# Script de Activación - Sistema de Voto Electrónico
# Coordinación General de Participación Ciudadana
# Plataforma: Linux
# ============================================

set -e

# Colores
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
CYAN='\033[0;36m'
NC='\033[0m' # No Color

echo -e "${CYAN}============================================${NC}"
echo -e "${CYAN}Sistema de Voto Electrónico${NC}"
echo -e "${CYAN}Coordinación General de Participación Ciudadana${NC}"
echo -e "${CYAN}============================================${NC}"
echo ""

# Verificar Docker
echo -e "${YELLOW}Verificando Docker...${NC}"
if ! command -v docker &> /dev/null; then
    echo -e "${RED}ERROR: Docker no está instalado${NC}"
    echo -e "${RED}Por favor instale Docker desde: https://docs.docker.com/engine/install/${NC}"
    exit 1
fi

# Verificar Docker Compose
if ! command -v docker compose &> /dev/null; then
    echo -e "${RED}ERROR: Docker Compose no está disponible${NC}"
    exit 1
fi

echo -e "${GREEN}Docker version:${NC}"
docker --version
docker compose version
echo ""

# Crear red si no existe
echo -e "${YELLOW}Creando red Docker...${NC}"
docker network create assamblea-network 2>/dev/null && echo -e "${GREEN}Red 'assamblea-network' creada correctamente${NC}" || echo -e "${YELLOW}Red 'assamblea-network' ya existe${NC}"
echo ""

# Detener contenedores existentes
echo -e "${YELLOW}Deteniendo contenedores existentes...${NC}"
docker compose -f docker-compose.backend.yml down 2>/dev/null || true
docker compose -f docker-compose.frontend.yml down 2>/dev/null || true
echo ""

# ==================== BACKEND (Base de Datos + API) ====================
echo -e "${CYAN}============================================${NC}"
echo -e "${CYAN}BACKEND: Construyendo e iniciando...${NC}"
echo -e "${CYAN}============================================${NC}"

docker compose -f docker-compose.backend.yml up -d --build

if [ $? -ne 0 ]; then
    echo -e "${RED}ERROR: Falló la construcción del backend${NC}"
    exit 1
fi

echo -e "${GREEN}Backend iniciado correctamente${NC}"
echo ""

# Esperar a que el API esté disponible
echo -e "${YELLOW}Esperando a que el API esté disponible...${NC}"
max_attempts=30
attempt=0
api_ready=false

while [ $attempt -lt $max_attempts ]; do
    attempt=$((attempt + 1))
    if curl -f -s http://localhost:8080/swagger/index.html > /dev/null 2>&1; then
        echo -e "${GREEN}API está disponible!${NC}"
        api_ready=true
        break
    fi
    echo -e "${NC}Intento $attempt de $max_attempts - Esperando...${NC}"
    sleep 2
done

if [ "$api_ready" = false ]; then
    echo -e "${YELLOW}ADVERTENCIA: El API no respondió después de $max_attempts intentos${NC}"
    echo -e "${YELLOW}Revise los logs con: docker compose -f docker-compose.backend.yml logs -f api${NC}"
fi
echo ""

# ==================== FRONTEND ====================
echo -e "${CYAN}============================================${NC}"
echo -e "${CYAN}FRONTEND: Construyendo e iniciando...${NC}"
echo -e "${CYAN}============================================${NC}"

# Verificar que exista la carpeta del frontend
frontend_path="../dashboard-asamblea"
if [ ! -d "$frontend_path" ]; then
    echo -e "${YELLOW}ADVERTENCIA: No se encontró la carpeta del frontend en: $frontend_path${NC}"
    echo -e "${YELLOW}Omitiendo despliegue del frontend${NC}"
else
    docker compose -f docker-compose.frontend.yml up -d --build

    if [ $? -ne 0 ]; then
        echo -e "${RED}ERROR: Falló la construcción del frontend${NC}"
    else
        echo -e "${GREEN}Frontend iniciado correctamente${NC}"
    fi
fi
echo ""

# ==================== VERIFICACIÓN ====================
echo -e "${CYAN}============================================${NC}"
echo -e "${CYAN}ESTADO DE LOS SERVICIOS${NC}"
echo -e "${CYAN}============================================${NC}"
echo ""

docker ps --filter "name=assamblea" --format "table {{.Names}}\t{{.Status}}\t{{.Ports}}"
echo ""

# ==================== INFORMACIÓN ====================
echo -e "${GREEN}============================================${NC}"
echo -e "${GREEN}SISTEMA INICIADO CORRECTAMENTE${NC}"
echo -e "${GREEN}============================================${NC}"
echo ""
echo -e "${CYAN}URLs de acceso:${NC}"
echo -e "  - API (Swagger):  ${NC}http://localhost:8080/swagger"
echo -e "  - Frontend:       ${NC}http://localhost:3000"
echo -e "  - Base de Datos:  ${NC}localhost:5433"
echo ""
echo -e "${CYAN}Credenciales de PostgreSQL:${NC}"
echo -e "  - Usuario:        ${NC}postgres"
echo -e "  - Contraseña:     ${NC}postgrespw"
echo -e "  - Base de Datos:  ${NC}assamblea"
echo ""
echo -e "${CYAN}Comandos útiles:${NC}"
echo -e "  Ver logs del backend:"
echo -e "    ${NC}docker compose -f docker-compose.backend.yml logs -f"
echo ""
echo -e "  Ver logs del frontend:"
echo -e "    ${NC}docker compose -f docker-compose.frontend.yml logs -f"
echo ""
echo -e "  Detener todos los servicios:"
echo -e "    ${NC}./stop.sh"
echo ""
echo -e "${GREEN}============================================${NC}"