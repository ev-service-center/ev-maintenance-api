domains=(evservicecenter.me www.evservicecenter.me)
rsa_key_size=4096

# Get script directory and project root
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_ROOT="$(cd "$SCRIPT_DIR/.." && pwd)"
data_path="$PROJECT_ROOT/certbot"

email="nvkhang0099@gmail.com" # Adding a valid address is strongly recommended
staging=0 # Set to 1 if you're testing your setup to avoid hitting request limits

echo "=== Let's Encrypt SSL Certificate Setup (DNS Challenge) ==="
echo ""
echo "Project root: $PROJECT_ROOT"
echo "Data path: $data_path"
echo ""

# Check if cloudflare.ini exists
if [ ! -f "$data_path/cloudflare.ini" ]; then
  echo "⚠️  WARNING: $data_path/cloudflare.ini not found!"
  read -p "Do you want to create it now? (y/N): " create_token
  if [ "$create_token" = "y" ] || [ "$create_token" = "Y" ]; then
    read -p "Enter your Cloudflare API Token: " token
    echo "dns_cloudflare_api_token = $token" > "$data_path/cloudflare.ini"
    chmod 600 "$data_path/cloudflare.ini"
    echo "✅ Created cloudflare.ini"
  else
    echo "   Will continue with dummy certificates for local testing"
    echo "   For production, create it manually:"
    echo "     cat > $data_path/cloudflare.ini << EOF"
    echo "     dns_cloudflare_api_token = YOUR_CLOUDFLARE_API_TOKEN"
    echo "     EOF"
    echo "     chmod 600 $data_path/cloudflare.ini"
  fi
  echo ""
else
  echo "✅ Cloudflare credentials found"
fi

## STEP 1: Download recommended TLS parameters
if [ ! -e "$data_path/conf/options-ssl-nginx.conf" ] || [ ! -e "$data_path/conf/ssl-dhparams.pem" ]; then
  echo "### Downloading recommended TLS parameters ..."
  mkdir -p "$data_path/conf"
  curl -s https://raw.githubusercontent.com/certbot/certbot/master/certbot-nginx/certbot_nginx/_internal/tls_configs/options-ssl-nginx.conf > "$data_path/conf/options-ssl-nginx.conf"
  curl -s https://raw.githubusercontent.com/certbot/certbot/master/certbot/certbot/ssl-dhparams.pem > "$data_path/conf/ssl-dhparams.pem"
  echo "✅ TLS parameters downloaded"
else
  echo "✅ TLS parameters already exist"
fi
echo ""

## STEP 2: Create dummy certificate for initial nginx startup
echo "### Creating dummy certificate for ${domains[0]} ..."
path="/etc/letsencrypt/live/${domains[0]}"
mkdir -p "$data_path/conf/live/${domains[0]}"

docker compose -f "$PROJECT_ROOT/docker-compose.deploy.yml" run --rm --entrypoint "\
  openssl req -x509 -nodes -newkey rsa:$rsa_key_size -days 1\
    -keyout '$path/privkey.pem' \
    -out '$path/fullchain.pem' \
    -subj '/CN=localhost'" certbot

if [ $? -eq 0 ]; then
  echo "✅ Dummy certificate created"
else
  echo "❌ Failed to create dummy certificate"
  exit 1
fi
echo ""

## STEP 3: Start all services (sqlserver, backend, nginx)
echo "### Starting SQL Server ..."
docker compose -f "$PROJECT_ROOT/docker-compose.deploy.yml" up -d sqlserver
echo "⏳ Waiting for SQL Server to be ready..."
sleep 30
echo "✅ SQL Server started"
echo ""

echo "### Restoring database from backup ..."
# Check if backup file exists
if docker compose -f "$PROJECT_ROOT/docker-compose.deploy.yml" exec -T sqlserver test -f /var/opt/mssql/backup/EVServiceCenterDB.bak; then
  docker compose -f "$PROJECT_ROOT/docker-compose.deploy.yml" exec -T sqlserver /opt/mssql-tools/bin/sqlcmd \
    -S localhost -U sa -P "${SA_PASSWORD}" \
    -Q "RESTORE DATABASE EVServiceCenterDB FROM DISK = '/var/opt/mssql/backup/EVServiceCenterDB.bak' WITH REPLACE, MOVE 'EVServiceCenterDB' TO '/var/opt/mssql/data/EVServiceCenterDB.mdf', MOVE 'EVServiceCenterDB_log' TO '/var/opt/mssql/data/EVServiceCenterDB_log.ldf'"
  
  if [ $? -eq 0 ]; then
    echo "✅ Database restored successfully"
  else
    echo "⚠️  Database restore failed or already exists"
  fi
else
  echo "⚠️  No backup file found, skipping restore"
fi
echo ""

echo "### Starting Backend API ..."
docker compose -f "$PROJECT_ROOT/docker-compose.deploy.yml" up -d backend
echo "✅ Backend API started"
echo ""

echo "### Starting Nginx (force recreate) ..."
docker compose -f "$PROJECT_ROOT/docker-compose.deploy.yml" up -d --force-recreate nginx
echo "✅ Nginx started"
echo ""

## STEP 4: Delete dummy certificate
echo "### Deleting dummy certificate for ${domains[0]} ..."
docker run --rm --entrypoint "\
  rm -Rf /etc/letsencrypt/live/${domains[0]} && \
  rm -Rf /etc/letsencrypt/archive/${domains[0]} && \
  rm -Rf /etc/letsencrypt/renewal/${domains[0]}.conf" \
  -v "$data_path/conf:/etc/letsencrypt" \
  certbot/dns-cloudflare
echo "✅ Dummy certificate deleted"
echo ""

## STEP 5: Request real Let's Encrypt certificate
echo "### Requesting Let's Encrypt certificate for ${domains[@]} ..."

# Join $domains to -d args
domain_args=""
for domain in "${domains[@]}"; do
  domain_args="$domain_args -d $domain"
done

# Select appropriate email arg
case "$email" in
  "") email_arg="--register-unsafely-without-email" ;;
  *) email_arg="--email $email" ;;
esac

# Enable staging mode if needed
if [ $staging != "0" ]; then 
  staging_arg="--staging"
  echo "⚠️  STAGING MODE: Using Let's Encrypt staging server"
else
  staging_arg=""
fi

docker run --rm --entrypoint "\
  certbot certonly --dns-cloudflare \
    --dns-cloudflare-credentials /etc/letsencrypt/cloudflare.ini \
    $staging_arg \
    $email_arg \
    $domain_args \
    --rsa-key-size $rsa_key_size \
    --agree-tos \
    --non-interactive \
    --force-renewal" \
  -v "$data_path/conf:/etc/letsencrypt" \
  -v "$data_path/cloudflare.ini:/etc/letsencrypt/cloudflare.ini:ro" \
  certbot/dns-cloudflare

if [ $? -eq 0 ]; then
  echo "✅ Real certificate obtained"
else
  echo "❌ Failed to obtain certificate"
  exit 1
fi
echo ""

## STEP 6: Reload nginx
echo "### Reloading nginx ..."
docker compose -f "$PROJECT_ROOT/docker-compose.deploy.yml" exec nginx nginx -s reload
echo "✅ Nginx reloaded"
echo ""

echo "🎉 SSL Certificate setup completed!"
echo ""
echo "Your site should now be accessible at:"
echo "  https://${domains[0]}"
echo ""
echo "Certificate will auto-renew via certbot container."

