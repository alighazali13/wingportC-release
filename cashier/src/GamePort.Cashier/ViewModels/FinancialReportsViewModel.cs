using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using GamePort.Cashier.Common;
using GamePort.Cashier.Contracts.Responses;
using GamePort.Cashier.Services;

namespace GamePort.Cashier.ViewModels;

public partial class FinancialReportsViewModel : ObservableObject
{
    private readonly IApiClient _apiClient;

    public ObservableCollection<FinancialKpi> KpiCards { get; } = new();
    public ObservableCollection<TransactionItem> Transactions { get; } = new();

    [ObservableProperty]
    private string _selectedPeriod = "امروز";

    [ObservableProperty]
    private string _totalRevenue = "۰";

    [ObservableProperty]
    private string _totalTransactions = "۰";

    [ObservableProperty]
    private string _cashBalance = "۰";

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLocked))]
    [NotifyPropertyChangedFor(nameof(BlurRadius))]
    private bool _isUnlocked;

    public bool IsLocked => !IsUnlocked;

    public double BlurRadius => IsUnlocked ? 0 : 14;

    public FinancialReportsViewModel(IApiClient apiClient)
    {
        _apiClient = apiClient;

        WeakReferenceMessenger.Default.Register<PinConfirmedMessage>(this, (recipient, message) =>
        {
            IsUnlocked = true;
        });

        _ = LoadAsync();
    }

    partial void OnSelectedPeriodChanged(string value) => _ = LoadAsync();

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsLoading = true;
        ErrorMessage = null;

        try
        {
            var (from, to) = ResolvePeriod();

            var result = await _apiClient.GetFinancialReportAsync(from, to);
            if (!result.IsSuccess)
            {
                ErrorMessage = result.Error;
                return;
            }

            var report = result.Data!;

            TotalRevenue = PersianFormat.Number(report.ServiceRevenue);
            TotalTransactions = PersianFormat.Number(report.TransactionCount);
            CashBalance = PersianFormat.Number(report.CashCollected);

            BuildKpis(report);
            BuildTransactions(report);
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void OpenPinModal()
    {
        WeakReferenceMessenger.Default.Send(new OpenPinModalMessage());
    }

    [RelayCommand]
    private void SetPeriod(string period) => SelectedPeriod = period;

    [RelayCommand]
    private void ExportExcel()
    {
    }

    [RelayCommand]
    private void PrintInvoice()
    {
    }

    private (DateTime From, DateTime To) ResolvePeriod()
    {
        var to = DateTime.Now.ToUniversalTime();
        var from = SelectedPeriod switch
        {
            "این هفته" => DateTime.Today.AddDays(-6).ToUniversalTime(),
            "این ماه" => DateTime.Today.AddDays(-29).ToUniversalTime(),
            _ => DateTime.Today.ToUniversalTime()
        };

        return (from, to);
    }

    private void BuildKpis(FinancialReportResponse report)
    {
        KpiCards.Clear();

        var total = report.ServiceRevenue <= 0 ? 1m : report.ServiceRevenue;
        var pcShare = (int)Math.Round(report.PcRevenue / total * 100);
        var psShare = (int)Math.Round(report.PsRevenue / total * 100);

        KpiCards.Add(new FinancialKpi
        {
            Label = "درآمد خدمات (کل)",
            Value = PersianFormat.Number(report.ServiceRevenue),
            Unit = "تومان",
            Change = PersianFormat.Number(report.SessionCount) + " نشست",
            ChangeLabel = "مجموع مصرف از کیف پول",
            Progress = 100,
            AccentColor = "#5046E5",
            ChangePositive = true
        });
        KpiCards.Add(new FinancialKpi
        {
            Label = "درآمد سیستم‌های گیمینگ (PC)",
            Value = PersianFormat.Number(report.PcRevenue),
            Unit = "تومان",
            Change = PersianFormat.Digits($"{pcShare}%"),
            ChangeLabel = "سهم از کل",
            Progress = pcShare,
            AccentColor = "#3B82F6"
        });
        KpiCards.Add(new FinancialKpi
        {
            Label = "درآمد کنسول‌های بازی (PS)",
            Value = PersianFormat.Number(report.PsRevenue),
            Unit = "تومان",
            Change = PersianFormat.Digits($"{psShare}%"),
            ChangeLabel = "سهم از کل",
            Progress = psShare,
            AccentColor = "#6366F1"
        });
        KpiCards.Add(new FinancialKpi
        {
            Label = "شارژ کیف پول",
            Value = PersianFormat.Number(report.WalletTopUps),
            Unit = "تومان",
            Change = PersianFormat.Number(report.CashCollected) + " نقدی",
            ChangeLabel = "شارژهای ثبت‌شده در بازه",
            Progress = report.WalletTopUps <= 0 ? 0 : 100,
            AccentColor = "#F59E0B",
            ChangePositive = true
        });
    }

    private void BuildTransactions(FinancialReportResponse report)
    {
        Transactions.Clear();

        foreach (var item in report.Transactions)
        {
            var isCharge = item.Amount < 0;
            var isConsole = string.Equals(item.DeviceType, "PlayStation", StringComparison.OrdinalIgnoreCase);

            Transactions.Add(new TransactionItem
            {
                Id = "#TRX-" + item.Id.ToString("N")[..6].ToUpperInvariant(),
                Time = PersianFormat.Digits(item.CreatedAt.ToLocalTime().ToString("HH:mm:ss")),
                DeviceLabel = isConsole ? "PS5" : item.DeviceType is null ? "کیف پول" : "PC",
                DeviceName = string.IsNullOrWhiteSpace(item.CustomerName) ? "مشتری" : item.CustomerName,
                DeviceDetail = item.Description ?? "-",
                DeviceColor = isConsole ? "#6366F1" : isCharge ? "#3B82F6" : "#10B981",
                CustomerName = string.IsNullOrWhiteSpace(item.CustomerName) ? "-" : item.CustomerName,
                CustomerType = isCharge ? "مصرف نشست" : "شارژ حساب",
                Duration = "-",
                PaymentMethod = isCharge ? "کسر از کیف پول" : "شارژ کیف پول",
                Amount = PersianFormat.Number(Math.Abs(item.Amount)),
                AmountPrefix = isCharge ? "-" : "+",
                Status = "ثبت شده",
                IsSettled = true
            });
        }
    }
}

public class FinancialKpi
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string Unit { get; set; } = string.Empty;
    public string Change { get; set; } = string.Empty;
    public string ChangeLabel { get; set; } = string.Empty;
    public int Progress { get; set; }
    public string AccentColor { get; set; } = string.Empty;
    public bool ChangePositive { get; set; }
}

public class TransactionItem
{
    public string Id { get; set; } = string.Empty;
    public string Time { get; set; } = string.Empty;
    public string DeviceLabel { get; set; } = string.Empty;
    public string DeviceName { get; set; } = string.Empty;
    public string DeviceDetail { get; set; } = string.Empty;
    public string DeviceColor { get; set; } = string.Empty;
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerType { get; set; } = string.Empty;
    public string Duration { get; set; } = string.Empty;
    public string PaymentMethod { get; set; } = string.Empty;
    public string Amount { get; set; } = string.Empty;
    public string AmountPrefix { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public bool IsSettled { get; set; }
}
