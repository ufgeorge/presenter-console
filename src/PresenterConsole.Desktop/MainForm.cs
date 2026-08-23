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

    private readonly ComboBox adapterChoice = new()
    {
        DropDownStyle = ComboBoxStyle.DropDownList,
        Width = 280
    };

    private readonly Button applyAdapter = new()
    {
        Text = "套用簡報軟體",
        AutoSize = true
    };

    private readonly Button configureOpenDesign = new()
    {
        Text = "設定 OpenDesign 資料夾",
        AutoSize = true
    };

    private IPresentationAdapter presentation;
    private readonly SyncEngine sync = new();
    private readonly OpenDesignSettings openDesignSettings;
    private IReadOnlyList<OpenDesignProject> openDesignProjects = [];
    private readonly bool openDesignRunning;
    private AgentServer? server;

    public MainForm()
    {
        openDesignRunning = OpenDesignProcessDetector.IsRunning();
        presentation = CreateInitialAdapter(out openDesignSettings);
        Text = "Presenter Console Agent";
        Width = 500;
        Height = 560;

        var panel = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.TopDown,
            Padding = new Padding(12)
        };
        panel.Controls.Add(new Label
        {
            Text = "簡報軟體",
            AutoSize = true
        });
        panel.Controls.Add(adapterChoice);
        panel.Controls.Add(applyAdapter);
        panel.Controls.Add(configureOpenDesign);
        panel.Controls.Add(status);
        panel.Controls.Add(slide);
        panel.Controls.Add(qr);
        Controls.Add(panel);

        PopulateAdapterChoices();
        SelectCurrentAdapter();
        SubscribeToPresentation(presentation);
        applyAdapter.Click += OnApplyAdapter;
        configureOpenDesign.Click += OnConfigureOpenDesign;
        Load += OnLoadAsync;
        FormClosed += OnFormClosed;
    }

    private async void OnLoadAsync(object? sender, EventArgs e)
    {
        server = new AgentServer(presentation, sync);
        await server.StartAsync(CancellationToken.None);
        RefreshState();
    }

    private void PopulateAdapterChoices()
    {
        adapterChoice.Items.Clear();
        adapterChoice.Items.Add("PowerPoint（COM 可用）");
        if (openDesignRunning || openDesignProjects.Count > 0)
        {
            adapterChoice.Items.Add("OpenDesign");
        }

        configureOpenDesign.Enabled = adapterChoice.Items.Contains("OpenDesign");
    }

    private void SelectCurrentAdapter()
    {
        var saved = string.Equals(
            openDesignSettings.LastAdapter,
            "OpenDesign",
            StringComparison.OrdinalIgnoreCase)
            ? "OpenDesign"
            : "PowerPoint（COM 可用）";
        adapterChoice.SelectedItem = adapterChoice.Items.Contains(saved)
            ? saved
            : adapterChoice.Items[0];
    }

    private void OnApplyAdapter(object? sender, EventArgs e)
    {
        if (adapterChoice.SelectedItem is not string choice)
        {
            return;
        }

        var next = CreateAdapter(choice);
        if (next is null)
        {
            RefreshState();
            return;
        }

        var previous = presentation;
        UnsubscribeFromPresentation(previous);
        presentation = next;
        SubscribeToPresentation(presentation);
        server?.ReplacePresentation(presentation);
        previous.Dispose();
        openDesignSettings.LastAdapter = choice == "OpenDesign" ? "OpenDesign" : "PowerPoint";
        openDesignSettings.Save();
        RefreshState();
    }

    private void OnConfigureOpenDesign(object? sender, EventArgs e)
    {
        using var dialog = new FolderBrowserDialog
        {
            Description = "選擇 OpenDesign deck 所在的資料夾"
        };
        if (dialog.ShowDialog(this) != DialogResult.OK)
        {
            return;
        }

        if (!openDesignSettings.ProjectRoots.Contains(
                dialog.SelectedPath,
                StringComparer.OrdinalIgnoreCase))
        {
            openDesignSettings.ProjectRoots.Add(dialog.SelectedPath);
        }

        openDesignSettings.Save();
        openDesignProjects = ScanOpenDesignProjects();
        PopulateAdapterChoices();
        adapterChoice.SelectedItem = "OpenDesign";
        RefreshState();
    }

    private void RefreshState()
    {
        slide.Text = Localization.SlideNumber(
            presentation.CurrentShowPosition,
            presentation.SlideCount);
        status.Text = GetStatusText();

        using var qrData = new QRCodeGenerator().CreateQrCode(
            server?.PairingUrl ?? string.Empty,
            QRCodeGenerator.ECCLevel.Q);
        var png = new PngByteQRCode(qrData).GetGraphic(8);
        using var stream = new MemoryStream(png);
        using var image = Image.FromStream(stream);
        qr.Image = new Bitmap(image);
    }

    private string GetStatusText()
    {
        if (presentation is UnavailablePresentationAdapter && openDesignRunning
            && openDesignProjects.Count == 0)
        {
            return "已偵測到 OpenDesign，但尚未設定 deck 資料夾";
        }

        return presentation is UnavailablePresentationAdapter
            ? Localization.StartedUnavailable
            : Localization.Started;
    }

    private IPresentationAdapter CreateInitialAdapter(out OpenDesignSettings settings)
    {
        settings = OpenDesignSettings.Load();
        openDesignProjects = ScanOpenDesignProjects(settings);
        var useOpenDesign = string.Equals(
            settings.LastAdapter,
            "OpenDesign",
            StringComparison.OrdinalIgnoreCase)
            && (openDesignRunning || openDesignProjects.Count > 0);
        return CreateAdapter(useOpenDesign ? "OpenDesign" : "PowerPoint")
            ?? new UnavailablePresentationAdapter();
    }

    private IPresentationAdapter? CreateAdapter(string choice)
    {
        if (choice == "OpenDesign")
        {
            if (openDesignProjects.Count == 0)
            {
                return null;
            }

            return new OpenDesignAdapter(openDesignProjects[0]);
        }

        try
        {
            return new PowerPointAdapter(SynchronizationContext.Current);
        }
        catch (FileNotFoundException exception)
        {
            LogAdapterFallback(exception);
        }
        catch (InvalidOperationException exception)
        {
            LogAdapterFallback(exception);
        }

        return new UnavailablePresentationAdapter();
    }

    private IReadOnlyList<OpenDesignProject> ScanOpenDesignProjects()
    {
        return ScanOpenDesignProjects(openDesignSettings);
    }

    private static IReadOnlyList<OpenDesignProject> ScanOpenDesignProjects(
        OpenDesignSettings settings)
    {
        var scanner = new OpenDesignProjectScanner();
        return settings.ProjectRoots
            .Select(root => Path.IsPathRooted(root)
                ? root
                : Path.Combine(AppContext.BaseDirectory, root))
            .Where(Directory.Exists)
            .SelectMany(scanner.Scan)
            .ToArray();
    }

    private void SubscribeToPresentation(IPresentationAdapter adapter)
    {
        adapter.StateChanged += OnPresentationStateChanged;
        adapter.ErrorOccurred += OnPresentationError;
    }

    private void UnsubscribeFromPresentation(IPresentationAdapter adapter)
    {
        adapter.StateChanged -= OnPresentationStateChanged;
        adapter.ErrorOccurred -= OnPresentationError;
    }

    private void OnPresentationStateChanged(object? sender, EventArgs e)
    {
        if (!IsDisposed)
        {
            BeginInvoke(RefreshState);
        }
    }

    private void OnPresentationError(object? sender, string error)
    {
        if (!IsDisposed)
        {
            BeginInvoke(() => status.Text = error);
        }
    }

    private void OnFormClosed(object? sender, FormClosedEventArgs e)
    {
        server?.DisposeAsync();
        sync.Dispose();
        UnsubscribeFromPresentation(presentation);
        presentation.Dispose();
    }

    private static void LogAdapterFallback(Exception exception)
    {
        try
        {
            var logPath = Path.Combine(AppContext.BaseDirectory, "presenter-console.log");
            var message = $"[{DateTime.Now:O}] PowerPoint adapter fallback: "
                + $"{exception.GetType().FullName}: {exception.Message}{Environment.NewLine}";
            File.AppendAllText(logPath, message);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }
}
