# GamePort — Master Project Context

## 1. Project Overview

GamePort is a gaming-center management ecosystem designed for GameNets.

The system manages:

- Gaming PCs
- PlayStation consoles
- Customer accounts
- Gaming sessions
- Reservations
- Wallets
- Payments
- Games
- Cafe operations
- Inventory
- Employees
- Attendance
- Financial reporting
- Device management
- Customer mobile access
- Manager management access
- Offline operation
- Local LAN communication
- Cloud synchronization
- Audit logging

GamePort consists of five applications:

1. Client
2. Cashier
3. Cafe
4. Customer
5. Manager

The system must be designed from the beginning as a production-grade application, not as a prototype.

The existing/previous GamePort application is only a source of experience and must NOT be treated as the architectural or codebase foundation.

This project is a complete rebuild from scratch.

---

# 2. Core Architectural Principle

GamePort is an offline-first system.

The most important architectural requirement is:

> A GameNet must continue operating normally even when Internet connectivity is unavailable.

The Windows applications communicate over the local GameNet LAN.

The Client does NOT communicate directly with the Cloud.

The normal communication model is:

    Client
       ↕
    Cashier
       ↕
     Cloud

Cafe also operates inside the GameNet local environment.

Customer and Manager applications communicate with the Cloud.

---

# 3. Applications

## 3.1 Client

The Client application is installed on every gaming PC.

Its primary responsibilities are:

- Kiosk mode
- Customer login
- Session display
- Game launching
- Windows integration
- Device status reporting
- Receiving commands from Cashier
- Reporting device/game state to Cashier
- Session recovery
- Reconnection
- Continuing an active session when Cashier temporarily becomes unavailable

The Client is an agent/controller.

It should NOT contain the primary business logic for:

- Pricing
- Wallet accounting
- Payments
- Reservations
- Financial calculations
- Customer management

Cashier is the operational authority for these concerns.

The Client should remain lightweight, responsive and highly reliable.

Gaming PCs are powerful, therefore responsiveness and reliability are more important than extreme memory optimization.

---

# 3.2 Cashier

Cashier is the operational authority and local server of a GameNet.

Cashier is NOT merely a UI application.

It contains:

- Cashier UI
- Local Server
- Local API
- Local Database
- Session Engine
- Reservation Engine
- Wallet operations
- Payment operations
- Device management
- Game management
- Sync Engine
- Real-time communication
- Audit logging
- Recovery mechanisms

Cashier must be able to operate without Internet access.

Cashier is normally the first GamePort system powered on each day.

Cashier must therefore be designed with startup recovery, local database recovery and synchronization-on-startup in mind.

---

# 3.3 Cafe

Cafe is a separate Windows application.

It manages:

- Products
- Orders
- Order items
- Inventory
- Payments
- Receipt printing
- Cafe revenue
- Stock receiving
- Customer/order association where applicable

Example:

Customer purchases one soda.

Cafe:

1. Creates order
2. Adds soda
3. Records payment
4. Marks order as paid
5. Decreases inventory
6. Records revenue

The operation must eventually be represented in the local GameNet data and synchronized with Cloud.

Cafe is a separate application from Cashier.

Customers do NOT order food/drinks from the Windows Client.

Customers physically visit the cafe counter.

The mobile Customer application may support cafe-related functionality in the future/current product scope, but its exact ordering workflow must remain isolated from the Client application.

---

# 3.4 Customer

Customer application provides customer-facing functionality.

Platforms:

- Android: installable application
- iOS: PWA / Add to Home Screen
- No iOS App Store dependency

Customer application communicates with Cloud.

It does not need to work offline.

Authentication:

- OTP

Core functionality:

- Customer login
- View GameNet information
- PC reservation
- PlayStation reservation
- View cafe information
- Events
- Event registration
- QR-based GamePort Client login
- Customer profile

Additional features may be added later.

---

# 3.5 Manager

Manager is a PWA.

Platforms:

- Web/PWA
- Android/iOS through Add to Home Screen
- No native mobile application required

Manager communicates with Cloud.

Manager functionality focuses on:

- Financial information
- Financial reports
- GameNet/device status
- Employee attendance
- Management information
- Operational reports
- Other management-level data

Manager should NOT have unrestricted access to sensitive customer operational information.

For example, Manager does not need to see customer Wallet details unless a future explicit requirement requires it.

---

# 4. Current Scale

Current expected GameNet size:

- Approximately 30 gaming clients
- Potentially up to approximately 100 clients

The architecture should comfortably support the current scale and foreseeable growth.

There is currently one GameNet.

Multi-GameNet support is NOT a current product requirement.

However, the architecture must not unnecessarily prevent future expansion to multiple GameNets.

---

# 5. Network Architecture

Typical physical network:

    Router
       │
     Switch
       │
       ├── Cashier
       ├── Cafe
       ├── Client #1
       ├── Client #2
       ├── Client #3
       └── ...

GameNet systems primarily communicate through Ethernet/LAN.

The architecture must not depend on Internet connectivity for normal local GameNet operations.

Wi-Fi is not considered the primary communication mechanism for GameNet devices.

---

# 6. Cloud Architecture

Cloud contains the complete system data.

Cloud is the central persistent store for the overall GamePort ecosystem.

Expected technology:

- ASP.NET Core
- PostgreSQL
- REST API
- SignalR where required
- HTTPS

Cloud responsibilities include:

- Customer data
- GameNet data
- Devices
- Games
- Sessions/history
- Reservations
- Wallet transactions
- Payments
- Cafe data
- Inventory
- Employees
- Attendance
- Financial information
- Audit information
- Synchronization state
- Manager data
- Customer-facing APIs

Cloud is deployed on Liara.

---

# 7. Local Architecture

Cashier has a Local PostgreSQL database.

The Local Database contains the data required for the GameNet to operate independently while offline.

The local system should not depend on Cloud availability for normal local operations.

Local data is synchronized with Cloud whenever Internet connectivity is available.

---

# 8. Offline-First Requirement

When:

    Internet = unavailable
    LAN = available

the following must continue to work:

- Customer login using available local information
- Sessions
- Session timers
- Reservations based on available local data
- Manual reservations
- Cancellations
- Payments
- Wallet operations
- Device management
- Game management
- Cafe operations
- Other normal GameNet operations

Offline operations must later synchronize to Cloud.

Mobile applications do not need offline functionality.

---

# 9. Client/Cashier Failure Recovery

If Internet becomes unavailable:

Client continues communicating with Cashier over LAN.

If Cashier application temporarily crashes:

Client should be able to continue an active session for approximately one hour using locally available session information.

If Cashier returns:

Client reconnects automatically and synchronizes/reconciles required state.

If Cashier does not return within the allowed recovery period, the system may restart clients according to the final implementation rules.

---

# 10. Client Recovery

If Client crashes during an active session:

The session must NOT simply disappear.

When Client restarts:

1. Client starts automatically
2. Client reconnects to Cashier
3. Customer logs in again if required
4. Client obtains the active session
5. Remaining time is restored
6. Customer continues the session

If Windows restarts:

1. Windows boots
2. Client starts automatically
3. Client appears instead of normal Windows usage
4. Customer logs in
5. Active session is restored
6. Remaining time continues

The system must support kiosk behavior.

---

# 11. Session Model

Cashier is the authority for sessions.

A session contains at minimum:

- Customer
- Device
- Start time
- End time / duration
- Pricing information
- Session state
- Financial information
- Recovery information

The price used by a session is determined at session start.

If the global price changes while a session is active:

The active session continues using the price that existed when the session started.

Example:

    14:00
    Price = 100,000

    Session starts

    14:30
    Price = 150,000

The active session continues using:

    100,000

The new price applies to future sessions.

---

# 12. Reservation Model

Reservations can originate from:

- Customer application
- Cashier staff

Reservations synchronized from Cloud are available to Cashier.

Cashier must also support manually creating reservations while offline.

A customer may arrive earlier or later than the reservation time.

If a reserved device is available, the customer can use it.

Example:

Reservation:

    18:00 → 20:00

Customer arrives:

    17:50

They can use:

    17:50 → 19:50

If customer arrives:

    18:20

They can use:

    18:20 → 20:00

Unused reserved time is returned to the customer's Wallet according to the final billing implementation.

The system must maintain full history of reservation changes.

---

# 13. Wallet

Wallet operations are financial operations.

Wallet history must be immutable.

Transactions must NOT be deleted.

Do not modify historical financial transactions to hide or replace previous activity.

Corrections must be represented using new transactions.

Example:

    +500,000 Recharge
    -120,000 Session
    -80,000 Cafe
    +80,000 Refund

Every financial operation must remain auditable.

The exact balance calculation mechanism must be implemented using transaction-safe accounting.

---

# 14. Payments

Payments are immutable financial records.

Never delete financial history.

If a payment is reversed, refunded or cancelled:

Do NOT modify the original payment to pretend it never happened.

Instead:

1. Keep the original payment
2. Record the reversal/refund
3. Link the reversal to the original transaction
4. Preserve the complete history

This rule applies to:

- Gaming payments
- Wallet operations
- Cafe payments
- Refunds
- Reversals
- Other financial operations

---

# 15. Audit Logging

GamePort must maintain a comprehensive audit trail.

Important operations must record information such as:

- Actor
- Action
- Timestamp
- Source application
- Entity
- Entity ID
- Previous value where relevant
- New value where relevant
- Related transaction/event
- Reason where applicable

Example:

    Operator: Cashier #1
    Action: Change PC Price
    Before: 100,000
    After: 150,000
    Time: ...
    Source: Cashier

Audit history must not be silently deleted.

---

# 16. Sync Architecture

The synchronization system must be designed for unreliable Internet connections.

Requirements:

- Automatic synchronization
- Retry
- Resume after interruption
- Duplicate protection
- Idempotent operations
- Sync state tracking
- Reliable event identification
- No loss of financial operations
- No duplicate financial operations
- Initial synchronization
- Synchronization on Cashier startup
- Synchronization when Internet returns

Use an Outbox/Inbox style architecture with cursor/checkpoint-based synchronization.

The exact implementation must be designed so that if:

    100 operations exist
    67 successfully synchronize
    Internet disconnects

then after reconnection:

    Continue from operation 68

rather than blindly resending all operations.

Duplicate operations must be safely detected using stable IDs/idempotency keys.

---

# 17. Real-Time Communication

Cashier and Client require real-time communication.

Expected technology:

- SignalR
- WebSockets where appropriate

Examples of real-time commands/events:

- Start session
- End session
- Extend session
- Launch game
- Device status change
- Game started
- Game closed
- Wallet-related updates
- Shutdown
- Restart
- Lock
- Unlock
- Background change
- Windows setting changes
- Session updates
- Other operational commands

Cashier may send commands to Client without requiring confirmation from Client.

Operator confirmation, when necessary, belongs in the Cashier UI.

Client should execute authorized commands.

---

# 18. Device States

Device power/availability state and network connection state are different concepts.

Do NOT conflate:

- Offline
- Disconnected
- Available
- In Use

Example:

    Available
    = device is powered on and healthy and can be reserved

    In Use
    = active session

    Offline
    = device itself is powered off/unavailable

    Disconnected
    = device is powered on but currently cannot communicate with Cashier

The final exact state machine will be defined during implementation.

---

# 19. Device Registration

Devices are registered from Cashier first.

Registration contains complete device information, including network information such as IP address where applicable.

After a device is registered:

Client installed on that device connects and identifies itself to Cashier.

Device identity must be persistent and secure.

A device should not be trusted solely because it knows the Cashier IP.

Device authentication/identity must be implemented.

---

# 20. Concurrent Sessions

Each customer account has a configurable maximum concurrent session count.

Default behavior is expected to be:

    MaxConcurrentSessions = 1

The same rule applies to emergency/GameNet accounts if such accounts are used.

The limit may be changed when required.

The system must prevent starting a new session when the customer's concurrent session limit has been reached.

---

# 21. Game Management

Games are managed by GamePort staff through Cashier.

Cashier can manage:

- Game records
- Game availability
- Game metadata
- Installation/state information as required
- Which games can be launched on devices

Cashier can instruct a Client to launch a selected game.

Client performs the actual Windows process/game launch.

---

# 22. Windows Client Requirements

Client may integrate with Windows for:

- Kiosk mode
- Automatic startup
- Game launching
- Process monitoring
- Volume control
- Mouse settings
- Display settings where required
- Brightness where supported
- Lock/unlock
- Shutdown
- Restart
- Background/wallpaper
- Other Windows-level controls

Windows integration must be implemented safely.

Do not introduce unnecessary privileged behavior.

---

# 23. Kiosk Mode

Client is intended to replace normal user interaction with Windows during GameNet operation.

Normal customers should not have access to:

- Windows desktop
- Task Manager
- Alt+Tab
- Windows key
- Ctrl+Esc
- Other escape mechanisms

The exact kiosk implementation must be robust and reversible by authorized staff.

Authorized staff must have a protected mechanism to exit kiosk mode and access Windows.

---

# 24. Screen Streaming

Cashier may need live screen streaming from Clients.

Requirement:

- Live streaming, not periodic screenshots
- Must be optimized for LAN usage
- Must scale to the expected GameNet size
- Must not unnecessarily consume Client resources
- Must not interfere with gaming performance

The implementation technology is intentionally left open until performance requirements are evaluated.

Do not implement expensive high-resolution streaming by default.

---

# 25. Cafe

Cafe operations include:

- Product catalog
- Product variants where needed
- Inventory
- Orders
- Order items
- Payments
- Cash/card/card-to-card payment records
- Receipt printing
- Stock receiving
- Revenue
- Inventory movements

Inventory operations must also be auditable.

Stock should be represented through inventory movements rather than silently changing history.

---

# 26. Employee Management

GamePort includes employee attendance/management information.

Potential capabilities:

- Employee accounts
- Clock-in
- Clock-out
- Attendance
- Roles
- Permissions

Manager should be able to view relevant attendance and management reports.

Exact role hierarchy will be finalized during implementation.

---

# 27. Security

Security must be treated as a first-class concern.

Expected mechanisms:

- Secure password hashing
- OTP authentication for customers
- JWT/session-based authentication where appropriate
- Role-based authorization
- Permission-based authorization where needed
- Device identity
- Secure local communication
- HTTPS for Cloud communication
- Input validation
- Rate limiting where appropriate
- Audit logging
- Secure secret management

Never store plaintext passwords.

Never expose secrets in source code.

Never trust Client-provided financial values.

---

# 28. Technology Stack

## Windows

Language:

    C#

Runtime:

    .NET 10 LTS

UI:

    WPF

Applications:

    Client
    Cashier
    Cafe

Architecture:

    MVVM
    Clean Architecture principles where useful
    Modular design

Do not over-engineer simple features.

---

# 29. Cloud

Backend:

    ASP.NET Core
    .NET 10

API:

    REST API

Real-time:

    SignalR where appropriate

Database:

    PostgreSQL

ORM:

    Entity Framework Core
    Npgsql

Validation:

    FluentValidation

Logging:

    Serilog

---

# 30. Customer Application

Preferred stack:

    React Native
    TypeScript
    Expo

Android:

    Native installable application

iOS:

    PWA / Add to Home Screen

Do not design the architecture around Apple App Store distribution.

---

# 31. Manager Application

Preferred stack:

    Next.js
    TypeScript
    Tailwind CSS

The application should be installable/usable as a PWA.

---

# 32. UI/UX

All GamePort applications support:

- Dark mode
- Light mode
- Persian
- RTL

English and Arabic may be introduced later.

The UI should be:

- Modern
- Premium
- Fast
- Practical
- Highly responsive
- Consistent
- Accessible
- Appropriate for long-duration professional use

Avoid unnecessary heavy animations.

Google Stitch is used for UI/UX exploration and design.

A shared design system should be maintained across applications where appropriate.

Shared design system includes:

- Typography
- Colors
- Spacing
- Border radius
- Components
- States
- Icons
- Dark/light themes
- RTL behavior
- Interaction patterns

---

# 33. Development Principles

The project must be developed from scratch.

Do NOT copy the architecture of the old GamePort application.

Old code may be consulted only when explicitly provided as reference.

Prioritize:

1. Correctness
2. Reliability
3. Security
4. Maintainability
5. Performance
6. Simplicity

Do not introduce technologies merely because they are popular.

Every dependency must have a clear reason.

Prefer well-supported, mature libraries.

---

# 34. Domain Rules

Business rules must not be duplicated across UI applications.

For example:

Pricing logic must NOT independently exist in:

- Client
- Cashier UI
- Cafe UI
- Customer App

The authoritative business logic must live in the appropriate Domain/Application layer.

UI applications should consume business functionality through defined interfaces/contracts.

---

# 35. Financial Integrity

Financial data is critical.

Rules:

- Never delete financial transactions
- Never silently modify historical financial records
- Use immutable transactions
- Use explicit reversals/refunds
- Maintain audit history
- Use database transactions for financial operations
- Prevent duplicate financial operations
- Use idempotency for sync
- Never trust financial amounts supplied by an untrusted client

Wallet and payment operations must be designed with strong consistency.

---

# 36. Error Handling

All applications must handle:

- Internet loss
- LAN loss
- Cashier crash
- Client crash
- Cafe crash
- Cloud unavailable
- Database unavailable
- Sync interruption
- Duplicate requests
- Invalid commands
- Authentication failure
- Device disconnection
- Application restart

Errors must be:

- Logged
- Recoverable where possible
- User-friendly
- Non-destructive

Do not hide errors silently.

---

# 37. Logging

Use structured logging.

Important logs include:

- Application startup
- Application shutdown
- Connection changes
- Authentication
- Session events
- Payment events
- Wallet events
- Sync events
- Device events
- Exceptions
- Recovery
- Commands
- Security events

Logs must not expose:

- Passwords
- OTPs
- Authentication secrets
- Tokens
- Sensitive credentials

---

# 38. Testing

Testing must cover critical paths.

At minimum:

## Unit Tests

Business logic.

## Integration Tests

- Database
- API
- Sync
- Authentication

## LAN Tests

- Client ↔ Cashier
- Cafe ↔ local infrastructure

## Failure Tests

- Internet disconnect
- Cashier crash
- Client crash
- Client restart
- Windows restart
- Sync interruption
- Database restart

## Load Tests

Expected target:

    ~100 Clients per GameNet

Critical systems must remain stable under expected load.

---

# 39. Deployment

Cloud:

    Liara

Windows applications require:

- Installer
- Versioning
- Automatic updates
- Retry behavior
- Recovery if Internet is unavailable
- Safe startup

If an update cannot be downloaded because Internet is unavailable, the application must not become permanently unusable.

Existing installed versions must continue functioning until an update is successfully available and validated.

---

# 40. Future-Proofing

Potential future requirements:

- Multiple GameNets
- One Manager managing multiple GameNets
- English
- Arabic
- More device types
- More management features
- Advanced analytics
- More payment providers
- Advanced screen streaming
- Additional customer features

Do not implement future features prematurely.

Do not make the current architecture unnecessarily complicated just to support hypothetical features.

The architecture should remain extensible.

---

# 41. Current Scope

Devices currently supported:

- Gaming PC
- PlayStation / console

Do NOT introduce additional device types unless explicitly requested.

Examples of currently out-of-scope device assumptions:

- Xbox
- Billiards
- Foosball
- VIP rooms
- Other physical gaming equipment

---

# 42. Important Unknowns

Some business rules are intentionally not finalized yet.

Do not invent permanent business rules for unresolved areas.

Examples:

- Exact behavior when LAN connection is lost
- Whether new sessions are allowed during Client/Cashier disconnection
- Exact Session pause behavior
- Exact Session transfer rules
- Guest account behavior
- Exact Wallet charging timing
- Exact Cafe Wallet behavior
- Final employee role hierarchy
- Exact multi-GameNet architecture
- Exact screen streaming implementation

When implementation reaches one of these areas:

1. Check whether a later project-specific Context has defined it.
2. If not, choose the simplest safe implementation that does not block future changes.
3. Document the decision.
4. Avoid hard-coding irreversible assumptions.

---

# 43. Repository Principle

The repository is one GamePort ecosystem.

Applications must remain logically separated.

Expected high-level structure:

    GamePort/
    ├── apps/
    │   ├── Client/
    │   ├── Cashier/
    │   ├── Cafe/
    │   ├── Customer/
    │   └── Manager/
    │
    ├── backend/
    │   └── Cloud/
    │
    ├── shared/
    │   ├── Domain/
    │   ├── Application/
    │   ├── Contracts/
    │   └── Infrastructure/
    │
    ├── docs/
    │
    └── GAMEPORT_CONTEXT.md

The exact solution/project structure may be adjusted during implementation.

Do not create unnecessary coupling between applications.

---

# 44. Context Hierarchy

This file is the Master Context.

Application-specific contexts may override or extend this document.

Expected structure:

    GAMEPORT_CONTEXT.md

    apps/
    ├── Client/
    │   └── CLIENT_CONTEXT.md
    │
    ├── Cashier/
    │   └── CASHIER_CONTEXT.md
    │
    ├── Cafe/
    │   └── CAFE_CONTEXT.md
    │
    ├── Customer/
    │   └── CUSTOMER_CONTEXT.md
    │
    └── Manager/
        └── MANAGER_CONTEXT.md

When working inside an application:

1. Read GAMEPORT_CONTEXT.md
2. Read that application's Context
3. Follow both
4. Application-specific Context may add details but must not silently violate global architecture
5. If a conflict exists, explicitly identify it before making a major architectural change

---

# 45. OpenCode Instructions

When working on GamePort:

- Read the relevant Context before making architectural decisions.
- Do not rewrite architecture without a clear reason.
- Do not introduce a new framework without justification.
- Do not modify unrelated applications.
- Keep changes focused.
- Prefer small, testable changes.
- Run relevant tests after significant changes.
- Do not assume Cloud is always available.
- Do not assume LAN is always available.
- Do not assume Client can reach Cloud.
- Treat Cashier as the local operational authority.
- Treat financial history as immutable.
- Preserve auditability.
- Never silently delete business data.
- Never store credentials in source code.
- Never expose secrets in logs.
- Do not implement unresolved business rules as irreversible architecture.
- Document important architectural decisions.

Before implementing a large feature, understand:

    Requirement
        ↓
    Domain rule
        ↓
    Application behavior
        ↓
    API/contract
        ↓
    Persistence
        ↓
    Real-time behavior
        ↓
    Offline behavior
        ↓
    Recovery behavior
        ↓
    UI

Do not solve a complex domain problem only at the UI level.

---

# 46. Golden Rules

These rules have highest priority:

1. GamePort must work without Internet inside a GameNet.
2. Client communicates operationally with Cashier, not Cloud.
3. Cashier is the local operational authority.
4. Cloud contains the complete overall system data.
5. Local data must synchronize with Cloud.
6. Sync must be resumable and idempotent.
7. Financial history must never be deleted.
8. Reversals/refunds create new transactions.
9. Important operations must be auditable.
10. Client must recover active sessions after restart/crash.
11. Cashier must support recovery after temporary failure.
12. Pricing is frozen at Session start.
13. Device power state and network connection state are separate concepts.
14. Security must never rely only on the LAN being trusted.
15. Do not introduce unsupported device types.
16. Do not over-engineer unresolved future requirements.
17. Reliability is more important than visual complexity.
18. Business logic must not be duplicated across applications.
19. Never trust financial values supplied directly by Client applications.
20. Preserve backward compatibility and data integrity when evolving the system.

---

# 47. Current Development Strategy

The project will be built application-by-application.

Recommended implementation order:

    1. Cashier
    2. Client
    3. Cafe
    4. Cloud
    5. Customer
    6. Manager

However, Cloud infrastructure/API contracts may be implemented incrementally whenever required by Cashier synchronization.

The first major application is Cashier because it defines the local GameNet operational environment.

Each application must have its own detailed Context before OpenCode begins substantial implementation.

The Master Context provides global rules.

Application Contexts provide implementation-specific requirements.

Do not attempt to build the entire GamePort ecosystem in one step.
Build stable vertical slices and integrate them progressively.