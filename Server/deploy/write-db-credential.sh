#!/usr/bin/env bash
set -euo pipefail

if (( EUID != 0 )); then
    echo "Run this script as root." >&2
    exit 1
fi

target=/etc/almost-delivered/db-connection
if [[ -e "$target" ]]; then
    echo "Credential file already exists; refusing to replace it." >&2
    exit 1
fi

umask 077
read -r -s -p "almost_api MySQL password: " db_password
printf '\n'
if [[ -z "$db_password" ]]; then
    echo "Password is empty." >&2
    exit 1
fi

temporary_file=$(mktemp /etc/almost-delivered/.db-connection.XXXXXX)
trap 'rm -f "$temporary_file"' EXIT
printf 'Server=127.0.0.1;Port=3306;Database=almost_delivered;User ID=almost_api;Password=%s;SslMode=None;Connection Timeout=5\n' "$db_password" > "$temporary_file"
unset db_password
chmod 0600 "$temporary_file"
mv "$temporary_file" "$target"
trap - EXIT
echo "Credential file stored with root-only access."
