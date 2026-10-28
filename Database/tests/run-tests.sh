#!/usr/bin/env bash
# Führt die Migrationen und SQL-Tests gegen ein frisches Postgres in Docker aus.
# Aufruf aus dem Repo-Root:  bash Database/tests/run-tests.sh
# Voraussetzung: Docker. Der Container wird am Ende wieder entfernt.
set -euo pipefail

cd "$(dirname "$0")/.."
CONTAINER="syncro-sql-tests"
IMAGE="${POSTGRES_IMAGE:-postgres:17}"

psql_db() {
    docker exec -i "$CONTAINER" psql -U postgres -d "$1" -v ON_ERROR_STOP=1 -q "${@:2}"
}

cleanup() { docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; }
trap cleanup EXIT

cleanup
docker run -d --name "$CONTAINER" -e POSTGRES_PASSWORD=postgres "$IMAGE" >/dev/null
echo "Warte auf Postgres ..."
until docker logs "$CONTAINER" 2>&1 | grep -q "PostgreSQL init process complete" \
      && docker exec "$CONTAINER" psql -U postgres -c 'select 1' >/dev/null 2>&1; do
    sleep 1
done

# Neue Datenbank mit Stub; alle Migrationen laufen zweimal, um die Idempotenz zu prüfen.
# Bricht das Skript ab, wenn eine Migration fehlschlägt.
prepare_db() {
    local db="$1" legacy="${2:-}"
    docker exec "$CONTAINER" psql -U postgres -q -c "create database $db" >/dev/null
    psql_db "$db" < tests/supabase_stub.sql
    if [[ -n "$legacy" ]]; then
        psql_db "$db" < tests/legacy_projects.sql
        psql_db "$db" -c "insert into auth.users values ('11111111-1111-1111-1111-111111111111','alt@example.test');
                          insert into projects(id,user_id,name) values (gen_random_uuid(),'11111111-1111-1111-1111-111111111111','123 Altprojekt');"
    fi
    for run in 1 2; do
        for migration in 0*.sql; do
            psql_db "$db" -c "set client_min_messages = warning" -f - < "$migration" \
                || { echo "FAILED: $migration (Durchlauf $run)"; exit 1; }
        done
    done
}

failed=0

echo "== Upgrade von projects.sql"
prepare_db t_upgrade legacy
key=$(psql_db t_upgrade -tAc "select key from projects where name = '123 Altprojekt'")
admins=$(psql_db t_upgrade -tAc "select count(*) from project_members where role = 'admin'")
if [[ "$key" == "P123" && "$admins" == "1" ]]; then
    echo "ok: bestehendes Projekt bekommt Kürzel P123 und den Eigentümer als admin"
else
    echo "FAILED: Upgrade (key=$key, admins=$admins)"
    failed=1
fi

for test in tests/0*.sql; do
    name="t_$(basename "$test" .sql)"
    echo "== $(basename "$test")"
    prepare_db "$name"

    status=0
    output=$(psql_db "$name" < "$test" 2>&1) || status=$?
    echo "$output" | grep -E "ok:|FAILED|ERROR" | sed 's/^.*NOTICE:  //' || true
    if [[ $status -ne 0 ]]; then
        failed=1
    fi
done

if [[ $failed -eq 0 ]]; then
    echo "Alle SQL-Tests bestanden."
else
    echo "SQL-Tests fehlgeschlagen."
    exit 1
fi
