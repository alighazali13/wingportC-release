namespace GamePort.Cashier.Application.Security;

public static class Permissions
{
    public const string CustomersView = "customers.view";
    public const string CustomersEdit = "customers.edit";
    public const string WalletRecharge = "wallet.recharge";
    public const string WalletRefund = "wallet.refund";
    public const string SessionsStart = "sessions.start";
    public const string SessionsView = "sessions.view";
    public const string SessionsEnd = "sessions.end";
    public const string SessionsExtend = "sessions.extend";
    public const string SessionsTransfer = "sessions.transfer";
    public const string SessionsPause = "sessions.pause";
    public const string ReservationsView = "reservations.view";
    public const string ReservationsManage = "reservations.manage";
    public const string DevicesView = "devices.view";
    public const string DevicesManage = "devices.manage";
    public const string DevicesCommand = "devices.command";
    public const string GamesView = "games.view";
    public const string GamesManage = "games.manage";
    public const string GamesLaunch = "games.launch";
    public const string PricingView = "pricing.view";
    public const string PricingManage = "pricing.manage";
    public const string EmployeesManage = "employees.manage";
    public const string AttendanceManage = "attendance.manage";
    public const string ReportsView = "reports.view";
    public const string AuditView = "audit.view";
    public const string SyncView = "sync.view";
    public const string ConfigManage = "config.manage";

    public static IReadOnlyList<string> All { get; } = new[]
    {
        CustomersView, CustomersEdit, WalletRecharge, WalletRefund,
        SessionsStart, SessionsView, SessionsEnd, SessionsExtend, SessionsTransfer, SessionsPause,
        ReservationsView, ReservationsManage, DevicesView, DevicesManage, DevicesCommand,
        GamesView, GamesManage, GamesLaunch, PricingView, PricingManage,
        EmployeesManage, AttendanceManage, ReportsView, AuditView, SyncView, ConfigManage
    };
}
