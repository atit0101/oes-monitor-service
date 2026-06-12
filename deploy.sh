#!/bin/bash
# ============================================================
# OES Monitor Service — Deploy Script
# Usage:
#   ./deploy.sh           → deploy DEV stack
#   ./deploy.sh uat       → deploy UAT stack
#   ./deploy.sh down      → stop DEV stack
#   ./deploy.sh uat down  → stop UAT stack
# ============================================================

set -e

ENV="${1:-dev}"
ACTION="${2:-up}"

# ── Load .env ──────────────────────────────────────────────
if [ "$ENV" = "uat" ]; then
  ENV_FILE=".env.uat"
  COMPOSE_FILE="docker-compose.uat.yml"
  PROM_TEMPLATE="prometheus/prometheus.uat.template.yml"
  PROM_OUTPUT="prometheus/prometheus.uat.yml"
else
  ENV_FILE=".env"
  COMPOSE_FILE="docker-compose.yml"
  PROM_TEMPLATE="prometheus/prometheus.template.yml"
  PROM_OUTPUT="prometheus/prometheus.yml"
fi

if [ ! -f "$ENV_FILE" ]; then
  echo "❌ ไม่พบ $ENV_FILE — กรุณา copy จาก .env.example แล้วกรอก password"
  echo "   cp .env.example $ENV_FILE"
  exit 1
fi

# โหลดตัวแปรจาก .env
set -a
source "$ENV_FILE"
set +a

# ── Stop ───────────────────────────────────────────────────
if [ "$ACTION" = "down" ]; then
  echo "🛑 Stopping $ENV stack..."
  docker compose -f "$COMPOSE_FILE" down
  exit 0
fi

# ── Generate prometheus.yml จาก template ──────────────────
echo "⚙️  Generating $PROM_OUTPUT from template..."

if command -v envsubst &> /dev/null; then
  envsubst < "$PROM_TEMPLATE" > "$PROM_OUTPUT"
else
  # macOS fallback: ใช้ sed แทน envsubst
  sed \
    -e "s|HOST_DEV|${HOST_DEV}|g" \
    -e "s|HOST_UAT|${HOST_UAT}|g" \
    "$PROM_TEMPLATE" > "$PROM_OUTPUT"
fi

echo "   ✅ $PROM_OUTPUT พร้อมแล้ว (HOST_DEV=${HOST_DEV}, HOST_UAT=${HOST_UAT})"

# ── Create Grafana dashboard folder ────────────────────────
mkdir -p grafana/provisioning/dashboards/json

# ── Deploy ─────────────────────────────────────────────────
echo "🚀 Starting $ENV stack..."
docker compose -f "$COMPOSE_FILE" --env-file "$ENV_FILE" up -d --build

echo ""
echo "✅ OES Monitor ($ENV) is running!"
echo ""
echo "  Grafana    → http://localhost:3000  (admin / ${GRAFANA_ADMIN_PASSWORD:-admin123})"
echo "  Prometheus → http://localhost:9090"
echo "  MonitorAPI → http://localhost:${MONITOR_API_PORT:-5000}/swagger"
echo ""
