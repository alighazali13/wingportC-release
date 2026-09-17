namespace GamePort.Cashier.Application.Security;

public static class Roles
{
    public const string Admin = "Admin";
    public const string Manager = "Manager";
    public const string Operator = "Operator";

    private static readonly string[] AdminPermissions = Permissions.All.ToArray();

    private static readonly string[] ManagerPermissions =
    {
        Permissions.CustomersView,
        Permissions.SessionsView, Permissions.SessionsStart, Permissions.SessionsEnd,
        Permissions.SessionsExtend, Permissions.SessionsTransfer, Permissions.SessionsPause,
        Permissions.ReservationsView, Permissions.ReservationsManage,
        Permissions.DevicesView, Permissions.DevicesCommand,
        Permissions.GamesView, Permissions.GamesLaunch,
        Permissions.PricingView,
        Permissions.ReportsView, Permissions.AuditView, Permissions.SyncView
    };

    private static readonly string[] OperatorPermissions =
    {
        Permissions.CustomersView, Permissions.CustomersEdit,
        Permissions.WalletRecharge,
        Permissions.SessionsView, Permissions.SessionsStart, Permissions.SessionsEnd,
        Permissions.SessionsExtend, Permissions.SessionsTransfer, Permissions.SessionsPause,
        Permissions.ReservationsView, Permissions.ReservationsManage,
        Permissions.DevicesView, Permissions.DevicesCommand,
        Permissions.GamesView, Permissions.GamesLaunch,
        Permissions.PricingView,
        Permissions.AttendanceManage
    };

    private static readonly Dictionary<string, IReadOnlyList<string>> Map = new(StringComparer.OrdinalIgnoreCase)
    {
        [Admin] = AdminPermissions,
        [Manager] = ManagerPermissions,
        [Operator] = OperatorPermissions
    };

    public static IReadOnlyList<string> All { get; } = new[] { Admin, Manager, Operator };

    public static bool IsValid(string role) => Map.ContainsKey(role);

    public static IReadOnlyList<string> GetPermissions(string role)
        => Map.TryGetValue(role, out var permissions) ? permissions : Array.Empty<string>();
}
