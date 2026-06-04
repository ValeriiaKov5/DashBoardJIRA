using System.Data;

namespace JiraSprintDashboard;

public class MainForm : Form
{
    private readonly SettingsStore _settingsStore = new();
    private readonly AppSettings _settings;

    private readonly TextBox _txtBaseUrl = new();
    private readonly TextBox _txtUsername = new();
    private readonly TextBox _txtToken = new();
    private readonly TextBox _txtProjectName = new();
    private readonly ComboBox _cmbSprints = new();
    private readonly CheckedListBox _clbTeam = new();
    private readonly Label _lblSettingsStatus = new();
    private readonly Button _btnLoadProjectData = new();
    private readonly Button _btnSaveSettings = new();

    private readonly Label _lblTotalTasks = new();
    private readonly Label _lblInProgress = new();
    private readonly Label _lblDone = new();
    private readonly Label _lblPlanned = new();
    private readonly Label _lblSpent = new();
    private readonly Label _lblPercent = new();
    private readonly ComboBox _cmbMemberFilter = new();
    private readonly DataGridView _gridIssues = new();
    private readonly Button _btnRefreshDashboard = new();
    private readonly Label _lblMainStatus = new();

    private List<TeamMember> _teamMembers = new();
    private List<SprintInfo> _sprints = new();

    public MainForm()
    {
        _settings = _settingsStore.Load();
        InitializeUi();
        LoadSettingsToUi();
    }

    private void InitializeUi()
    {
        Text = "Jira Sprint Dashboard";
        Width = 1280;
        Height = 860;
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(248, 244, 240);
        Font = new Font("Segoe UI", 10f);

        var tabs = new TabControl
        {
            Dock = DockStyle.Fill,
            Font = new Font("Segoe UI", 10f)
        };

        var settingsTab = new TabPage("Настройки") { BackColor = Color.FromArgb(251, 247, 245) };
        var dashboardTab = new TabPage("Основное") { BackColor = Color.FromArgb(248, 243, 250) };

        tabs.TabPages.Add(settingsTab);
        tabs.TabPages.Add(dashboardTab);

        Controls.Add(tabs);

        BuildSettingsTab(settingsTab);
        BuildDashboardTab(dashboardTab);
    }

    private void BuildSettingsTab(TabPage tab)
    {
        var panel = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 2,
            Padding = new Padding(24),
        };
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 280));
        panel.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        panel.RowCount = 9;

        AddLabeledControl(panel, "Jira URL", _txtBaseUrl, 0);

        AddLabeledControl(panel, "Логин Jira (можно оставить пустым для API-токена)", _txtUsername, 1);

        _txtToken.UseSystemPasswordChar = true;
        AddLabeledControl(panel, "Пароль Jira или API-токен", _txtToken, 2);

        AddLabeledControl(panel, "Наименование проекта Jira", _txtProjectName, 3);

        _btnLoadProjectData.Text = "Загрузить проект, спринты и команду";
        _btnLoadProjectData.Height = 36;
        _btnLoadProjectData.BackColor = Color.FromArgb(220, 235, 245);
        _btnLoadProjectData.Click += async (_, _) => await LoadProjectDataAsync();
        panel.Controls.Add(_btnLoadProjectData, 1, 4);

        _cmbSprints.DropDownStyle = ComboBoxStyle.DropDownList;
        AddLabeledControl(panel, "Спринт", _cmbSprints, 5);

        _clbTeam.Height = 220;
        _clbTeam.CheckOnClick = true;
        AddLabeledControl(panel, "Команда продукта (участники Jira)", _clbTeam, 6);

        _btnSaveSettings.Text = "Сохранить настройки";
        _btnSaveSettings.Height = 36;
        _btnSaveSettings.BackColor = Color.FromArgb(210, 240, 225);
        _btnSaveSettings.Click += (_, _) => SaveSettings();
        panel.Controls.Add(_btnSaveSettings, 1, 7);

        _lblSettingsStatus.AutoSize = true;
        _lblSettingsStatus.ForeColor = Color.FromArgb(70, 70, 70);
        panel.Controls.Add(_lblSettingsStatus, 1, 8);

        tab.Controls.Add(panel);
    }

    private void BuildDashboardTab(TabPage tab)
    {
        var container = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            RowCount = 3,
            ColumnCount = 1,
            Padding = new Padding(18)
        };
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 140));
        container.RowStyles.Add(new RowStyle(SizeType.Absolute, 45));
        container.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var metrics = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            WrapContents = true,
            AutoScroll = true
        };
        metrics.Controls.Add(CreateMetricCard("Всего задач", _lblTotalTasks));
        metrics.Controls.Add(CreateMetricCard("Задач в работе", _lblInProgress));
        metrics.Controls.Add(CreateMetricCard("Задач выполнено", _lblDone));
        metrics.Controls.Add(CreateMetricCard("План, часы", _lblPlanned));
        metrics.Controls.Add(CreateMetricCard("Факт, часы", _lblSpent));
        metrics.Controls.Add(CreateMetricCard("% факт/план", _lblPercent));
        container.Controls.Add(metrics, 0, 0);

        var filters = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            FlowDirection = FlowDirection.LeftToRight,
            WrapContents = false
        };
        filters.Controls.Add(new Label { Text = "Фильтр по члену команды:", Width = 220, TextAlign = ContentAlignment.MiddleLeft });
        _cmbMemberFilter.Width = 280;
        _cmbMemberFilter.DropDownStyle = ComboBoxStyle.DropDownList;
        _cmbMemberFilter.SelectedIndexChanged += async (_, _) => await LoadDashboardAsync();
        filters.Controls.Add(_cmbMemberFilter);

        _btnRefreshDashboard.Text = "Обновить";
        _btnRefreshDashboard.Width = 120;
        _btnRefreshDashboard.Click += async (_, _) => await LoadDashboardAsync();
        filters.Controls.Add(_btnRefreshDashboard);

        _lblMainStatus.Width = 500;
        _lblMainStatus.TextAlign = ContentAlignment.MiddleLeft;
        filters.Controls.Add(_lblMainStatus);

        container.Controls.Add(filters, 0, 1);

        _gridIssues.Dock = DockStyle.Fill;
        _gridIssues.ReadOnly = true;
        _gridIssues.AutoGenerateColumns = false;
        _gridIssues.AllowUserToAddRows = false;
        _gridIssues.SelectionMode = DataGridViewSelectionMode.FullRowSelect;
        _gridIssues.BackgroundColor = Color.White;
        _gridIssues.RowHeadersVisible = false;
        _gridIssues.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Задача", DataPropertyName = "Key", Width = 120 });
        _gridIssues.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Описание", DataPropertyName = "Summary", Width = 420 });
        _gridIssues.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Статус", DataPropertyName = "Status", Width = 180 });
        _gridIssues.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Исполнитель", DataPropertyName = "Assignee", Width = 200 });
        _gridIssues.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "План, ч", DataPropertyName = "PlannedHours", Width = 120 });
        _gridIssues.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Факт, ч", DataPropertyName = "SpentHours", Width = 120 });

        container.Controls.Add(_gridIssues, 0, 2);
        tab.Controls.Add(container);
    }

    private Panel CreateMetricCard(string title, Label valueLabel)
    {
        var panel = new Panel
        {
            Width = 190,
            Height = 100,
            Margin = new Padding(8),
            BackColor = Color.FromArgb(236, 233, 250)
        };
        panel.BorderStyle = BorderStyle.FixedSingle;

        var titleLabel = new Label
        {
            Text = title,
            Dock = DockStyle.Top,
            Height = 40,
            Font = new Font("Segoe UI Semibold", 9f),
            TextAlign = ContentAlignment.MiddleCenter
        };

        valueLabel.Text = "-";
        valueLabel.Dock = DockStyle.Fill;
        valueLabel.Font = new Font("Segoe UI Semibold", 16f);
        valueLabel.TextAlign = ContentAlignment.MiddleCenter;

        panel.Controls.Add(valueLabel);
        panel.Controls.Add(titleLabel);
        return panel;
    }

    private static void AddLabeledControl(TableLayoutPanel panel, string label, Control control, int row)
    {
        panel.RowStyles.Add(new RowStyle(SizeType.AutoSize));

        var lbl = new Label
        {
            Text = label,
            AutoSize = true,
            Margin = new Padding(0, 8, 0, 8),
            Font = new Font("Segoe UI Semibold", 10f)
        };

        control.Dock = DockStyle.Top;
        control.Margin = new Padding(0, 4, 0, 10);
        control.Height = Math.Max(control.Height, 30);

        panel.Controls.Add(lbl, 0, row);
        panel.Controls.Add(control, 1, row);
    }

    private void LoadSettingsToUi()
    {
        _txtBaseUrl.Text = _settings.JiraBaseUrl;
        _txtUsername.Text = _settings.JiraUsername;
        _txtToken.Text = _settings.Token;
        _txtProjectName.Text = _settings.ProjectName;
        _lblSettingsStatus.Text = "Настройки загружены.";
    }

    private async Task LoadProjectDataAsync()
    {
        if (string.IsNullOrWhiteSpace(_txtBaseUrl.Text))
        {
            SetSettingsStatus("Укажите URL Jira.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtToken.Text))
        {
            SetSettingsStatus("Укажите пароль или API-токен.", true);
            return;
        }

        if (string.IsNullOrWhiteSpace(_txtProjectName.Text))
        {
            SetSettingsStatus("Укажите наименование проекта Jira.", true);
            return;
        }

        SetSettingsStatus("Загрузка данных проекта...");
        ToggleSettingsButtons(false);
        try
        {
            using var client = await CreateClientWithAuthFallbackAsync();
            var project = await client.FindProjectByNameAsync(_txtProjectName.Text.Trim(), CancellationToken.None);
            if (project is null)
            {
                throw new InvalidOperationException("Проект с таким именем не найден в Jira.");
            }

            _settings.ProjectKey = project.Key;
            _settings.ProjectName = project.Name;
            _txtProjectName.Text = project.Name;

            _sprints = await client.GetSprintsAsync(project.Key, CancellationToken.None);
            _cmbSprints.Items.Clear();
            foreach (var sprint in _sprints)
            {
                _cmbSprints.Items.Add($"{sprint.Name} ({sprint.State})");
            }

            var selectedSprintIndex = _sprints.FindIndex(s => s.Id == _settings.SprintId);
            _cmbSprints.SelectedIndex = selectedSprintIndex >= 0 ? selectedSprintIndex : (_sprints.Count > 0 ? 0 : -1);

            _teamMembers = await client.GetProjectUsersAsync(project.Key, CancellationToken.None);
            _clbTeam.Items.Clear();
            foreach (var member in _teamMembers)
            {
                var isChecked = _settings.TeamMemberAccountIds.Contains(member.AccountId);
                _clbTeam.Items.Add(member, isChecked);
            }

            FillMemberFilter();
            if (_sprints.Count == 0)
            {
                SetSettingsStatus(
                    "Проект и команда загружены. Спринтов после 01.01.2026 не найдено (нужна дата в API или в названии спринта).",
                    true);
            }
            else
            {
                SetSettingsStatus($"Проект, спринты ({_sprints.Count}) и команда успешно загружены.");
            }
        }
        catch (Exception ex)
        {
            SetSettingsStatus(GetErrorMessage(ex), true);
        }
        finally
        {
            ToggleSettingsButtons(true);
        }
    }

    private void FillMemberFilter()
    {
        var currentMemberId = (_cmbMemberFilter.SelectedItem as TeamMember)?.AccountId;
        _cmbMemberFilter.Items.Clear();
        _cmbMemberFilter.Items.Add(new TeamMember("", "Все члены команды"));

        var selectedMembers = _teamMembers
            .Where(m => _settings.TeamMemberAccountIds.Contains(m.AccountId))
            .OrderBy(m => m.DisplayName)
            .ToList();
        foreach (var member in selectedMembers)
        {
            _cmbMemberFilter.Items.Add(member);
        }

        var selectedIndex = 0;
        if (!string.IsNullOrWhiteSpace(currentMemberId))
        {
            var idx = _cmbMemberFilter.Items.Cast<TeamMember>().ToList()
                .FindIndex(m => m.AccountId == currentMemberId);
            if (idx >= 0)
            {
                selectedIndex = idx;
            }
        }

        _cmbMemberFilter.DisplayMember = nameof(TeamMember.DisplayName);
        _cmbMemberFilter.SelectedIndex = _cmbMemberFilter.Items.Count > 0 ? selectedIndex : -1;
    }

    private void SaveSettings()
    {
        if (_cmbSprints.SelectedIndex >= 0 && _cmbSprints.SelectedIndex < _sprints.Count)
        {
            var selected = _sprints[_cmbSprints.SelectedIndex];
            _settings.SprintId = selected.Id;
            _settings.SprintName = selected.Name;
        }

        _settings.JiraBaseUrl = _txtBaseUrl.Text.Trim();
        _settings.JiraUsername = _txtUsername.Text.Trim();
        _settings.Token = _txtToken.Text.Trim();
        _settings.ProjectName = _txtProjectName.Text.Trim();
        _settings.TeamMemberAccountIds = _clbTeam.CheckedItems
            .Cast<TeamMember>()
            .Select(m => m.AccountId)
            .Distinct()
            .ToList();

        _settingsStore.Save(_settings);
        FillMemberFilter();
        SetSettingsStatus("Настройки сохранены.");
    }

    private async Task LoadDashboardAsync()
    {
        if (_settings.SprintId <= 0 || string.IsNullOrWhiteSpace(_settings.ProjectKey))
        {
            _lblMainStatus.Text = "Сначала заполните и сохраните настройки.";
            return;
        }

        ToggleMainButtons(false);
        _lblMainStatus.Text = "Обновление дашборда...";

        try
        {
            using var client = await CreateClientWithAuthFallbackAsync(
                _settings.JiraBaseUrl, _settings.JiraUsername, _settings.Token);
            var selectedMember = _cmbMemberFilter.SelectedItem as TeamMember;
            var memberId = string.IsNullOrWhiteSpace(selectedMember?.AccountId) ? null : selectedMember!.AccountId;

            var issues = await client.GetSprintIssuesAsync(
                _settings.ProjectKey,
                _settings.SprintId,
                memberId,
                CancellationToken.None);

            var total = issues.Count;
            var inProgress = issues.Count(i => IsInProgressStatus(i.Status));
            var done = issues.Count(i => IsDoneStatus(i.Status));
            var planned = Math.Round(issues.Sum(i => i.PlannedHours), 2, MidpointRounding.AwayFromZero);
            var spent = Math.Round(issues.Sum(i => i.SpentHours), 2, MidpointRounding.AwayFromZero);
            var percent = planned <= 0 ? 0 : Math.Round((spent / planned) * 100, 1, MidpointRounding.AwayFromZero);

            _lblTotalTasks.Text = total.ToString();
            _lblInProgress.Text = inProgress.ToString();
            _lblDone.Text = done.ToString();
            _lblPlanned.Text = planned.ToString("0.##");
            _lblSpent.Text = spent.ToString("0.##");
            _lblPercent.Text = $"{percent:0.#}%";

            _gridIssues.DataSource = new BindingSource { DataSource = issues };
            _lblMainStatus.Text = $"Спринт: {_settings.SprintName}. Обновлено: {DateTime.Now:HH:mm:ss}";
        }
        catch (Exception ex)
        {
            _lblMainStatus.Text = GetErrorMessage(ex);
        }
        finally
        {
            ToggleMainButtons(true);
        }
    }

    private static bool IsDoneStatus(string status)
    {
        var s = status.ToLowerInvariant();
        return s.Contains("done") || s.Contains("готов") || s.Contains("выполн");
    }

    private static bool IsInProgressStatus(string status)
    {
        var s = status.ToLowerInvariant();
        return s.Contains("progress") || s.Contains("работ") || s.Contains("in review");
    }

    private Task<JiraClient> CreateClientWithAuthFallbackAsync() =>
        CreateClientWithAuthFallbackAsync(
            _txtBaseUrl.Text.Trim(),
            _txtUsername.Text.Trim(),
            _txtToken.Text.Trim());

    private static async Task<JiraClient> CreateClientWithAuthFallbackAsync(
        string baseUrl,
        string username,
        string token)
    {
        var hadUsername = !string.IsNullOrWhiteSpace(username);
        var client = new JiraClient(baseUrl, username, token);

        try
        {
            await client.VerifyConnectionAsync(CancellationToken.None);
            return client;
        }
        catch (InvalidOperationException ex) when (hadUsername && IsAuthError(ex))
        {
            client.Dispose();
            var bearerClient = new JiraClient(baseUrl, "", token);
            await bearerClient.VerifyConnectionAsync(CancellationToken.None);
            return bearerClient;
        }
    }

    private static bool IsAuthError(Exception ex) =>
        ex.Message.Contains("(401)", StringComparison.Ordinal)
        || ex.Message.Contains("(403)", StringComparison.Ordinal);

    private static string GetErrorMessage(Exception ex) =>
        ex.InnerException is null ? ex.Message : $"{ex.Message} ({ex.InnerException.Message})";

    private void SetSettingsStatus(string text, bool isError = false)
    {
        _lblSettingsStatus.Text = text;
        _lblSettingsStatus.ForeColor = isError ? Color.FromArgb(180, 55, 70) : Color.FromArgb(70, 70, 70);
    }

    private void ToggleSettingsButtons(bool enabled)
    {
        _btnLoadProjectData.Enabled = enabled;
        _btnSaveSettings.Enabled = enabled;
    }

    private void ToggleMainButtons(bool enabled)
    {
        _btnRefreshDashboard.Enabled = enabled;
        _cmbMemberFilter.Enabled = enabled;
    }
}
