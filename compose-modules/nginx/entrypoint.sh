#!/bin/sh
set -e

DOMAIN="${DOMAIN:-localhost}"
DEBUG="${DEBUG:-false}"
CERT_DAYS="${CERT_DAYS:-3650}"
CERT_DIR="/etc/nginx/certs"
CRT="${CERT_DIR}/server.crt"
KEY="${CERT_DIR}/server.key"

log() { echo "[cert-init] $*"; }

rm -f /etc/nginx/conf.d/default.conf # remove default config
# ---------- 1. Render nginx config ----------
if [ "${DEBUG}" = "true" ]; then
    log "DEBUG=true -> HTTP only"
    envsubst '${DOMAIN}' < /etc/nginx/configs/http-only.conf.template \
        > /etc/nginx/conf.d/default.conf
else
    log "DEBUG=false -> HTTPS + HTTP redirect"
    envsubst '${DOMAIN}' < /etc/nginx/configs/https.conf.template \
        > /etc/nginx/conf.d/default.conf
fi

# ---------- 2. Ensure cert (skip in DEBUG) ----------
if [ "${DEBUG}" = "true" ]; then
    exit 0
fi

mkdir -p "${CERT_DIR}"

need_regen=0
reason=""

if [ ! -s "${CRT}" ] || [ ! -s "${KEY}" ]; then
    need_regen=1
    reason="cert or key missing"
else
    # Check CN matches DOMAIN
    cn="$(openssl x509 -in "${CRT}" -noout -subject -nameopt multiline 2>/dev/null \
          | awk -F'= ' '/commonName/{print $2; exit}')"

    # Check SAN contains DOMAIN
    san="$(openssl x509 -in "${CRT}" -noout -ext subjectAltName 2>/dev/null || true)"

    # Check expiry (regen if < 30 days left)
    if ! openssl x509 -in "${CRT}" -noout -checkend $((30*24*3600)) >/dev/null 2>&1; then
        need_regen=1
        reason="cert expires within 30 days"
    fi

#    if [ "$cn" != "$DOMAIN" ]; then
#        # CN mismatch is only fatal if SAN also lacks DOMAIN
#        if ! echo "$san" | grep -q "DNS:${DOMAIN}\([,[:space:]]\|$\)"; then
#            need_regen=1
#            reason="cert CN='$cn' SAN does not include '$DOMAIN'"
#        fi
#    fi

    # Key/cert pair sanity
    if [ "${need_regen}" = "0" ]; then
        if ! openssl x509 -noout -modulus -in "${CRT}" 2>/dev/null \
             | openssl md5 > /tmp/a 2>/dev/null \
           || ! openssl rsa  -noout -modulus -in "${KEY}" 2>/dev/null \
             | openssl md5 > /tmp/b 2>/dev/null \
           || ! cmp -s /tmp/a /tmp/b; then
            need_regen=1
            reason="key/cert pair mismatch"
        fi
    fi
fi

if [ "${need_regen}" = "1" ]; then
    log "Generating self-signed cert for '${DOMAIN}' (${reason})"

    # Build SAN. If DOMAIN is not an IP, also add www.<domain>.
    case "${DOMAIN}" in
        *[!0-9.]*)  # contains non-ip chars -> treat as hostname
            SAN="DNS:${DOMAIN},DNS:www.${DOMAIN}"
            ;;
        *)
            SAN="IP:${DOMAIN}"
            ;;
    esac

    openssl req -x509 -nodes -newkey rsa:2048 \
        -days "${CERT_DAYS}" \
        -keyout "${KEY}" \
        -out "${CRT}" \
        -subj "/CN=${DOMAIN}" \
        -addext "subjectAltName=${SAN}" \
        -addext "keyUsage=digitalSignature,keyEncipherment" \
        -addext "extendedKeyUsage=serverAuth" \
        >/dev/null 2>&1

    chmod 600 "${KEY}"
    chmod 644 "${CRT}"
    log "Cert written to ${CRT}"
else
    log "Existing cert for '${DOMAIN}' is valid — reusing"
fi