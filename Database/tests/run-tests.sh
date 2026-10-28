#!/usr/bin/env bash
# Führt die Migrationen und SQL-Tests gegen ein frisches Postgres in Docker aus.
# Aufruf aus dem Repo-Root:  bash Database/tests/run-tests.sh
# Voraussetzung: Docker. Der Container wird am Ende wieder entfernt.
set -euo pipefail

cd "$(dirname "$0")/.."
CONTAINER="syncro-sql-tests"
IMAGE="${POSTGRES_IMAGE:-postgres:17}"
failed=0

psql_db() {
    docker exec -i "$CONTAINER" psql -U postgres -d "$1" -v ON_ERROR_STOP=1 -q "${@:2}"
}

check() {
    local description="$1" actual="$2" expected="$3"
    if [[ "$actual" == "$expected" ]]; then
        echo "ok: $description"
    else
        echo "FAILED: $description (erwartet: $expected, erhalten: $actual)"
        failed=1
    fi
}

echo "== setup_all.sql ist aktuell"
generated=$(mktemp)
bash build-setup-all.sh "$generated" >/dev/null
if diff -q --strip-trailing-cr "$generated" setup_all.sql >/dev/null; then
    echo "ok: setup_all.sql entspricht den Einzeldateien"
else
    echo "FAILED: setup_all.sql ist veraltet – bash Database/build-setup-all.sh ausführen"
    failed=1
fi
rm -f "$generated"

cleanup() { docker rm -f "$CONTAINER" >/dev/null 2>&1 || true; }
trap cleanup EXIT

cleanup
docker run -d --name "$CONTAINER" -e POSTGRES_PASSWORD=postgres "$IMAGE" >/dev/null
echo "Warte auf Postgres ..."
until docker logs "$CONTAINER" 2>&1 | grep -q "PostgreSQL init process complete" \
      && docker exec "$CONTAINER" psql -U postgres -c 'select 1' >/dev/null 2>&1; do
    sleep 1
done

run_sql_file() {
    local db="$1" file="$2"
    psql_db "$db" -c "set client_min_messages = warning" -f - < "$file" \
        || { echo "FAILED: $file"; exit 1; }
}

# Neue Datenbank mit Stub. Modus "single" führt 001–010 einzeln aus, "all" das Gesamtskript;
# beides jeweils zweimal, um die Idempotenz zu prüfen. "legacy" legt vorher die alte projects.sql
# mit Daten an, wie sie der frühere Freitext-Editor erlaubte.
prepare_db() {
    local db="$1" mode="$2" legacy="${3:-}"
    docker exec "$CONTAINER" psql -U postgres -q -c "create database $db" >/dev/null
    psql_db "$db" < tests/supabase_stub.sql
    if [[ "$legacy" == "manual" ]]; then
        psql_db "$db" < tests/legacy_manual_table.sql
    elif [[ -n "$legacy" ]]; then
        psql_db "$db" < tests/legacy_projects.sql
        psql_db "$db" -c "
            insert into auth.users values ('11111111-1111-1111-1111-111111111111','alt@example.test');
            insert into projects(id, user_id, name, status, color) values
                (gen_random_uuid(), '11111111-1111-1111-1111-111111111111', '123 Altprojekt', 'Aktiv', '#0f766e'),
                (gen_random_uuid(), '11111111-1111-1111-1111-111111111111', 'X', 'Aktiv', '#0f766e'),
                (gen_random_uuid(), '11111111-1111-1111-1111-111111111111', 'Ölwechsel', 'Active', 'teal'),
                (gen_random_uuid(), '11111111-1111-1111-1111-111111111111', 'Web', 'Pausiert', '#fff');"
    fi
    for run in 1 2; do
        if [[ "$mode" == "all" ]]; then
            run_sql_file "$db" setup_all.sql
        else
            for migration in 0*.sql; do
                run_sql_file "$db" "$migration"
            done
        fi
    done
}

check_upgrade() {
    local db="$1"
    check "Kürzel aus Altdaten" \
        "$(psql_db "$db" -tAc "select string_agg(key || '=' || name, ',' order by key) from projects")" \
        "LWE=Ölwechsel,P123=123 Altprojekt,WEB=Web,XP=X"
    check "Ungültiger Status wird Aktiv, gültiger bleibt" \
        "$(psql_db "$db" -tAc "select string_agg(status || '=' || key, ',' order by key) from projects where key in ('LWE','WEB')")" \
        "Aktiv=LWE,Pausiert=WEB"
    check "Ungültige Farben werden ersetzt" \
        "$(psql_db "$db" -tAc "select count(*) from projects where color = '#0f766e'")" "4"
    check "Eigentümer ist admin in allen Altprojekten" \
        "$(psql_db "$db" -tAc "select count(*) from project_members where role = 'admin'")" "4"
}

echo "== Upgrade von projects.sql (Einzeldateien)"
prepare_db t_upgrade_single single legacy
check_upgrade t_upgrade_single

echo "== Upgrade von projects.sql (setup_all.sql)"
prepare_db t_upgrade_all all legacy
check_upgrade t_upgrade_all

echo "== Upgrade einer von Hand angelegten Tabelle (setup_all.sql)"
prepare_db t_upgrade_manual all manual
check "Fehlende Spalten ergänzt, user_id ist uuid" \
    "$(psql_db t_upgrade_manual -tAc "select string_agg(column_name || ':' || data_type, ',' order by column_name) from information_schema.columns where table_schema = 'public' and table_name = 'projects' and column_name in ('color', 'description', 'status', 'user_id')")" \
    "color:text,description:text,status:text,user_id:uuid"
check "Leerer Name wird ersetzt, Status und Farbe gesetzt" \
    "$(psql_db t_upgrade_manual -tAc "select string_agg(name || '/' || status || '/' || color, ',' order by name) from projects where user_id is not null")" \
    "Projekt/Aktiv/#0f766e,Syncro/Aktiv/#0f766e"
check "Eigene Policy ersetzt, Syncro-Policies vorhanden" \
    "$(psql_db t_upgrade_manual -tAc "select count(*) filter (where policyname = 'Enable read access for own rows') || '/' || count(*) from pg_policies where tablename = 'projects'")" \
    "0/4"
check "Eigentümer ist admin, Projekt ohne Eigentümer bleibt erhalten" \
    "$(psql_db t_upgrade_manual -tAc "select (select count(*) from project_members where role = 'admin') || '/' || (select count(*) from projects where user_id is null)")" \
    "2/1"

echo "== Tabelle mit bigint-Id wird mit klarer Meldung abgelehnt"
docker exec "$CONTAINER" psql -U postgres -q -c "create database t_bigint" >/dev/null
psql_db t_bigint < tests/supabase_stub.sql
psql_db t_bigint -c "create table public.projects (id bigint generated by default as identity primary key, name text);"
bigint_output=$(psql_db t_bigint -c "set client_min_messages = warning" -f - < setup_all.sql 2>&1 || true)
check "Abbruch mit Hinweis auf uuid" \
    "$(grep -c "Syncro erwartet uuid" <<< "$bigint_output")" "1"

run_test() {
    local test="$1" mode="$2"
    local name="t_${mode}_$(basename "$test" .sql)"
    echo "== $(basename "$test") ($mode)"
    prepare_db "$name" "$mode"

    local status=0 output
    output=$(psql_db "$name" < "$test" 2>&1) || status=$?
    echo "$output" | grep -E "ok:|FAILED|ERROR" | sed 's/^.*NOTICE:  //' || true
    if [[ $status -ne 0 ]]; then
        failed=1
    fi
}

for test in tests/0*.sql; do
    run_test "$test" single
done
for test in tests/0*.sql; do
    run_test "$test" all
done

if [[ $failed -eq 0 ]]; then
    echo "Alle SQL-Tests bestanden."
else
    echo "SQL-Tests fehlgeschlagen."
    exit 1
fi
