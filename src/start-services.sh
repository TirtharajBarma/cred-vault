#!/bin/bash

# CredVault Server Startup Script
# Starts all 6 microservices

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
SERVER_DIR="$SCRIPT_DIR/server/services"

# Colors for output
RED='\033[0;31m'
GREEN='\033[0;32m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

echo -e "${GREEN}Starting all CredVault services...${NC}\n"

# Function to start a service in background
start_service() {
    local service_name=$1
    local service_path=$2
    local port=$3
    
    echo -e "${YELLOW}Starting $service_name on port $port...${NC}"
    
    cd "$service_path" && dotnet run &
    echo $! > /tmp/$service_name.pid
    
    sleep 2
}

# Kill any existing running services
echo -e "${YELLOW}Cleaning up any existing processes...${NC}\n"
pkill -f "dotnet run" 2>/dev/null || true
sleep 1

# Start all services (order matters - gateway last)
start_service "IdentityService" "$SERVER_DIR/identity-service/IdentityService.API" "5001"
start_service "CardService" "$SERVER_DIR/card-service/CardService.API" "5002"
start_service "BillingService" "$SERVER_DIR/billing-service/BillingService.API" "5003"
start_service "PaymentService" "$SERVER_DIR/payment-service/PaymentService.API" "5004"
start_service "NotificationService" "$SERVER_DIR/notification-service/NotificationService.API" "5005"
start_service "Gateway" "$SERVER_DIR/gateway/Gateway.API" "5006"

echo -e "\n${GREEN}========================================${NC}"
echo -e "${GREEN}All services started!${NC}"
echo -e "${GREEN}========================================${NC}"
echo ""
echo "Services running:"
echo "  - Gateway:        http://localhost:5006"
echo "  - Identity:       http://localhost:5001"
echo "  - Card:            http://localhost:5002"
echo "  - Billing:         http://localhost:5003"
echo "  - Payment:         http://localhost:5004"
echo "  - Notification:    http://localhost:5005"
echo ""
echo "To stop all services, run: ./stop-services.sh"
echo ""

# Save PIDs for stop script
echo "#!/bin/bash" > /tmp/stop-services.sh
echo "pkill -f 'dotnet run' 2>/dev/null || true" >> /tmp/stop-services.sh
echo "rm -f /tmp/*.pid" >> /tmp/stop-services.sh
echo "echo 'All services stopped'" >> /tmp/stop-services.sh
chmod +x /tmp/stop-services.sh