# GamePort Manager — Development Context

## 1. Project Overview

GamePort Manager is the management dashboard for GamePort owners/managers.

It is a **PWA**, not a native mobile application.

It should work on:

- Desktop browsers
- Android browsers / Add to Home Screen
- iOS Safari / Add to Home Screen

Primary technologies:

- Next.js
- TypeScript
- Tailwind CSS
- PWA
- TanStack Query

---

# 2. Read These Context Files First

Before making decisions:

1. Read `../GAMEPORT_CONTEXT.md`
2. Read this `MANAGER_CONTEXT.md`

Global context is the source of truth.

---

# 3. Architecture

```text
Manager PWA
    ↓ HTTPS
Cloud API
    ↓
Cloud PostgreSQL
```

Manager does NOT directly communicate with:

- Cashier
- Client
- Cafe local database

All management information comes through Cloud APIs.

---

# 4. Platform

Manager is a PWA.

Do NOT build a native Android/iOS application unless the global architecture changes.

The app should support:

- Responsive desktop
- Tablet
- Mobile
- Installable PWA behavior

---

# 5. Authentication

Manager authentication is separate from Customer authentication.

Expected authentication:

- Email/password
- Session-based/token authentication
- 2FA later

Manager credentials must never be mixed with Customer OTP authentication.

---

# 6. Authorization

Manager permissions are important.

Potential roles:

```text
Owner
Manager
Supervisor
```

Do not hard-code authorization rules into UI only.

Every protected operation must be authorized by the backend.

---

# 7. Main Responsibilities

Manager should provide:

- Business overview
- Revenue reports
- Gaming session reports
- Cafe reports
- Device status overview
- Employee attendance
- Employee management
- Operational reports
- Reservation overview
- GameNet management information
- Pricing overview
- Events overview
- Management settings

Manager does NOT need access to all sensitive customer operational information.

---

# 8. Customer Privacy

Manager should not unnecessarily expose:

- Customer passwords
- Authentication tokens
- Sensitive wallet details
- Private customer information

Only show customer information required for the management task.

---

# 9. Dashboard

Main dashboard should provide a high-level operational overview.

Potential cards:

```text
درآمد امروز
سشن‌های فعال
سفارش‌های کافه
رزروهای امروز
دستگاه‌های فعال
```

Other sections:

- Revenue trend
- Gaming activity
- Cafe performance
- Device health
- Recent operational events

Do not make the dashboard visually overloaded.

---

# 10. Financial Reports

Support:

- Daily revenue
- Weekly revenue
- Monthly revenue
- Gaming revenue
- Cafe revenue
- Payment methods
- Refunds
- Revenue by GameNet

Financial data must come from authoritative backend sources.

Do not calculate authoritative revenue entirely in the browser.

---

# 11. Gaming Reports

Potential reports:

- Session count
- Session duration
- Device utilization
- Revenue by device
- Revenue by GameNet
- Popular games
- Reservation usage

Be careful to distinguish:

- Actual session usage
- Reservation
- Cancelled reservation
- Unused reserved time

---

# 12. Cafe Reports

Potential reports:

- Orders
- Revenue
- Product sales
- Category sales
- Popular products
- Inventory status
- Low-stock products

Cafe data should come from the authoritative backend.

---

# 13. Device Overview

Manager may see device status overview.

States:

```text
Available
In Use
Reserved
Offline
Disconnected
```

Do not confuse:

```text
Offline
```

with:

```text
Disconnected
```

Manager may see aggregated status but should not replace Cashier as operational authority.

---

# 14. Employees

Manager can view/manage employees where authorized.

Potential data:

- Name
- Role
- Status
- Attendance
- Shift
- Activity summary

Sensitive employee information should be minimized.

---

# 15. Attendance

Potential features:

- Clock-in
- Clock-out
- Current employees
- Shift history
- Attendance reports

The backend remains authoritative.

---

# 16. Reservations

Manager can view:

- Upcoming reservations
- Historical reservations
- Cancelled reservations
- Utilization
- Reservation revenue

Do not let Manager directly mutate local Cashier state.

Management actions should go through Cloud and synchronize through the normal GamePort architecture.

---

# 17. Events

Manager can:

- Create events
- Edit events
- View registrations
- Manage capacity
- Publish/unpublish events
- View event performance

All authorization must be server-side.

---

# 18. Multi-GameNet Support

The architecture must support future multiple GameNets.

Even if there is currently only one venue, do not hard-code:

```text
GameNetId = 1
```

The conceptual hierarchy is:

```text
Owner
 ├── GameNet A
 │    ├── Devices
 │    ├── Sessions
 │    ├── Cafe
 │    └── Employees
 │
 └── GameNet B
      ├── Devices
      ├── Sessions
      ├── Cafe
      └── Employees
```

The UI should allow selecting a GameNet where appropriate.

---

# 19. Data Fetching

Use TanStack Query for server state.

Support:

- Query caching
- Pagination
- Filters
- Sorting
- Mutations
- Cache invalidation
- Loading states
- Error states

Do not duplicate server state unnecessarily.

---

# 20. API

Use Cloud REST APIs.

Potential areas:

```text
/auth
/gamenets
/devices
/sessions
/reservations
/cafe
/reports
/employees
/events
/settings
```

Actual endpoints must follow backend contracts.

Do not invent backend behavior in the frontend.

---

# 21. Real-Time

Use real-time updates where useful.

Examples:

- Device status
- Active sessions
- Live dashboard metrics
- Order status
- Alerts

Do not turn every page into a WebSocket-driven application.

---

# 22. UI/UX

The Manager application should feel like a premium modern management product.

It should be:

- Professional
- Clean
- Data-oriented
- Responsive
- Fast
- Modern

Avoid:

- Generic admin templates
- Excessive gradients
- Giant KPI cards
- Visual clutter
- Excessive animations

---

# 23. RTL

Primary language:

Persian.

True RTL is mandatory.

Architecture should allow future:

- English
- Arabic

Do not hard-code directional assumptions.

Use logical CSS properties where possible.

---

# 24. Theme

Support:

- Dark
- Light

Dark mode can be default.

Use the same general GamePort design language as Cashier/Cafe/Customer.

---

# 25. Navigation

Suggested navigation:

```text
داشبورد
گزارش‌ها
دستگاه‌ها
سشن‌ها
رزروها
کافه
کارکنان
رویدادها
تنظیمات
```

Navigation can adapt based on permissions.

---

# 26. Responsive Design

Desktop:

Use sidebar + main content.

Tablet:

Reduce sidebar density where appropriate.

Mobile:

Use compact navigation / mobile navigation.

Do not simply shrink desktop layouts.

Important tables should become usable mobile cards or horizontally scrollable views where appropriate.

---

# 27. Reports

Reports should support:

- Date range
- GameNet
- Category
- Payment method
- Employee
- Device

Where useful:

- Export
- Print
- CSV/PDF generation through backend or appropriate service

Do not perform huge data processing in the browser.

---

# 28. Loading / Error States

Every major screen must support:

- Loading
- Empty
- Error
- Success

Examples:

```text
اطلاعاتی برای نمایش وجود ندارد.

دریافت اطلاعات با مشکل مواجه شد.
```

Never expose raw API errors.

---

# 29. Security

Never:

- Store passwords
- Expose secrets
- Trust frontend authorization
- Put API secrets in client code
- Allow UI-only permission enforcement

The backend is authoritative.

Use secure authentication/session mechanisms appropriate to the deployment.

---

# 30. Performance

Optimize for:

- Fast initial load
- Efficient queries
- Pagination
- Lazy loading where useful
- Image optimization
- Minimal JavaScript
- Avoiding unnecessary rerenders

Do not load huge datasets into the browser unnecessarily.

---

# 31. Testing

Important tests:

### Unit

- Formatting
- Permission visibility
- Filters
- Calculations that are purely presentational

### Integration

- API calls
- Authentication
- Query invalidation
- Permissions

### E2E

Important workflows:

```text
Login
→ Dashboard
→ Filter GameNet
→ Open Report
→ Export
```

and:

```text
Login
→ View Device Status
→ Open Session
→ View details
```

---

# 32. Implementation Order

### Phase 1
- Next.js
- TypeScript
- Tailwind
- PWA
- RTL
- Theme
- Design system

### Phase 2
- Authentication
- Authorization
- App shell

### Phase 3
- Dashboard

### Phase 4
- Devices
- Sessions
- Reservations

### Phase 5
- Financial reports
- Cafe reports

### Phase 6
- Employees
- Attendance

### Phase 7
- Events
- Settings

### Phase 8
- Real-time
- Performance
- Testing
- PWA hardening

---

# 33. Golden Rules

1. Manager is a management interface, not a local operational authority.
2. Manager communicates with Cloud.
3. Manager does not directly access Cashier/Client/Cafe databases.
4. Backend authorization is authoritative.
5. Support future multi-GameNet operation.
6. Do not expose unnecessary customer information.
7. Financial reports must use authoritative backend data.
8. Use TanStack Query for server state.
9. True RTL is mandatory.
10. Support dark and light themes.
11. Responsive behavior must be intentional.
12. Do not turn the UI into a generic admin template.
13. Do not invent backend endpoints or business rules.
14. Never treat frontend permission checks as security.
15. Follow `GAMEPORT_CONTEXT.md` whenever this file does not define a rule.