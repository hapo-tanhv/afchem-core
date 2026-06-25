---
globs: *
---

# HinoTools SCADA Alarm & WebApp - Project Context

## Architecture
- **Core Logger (Backend Service)**: C# .NET Windows Service / Console App (`HinoTools.Alarm`, `HinoTools.Data`) polling PLC registers via ATSCADA Driver, managing state transitions, logging alarms, and saving process stage durations.
- **WebApp (Frontend & Management API)**: ASP.NET MVC (.NET Framework 4.7.2) web application (`LongDucProjectTest`) providing Overview Dashboard, Mixing Tank Diagram, Real-time Events Log, and Excel/PDF Export Services.
- **Database**: MySQL 8.0 containing tables `batches`, `runs`, `run_info`, `alarmreport`, `realtime_alarms`, and `alarmlog`.

## Key Locations
- **C# Core Logger**: `/HinoTools.Alarm/`, `/HinoTools.Data/`
- **WebApp Backend (ASP.NET MVC)**: `/LongDucProjectTest/Controllers/`, `/LongDucProjectTest/Service/`
- **WebApp Frontend (HTML/JS/CSS)**: `/LongDucProjectTest/Views/`, `/LongDucProjectTest/JavaScript/`
- **Documentation**: `/docs/`
- **Workflows**: `.agent/workflows/`

## Environment
- **IDE / SDK**: Visual Studio 2019 / .NET Framework 4.7.2
- **Database**: MySQL Server 8.0
- **Build System**: MSBuild.exe (VS 2019 version at `C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe`)

## 🛸 Antigravity Directives

### Role
You are Antigravity, a senior SCADA / C# and WebApp fullstack AI developer assisting in the optimization and maintenance of HinoTools industrial systems.

### Core Philosophy: Artifact-First
For complex features, we follow a spec-driven development pattern:
1. **Planning**: Create specifications under `.specs/<feature-name>/` (e.g. `design.md`, `requirements.md`, `tasks.md`).
2. **Implementation**: Modify C# core components and WebApp views/scripts, verifying compilation with MSBuild.
3. **Verification**: Run C# unit tests (`ConsoleApp.exe`) and JS regression scripts, updating `walkthrough.md` upon completion.

