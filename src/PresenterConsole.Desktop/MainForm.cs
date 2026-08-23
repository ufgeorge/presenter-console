using System.Drawing;
using QRCoder;
using PresenterConsole.Sync;

namespace PresenterConsole.Desktop;

public sealed class MainForm : Form
{
    private readonly Label status = new()
    {
        Text = Localization.NotConnected,
        AutoSize = true
    };

    private readonly Label slide = new()
    {
        Text = Localization.SlideNumber(0, 0).Replace("0/0", "—", StringComparison.Ordinal),
        AutoSize = true
    };

    private readonly PictureBox qr = new()
    {
        Size = new Size(260, 260),
        SizeMode = PictureBoxSizeMode.Zoom
    };

    private readonly IPresentationAdapter presentation;
    private readonly SyncEngine sync = new();
    private readonly OpenDesignSettings openDesignSettings;
    private AgentServer? server;

    public MainForm()
    {
        presentation = CreatePresentationAdapter(out openDesignSettings);
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
        slide.Text = Localization.SlideNumber(
            presentation.CurrentShowPosition,
            presentation.SlideCount);
        status.Text = presentation is UnavailablePresentationAdapter
            ? Localization.StartedUnavailable
            : Localization.Started;

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

    private static IPresentationAdapter CreatePresentationAdapter(
        out OpenDesignSettings settings)
    {
        settings = OpenDesignSettings.Load();
        var projects = ScanOpenDesignProjects(settings);
        if (projects.Count > 0 && ShouldUseOpenDesign(settings))
        {
            settings.LastAdapter = "OpenDesign";
            settings.Save();
            return new OpenDesignAdapter(projects[0]);
        }

        settings.LastAdapter = "PowerPoint";
        settings.Save();
        try
        {
            return new PowerPointAdapter(SynchronizationContext.Current);
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

    private static IReadOnlyList<OpenDesignProject> ScanOpenDesignProjects(
        OpenDesignSettings settings)
    {
        if (settings.ProjectRoots.Count == 0)
        {
            return [];
        }

        var scanner = new OpenDesignProjectScanner();
        var projects = settings.ProjectRoots
            .Select(root => Path.IsPathRooted(root)
                ? root
                : Path.Combine(AppContext.BaseDirectory, root))
            .Where(Directory.Exists)
            .SelectMany(scanner.Scan)
            .ToArray();
        return projects;
    }

    private static bool ShouldUseOpenDesign(OpenDesignSettings settings)
    {
        if (string.Equals(settings.LastAdapter, "OpenDesign", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        var choice = MessageBox.Show(
            "偵測到 OpenDesign deck。按「是」使用 OpenDesign，按「否」使用 PowerPoint。",
            "選擇簡報模式",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question,
            MessageBoxDefaultButton.Button1);
        return choice == DialogResult.Yes;
    }

    private static void LogAdapterFallback(Exception exception)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "presenter-console.log");
            var message = $"[{DateTime.Now:O}] PowerPoint adapter fallback: "
                + $"{exception.GetType().FullName}: {exception.Message}"
                + Environment.NewLine;
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
