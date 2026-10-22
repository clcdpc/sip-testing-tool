using System.Globalization;
using System.Text;
using Clc.Sip.Desktop.Models;

namespace Clc.Sip.Desktop.Services;

public sealed class SipSession : IDisposable
{
    private readonly SemaphoreSlim gate = new(1, 1);
    private readonly CancellationTokenSource lifetime = new();
    private readonly TimeSpan timeout;
    private sip2? client;
    private bool disposed;

    public bool IsConnected => client?.IsConnected() == true;
    public string Endpoint { get; private set; } = "";

    public SipSession(TimeSpan? timeout = null) => this.timeout = timeout ?? TimeSpan.FromSeconds(30);

    public async Task ConnectAsync(SessionFields fields, string encoding, bool checkChecksum)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (string.IsNullOrWhiteSpace(fields.Server))
        {
            throw new ArgumentException("Server is required.");
        }

        if (!int.TryParse(fields.Port, out var port) || port is < 1 or > 65535)
        {
            throw new ArgumentException("Port must be a number between 1 and 65535.");
        }

        var textEncoding = Encoding.GetEncoding(encoding);
        await gate.WaitAsync(lifetime.Token);
        try
        {
            Disconnect();
            var connection = new sip2(fields.Server.Trim(), port, fields.Ao, fields.Ap)
            {
                TextEncoding = textEncoding,
                ValidateResponseChecksum = checkChecksum
            };
            client = connection;
            try
            {
                await RunAsync(connection, () =>
                {
                    connection.Connect();
                    return true;
                });
                Endpoint = $"{fields.Server.Trim()}:{port}";
            }
            catch
            {
                Disconnect();
                throw;
            }
        }
        finally { gate.Release(); }
    }

    public async Task<SipTransaction> ExecuteAsync(SipCommand command, SessionFields fields, CommandFields inputs,
        string encoding, bool checkChecksum)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        Validate(command, fields, inputs);
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
                SipCommand.Checkout => connection.ItemCheckOut(fields.PatronBarcode, fields.PatronPin, fields.ItemBarcode),
                SipCommand.Checkin => connection.ItemCheckIn(fields.ItemBarcode),
                SipCommand.Renew => connection.Renew(fields.PatronBarcode, fields.PatronPin, DateTime.Now.AddDays(14), fields.ItemBarcode),
                SipCommand.RenewAll => connection.RenewAll(fields.PatronBarcode, fields.PatronPin),
                SipCommand.ItemInformation => connection.ItemInformation(fields.ItemBarcode),
                SipCommand.PatronInformation => inputs.Summary == "none"
                    ? connection.PatronInformation(fields.PatronBarcode, fields.PatronPin)
                    : connection.PatronInformation(fields.PatronBarcode, fields.PatronPin, inputs.Summary, fields.Bp, fields.Bq),
                SipCommand.PatronStatus => connection.PatronStatus(fields.PatronBarcode, fields.PatronPin),
                SipCommand.EndSession => connection.EndSession(fields.PatronBarcode, fields.PatronPin),
                SipCommand.FeePaid => connection.FeePaid(int.Parse(inputs.FeeType), int.Parse(inputs.PaymentType),
                    ParseAmount(inputs.Amount), fields.PatronBarcode, fields.PatronPin, "USD", inputs.FeeIdentifier, inputs.TransactionId),
                SipCommand.NoticeUpdate => connection.NotificationUpdate(inputs.NoticeStatus, inputs.DeliveryDate,
                    inputs.NoticeMedium, inputs.NoticeType, fields.PatronBarcode, fields.ItemBarcode, inputs.NoticeComment),
                SipCommand.Login => connection.Login(fields.Username, fields.Password, fields.LocationCode, fields.VendorProfile),
                SipCommand.AcsStatus => connection.AcsStatus(),
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
        if (command == SipCommand.FeePaid)
        {
            if (!int.TryParse(inputs.FeeType, out var fee) || fee is < 0 or > 99)
            {
                throw new ArgumentException("Fee type must be a number between 0 and 99.");
            }

            if (!int.TryParse(inputs.PaymentType, out var payment) || payment is < 0 or > 99)
            {
                throw new ArgumentException("Payment type must be a number between 0 and 99.");
            }

            _ = ParseAmount(inputs.Amount);
        }
        if (command == SipCommand.PatronInformation && !SummaryChoice.All.Any(s => s.Key == inputs.Summary))
        {
            throw new ArgumentException("Choose a patron summary.");
        }
    }

    private static double ParseAmount(string value)
    {
        if (!double.TryParse(value.Replace("$", ""), NumberStyles.Number, CultureInfo.CurrentCulture, out var amount)
            || !double.IsFinite(amount))
        {
            throw new ArgumentException("Payment amount must be a number.");
        }

        return amount;
    }

    public void Disconnect() => Interlocked.Exchange(ref client, null)?.Dispose();

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
