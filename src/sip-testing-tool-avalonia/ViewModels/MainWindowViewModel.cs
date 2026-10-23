using System.Text;
using AvaloniaEdit.Document;
using Clc.Sip.Desktop.Models;
using Clc.Sip.Desktop.Services;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Clc.Sip.Desktop.ViewModels;

public partial class MainWindowViewModel : ObservableObject, IDisposable
{
    private readonly SipSession session;
    private readonly SettingsStore settingsStore;
    private readonly UpdateService updates;
    private CancellationTokenSource? connectionCancellation;
    private readonly Dictionary<SipCommand, CommandOptions> commandOptions = CommandChoice.All
        .ToDictionary(choice => choice.Command, choice => CommandOptions.Create(choice.Command));

    [ObservableProperty] private SessionFields fields = new();
    [ObservableProperty] private AppPreferences preferences = new();
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPatronQuery), nameof(IsFeePaid), nameof(IsNotice), nameof(ExecuteLabel), nameof(Options))]
    private CommandChoice selectedCommand = CommandChoice.All[0];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(UsesRange))]
    private SummaryChoice selectedSummary = SummaryChoice.All[0];
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRun), nameof(CanConnect), nameof(CanRestartUpdate))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(ExecuteCommand), nameof(LoginCommand), nameof(RestartUpdateCommand), nameof(CheckUpdatesCommand))]
    private bool isBusy;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRun), nameof(CanConnect))]
    [NotifyCanExecuteChangedFor(nameof(ConnectCommand), nameof(ExecuteCommand), nameof(LoginCommand))]
    private bool isConnected;
    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(CancelConnectCommand))]
    private bool isConnecting;
    [ObservableProperty] private string status = "Not connected";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasError))]
    private string? errorMessage;
    [ObservableProperty] private bool alwaysOnTop;
    [ObservableProperty] private string feeType = "";
    [ObservableProperty] private string paymentType = "";
    [ObservableProperty] private string amount = "";
    [ObservableProperty] private string feeIdentifier = "";
    [ObservableProperty] private string transactionId = "";
    [ObservableProperty] private NoticeStatus selectedNoticeStatus;
    [ObservableProperty] private NoticeMedium selectedNoticeMedium;
    [ObservableProperty] private NotificationType selectedNoticeType;
    [ObservableProperty] private string noticeComment = "";
    [ObservableProperty] private string updateStatus = "";
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRestartUpdate))]
    [NotifyCanExecuteChangedFor(nameof(RestartUpdateCommand))]
    private bool updateReady;
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanRestartUpdate))]
    [NotifyCanExecuteChangedFor(nameof(RestartUpdateCommand))]
    [NotifyCanExecuteChangedFor(nameof(CheckUpdatesCommand))]
    private bool checkingUpdates;

    public IReadOnlyList<CommandChoice> Commands => CommandChoice.All;
    public IReadOnlyList<SummaryChoice> Summaries => SummaryChoice.All;
    public IReadOnlyList<string> VendorProfiles { get; } = ["SIP", "ENVISIONWARE", "COMPRISE", "3M", "ITG"];
    public CommandOptions Options => commandOptions[SelectedCommand.Command];
    public IReadOnlyList<NoticeStatus> NoticeStatuses { get; } = Enum.GetValues<NoticeStatus>();
    public IReadOnlyList<NoticeMedium> NoticeMedia { get; } = Enum.GetValues<NoticeMedium>();
    public IReadOnlyList<NotificationType> NoticeTypes { get; } = Enum.GetValues<NotificationType>();
    public TextDocument LogDocument { get; } = new();
    public bool IsPatronQuery => SelectedCommand.Command == SipCommand.PatronInformation;
    public bool IsFeePaid => SelectedCommand.Command == SipCommand.FeePaid;
    public bool IsNotice => SelectedCommand.Command == SipCommand.NoticeUpdate;
    public bool UsesRange => SelectedSummary.Key != "none";
    public bool CanRun => IsConnected && !IsBusy;
    public bool CanConnect => !IsBusy;
    public bool CanRestartUpdate => UpdateReady && !IsBusy && !CheckingUpdates;
    public bool HasError => !string.IsNullOrEmpty(ErrorMessage);
    public string ExecuteLabel => "Execute " + SelectedCommand.Name;
    public string WindowTitle => "SIP Testing Tool v" + ReleaseVersion.Current.DisplayValue;

    public event Action? TransactionAppended;
    public event Action? ThemeChanged;
    public event Action<string>? ValidationFailed;
    public event Action? RestartRequested;

    [RelayCommand]
    private void ResetOptions()
    {
        var command = SelectedCommand.Command;
        commandOptions[command] = CommandOptions.Create(command);
        if (command == SipCommand.PatronInformation)
        {
            SelectedSummary = SummaryChoice.All[0];
            Fields.Bp = "1";
            Fields.Bq = "5";
        }
        if (command == SipCommand.FeePaid)
        {
            FeeType = PaymentType = Amount = FeeIdentifier = TransactionId = "";
        }
        if (command == SipCommand.NoticeUpdate)
        {
            SelectedNoticeStatus = default;
            SelectedNoticeMedium = default;
            SelectedNoticeType = default;
            NoticeComment = "";
        }
        OnPropertyChanged(nameof(Options));
    }

    [RelayCommand]
    private async Task ToggleThemeAsync()
    {
        Preferences.DarkTheme = !Preferences.DarkTheme;
        ThemeChanged?.Invoke();
        try { await settingsStore.SaveAsync(Preferences); }
        catch (Exception ex) { ErrorMessage = "Theme changed, but the preference could not be saved: " + ex.Message; }
    }

    public MainWindowViewModel(SipSession? session = null, SettingsStore? settingsStore = null, UpdateService? updates = null)
    {
        this.session = session ?? new SipSession();
        this.settingsStore = settingsStore ?? new SettingsStore();
        this.updates = updates ?? new UpdateService();
    }

    public async Task InitializeAsync()
    {
        await ReloadDefaultsAsync();
        if (Preferences.ConnectOnStartup)
        {
            await ConnectAsync();
        }

        if (Preferences.CheckUpdatesOnStartup)
        {
            await CheckUpdatesAsync();
        }
    }

    public async Task ReloadDefaultsAsync()
    {
        try
        {
            var defaults = await settingsStore.LoadAsync();
            ApplyPreferences(defaults, true);
        }
        catch (Exception ex) { ErrorMessage = "Could not load defaults: " + ex.Message; }
    }

    public async Task SavePreferencesAsync(AppPreferences value)
    {
        _ = Encoding.GetEncoding(value.EncodingName);
        if (!string.IsNullOrWhiteSpace(value.UpdateFeedUrl))
        {
            _ = UpdateService.GetRepositoryUrl(value.UpdateFeedUrl);
        }

        await settingsStore.SaveAsync(value);
        ApplyPreferences(value, false);
    }

    private void ApplyPreferences(AppPreferences value, bool reloadFields)
    {
        Preferences = value;
        if (reloadFields)
        {
            Fields = value.Defaults.Copy();
        }

        ThemeChanged?.Invoke();
    }

    [RelayCommand(CanExecute = nameof(CanConnect))]
    private async Task ConnectAsync()
    {
        await RunAsync(async () =>
        {
            using var cancellation = new CancellationTokenSource();
            connectionCancellation = cancellation;
            IsConnecting = true;
            Status = $"Connecting to {Fields.Server.Trim()}:{Fields.Port}…";
            try
            {
                await session.ConnectAsync(Fields.Copy(), Preferences.EncodingName, Preferences.ValidateChecksums, cancellation.Token);
                Status = "Connected to " + session.Endpoint;
            }
            catch (OperationCanceledException) when (cancellation.IsCancellationRequested)
            {
                Status = "Connection cancelled";
            }
            finally
            {
                connectionCancellation = null;
                IsConnecting = false;
            }
        });
    }

    [RelayCommand(CanExecute = nameof(IsConnecting))]
    private void CancelConnect() => connectionCancellation?.Cancel();

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task ExecuteAsync() => ExecuteSipAsync(SelectedCommand.Command);

    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task LoginAsync() => ExecuteSipAsync(SipCommand.Login);

    private Task ExecuteSipAsync(SipCommand command) => RunAsync(async () =>
    {
        var inputs = new CommandFields
        {
            Options = commandOptions[command].Copy(),
            Summary = SelectedSummary.Key,
            FeeType = FeeType,
            PaymentType = PaymentType,
            Amount = Amount,
            FeeIdentifier = FeeIdentifier,
            TransactionId = TransactionId,
            NoticeStatus = SelectedNoticeStatus,
            NoticeMedium = SelectedNoticeMedium,
            NoticeType = SelectedNoticeType,
            NoticeComment = NoticeComment,
            DeliveryDate = DateTime.Now
        };
        var transaction = await session.ExecuteAsync(command, Fields.Copy(), inputs,
            Preferences.EncodingName, Preferences.ValidateChecksums);
        AppendTransaction(transaction);
        Status = transaction.ConnectionReset ? "Connection closed — reconnect to continue" : "Connected to " + session.Endpoint;
        if (transaction.ConnectionReset)
        {
            ErrorMessage = "The connection was closed or the request timed out.";
        }
        else if (!transaction.ChecksumValid)
        {
            ErrorMessage = "The response checksum is invalid.";
        }
    });

    private async Task RunAsync(Func<Task> operation)
    {
        if (IsBusy)
        {
            return;
        }

        IsBusy = true;
        ErrorMessage = null;
        string? invalidField = null;
        try
        {
            await operation();
        }
        catch (Exception ex)
        {
            ErrorMessage = ex.Message;
            invalidField = (ex as CommandValidationException)?.FieldName;
            Status = session.IsConnected ? "Connected to " + session.Endpoint : "Not connected";
        }
        finally
        {
            IsConnected = session.IsConnected;
            IsBusy = false;
            if (invalidField != null) { ValidationFailed?.Invoke(invalidField); }
        }
    }

    public void AppendTransaction(SipTransaction transaction)
    {
        static string Line(string? value) => (value ?? "").Replace("\r", "").Replace("\n", "");
        var text = (LogDocument.TextLength > 0 ? "\n\n" : "")
            + "--> " + Line(transaction.Message) + "\n<-- " + Line(transaction.Response);
        LogDocument.Insert(LogDocument.TextLength, text);
        TransactionAppended?.Invoke();
    }

    [RelayCommand]
    private void ClearLog() => LogDocument.Text = "";

    [RelayCommand]
    private void DismissError() => ErrorMessage = null;

    private bool CanCheckUpdates() => !CheckingUpdates && !IsBusy;

    [RelayCommand(CanExecute = nameof(CanCheckUpdates))]
    private async Task CheckUpdatesAsync()
    {
        if (CheckingUpdates)
        {
            return;
        }

        CheckingUpdates = true;
        UpdateStatus = "Checking for updates…";
        try
        {
            UpdateStatus = await updates.CheckAndDownloadAsync(Preferences.UpdateFeedUrl);
        }
        catch { UpdateStatus = "Update check unavailable. Try again later."; }
        finally
        {
            UpdateReady = updates.IsReady;
            CheckingUpdates = false;
        }
    }

    [RelayCommand(CanExecute = nameof(CanRestartUpdate))]
    private async Task RestartUpdateAsync()
    {
        if (!CanRestartUpdate) { return; }
        IsBusy = true;
        try
        {
            await settingsStore.SaveAsync(Preferences);
            await updates.PrepareRestartAsync(Preferences.UpdateFeedUrl);
            session.Disconnect();
            IsConnected = false;
            UpdateReady = false;
            RestartRequested?.Invoke();
        }
        catch (Exception ex)
        {
            UpdateReady = updates.IsReady;
            ErrorMessage = "Could not install the update: " + ex.Message;
        }
        finally { IsBusy = false; }
    }

    public void Dispose()
    {
        connectionCancellation?.Cancel();
        updates.Dispose();
        session.Dispose();
    }
}
