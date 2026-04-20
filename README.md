# Syncro 🚀

**Syncro** ist ein modernes, leichtgewichtiges Projektmanagement-Tool, entwickelt als Progressive Web App (PWA). Es kombiniert die Performance von C# im Browser mit der Skalierbarkeit eines Real-time Backends.

Dieses Projekt ist ein leidenschaftliches Hobby-Projekt mit einem klaren Fokus: **Handcrafted Code**. In einer Zeit von massenhaft generiertem KI-Code setzt Syncro auf tiefes Verständnis der Architektur, saubere manuelle Implementierung und eine durchdachte Codebase.

---

## 🛠 Tech Stack

Der Stack wurde gewählt, um eine nahtlose C#-Experience vom Frontend bis zur Datenbank-Interaktion zu ermöglichen:

- **Frontend:** [Blazor WebAssembly](https://dotnet.microsoft.com/apps/aspnet/web-apps/blazor) (.NET 10)
- **Backend-as-a-Service:** [Supabase](https://supabase.com/)
  - **PostgreSQL:** Relationale Datenhaltung mit komplexen Schemata.
  - **Supabase Auth:** Sicherer Login & Rollenmanagement (GoTrue).
  - **Realtime:** Bidirektionale Updates für kollaboratives Arbeiten.
- **UI & Styling:** - [MudBlazor](https://mudblazor.com/) (Komponenten-Bibliothek) oder Custom CSS/Tailwind.
- **PWA:** Vollständige Offline-Unterstützung und Installation auf Desktop/Mobile.

---

## ✨ Features (Geplant & Teilweise Implementiert)

- **Interaktive Boards:** Verwalte Aufgaben visuell auf Kanban-Boards.
- **Echtzeit-Sync:** Änderungen von Teammitgliedern werden ohne Refresh sofort sichtbar.
- **Rollenbasiertes System (RBAC):** Definierte Berechtigungen (Admin, Member, Viewer) für Projekte.
- **User-Management:** Einfaches Einladen von Teammitgliedern über Supabase Auth.
- **Mobile First:** Optimierte Bedienung für Smartphones dank PWA-Technologie.

---

## 🗺️ Roadmap

Das Projekt folgt einem modularen Entwicklungsplan:

### Phase 1: Die Basis (MVP) 🏗️
- [ ] Initiales Projekt-Setup & PWA-Konfiguration.
- [ ] Anbindung an Supabase (Auth & Database).
- [ ] Erstellung von Projekten und einfachen Tasks (CRUD).

### Phase 2: Collaboration & Rollen 👥
- [ ] Implementierung der Rollenlogik (RBAC) in der Datenbank.
- [ ] Einladungssystem für neue Nutzer.
- [ ] Realtime-Subscription für Task-Updates.

### Phase 3: Advanced Features & Polish ✨
- [ ] Dateianhänge über Supabase Storage.
- [ ] Dark Mode Support & UI-Feinschliff.
- [ ] Push-Benachrichtigungen bei Erwähnungen oder Deadlines.


