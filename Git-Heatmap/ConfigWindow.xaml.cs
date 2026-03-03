using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using GitHeatmap.Core.Models;
using GitHeatmap.Core.Services;

namespace Git_Heatmap;

public partial class ConfigWindow : Window
{
    private static readonly Brush DirtyBrush = new SolidColorBrush(Color.FromRgb(0xFF, 0xFF, 0xCC));
    private static readonly Brush CleanBrush = new SolidColorBrush(Color.FromRgb(0x16, 0x1B, 0x22));

	private readonly string _configPath;
	private readonly Func<Task> _onSaved;
	private readonly ObservableCollection<RepoConfig> _repositories = [];

	private HeatmapConfig _savedConfig = new();
	private bool _isLoading;

	public ConfigWindow(string configPath, Func<Task> onSaved)
	{
		InitializeComponent();
		_configPath = configPath;
		_onSaved = onSaved;

		RepoTypeComboBox.ItemsSource = Enum.GetValues<RepoType>();
		RepoListBox.ItemsSource = _repositories;

		Loaded += async (_, _) => await LoadConfigAsync();
	}

	private async Task LoadConfigAsync()
	{
		_isLoading = true;
		_savedConfig = await ConfigLoader.LoadAsync(_configPath);

		LookbackDaysTextBox.Text = _savedConfig.LookbackDays.ToString();
		AuthorMatchTextBox.Text = _savedConfig.AuthorMatch ?? string.Empty;

		_repositories.Clear();
		foreach( var repo in _savedConfig.Repositories )
		{
			_repositories.Add(CloneRepo(repo));
		}

		if( _repositories.Count > 0 )
		{
			RepoListBox.SelectedIndex = 0;
			LoadSelectedRepoFields();
		}
		else
		{
			ClearRepoFields();
		}

		_isLoading = false;
		UpdateDirtyIndicators();
		StatusText.Text = $"Config: {_configPath}";
	}

	private static RepoConfig CloneRepo(RepoConfig repo)
	{
		return new RepoConfig
		{
			Name = repo.Name,
			Type = repo.Type,
			Path = repo.Path,
			Owner = repo.Owner,
			Repo = repo.Repo
		};
	}

	private void GlobalField_OnChanged(object sender, TextChangedEventArgs e)
	{
		if( _isLoading )
		{
			return;
		}

		UpdateDirtyIndicators();
	}

	private void RepoFieldDraft_OnChanged(object sender, RoutedEventArgs e)
	{
		if( _isLoading )
		{
			return;
		}

		UpdateDirtyIndicators();
	}

	private void RepoFieldCommit_OnChanged(object sender, SelectionChangedEventArgs e)
	{
		if( _isLoading )
		{
			return;
		}

		ApplySelectedRepoFieldChanges();
		UpdateTypeVisibility();
		UpdateDirtyIndicators();
	}

	private void RepoField_OnLostFocus(object sender, RoutedEventArgs e)
	{
		if( _isLoading )
		{
			return;
		}

		ApplySelectedRepoFieldChanges();
		UpdateDirtyIndicators();
	}

	private void RepoField_OnKeyDown(object sender, KeyEventArgs e)
	{
		if( _isLoading || e.Key != Key.Enter )
		{
			return;
		}

		ApplySelectedRepoFieldChanges();
		UpdateDirtyIndicators();
	}

	private void RepoListBox_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
	{
		if( _isLoading )
		{
			return;
		}

		LoadSelectedRepoFields();
		UpdateDirtyIndicators();
	}

	private void AddLocalRepoButton_OnClick(object sender, RoutedEventArgs e)
	{
		var repo = new RepoConfig { Name = "New Local Repo", Type = RepoType.Local };
		_repositories.Add(repo);
		RepoListBox.SelectedItem = repo;
		UpdateDirtyIndicators();
	}

	private void AddGitHubRepoButton_OnClick(object sender, RoutedEventArgs e)
	{
		var repo = new RepoConfig { Name = "New GitHub Repo", Type = RepoType.GitHub };
		_repositories.Add(repo);
		RepoListBox.SelectedItem = repo;
		UpdateDirtyIndicators();
	}

	private void RemoveRepoButton_OnClick(object sender, RoutedEventArgs e)
	{
		if( RepoListBox.SelectedItem is not RepoConfig selected )
		{
			return;
		}

		var index = RepoListBox.SelectedIndex;
		_repositories.Remove(selected);

		if( _repositories.Count == 0 )
		{
			ClearRepoFields();
		}
		else
		{
			RepoListBox.SelectedIndex = Math.Clamp(index, 0, _repositories.Count - 1);
		}

		UpdateDirtyIndicators();
	}

	private async void SaveButton_OnClick(object sender, RoutedEventArgs e)
	{
		if( !TryBuildCurrentConfig(out var config, out var error) )
		{
			StatusText.Text = error ?? "Invalid configuration.";
			return;
		}

		await ConfigLoader.SaveAsync(_configPath, config);
		_savedConfig = CloneConfig(config);
		UpdateDirtyIndicators();
		await _onSaved();
		Close();
	}

	private void CancelButton_OnClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

	private void LoadSelectedRepoFields()
	{
		_isLoading = true;
		if( RepoListBox.SelectedItem is not RepoConfig selected )
		{
			ClearRepoFields();
			_isLoading = false;
			return;
		}

		RepoNameTextBox.Text = selected.Name;
		RepoTypeComboBox.SelectedItem = selected.Type;
		RepoPathTextBox.Text = selected.Path ?? string.Empty;
		RepoOwnerTextBox.Text = selected.Owner ?? string.Empty;
		RepoRepoTextBox.Text = selected.Repo ?? string.Empty;
		UpdateTypeVisibility();
		_isLoading = false;
	}

	private void ClearRepoFields()
	{
		_isLoading = true;
		RepoNameTextBox.Text = string.Empty;
		RepoTypeComboBox.SelectedItem = RepoType.Local;
		RepoPathTextBox.Text = string.Empty;
		RepoOwnerTextBox.Text = string.Empty;
		RepoRepoTextBox.Text = string.Empty;
		UpdateTypeVisibility();
		_isLoading = false;
	}

	private void ApplySelectedRepoFieldChanges()
	{
		if( RepoListBox.SelectedItem is not RepoConfig selected )
		{
			return;
		}

		selected.Name = string.IsNullOrWhiteSpace(RepoNameTextBox.Text) ? "Unnamed Repo" : RepoNameTextBox.Text.Trim();
		selected.Type = RepoTypeComboBox.SelectedItem is RepoType type ? type : RepoType.Local;
		selected.Path = Normalize(RepoPathTextBox.Text);
		selected.Owner = Normalize(RepoOwnerTextBox.Text);
		selected.Repo = Normalize(RepoRepoTextBox.Text);
		RepoListBox.Items.Refresh();
	}

	private static string? Normalize(string? value)
	{
		return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
	}

	private bool TryBuildCurrentConfig(out HeatmapConfig config, out string? error)
	{
		error = null;
		config = new HeatmapConfig();

		if( !int.TryParse(LookbackDaysTextBox.Text, out var lookbackDays) || lookbackDays <= 0 )
		{
			error = "Lookback days must be a positive number.";
			return false;
		}

		ApplySelectedRepoFieldChanges();

		config.LookbackDays = lookbackDays;
		config.AuthorMatch = Normalize(AuthorMatchTextBox.Text);
		config.Repositories = _repositories.Select(CloneRepo).ToList();

		return true;
	}

	private void UpdateDirtyIndicators()
	{
		SetDirty(LookbackDaysTextBox, !int.TryParse(LookbackDaysTextBox.Text, out var lookback) || lookback != _savedConfig.LookbackDays);
		SetDirty(AuthorMatchTextBox, Normalize(AuthorMatchTextBox.Text) != Normalize(_savedConfig.AuthorMatch));

		var idx = RepoListBox.SelectedIndex;
		RepoConfig? savedRepo = idx >= 0 && idx < _savedConfig.Repositories.Count ? _savedConfig.Repositories[idx] : null;

		var currentName = string.IsNullOrWhiteSpace(RepoNameTextBox.Text) ? "Unnamed Repo" : RepoNameTextBox.Text.Trim();
		var currentType = RepoTypeComboBox.SelectedItem is RepoType type ? type : RepoType.Local;
		var currentPath = Normalize(RepoPathTextBox.Text);
		var currentOwner = Normalize(RepoOwnerTextBox.Text);
		var currentRepo = Normalize(RepoRepoTextBox.Text);

		SetDirty(RepoNameTextBox, currentName != (savedRepo?.Name ?? string.Empty));
		SetDirty(RepoTypeComboBox, currentType != (savedRepo?.Type ?? RepoType.Local));
		SetDirty(RepoPathTextBox, currentPath != Normalize(savedRepo?.Path));
		SetDirty(RepoOwnerTextBox, currentOwner != Normalize(savedRepo?.Owner));
		SetDirty(RepoRepoTextBox, currentRepo != Normalize(savedRepo?.Repo));

		var reposDirty = _repositories.Count != _savedConfig.Repositories.Count || _repositories.Where((repo, i) =>
		{
			if( i >= _savedConfig.Repositories.Count )
			{
				return true;
			}

			var saved = _savedConfig.Repositories[i];
			return repo.Name != saved.Name || repo.Type != saved.Type || Normalize(repo.Path) != Normalize(saved.Path) ||
			       Normalize(repo.Owner) != Normalize(saved.Owner) || Normalize(repo.Repo) != Normalize(saved.Repo);
		}).Any();

		SetDirty(RepoListBox, reposDirty);
	}

	private void UpdateTypeVisibility()
	{
		var selectedType = RepoTypeComboBox.SelectedItem is RepoType repoType ? repoType : RepoType.Local;
		var localVisibility = selectedType == RepoType.Local ? Visibility.Visible : Visibility.Collapsed;
		var githubVisibility = selectedType == RepoType.GitHub ? Visibility.Visible : Visibility.Collapsed;

		RepoPathLabel.Visibility = localVisibility;
		RepoPathTextBox.Visibility = localVisibility;

		RepoOwnerLabel.Visibility = githubVisibility;
		RepoOwnerTextBox.Visibility = githubVisibility;
		RepoRepoLabel.Visibility = githubVisibility;
		RepoRepoTextBox.Visibility = githubVisibility;
	}

	private static void SetDirty(Control control, bool dirty)
	{
		control.Background = dirty ? DirtyBrush : CleanBrush;
	}

	private static HeatmapConfig CloneConfig(HeatmapConfig config)
	{
		return new HeatmapConfig
		{
			LookbackDays = config.LookbackDays,
			AuthorMatch = config.AuthorMatch,
			Repositories = config.Repositories.Select(CloneRepo).ToList()
		};
	}
}
