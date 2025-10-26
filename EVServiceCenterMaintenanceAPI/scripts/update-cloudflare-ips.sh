#!/bin/bash
# Auto-update Cloudflare IPs for Nginx
# Run this script weekly via cron

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT_DIR="$(dirname "$SCRIPT_DIR")"
NGINX_CONF="$PROJECT_DIR/nginx/conf.d/cloudflare-ips.conf"
TMP_CONF="/tmp/cloudflare-ips.conf"

echo "=== Cloudflare IPs Auto-Update ==="
echo "Checking for updates..."

# Download latest Cloudflare IPs
echo "Downloading Cloudflare IP ranges..."
CF_IPV4=$(curl -s https://www.cloudflare.com/ips-v4)
CF_IPV6=$(curl -s https://www.cloudflare.com/ips-v6)

# Check if download successful
if [ -z "$CF_IPV4" ] || [ -z "$CF_IPV6" ]; then
    echo "ERROR: Failed to download Cloudflare IPs"
    exit 1
fi

# Generate new config
cat > "$TMP_CONF" << 'EOF'
# Cloudflare real IP configuration
# Auto-generated - DO NOT EDIT MANUALLY
# Last update: $(date '+%Y-%m-%d %H:%M:%S')

EOF

echo "# IPv4 ranges" >> "$TMP_CONF"
echo "$CF_IPV4" | while read -r ip; do
    echo "set_real_ip_from $ip;" >> "$TMP_CONF"
done

echo "" >> "$TMP_CONF"
echo "# IPv6 ranges" >> "$TMP_CONF"
echo "$CF_IPV6" | while read -r ip; do
    echo "set_real_ip_from $ip;" >> "$TMP_CONF"
done

echo "" >> "$TMP_CONF"
echo "real_ip_header CF-Connecting-IP;" >> "$TMP_CONF"

# Compare with existing config
if [ -f "$NGINX_CONF" ]; then
    if diff -q "$TMP_CONF" "$NGINX_CONF" > /dev/null 2>&1; then
        echo "✅ No changes detected"
        rm "$TMP_CONF"
        exit 0
    fi
fi

# Backup old config
if [ -f "$NGINX_CONF" ]; then
    cp "$NGINX_CONF" "$NGINX_CONF.backup"
    echo "📦 Backed up old config"
fi

# Install new config
cp "$TMP_CONF" "$NGINX_CONF"
echo "✅ Updated Cloudflare IPs"

# Test nginx config
echo "Testing Nginx configuration..."
if docker exec evservice-nginx nginx -t 2>/dev/null; then
    echo "✅ Nginx config is valid"
    
    # Reload nginx
    echo "Reloading Nginx..."
    docker exec evservice-nginx nginx -s reload
    echo "✅ Nginx reloaded successfully"
    
    echo ""
    echo "🎉 Cloudflare IPs updated successfully!"
else
    echo "❌ Nginx config test failed!"
    echo "Restoring backup..."
    mv "$NGINX_CONF.backup" "$NGINX_CONF"
    echo "⚠️  Rolled back to previous config"
    exit 1
fi

# Cleanup
rm -f "$TMP_CONF"

