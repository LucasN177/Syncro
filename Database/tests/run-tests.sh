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
            insert into syncro.projects(id, user_id, name, status, color) values
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
        "$(psql_db "$db" -tAc "select string_agg(key || '=' || name, ',' order by key) from syncro.projects")" \
        "LWE=Ölwechsel,P123=123 Altprojekt,WEB=Web,XP=X"
    check "Ungültiger Status wird Aktiv, gültiger bleibt" \
        "$(psql_db "$db" -tAc "select string_agg(status || '=' || key, ',' order by key) from syncro.projects where key in ('LWE','WEB')")" \
        "Aktiv=LWE,Pausiert=WEB"
    check "Ungültige Farben werden ersetzt" \
        "$(psql_db "$db" -tAc "select count(*) from syncro.projects where color = '#0f766e'")" "4"
    check "Eigentümer ist admin in allen Altprojekten" \
        "$(psql_db "$db" -tAc "select count(*) from syncro.project_members where role = 'admin'")" "4"
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
    "$(psql_db t_upgrade_manual -tAc "select string_agg(column_name || ':' || data_type, ',' order by column_name) from information_schema.columns where table_schema = 'syncro' and table_name = 'projects' and column_name in ('color', 'description', 'status', 'user_id')")" \
    "color:text,description:text,status:text,user_id:uuid"
check "Leerer Name wird ersetzt, Status und Farbe gesetzt" \
    "$(psql_db t_upgrade_manual -tAc "select string_agg(name || '/' || status || '/' || color, ',' order by name) from syncro.projects where user_id is not null")" \
    "Projekt/Aktiv/#0f766e,Syncro/Aktiv/#0f766e"
check "Eigene Policy ersetzt, Syncro-Policies vorhanden" \
    "$(psql_db t_upgrade_manual -tAc "select count(*) filter (where policyname = 'Enable read access for own rows') || '/' || count(*) from pg_policies where schemaname = 'syncro' and tablename = 'projects'")" \
    "0/4"
check "Eigentümer ist admin, Projekt ohne Eigentümer bleibt erhalten" \
    "$(psql_db t_upgrade_manual -tAc "select (select count(*) from syncro.project_members where role = 'admin') || '/' || (select count(*) from syncro.projects where user_id is null)")" \
    "2/1"

echo "== Gleichnamige Funktionen aus früheren Setups"
# Zu jeder Funktion aus den Migrationen eine abweichende Variante erzeugen (anderer Rückgabetyp,
# keine Parameternamen). Fehlt eine Funktion in 000_preflight.sql, scheitert setup_all.sql.
functions_query="select string_agg(p.proname || '(' || pg_get_function_arguments(p.oid) || ') ' || pg_get_function_result(p.oid), E'\n' order by p.proname, oidvectortypes(p.proargtypes))
                 from pg_proc p where p.pronamespace = 'syncro'::regnamespace"
prepare_db t_functions_reference all
reference_functions=$(psql_db t_functions_reference -tAc "$functions_query")
conflicting_ddl=$(psql_db t_functions_reference -tAc "
    select string_agg(format('create function syncro.%I(%s) returns %s language sql as \$f\$ select null::%s \$f\$;',
               p.proname, oidvectortypes(p.proargtypes), alt.type, alt.type), E'\n')
    from pg_proc p
    cross join lateral (select case when pg_get_function_result(p.oid) = 'integer' then 'boolean' else 'integer' end as type) alt
    where p.pronamespace = 'syncro'::regnamespace")

docker exec "$CONTAINER" psql -U postgres -q -c "create database t_function_conflicts" >/dev/null
psql_db t_function_conflicts < tests/supabase_stub.sql
psql_db t_function_conflicts -c "create schema syncro; $conflicting_ddl"
check "Abweichende Funktionen angelegt" \
    "$(psql_db t_function_conflicts -tAc "select count(*) from pg_proc where pronamespace = 'syncro'::regnamespace")" \
    "$(grep -c . <<< "$reference_functions")"
for run in 1 2; do
    run_sql_file t_function_conflicts setup_all.sql
done
check "Nach dem Setup entsprechen alle Funktionen der Referenz" \
    "$(psql_db t_function_conflicts -tAc "$functions_query")" "$reference_functions"

echo "== Tabelle mit bigint-Id wird mit klarer Meldung abgelehnt"
docker exec "$CONTAINER" psql -U postgres -q -c "create database t_bigint" >/dev/null
psql_db t_bigint < tests/supabase_stub.sql
psql_db t_bigint -c "create schema syncro; create table syncro.projects (id bigint generated by default as identity primary key, name text);"
bigint_output=$(psql_db t_bigint -c "set client_min_messages = warning" -f - < setup_all.sql 2>&1 || true)
check "Abbruch mit Hinweis auf uuid" \
    "$(grep -c "Syncro erwartet uuid" <<< "$bigint_output")" "1"

echo "== Unpassende Tabellen aus einem früheren Setup werden vorab gemeldet"
docker exec "$CONTAINER" psql -U postgres -q -c "create database t_old_tables" >/dev/null
psql_db t_old_tables < tests/supabase_stub.sql
psql_db t_old_tables -c "
    create schema syncro;
    create table syncro.project_members (id uuid primary key, project_id uuid, user_id uuid, role smallint not null default 1);
    create table syncro.issues (id uuid primary key, title text, sprint_id uuid not null);"
old_tables_output=$(psql_db t_old_tables -c "set client_min_messages = warning" -f - < setup_all.sql 2>&1 || true)
check "Meldung nennt beide Tabellen" \
    "$(grep -c "passen nicht zu Syncro: issues, project_members" <<< "$old_tables_output")" "1"
check "Meldung nennt den falschen Typ von role" \
    "$(grep -c "project_members.role: erwartet text, vorhanden smallint" <<< "$old_tables_output")" "1"
check "Meldung nennt die zusätzliche Pflichtspalte" \
    "$(grep -c "issues.sprint_id: zusätzliche Pflichtspalte" <<< "$old_tables_output")" "1"
check "Hinweis enthält den drop-Befehl" \
    "$(grep -c "drop table if exists syncro.issues, syncro.project_members cascade" <<< "$old_tables_output")" "1"
check "Es wurde nichts angelegt (Abbruch vor 001)" \
    "$(psql_db t_old_tables -tAc "select count(*) from information_schema.tables where table_schema = 'syncro' and table_name = 'profiles'")" "0"
psql_db t_old_tables -c "drop table if exists syncro.issues, syncro.project_members cascade;"
for run in 1 2; do
    run_sql_file t_old_tables setup_all.sql
done
check "Nach dem Löschen läuft setup_all.sql durch" \
    "$(psql_db t_old_tables -tAc "select data_type from information_schema.columns where table_schema = 'syncro' and table_name = 'project_members' and column_name = 'role'")" "text"

# ── Gemeinsame Datenbank mit Monexa ──────────────────────────────────────────
# Monexas Setup wird aus dem Nachbar-Repository gelesen (nicht ins Repo kopiert).
MONEXA_SETUP_ALL="${MONEXA_SETUP_ALL:-../../monexa/database/setup_all.sql}"

# Alles, was Monexa (oder eine andere App) in public, auth und storage sehen kann
public_snapshot_query="select string_agg(x, E'\n' order by x) from (
    select 'column ' || table_name || '.' || column_name || ' ' || data_type
        from information_schema.columns where table_schema = 'public'
    union all select 'function ' || p.proname || '(' || pg_get_function_arguments(p.oid) || ') ' || pg_get_function_result(p.oid)
        from pg_proc p where p.pronamespace = 'public'::regnamespace
    union all select 'policy ' || schemaname || '.' || tablename || ' ' || policyname
        from pg_policies where schemaname in ('public', 'storage')
    union all select 'trigger ' || n.nspname || '.' || c.relname || ' ' || t.tgname
        from pg_trigger t join pg_class c on c.oid = t.tgrelid join pg_namespace n on n.oid = c.relnamespace
        where not t.tgisinternal and n.nspname in ('public', 'auth')
    union all select 'publication ' || tablename
        from pg_publication_tables where pubname = 'supabase_realtime' and schemaname = 'public'
) s(x)"

# Monexa einrichten und Daten anlegen wie die App: Mia legt ein Projekt an und lädt jemanden ein
prepare_monexa() {
    local db="$1"
    docker exec "$CONTAINER" psql -U postgres -q -c "create database $db" >/dev/null
    psql_db "$db" < tests/supabase_stub.sql
    psql_db "$db" -c "set client_min_messages = warning" -f - < "$MONEXA_SETUP_ALL"
    psql_db "$db" -c "
        insert into auth.users values ('33333333-3333-3333-3333-333333333333', 'mia@example.test');
        set role authenticated;
        select set_config('request.jwt.claim.sub', '33333333-3333-3333-3333-333333333333', false);
        insert into public.projects (id, name) values ('44444444-4444-4444-4444-444444444444', 'Haushalt');
        insert into public.project_members (project_id, email, role) values ('44444444-4444-4444-4444-444444444444', 'gast@example.test', 0);"  >/dev/null
}

# Monexa-Nutzerin legt ein Projekt an und sieht ihre Projekte (prüft Trigger und Policies)
monexa_user_check() {
    psql_db "$1" -tAc "
        set role authenticated;
        select set_config('request.jwt.claim.sub', '33333333-3333-3333-3333-333333333333', false);
        insert into public.projects (name) values ('Neu $2');
        select (select count(*) from public.projects) || '/' || public.project_role('44444444-4444-4444-4444-444444444444')
            || '/' || pg_typeof(public.project_role('44444444-4444-4444-4444-444444444444'));" | tail -1
}

# Syncro-Nutzerin legt im Schema syncro ein Projekt an
syncro_user_check() {
    psql_db "$1" -tAc "
        set role authenticated;
        select set_config('request.jwt.claim.sub', '33333333-3333-3333-3333-333333333333', false);
        insert into syncro.projects (user_id, name) values ('33333333-3333-3333-3333-333333333333', 'Board');
        select (select count(*) from syncro.projects) || '/' || syncro.project_role((select id from syncro.projects));" | tail -1
}

if [[ -f "$MONEXA_SETUP_ALL" ]]; then
    echo "== Syncro neben Monexa (neues Setup im Schema syncro)"
    prepare_monexa t_monexa_side_by_side
    before=$(psql_db t_monexa_side_by_side -tAc "$public_snapshot_query")
    for run in 1 2; do
        run_sql_file t_monexa_side_by_side setup_all.sql
    done
    # Außerhalb von syncro legt Syncro nur eindeutig benannte Objekte an: Storage-Policies
    # "Syncro: …" und die Trigger syncro_on_… auf auth.users
    after_all=$(psql_db t_monexa_side_by_side -tAc "$public_snapshot_query")
    after=$(grep -v -e "storage.objects Syncro: " -e "auth.users syncro_on_" <<< "$after_all" || true)
    check "Außerhalb von syncro nur eigene, eindeutig benannte Objekte" \
        "$(grep -c -e "storage.objects Syncro: " -e "auth.users syncro_on_" <<< "$after_all")" "5"
    if [[ "$before" == "$after" ]]; then
        echo "ok: Monexas Objekte in public, auth und storage sind unverändert"
    else
        echo "FAILED: Syncro hat Objekte außerhalb von syncro verändert:"
        diff <(echo "$before") <(echo "$after") || true
        failed=1
    fi
    check "Monexa: Projekt anlegen, Rolle als smallint" "$(monexa_user_check t_monexa_side_by_side 1)" "2/2/smallint"
    check "Syncro: Projekt im Schema syncro anlegen" "$(syncro_user_check t_monexa_side_by_side)" "1/admin"

    echo "== Reparatur nach dem alten Setup in public"
    prepare_monexa t_monexa_repair
    reference=$(psql_db t_monexa_repair -tAc "$public_snapshot_query")
    # Nachstellen, was in Supabase passiert ist: Das alte Setup (eine Transaktion wie im SQL-Editor)
    # bricht an Monexas project_members ab, nach dem drop aus dem Hinweis läuft es durch.
    if docker exec -i "$CONTAINER" psql -U postgres -d t_monexa_repair -v ON_ERROR_STOP=1 -q -1 \
           -c "set client_min_messages = error" -f - < tests/fixtures/syncro_public_setup_279989f.sql >/dev/null 2>&1; then
        echo "FAILED: altes Setup hätte an Monexas project_members scheitern müssen"; failed=1
    fi
    psql_db t_monexa_repair -c "drop table if exists public.project_members cascade;"
    docker exec -i "$CONTAINER" psql -U postgres -d t_monexa_repair -v ON_ERROR_STOP=1 -q -1 \
        -c "set client_min_messages = error" -f - < tests/fixtures/syncro_public_setup_279989f.sql >/dev/null
    check "Ausgangslage: Monexas Policies fehlen, Rolle ist text" \
        "$(psql_db t_monexa_repair -tAc "select (select count(*) from pg_policies where schemaname = 'public' and tablename = 'accounts') || '/' || pg_get_function_result('public.project_role(uuid)'::regprocedure)")" \
        "0/text"

    # Reparatur in der dokumentierten Reihenfolge
    psql_db t_monexa_repair -c "set client_min_messages = warning" -f - < maintenance/remove_syncro_from_public.sql
    psql_db t_monexa_repair -c "set client_min_messages = warning" -f - < "$MONEXA_SETUP_ALL"
    restored=$(psql_db t_monexa_repair -tAc "set client_min_messages = warning" -f - < maintenance/restore_monexa_owners.sql | tail -1)
    check "Eigentümer wiederhergestellt (Projekte|ohne Eigentümer|Admins)" "$restored" "1|0|1"
    after_repair=$(psql_db t_monexa_repair -tAc "$public_snapshot_query")
    if [[ "$reference" == "$after_repair" ]]; then
        echo "ok: public, auth.users und storage entsprechen wieder dem Stand vor Syncro"
    else
        echo "FAILED: Unterschiede zum Stand vor Syncro:"
        diff <(echo "$reference") <(echo "$after_repair") || true
        failed=1
    fi
    for run in 1 2; do
        run_sql_file t_monexa_repair setup_all.sql
    done
    check "Monexa nach der Reparatur: Projekt anlegen, Rolle als smallint" "$(monexa_user_check t_monexa_repair 2)" "2/2/smallint"
    check "Syncro nach der Reparatur im Schema syncro" "$(syncro_user_check t_monexa_repair)" "1/admin"
    check "Keine Syncro-Objekte mehr in public" \
        "$(psql_db t_monexa_repair -tAc "select count(*) from pg_proc where pronamespace = 'public'::regnamespace and proname in ('is_project_member', 'handle_new_user', 'set_updated_at', 'notify_user')")" "0"
else
    echo "== Monexa-Szenarien übersprungen ($MONEXA_SETUP_ALL nicht gefunden, Pfad per MONEXA_SETUP_ALL setzen)"
fi

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
