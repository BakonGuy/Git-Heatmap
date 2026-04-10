using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using GitHeatmap.Core.Export;
using GitHeatmap.Core.Models;
using GitHeatmap.Core.Services;
using Git_Heatmap.Services;
using Microsoft.Win32;

namespace Git_Heatmap;

public partial class MainWindow : Window
{
	private static readonly SolidColorBrush[] Palette =
	[
		new(Color.FromRgb(0x1A, 0x1A, 0x1A)),
		new(Color.FromRgb(0x4A, 0x2A, 0x0A)),
		new(Color.FromRgb(0x7A, 0x3E, 0x0B)),
		new(Color.FromRgb(0xB4, 0x53, 0x09)),
		new(Color.FromRgb(0xF9, 0x73, 0x16))
	];

	private readonly HeatmapService _service = new();
	private string _configPath = string.Empty;
	private int _lookbackDays = 365;
	private ConfigWindow? _configWindow;
    private readonly bool _hasSavedWindowState;

	public MainWindow()
	{
		InitializeComponent();
        _hasSavedWindowState = WindowSizeStore.HasSavedState(nameof(MainWindow));
		WindowSizeStore.Apply(this, nameof(MainWindow));
		Loaded += async (_, _) => await RefreshAsync();
		Closing += (_, _) => WindowSizeStore.Save(this, nameof(MainWindow));
        SizeChanged += (_, _) => UpdateMinimumWindowSize();
	}

	private async Task EnsureConfigPathAsync()
	{
		if( !string.IsNullOrWhiteSpace(_configPath) )
		{
			return;
		}

		_configPath = ConfigPathResolver.ResolveFrom(AppContext.BaseDirectory);
		await ConfigLoader.WriteTemplateIfMissingAsync(_configPath);
	}

	private async Task RefreshAsync()
	{
		try
		{
            RenderHeatmap(CreateEmptyResult());
            SummaryText.Text = $"0 commits in the last {_lookbackDays} days";
            StatusText.Text = "Loading commit data...";
            UpdateMinimumWindowSize();
            if( !_hasSavedWindowState )
            {
                Width = MinWidth;
                Height = MinHeight;
            }

			await EnsureConfigPathAsync();
			var config = await ConfigLoader.LoadAsync(_configPath);
			_lookbackDays = config.LookbackDays;

			var result = await _service.BuildAsync(config);
			RenderHeatmap(result);
            UpdateMinimumWindowSize();
            if( !_hasSavedWindowState )
            {
                Width = MinWidth;
                Height = MinHeight;
            }

			SummaryText.Text = $"{result.TotalContributions} commits in the last {_lookbackDays} days";
			StatusText.Text = result.Warnings.Count > 0
				? string.Join(Environment.NewLine, result.Warnings)
				: string.Empty;
		}
		catch( Exception ex )
		{
			StatusText.Text = ex.Message;
			SummaryText.Text = "Failed to load heatmap";
		}
	}

    private static HeatmapResult CreateEmptyResult()
    {
        return new HeatmapResult
        {
            DailyCounts = new Dictionary<DateOnly, int>(),
            DailyRepoNames = new Dictionary<DateOnly, HashSet<string>>(),
            Warnings = []
        };
    }

    private void UpdateMinimumWindowSize()
    {
        RootDock.UpdateLayout();
        HeatmapPanel.UpdateLayout();
        StatusText.UpdateLayout();
        ActionBar.UpdateLayout();

        const double rootMargin = 28; // Root DockPanel has Margin=14
        var chromeWidth = Math.Max(0, ActualWidth - RootDock.ActualWidth);
        var chromeHeight = Math.Max(0, ActualHeight - RootDock.ActualHeight);

        var actionBarHeight = ActionBar.ActualHeight + ActionBar.Margin.Top + ActionBar.Margin.Bottom;
        var statusHeight = StatusText.ActualHeight + StatusText.Margin.Top + StatusText.Margin.Bottom;

        var minContentWidth = HeatmapPanel.ActualWidth + rootMargin;
        var minContentHeight = HeatmapPanel.ActualHeight + actionBarHeight + statusHeight + rootMargin;

        MinWidth = Math.Ceiling(minContentWidth + chromeWidth);
        MinHeight = Math.Ceiling(minContentHeight + chromeHeight);
    }

	private void RenderHeatmap(HeatmapResult result)
	{
		HeatmapGrid.Children.Clear();
        HeatmapGrid.RowDefinitions.Clear();
        HeatmapGrid.ColumnDefinitions.Clear();

        for( var day = 0; day < 7; day++ )
        {
            HeatmapGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        }

        for( var week = 0; week < 53; week++ )
        {
            HeatmapGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        }

		var end = DateOnly.FromDateTime(DateTime.Today);
		var start = end.AddDays(-(_lookbackDays - 1));
		var startOnSunday = start.AddDays(-(int)start.DayOfWeek);
		var max = result.DailyCounts.Values.DefaultIfEmpty(0).Max();

		for( var week = 0; week < 53; week++ )
		{
			for( var day = 0; day < 7; day++ )
			{
				var current = startOnSunday.AddDays((week * 7) + day);
                if( current > end )
                {
                    continue;
                }

				var count = result.DailyCounts.TryGetValue(current, out var c) && current >= start && current <= end ? c : 0;
				var level = HtmlHeatmapExporter.ToLevel(count, max);
                var repoLines = result.DailyRepoNames.TryGetValue(current, out var repos)
                    ? string.Join(Environment.NewLine, repos.OrderBy(x => x, StringComparer.OrdinalIgnoreCase).Select(x => $"- {x}"))
                    : "- None";
                var commitLabel = count == 1 ? "Commit" : "Commits";
				var tooltipText = $"{current:MMMM d, yyyy} - {count} {commitLabel}{Environment.NewLine}{repoLines}";
				var cell = new Border
				{
					Width = 12,
					Height = 12,
					Margin = new Thickness(1.5),
					CornerRadius = new CornerRadius(2),
					Background = Palette[level],
					IsHitTestVisible = true
				};
				var toolTip = new ToolTip { Content = tooltipText };
				ToolTipService.SetInitialShowDelay(cell, 0);
				ToolTipService.SetShowDuration(cell, 60000);
				ToolTipService.SetToolTip(cell, toolTip);
                Grid.SetColumn(cell, week);
                Grid.SetRow(cell, day);

				HeatmapGrid.Children.Add(cell);
			}
		}
	}

	private async void RefreshButton_OnClick(object sender, RoutedEventArgs e)
	{
		await RefreshAsync();
	}

	private async void ConfigButton_OnClick(object sender, RoutedEventArgs e)
	{
		await EnsureConfigPathAsync();

		if( _configWindow is { IsLoaded: true } )
		{
			_configWindow.Activate();
			return;
		}

		_configWindow = new ConfigWindow(_configPath, async () => await RefreshAsync())
		{
			Owner = this
		};
		_configWindow.Closed += (_, _) => _configWindow = null;
		_configWindow.Show();
	}

	private async void ExportHtmlButton_OnClick(object sender, RoutedEventArgs e)
	{
		await EnsureConfigPathAsync();

		var dialog = new SaveFileDialog
		{
			Filter = "HTML file (*.html)|*.html",
			FileName = "heatmap.html"
		};

		if( dialog.ShowDialog() != true )
		{
			return;
		}

		var config = await ConfigLoader.LoadAsync(_configPath);
		var result = await _service.BuildAsync(config);
		var html = HtmlHeatmapExporter.Export(result, config.LookbackDays);
		await File.WriteAllTextAsync(dialog.FileName, html);
		StatusText.Text = $"HTML exported to {dialog.FileName}";
	}

	private void ExportPngButton_OnClick(object sender, RoutedEventArgs e)
	{
		var dialog = new SaveFileDialog
		{
			Filter = "PNG image (*.png)|*.png",
			FileName = "heatmap.png"
		};

		if( dialog.ShowDialog() != true )
		{
			return;
		}

        HeatmapPanel.UpdateLayout();
        HeatmapPanel.Measure(new Size(double.PositiveInfinity, double.PositiveInfinity));
        HeatmapPanel.Arrange(new Rect(HeatmapPanel.DesiredSize));

        var width = Math.Max(1, (int)Math.Ceiling(HeatmapPanel.ActualWidth));
        var height = Math.Max(1, (int)Math.Ceiling(HeatmapPanel.ActualHeight));

        var render = new RenderTargetBitmap(width, height, 96, 96, PixelFormats.Pbgra32);
        render.Render(HeatmapPanel);

		var encoder = new PngBitmapEncoder();
		encoder.Frames.Add(BitmapFrame.Create(render));

		using var stream = File.Create(dialog.FileName);
		encoder.Save(stream);

		StatusText.Text = $"PNG exported to {dialog.FileName}";
	}
}
