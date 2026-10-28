# Datenbank

**Am einfachsten:** `setup_all.sql` komplett im Supabase SQL-Editor ausführen. Die Datei enthält alle Migrationen 001–010 in der richtigen Reihenfolge.

Alternativ lassen sich die Einzeldateien **in Reihenfolge** ausführen. Jede Datei ist idempotent und kann erneut ausgeführt werden. `002_projects.sql` läuft auch auf einer Datenbank, in der die frühere `projects.sql` bereits angelegt wurde. Ungültige Altdaten werden dabei bereinigt: Ein zu kurzes Kürzel wird ergänzt, ein unbekannter Status wird zu `Aktiv` und eine ungültige Farbe zu `#0f766e`.

`setup_all.sql` wird aus den Einzeldateien erzeugt und nicht von Hand bearbeitet. Nach Änderungen an einer Migration neu generieren:

```bash
bash Database/build-setup-all.sh
```

| Datei | Inhalt |
|---|---|
| `001_profiles.sql` | `profiles` je Auth-Nutzer, wird beim Sign-up per Trigger angelegt |
| `002_projects.sql` | `projects` mit Key (z. B. `SYN`), Issue-Zähler und Status-Prüfung |
| `003_project_members.sql` | `project_members` mit Rollen `admin`/`member`/`viewer`, Hilfsfunktionen und alle Projekt-Policies |
| `004_issues.sql` | `issues` (Board-Karten) mit fortlaufender Nummer pro Projekt |
| `005_issue_comments.sql` | `issue_comments`, pflegt `issues.comment_count` |
| `006_events.sql` | `events` und `event_attendees` für den Kalender |
| `007_activity_log.sql` | `activity_log`, wird per Trigger befüllt |
| `008_attachments.sql` | `issue_attachments`, privater Storage-Bucket `attachments` (max. 10 MB je Datei) mit Policies |
| `009_notifications.sql` | `notifications` bei Zuweisung, Kommentar, `@username`-Erwähnung und Projekteinladung |
| `010_realtime.sql` | nimmt `issues` und `notifications` in die Realtime-Publikation auf |

`004_issues.sql` wurde für die Anhänge um die Spalte `attachment_count` erweitert. Wer 004 schon früher ausgeführt hat, führt die Datei vor 008 einfach erneut aus.

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
- die Rechte und Trigger mit vier Testnutzern (`01_core.sql`, `02_attachments_notifications.sql`).

```bash
bash Database/tests/run-tests.sh
```

Der Stub ersetzt nur Auth, Storage-Tabellen und die Realtime-Publikation. Ob Supabase Realtime und Storage selbst wie erwartet arbeiten, zeigt erst ein Test im echten Projekt.
