# Datenbank

Syncro liegt komplett im eigenen Schema **`syncro`**. So kann es sich ein Supabase-Projekt mit anderen Apps teilen (z. B. Monexa im Schema `public`), ohne dass sich Tabellen, Funktionen oder Policies überschneiden. Außerhalb von `syncro` legt Syncro nur eindeutig benannte Objekte an: die Trigger `syncro_on_auth_user_created` und `syncro_on_auth_user_email_changed` auf `auth.users`, den Bucket `syncro-attachments` und Storage-Policies mit dem Präfix „Syncro:“.

## Einrichten

1. Im Supabase-Dashboard unter **Project Settings → Data API → Exposed schemas** den Eintrag `syncro` ergänzen. Ohne diesen Schritt kann die App nicht auf ihre Tabellen zugreifen.
2. `setup_all.sql` komplett im SQL-Editor ausführen. Die Datei enthält alle Migrationen 000–010 in der richtigen Reihenfolge.

Alternativ lassen sich die Einzeldateien **in Reihenfolge** ausführen. Jede Datei ist idempotent und kann erneut ausgeführt werden.

## Aufräumen nach einer früheren Version in `public`

Frühere Syncro-Versionen lagen im Schema `public` und haben sich dort mit Monexa überschnitten (`projects`, `project_members`, `project_role`). Zum Bereinigen im SQL-Editor nacheinander ausführen:

1. `maintenance/remove_syncro_from_public.sql`: Entfernt nur Syncro-Objekte aus `public`. Tabellen und Funktionen werden nur gelöscht, wenn ihre Spalten bzw. ihre Signatur eindeutig zu Syncro passen. Gibt es in `public` noch Syncro-Daten (Projekte, Issues, Termine), bricht das Skript ab und nennt sie.
2. Monexas `database/setup_all.sql`: Stellt `project_members`, `project_role` und alle Policies wieder her.
3. `maintenance/restore_monexa_owners.sql`: Trägt jeden Projekteigentümer wieder als Admin in Monexas `project_members` ein. Weitere Mitglieder und offene Einladungen lassen sich nur aus einem Backup zurückholen.
4. `setup_all.sql` von Syncro, wie unter „Einrichten“ beschrieben.

`setup_all.sql` wird aus den Einzeldateien erzeugt und nicht von Hand bearbeitet. Nach Änderungen an einer Migration neu generieren:

```bash
bash Database/build-setup-all.sh
```

| Datei | Inhalt |
|---|---|
| `000_preflight.sql` | legt das Schema `syncro` mit Rechten für die Supabase-Rollen an; Vorabprüfung: entfernt abweichende gleichnamige Funktionen und meldet unpassende Tabellen mit allen Abweichungen auf einmal (löscht keine Daten) |
| `001_profiles.sql` | `profiles` je Auth-Nutzer, wird beim Sign-up per Trigger angelegt |
| `002_projects.sql` | `projects` mit Key (z. B. `SYN`), Issue-Zähler und Status-Prüfung |
| `003_project_members.sql` | `project_members` mit Rollen `admin`/`member`/`viewer`, Hilfsfunktionen und alle Projekt-Policies |
| `004_issues.sql` | `issues` (Board-Karten) mit fortlaufender Nummer pro Projekt |
| `005_issue_comments.sql` | `issue_comments`, pflegt `issues.comment_count` |
| `006_events.sql` | `events` und `event_attendees` für den Kalender |
| `007_activity_log.sql` | `activity_log`, wird per Trigger befüllt |
| `008_attachments.sql` | `issue_attachments`, privater Storage-Bucket `syncro-attachments` (max. 10 MB je Datei) mit Policies |
| `009_notifications.sql` | `notifications` bei Zuweisung, Kommentar, `@username`-Erwähnung und Projekteinladung |
| `010_realtime.sql` | nimmt `issues` und `notifications` in die Realtime-Publikation auf, zieht Rechte nach |


Dateien im Storage liegen unter `{project_id}/{issue_id}/{dateiname}`. Wird ein Issue oder Projekt gelöscht, verschwinden die Einträge in `issue_attachments`, die Dateien selbst bleiben aber im Bucket liegen. Supabase empfiehlt, Storage-Dateien nicht per SQL zu löschen.

## Rechte

| Rolle | Projekte | Mitglieder | Issues/Kommentare | Projekttermine |
|---|---|---|---|---|
| Eigentümer | alles inkl. Löschen | verwalten | bearbeiten | bearbeiten |
| `admin` | bearbeiten | verwalten | bearbeiten | bearbeiten |
| `member` | lesen | lesen, selbst austreten | bearbeiten | bearbeiten |
| `viewer` | lesen | lesen, selbst austreten | lesen | lesen |

Termine ohne Projekt sind privat: Der Ersteller und die eingeladenen Teilnehmer sehen sie.

## Werte für die App

- `issues.status` entspricht den Namen des Enums `Column` (`Backlog`, `Todo`, `InProgress`, `Review`, `Done`).
- `issues.type`: `Feature`, `Bug`, `Task`, `Improvement`, `Documentation`
- `issues.priority`: `Low`, `Medium`, `High`, `Critical`
- `events.category`: `Meeting`, `Review`, `Release`, `Personal`, `Deadline`
- `projects.status`: `Aktiv`, `Pausiert`, `Abgeschlossen`, `Archiviert`

Diese Felder setzt die Datenbank selbst. Werte, die der Client mitschickt, werden ignoriert:
`issues.number`, `issues.created_by`, `issues.comment_count`, `projects.issue_counter`, `projects.user_id` (bei Updates), `events.owner_id`, `issue_comments.user_id`.

Um Nutzer einzuladen, sucht `rpc('find_profile_by_email', { p_email })` das passende Profil.

## Tests

`Database/tests/run-tests.sh` startet ein Postgres in Docker, baut die Supabase-Umgebung minimal nach (`supabase_stub.sql`) und prüft:

- dass `setup_all.sql` zu den Einzeldateien passt,
- das Upgrade einer Datenbank mit der alten `projects.sql` und unsauberen Altdaten, über die Einzeldateien und über `setup_all.sql`,
- dass alle Migrationen zweimal hintereinander fehlerfrei laufen,
- dass `000` jede Funktion abdeckt: zu jeder Funktion wird vorher eine abweichende Variante angelegt,
- die Rechte und Trigger mit vier Testnutzern (`01_core.sql`, `02_attachments_notifications.sql`).
- mit Monexa im Nachbarordner `../monexa`: dass Syncro neben Monexa nichts an deren Objekten ändert und dass die Aufräumskripte Monexa nach dem alten Setup wiederherstellen. Der Pfad lässt sich mit `MONEXA_SETUP_ALL` setzen; fehlt die Datei, werden diese Szenarien übersprungen.

```bash
bash Database/tests/run-tests.sh
```

Der Stub ersetzt nur Auth, Storage-Tabellen und die Realtime-Publikation. Ob Supabase Realtime und Storage selbst wie erwartet arbeiten, zeigt erst ein Test im echten Projekt.
