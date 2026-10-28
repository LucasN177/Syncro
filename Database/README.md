# Datenbank

Die Migrationen werden im Supabase SQL-Editor **in Reihenfolge** ausgeführt. Jede Datei ist idempotent und kann erneut ausgeführt werden. `002_projects.sql` läuft auch auf einer Datenbank, in der die frühere `projects.sql` bereits angelegt wurde.

| Datei | Inhalt |
|---|---|
| `001_profiles.sql` | `profiles` je Auth-Nutzer, wird beim Sign-up per Trigger angelegt |
| `002_projects.sql` | `projects` mit Key (z. B. `SYN`), Issue-Zähler und Status-Prüfung |
| `003_project_members.sql` | `project_members` mit Rollen `admin`/`member`/`viewer`, Hilfsfunktionen und alle Projekt-Policies |
| `004_issues.sql` | `issues` (Board-Karten) mit fortlaufender Nummer pro Projekt |
| `005_issue_comments.sql` | `issue_comments`, pflegt `issues.comment_count` |
| `006_events.sql` | `events` und `event_attendees` für den Kalender |
| `007_activity_log.sql` | `activity_log`, wird per Trigger befüllt |

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
