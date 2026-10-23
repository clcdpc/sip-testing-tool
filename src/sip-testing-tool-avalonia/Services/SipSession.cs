using System.Globalization;
using System.Net.Sockets;
using System.Text;
using Clc.Sip.Desktop.Models;

namespace Clc.Sip.Desktop.Services;

public sealed class SipSession : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly TimeSpan timeout;
    private readonly TimeSpan connectTimeout;
    private readonly Func<sip2, CancellationToken, Task> connect;
    private sip2? client;
    private bool disposed;

    public bool IsConnected => client?.IsConnected() == true;
    public string Endpoint { get; private set; } = "";

    public SipSession(TimeSpan? timeout = null, TimeSpan? connectTimeout = null)
        : this((connection, cancellation) => connection.ConnectAsync(cancellation).AsTask(), timeout, connectTimeout) { }

    internal SipSession(Func<sip2, CancellationToken, Task> connect, TimeSpan? timeout = null, TimeSpan? connectTimeout = null)
    {
        this.connect = connect;
        this.timeout = timeout ?? TimeSpan.FromSeconds(30);
        this.connectTimeout = connectTimeout ?? TimeSpan.FromSeconds(5);
    }

    public async Task ConnectAsync(SessionFields fields, string encoding, bool checkChecksum, CancellationToken cancellationToken = default)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        fields = fields.Copy();
        if (string.IsNullOrWhiteSpace(fields.Server))
        {
            throw new CommandValidationException(nameof(SessionFields.Server), "Server is required.");
        }

        if (!int.TryParse(fields.Port, out var port) || port is < 1 or > 65535)
        {
            throw new CommandValidationException(nameof(SessionFields.Port), "Port must be a number between 1 and 65535.");
        }

        var textEncoding = Encoding.GetEncoding(encoding);
        var server = fields.Server.Trim();
        var endpoint = $"{server}:{port}";
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token, cancellationToken);
        cancellation.CancelAfter(connectTimeout);
        try
        {
            await gate.WaitAsync(cancellation.Token).ConfigureAwait(false);
            try
            {
                ObjectDisposedException.ThrowIf(disposed, this);
                Disconnect();
                var connection = new sip2(server, port, fields.Ao, fields.Ap)
                {
                    TextEncoding = textEncoding,
                    ValidateResponseChecksum = checkChecksum
                };
                client = connection;
                try
                {
                    // Bound the entire attempt, including DNS, even if a connector is slow to acknowledge cancellation.
                    await connect(connection, cancellation.Token).WaitAsync(cancellation.Token).ConfigureAwait(false);
                    cancellation.Token.ThrowIfCancellationRequested();
                    Endpoint = endpoint;
                }
                catch
                {
                    Disconnect();
                    throw;
                }
            }
            finally { gate.Release(); }
        }
        catch (Exception ex) when (cancellation.IsCancellationRequested)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (lifetime.IsCancellationRequested)
            {
                throw new OperationCanceledException("The connection attempt was cancelled.", ex, cancellation.Token);
            }
            throw new TimeoutException($"Connection to {endpoint} timed out after {connectTimeout.TotalSeconds.ToString("0.###", CultureInfo.InvariantCulture)} seconds. Check the server and port, then try again.", ex);
        }
        catch (SocketException ex)
        {
            var message = ex.SocketErrorCode switch
            {
                SocketError.HostNotFound or SocketError.NoData or SocketError.TryAgain => $"Could not resolve server '{server}'. Check the server name or use its IP address.",
                SocketError.ConnectionRefused => $"The server refused the connection to {endpoint}. Check the port and that the SIP service is running.",
                SocketError.NetworkUnreachable or SocketError.HostUnreachable => $"Cannot reach {endpoint}. Check your network or VPN connection.",
                SocketError.TimedOut => $"Connection to {endpoint} timed out. Check the server and port, then try again.",
                _ => $"Could not connect to {endpoint}: {ex.Message}"
            };
            throw new IOException(message, ex);
        }
    }

    public async Task<SipTransaction> ExecuteAsync(SipCommand command, SessionFields fields, CommandFields inputs,
        string encoding, bool checkChecksum)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        fields = fields.Copy();
        var options = (inputs.Options ?? CommandOptions.Create(command)).Copy();
        inputs = inputs with { Options = options };
        Validate(command, fields, inputs);
        var usesDate = command is SipCommand.Checkout or SipCommand.Checkin or SipCommand.Renew or SipCommand.NoticeUpdate;
        var date = usesDate && options.UseDate ? DateTime.ParseExact(options.DateText, CommandOptions.DateFormat, CultureInfo.InvariantCulture) : (DateTime?)null;
        var textEncoding = Encoding.GetEncoding(encoding);
        await gate.WaitAsync(lifetime.Token);
        try
        {
            var connection = client;
            if (connection == null || !connection.IsConnected())
            {
                throw new InvalidOperationException("Connect before executing a command.");
            }

            connection.AO = fields.Ao;
            connection.AP = fields.Ap;
            connection.TextEncoding = textEncoding;
            connection.ValidateResponseChecksum = checkChecksum;
            var transaction = await RunAsync(connection, () => command switch
            {
                SipCommand.Checkout => connection.ItemCheckOut(fields.PatronBarcode, fields.PatronPin, fields.ItemBarcode, date,
                    Flag(options.RenewalAllowed), options.ItemProperties, options.FeeAcknowledged, Flag(options.NoBlock), options.Cancel, options.TerminalPassword),
                SipCommand.Checkin => connection.ItemCheckIn(fields.ItemBarcode, date, options.ReturnLocation, options.ItemProperties,
                    Flag(options.NoBlock), options.Cancel, options.TerminalPassword),
                SipCommand.Renew => connection.Renew(fields.PatronBarcode, fields.PatronPin, date, options.IncludeItemBarcode ? fields.ItemBarcode : "",
                    options.TitleIdentifier, options.ItemProperties, Flag(options.ThirdPartyAllowed), Flag(options.NoBlock), options.FeeAcknowledged, options.TerminalPassword),
                SipCommand.RenewAll => connection.RenewAll(fields.PatronBarcode, fields.PatronPin, options.FeeAcknowledged, options.TerminalPassword),
                SipCommand.ItemInformation => connection.ItemInformation(fields.ItemBarcode, options.TerminalPassword),
                SipCommand.PatronInformation => connection.PatronInformation(fields.PatronBarcode, fields.PatronPin, inputs.Summary,
                    fields.Bp.Trim(), fields.Bq.Trim(), options.TerminalPassword, options.LanguageCode.Trim()),
                SipCommand.PatronStatus => connection.PatronStatus(fields.PatronBarcode, fields.PatronPin, options.TerminalPassword, options.LanguageCode.Trim()),
                SipCommand.EndSession => connection.EndSession(fields.PatronBarcode, fields.PatronPin, options.TerminalPassword),
                SipCommand.FeePaid => connection.FeePaid(int.Parse(inputs.FeeType), int.Parse(inputs.PaymentType),
                    ParseAmount(inputs.Amount), fields.PatronBarcode, fields.PatronPin, options.Currency.Trim().ToUpperInvariant(),
                    inputs.FeeIdentifier, inputs.TransactionId, options.TerminalPassword),
                SipCommand.NoticeUpdate => connection.NotificationUpdate(inputs.NoticeStatus, date ?? DateTime.Now,
                    inputs.NoticeMedium, inputs.NoticeType, fields.PatronBarcode, fields.ItemBarcode, inputs.NoticeComment, options.TerminalPassword),
                SipCommand.Login => connection.Login(fields.Username, fields.Password, fields.LocationCode, fields.VendorProfile, options.UidAlgorithm, options.PwdAlgorithm),
                SipCommand.AcsStatus => connection.AcsStatus(options.AcsStatus, int.Parse(options.PrintWidth.Trim(), CultureInfo.InvariantCulture), options.ProtocolVersion.Trim()),
                _ => throw new ArgumentOutOfRangeException(nameof(command))
            });
            if (transaction.ConnectionReset)
            {
                Disconnect();
            }

            return transaction;
        }
        catch
        {
            if (client?.IsConnected() != true)
            {
                Disconnect();
            }

            throw;
        }
        finally { gate.Release(); }
    }

    private async Task<T> RunAsync<T>(sip2 connection, Func<T> operation)
    {
        using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(lifetime.Token);
        cancellation.CancelAfter(timeout);
        using var registration = cancellation.Token.Register(connection.Dispose);
        try
        {
            return await Task.Run(operation, cancellation.Token);
        }
        catch (Exception ex) when (cancellation.IsCancellationRequested)
        {
            throw new TimeoutException("The connection timed out or was closed. Reconnect to try again.", ex);
        }
    }

    public static void Validate(SipCommand command, SessionFields fields, CommandFields inputs)
    {
        var options = inputs.Options ?? CommandOptions.Create(command);
        if (command is SipCommand.Checkout or SipCommand.Checkin or SipCommand.Renew or SipCommand.NoticeUpdate
            && options.UseDate && !DateTime.TryParseExact(options.DateText, CommandOptions.DateFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out _))
        {
            throw new CommandValidationException(nameof(CommandOptions.DateText), "Enter a valid date/time (yyyy-MM-dd HH:mm:ss), or uncheck the date override.");
        }
        if (command is SipCommand.PatronInformation or SipCommand.PatronStatus
            && (options.LanguageCode.Trim().Length != 3 || !options.LanguageCode.Trim().All(char.IsAsciiDigit)))
        {
            throw new CommandValidationException(nameof(CommandOptions.LanguageCode), "Language must be a three-digit SIP code, such as 000.");
        }
        if (command == SipCommand.PatronInformation)
        {
            var start = Range(fields.Bp, nameof(SessionFields.Bp));
            var end = Range(fields.Bq, nameof(SessionFields.Bq));
            if (start.HasValue && end.HasValue && start > end)
            {
                throw new CommandValidationException(nameof(SessionFields.Bq), "End item must be greater than or equal to start item.");
            }
        }
        if (options.FeeAcknowledged is not ("" or "N" or "Y") || options.Cancel is not ("" or "N" or "Y"))
        {
            throw new ArgumentException("Optional flags must be omitted, N or Y.");
        }
        if (command == SipCommand.FeePaid)
        {
            if (!int.TryParse(inputs.FeeType.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var fee) || fee is < 0 or > 99)
            {
                throw new CommandValidationException(nameof(CommandFields.FeeType), "Fee type must be a number between 0 and 99.");
            }

            if (!int.TryParse(inputs.PaymentType.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var payment) || payment is < 0 or > 99)
            {
                throw new CommandValidationException(nameof(CommandFields.PaymentType), "Payment type must be a number between 0 and 99.");
            }

            _ = ParseAmount(inputs.Amount);
            var currency = options.Currency.Trim().ToUpperInvariant();
            if (currency.Length != 3 || !currency.All(char.IsAsciiLetterUpper))
            {
                throw new CommandValidationException(nameof(CommandOptions.Currency), "Currency must be a three-letter code, such as USD.");
            }
        }
        if (command == SipCommand.PatronInformation && !SummaryChoice.All.Any(s => s.Key == inputs.Summary))
        {
            throw new ArgumentException("Choose a patron summary.");
        }
        if (command == SipCommand.Login)
        {
            foreach (var (name, value) in new[] { (nameof(options.UidAlgorithm), options.UidAlgorithm), (nameof(options.PwdAlgorithm), options.PwdAlgorithm) })
            {
                if (value.Length != 1 || value[0] is < '!' or > '~' || value[0] == '|')
                {
                    throw new CommandValidationException(name, "Algorithm identifiers must be one printable protocol character; use 0 for plain text.");
                }
            }
        }
        if (command == SipCommand.AcsStatus)
        {
            if (options.AcsStatus is not ("0" or "1" or "2")) { throw new CommandValidationException(nameof(options.AcsStatus), "Choose a self-check status."); }
            if (!int.TryParse(options.PrintWidth.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var width) || width is < 0 or > 999)
            {
                throw new CommandValidationException(nameof(options.PrintWidth), "Maximum print width must be between 0 and 999.");
            }
            var version = options.ProtocolVersion.Trim();
            if (version.Length != 4 || version[1] != '.' || !new[] { version[0], version[2], version[3] }.All(char.IsAsciiDigit))
            {
                throw new CommandValidationException(nameof(options.ProtocolVersion), "Protocol version must use x.xx format, such as 2.00.");
            }
        }
    }

    private static string Flag(bool value) => value ? "Y" : "N";

    private static int? Range(string value, string field)
    {
        if (string.IsNullOrWhiteSpace(value)) { return null; }
        if (int.TryParse(value.Trim(), NumberStyles.None, CultureInfo.InvariantCulture, out var number) && number > 0) { return number; }
        throw new CommandValidationException(field, "Item ranges must be positive numbers, or blank to omit the field.");
    }

    private static double ParseAmount(string value)
    {
        if (!double.TryParse(value.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            || !double.IsFinite(amount))
        {
            throw new CommandValidationException(nameof(CommandFields.Amount), "Payment amount must be a finite number.");
        }

        return amount;
    }

    public void Disconnect()
    {
        Interlocked.Exchange(ref client, null)?.Dispose();
        Endpoint = "";
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        lifetime.Cancel();
        Disconnect();
        lifetime.Dispose();
    }
}
