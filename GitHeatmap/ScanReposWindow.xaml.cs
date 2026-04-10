using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using Git_Heatmap.Services;
using Microsoft.Win32;

namespace Git_Heatmap;

public partial class ScanReposWindow : Window
{
    private readonly ObservableCollection<RepoScanItem> _results = [];
    private readonly Action<IReadOnlyList<string>> _onAddToConfig;

    public ScanReposWindow(Action<IReadOnlyList<string>> onAddToConfig)
    {
        InitializeComponent();
        WindowSizeStore.Apply(this, nameof(ScanReposWindow));
        Closing += (_, _) => WindowSizeStore.Save(this, nameof(ScanReposWindow));

        _onAddToConfig = onAddToConfig;
        ResultsItemsControl.ItemsSource = _results;
    }

    private void PathTextBox_OnTextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
    {
        var isValidDirectory = Directory.Exists(PathTextBox.Text.Trim());
        ScanButton.IsEnabled = isValidDirectory;
    }

    private void BrowseButton_OnClick(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Pick a directory to scan for Git repositories."
        };

        if (dialog.ShowDialog() != true)
        {
            return;
        }

        PathTextBox.Text = dialog.FolderName;
    }

    private async void ScanButton_OnClick(object sender, RoutedEventArgs e)
    {
        var root = PathTextBox.Text.Trim();
        if (!Directory.Exists(root))
        {
            StatusText.Text = "Pick a valid directory first.";
            return;
        }

        ScanButton.IsEnabled = false;
        _results.Clear();
        StatusText.Text = "Scanning...";

        List<string> found;
        try
        {
            var includeSubmodules = IncludeSubmodulesCheckBox.IsChecked == true;
            found = await Task.Run(() => ScanForGitRepos(root, includeSubmodules));
        }
        finally
        {
            ScanButton.IsEnabled = true;
        }

        foreach (var path in found)
        {
            _results.Add(new RepoScanItem(path));
        }

        StatusText.Text = found.Count == 0
            ? "No repositories found."
            : $"Found {found.Count} repositories.";
        UpdateAddButtonState();
    }

    private static List<string> ScanForGitRepos(string root, bool includeSubmodules)
    {
        var stack = new Stack<string>();
        var results = new List<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            var current = stack.Pop();
            try
            {
                var gitPath = Path.Combine(current, ".git");
                var isGitRepo = Directory.Exists(gitPath) || File.Exists(gitPath);
                if (isGitRepo)
                {
                    results.Add(current);
                    if (!includeSubmodules)
                    {
                        continue;
                    }
                }

                foreach (var child in Directory.EnumerateDirectories(current))
                {
                    stack.Push(child);
                }
            }
            catch
            {
                // Skip paths that cannot be accessed.
            }
        }

        return results
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private void RepoCheckBox_OnCheckedChanged(object sender, RoutedEventArgs e)
    {
        UpdateAddButtonState();
    }

    private void UpdateAddButtonState()
    {
        AddToConfigButton.IsEnabled = _results.Any(x => x.IsSelected);
    }

    private void AddToConfigButton_OnClick(object sender, RoutedEventArgs e)
    {
        var selected = _results.Where(x => x.IsSelected).Select(x => x.Path).ToList();
        if (selected.Count == 0)
        {
            return;
        }

        _onAddToConfig(selected);
        Close();
    }

    private sealed class RepoScanItem
    {
        public RepoScanItem(string path)
        {
            Path = path;
            DisplayText = path;
        }

        public string Path { get; }
        public string DisplayText { get; }
        public bool IsSelected { get; set; }
    }
}
