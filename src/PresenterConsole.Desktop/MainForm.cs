using System.Drawing;
using QRCoder;
using PresenterConsole.Sync;

namespace PresenterConsole.Desktop;

public sealed class MainForm : Form
{
    private readonly Label status = new()
    {
        Text = "未連線",
        AutoSize = true
    };

    private readonly Label slide = new()
    {
        Text = "目前頁碼：—",
        AutoSize = true
    };

    private readonly PictureBox qr = new()
    {
        Size = new Size(260, 260),
        SizeMode = PictureBoxSizeMode.Zoom
    };

    private readonly IPresentationAdapter presentation = CreatePresentationAdapter();
    private readonly SyncEngine sync = new();
    private AgentServer? server;

    public MainForm()
    {
        Text = "Presenter Console Agent";
        Width = 500;
        Height = 500;

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown
        };
        panel.Controls.Add(status);
        panel.Controls.Add(slide);
        panel.Controls.Add(qr);
        Controls.Add(panel);

        presentation.StateChanged += (_, _) => BeginInvoke(RefreshState);
        Load += OnLoadAsync;
        FormClosed += OnFormClosed;
    }

    private async void OnLoadAsync(object? sender, EventArgs e)
    {
        server = new AgentServer(presentation, sync);
        await server.StartAsync(CancellationToken.None);
        RefreshState();
    }

    private void RefreshState()
    {
        slide.Text = $"目前頁碼：{presentation.CurrentShowPosition}/{presentation.SlideCount}";
        status.Text = presentation is UnavailablePresentationAdapter
            ? "Agent 已啟動 · 尚未連線到 PowerPoint · QR 有效 2 小時"
            : "Agent 已啟動 · LAN WebSocket · QR 有效 2 小時";

        using var qrData = new QRCodeGenerator().CreateQrCode(
            server?.PairingUrl ?? string.Empty,
            QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(qrData).GetGraphic(8);
        using var stream = new MemoryStream(png);
        using var image = Image.FromStream(stream);
        qr.Image = new Bitmap(image);
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        server?.DisposeAsync();
        sync.Dispose();
        presentation.Dispose();
    }

    private static IPresentationAdapter CreatePresentationAdapter()
    {
        try
        {
            return new PowerPointAdapter();
        }
        catch (FileNotFoundException exception)
        {
            LogAdapterFallback(exception);
            return new UnavailablePresentationAdapter();
        }
        catch (InvalidOperationException exception)
        {
            LogAdapterFallback(exception);
            return new UnavailablePresentationAdapter();
        }
    }

    private static void LogAdapterFallback(Exception exception)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "presenter-console.log");
            var message = $"[{DateTime.Now:O}] PowerPoint adapter fallback: {exception.GetType().FullName}: {exception.Message}{Environment.NewLine}";
            File.AppendAllText(logPath, message);
        }
        catch (IOException)
        {
            // Logging must not prevent the fallback adapter from starting.
        }
        catch (UnauthorizedAccessException)
        {
            // Logging must not prevent the fallback adapter from starting.
        }
    }
}
