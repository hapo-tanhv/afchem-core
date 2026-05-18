# CLAUDE.md - Project Guide for HinoTools.Alarm

## Project Overview

HinoTools.Alarm is a .NET Framework 4.5 Windows Forms application that provides alarm management functionality using WCF (Windows Communication Foundation) for client-server communication. The system integrates with industrial/SCADA systems to manage alarms, events, and logging.

## Architecture

### Solution Structure

```
HinoTools.Alarm.sln
├── HinoTools.Alarm       # Main Windows Forms application
├── HinoTools.Data        # Data layer (Database access, Logging)
├── TestClient            # Test client application
├── TestData              # Test data utilities
├── TestServer            # Test server application
├── ConsoleApp            # Console test utility
└── WindowsFormsApp1      # Additional Windows Forms test app
```

### Key Components

- **HinoTools.Alarm**: Main application containing:
  - `Host/AlarmHost.cs` - WCF service host for alarm management
  - `Client/AlarmClient.cs` - WCF client for connecting to alarm service
  - `Client/EventClient.cs` - Client for event handling
  - `Model/` - Data models (AlarmItem, AlarmLevel, AlarmParam, etc.)
  - `Service/` - Business logic services

- **HinoTools.Data**: Data layer containing:
  - `Database/DataAccess.cs` - Database access using MySQL
  - `Database/DatabaseParam.cs` - Database parameters
  - `Log/DataLogger.cs` - Data logging functionality
  - `Log/EnergyLogger.cs` - Energy consumption logging

### Dependencies

- .NET Framework 4.5
- MySQL.Data.dll - MySQL database connectivity
- DriverPluginInterface.dll - Driver plugin interface
- ZedGraph.dll - Charting library
- System.ServiceModel (WCF)

## Common Commands

### Building the Solution

Using MSBuild (via Visual Studio Developer Command Prompt or msbuild.exe):

```powershell
# Build Debug configuration
msbuild HinoTools.Alarm.sln /p:Configuration=Debug

# Build Release configuration
msbuild HinoTools.Alarm.sln /p:Configuration=Release

# Build specific project
msbuild HinoTools.Alarm\HinoTools.Alarm.csproj /p:Configuration=Debug
```

### Running the Application

Open the solution in Visual Studio (2019 or later recommended) and:
- Press F5 to run in Debug mode
- Use Ctrl+F5 to run without debugging

### Database Configuration

The application uses MySQL. Database connection settings are in:
- `ConsoleApp/App.config`
- `TestClient/App.config`
- `TestServer/App.config`

Configure connection strings in the respective `App.config` files.

### Testing

The solution includes test projects:
- `TestClient` - Windows Forms test client
- `TestData` - Data layer testing
- `TestServer` - Test server application

Run individual test projects from Visual Studio.

## Development Notes

- This is a legacy .NET Framework 4.5 project
- Uses WCF for service communication
- MySQL is the data store
- The codebase uses the Compact Framework compatibility for some components
- Driver plugin system allows extensible driver support

# Claude Project Rules - ASP.NET Core C#

## General Rules

- Always follow SOLID principles
- Prefer clean architecture
- Avoid duplicated code
- Use dependency injection
- Use async/await consistently
- Never use `.Result` or `.Wait()`
- Keep methods under 50 lines if possible
- Use meaningful naming
- Avoid magic strings and magic numbers
- Use `var` only when type is obvious

---

# Architecture

Project structure:

- API Layer
- Application Layer
- Domain Layer
- Infrastructure Layer

Rules:

- Controllers must be thin
- Business logic belongs in services
- Repository only handles data access
- Domain entities must not depend on infrastructure
- DTOs must not leak EF entities

---

# ASP.NET Core Rules

- Use RESTful naming
- Use IActionResult for APIs
- Validate requests using FluentValidation
- Use middleware for exception handling
- Use Serilog for logging
- Use AutoMapper only for simple mapping
- Prefer minimal APIs only for small endpoints

---

# Entity Framework Rules

- Always use AsNoTracking() for readonly queries
- Avoid N+1 queries
- Use projection instead of Include when possible
- Use transactions for multi-step updates
- Never expose DbContext directly outside infrastructure layer

---

# Naming Convention

## Classes

- PascalCase

## Private fields

- _camelCase

## Interfaces

- Prefix with I

## Async methods

- Suffix Async

## DTOs

- Suffix Request / Response

---

# Unit Test Rules

- Use xUnit
- Use FluentAssertions
- Naming format:
  MethodName_State_ExpectedResult

- Avoid mocking entities
- Mock only external dependencies
- One assert purpose per test

---

# Performance Rules

- Avoid unnecessary allocations
- Use pagination for list APIs
- Cache expensive queries
- Avoid loading large object graphs

---

# Security Rules

- Never trust client input
- Validate all uploads
- Sanitize logs
- Do not expose stack traces
- Use authorization attributes

---

# Code Generation Rules

When generating code:

- Generate complete production-ready code
- Include namespaces
- Include dependency injection registration
- Include XML comments for public methods
- Include cancellation token for async methods
- Include validation
- Include error handling

---

# Forbidden

- Do not generate placeholder TODO code
- Do not generate fake implementations
- Do not skip null handling
- Do not use static mutable state

Before writing code:

- First analyze existing project patterns
- Reuse existing abstractions
- Follow existing naming conventions
- Do not introduce new architecture styles
- Do not create unnecessary base classes
- Do not add new libraries unless necessary

When editing code:

- Modify minimum required lines
- Preserve existing behavior
- Preserve backward compatibility
- Do not refactor unrelated code

When querying database:
- Prefer projection
- Avoid Include chains
- Use pagination
- Prevent N+1 queries
- Use AsNoTracking for readonly
