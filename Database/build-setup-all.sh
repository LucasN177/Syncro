#!/usr/bin/env bash
# Fügt die Migrationen 000–010 zu einem Skript für den Supabase SQL-Editor zusammen.
# Aufruf aus dem Repo-Root:  bash Database/build-setup-all.sh
# Optional ein anderes Ziel:  bash Database/build-setup-all.sh /tmp/setup_all.sql
set -euo pipefail

cd "$(dirname "$0")"
TARGET="${1:-setup_all.sql}"

{
    echo "-- ============================================================================"
    echo "-- Syncro: komplettes Datenbank-Setup für Supabase"
    echo "--"
    echo "-- Generiert aus den Migrationen 000–010 mit build-setup-all.sh."
    echo "-- Nicht von Hand bearbeiten: Änderungen in den Einzeldateien machen und neu generieren."
    echo "--"
    echo "-- Im Supabase SQL-Editor komplett ausführen. Das Skript ist idempotent und"
    echo "-- übernimmt auch eine Datenbank, in der die frühere projects.sql lief."
    echo "-- ============================================================================"
    for migration in 0*.sql; do
        echo
        echo
        echo "-- ┌──────────────────────────────────────────────────────────────────────────"
        echo "-- │ $migration"
        echo "-- └──────────────────────────────────────────────────────────────────────────"
        echo
        # BOM und Windows-Zeilenenden entfernen, damit das Ergebnis überall gleich ist
        sed -e '1s/^\xEF\xBB\xBF//' -e 's/\r$//' "$migration"
    done
} > "$TARGET"

echo "Geschrieben: Database/$TARGET"
