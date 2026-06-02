using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace JiraSprintDashboard;

public class JiraClient : IDisposable
{
    private readonly HttpClient _httpClient;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JiraClient(string baseUrl, string token)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Укажите URL Jira.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Укажите API токен Jira.");
        }

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/")
        };
        _httpClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token.Trim());
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    public async Task<ProjectInfo?> FindProjectByNameAsync(string projectName, CancellationToken ct)
    {
        var response = await GetAsync<JiraProjectSearchResponse>($"rest/api/3/project/search?maxResults=200", ct);
        var match = response.Values.FirstOrDefault(
            p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));

        return match is null ? null : new ProjectInfo(match.Id, match.Key, match.Name);
    }

    public async Task<List<SprintInfo>> GetSprintsAsync(string projectKey, CancellationToken ct)
    {
        var boardResponse = await GetAsync<JiraBoardSearchResponse>(
            $"rest/agile/1.0/board?projectKeyOrId={Uri.EscapeDataString(projectKey)}&maxResults=50", ct);

        var boardId = boardResponse.Values.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("Для проекта не найдена scrum/kanban доска.");

        var sprints = await GetAsync<JiraSprintSearchResponse>(
            $"rest/agile/1.0/board/{boardId}/sprint?state=active,future,closed&maxResults=200", ct);

        return sprints.Values
            .OrderByDescending(s => s.Id)
            .Select(s => new SprintInfo(s.Id, s.Name, s.State))
            .ToList();
    }

    public async Task<List<TeamMember>> GetProjectUsersAsync(string projectKey, CancellationToken ct)
    {
        var result = new Dictionary<string, TeamMember>(StringComparer.OrdinalIgnoreCase);

        var roles = await GetAsync<JiraProjectRolesResponse>(
            $"rest/api/3/project/{Uri.EscapeDataString(projectKey)}/role", ct);

        foreach (var role in roles.RoleUrls)
        {
            if (!Uri.TryCreate(role.Value.GetString(), UriKind.Absolute, out var roleUri))
            {
                continue;
            }

            var roleResponse = await GetAbsoluteAsync<JiraRoleUsersResponse>(roleUri, ct);
            foreach (var actor in roleResponse.Actors)
            {
                if (actor.ActorUser is null || string.IsNullOrWhiteSpace(actor.ActorUser.AccountId))
                {
                    continue;
                }

                var member = new TeamMember(actor.ActorUser.AccountId, actor.ActorUser.DisplayName);
                result[member.AccountId] = member;
            }
        }

        return result.Values
            .OrderBy(u => u.DisplayName, StringComparer.CurrentCultureIgnoreCase)
            .ToList();
    }

    public async Task<List<IssueDashboardRow>> GetSprintIssuesAsync(
        string projectKey,
        int sprintId,
        string? memberAccountId,
        CancellationToken ct)
    {
        var jql = $"project = \"{projectKey}\" AND sprint = {sprintId} ORDER BY key ASC";
        var payload = $$"""
        {
          "jql": "{{jql}}",
          "maxResults": 1000,
          "fields": [
            "summary",
            "status",
            "assignee",
            "subtasks",
            "worklog"
          ]
        }
        """;

        using var request = new HttpRequestMessage(HttpMethod.Post, "rest/api/3/search");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Jira API error ({(int)response.StatusCode}): {body}");
        }

        var data = JsonSerializer.Deserialize<JiraSearchResponse>(body, _jsonOptions)
            ?? new JiraSearchResponse();

        var result = new List<IssueDashboardRow>(data.Issues.Count);
        foreach (var issue in data.Issues)
        {
            var plannedSeconds = issue.Fields.Subtasks
                .Where(st => string.IsNullOrWhiteSpace(memberAccountId)
                    || st.Fields.Assignee?.AccountId == memberAccountId)
                .Sum(st => st.Fields.OriginalEstimateSeconds ?? 0);

            var spentSeconds = issue.Fields.Worklog?.Worklogs
                .Where(wl => string.IsNullOrWhiteSpace(memberAccountId)
                    || wl.Author?.AccountId == memberAccountId)
                .Sum(wl => wl.TimeSpentSeconds) ?? 0;

            if (!string.IsNullOrWhiteSpace(memberAccountId)
                && plannedSeconds == 0
                && spentSeconds == 0
                && issue.Fields.Assignee?.AccountId != memberAccountId)
            {
                continue;
            }

            result.Add(new IssueDashboardRow
            {
                Key = issue.Key,
                Summary = issue.Fields.Summary,
                Status = issue.Fields.Status?.Name ?? "Без статуса",
                Assignee = issue.Fields.Assignee?.DisplayName ?? "Не назначено",
                PlannedHours = SecondsToHours(plannedSeconds),
                SpentHours = SecondsToHours(spentSeconds)
            });
        }

        return result;
    }

    private static double SecondsToHours(int seconds) =>
        Math.Round(seconds / 3600d, 2, MidpointRounding.AwayFromZero);

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(relativeUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Jira API error ({(int)response.StatusCode}): {body}");
        }

        return JsonSerializer.Deserialize<T>(body, _jsonOptions)
            ?? throw new InvalidOperationException("Не удалось разобрать ответ Jira.");
    }

    private async Task<T> GetAbsoluteAsync<T>(Uri absoluteUri, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(absoluteUri, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Jira API error ({(int)response.StatusCode}): {body}");
        }

        return JsonSerializer.Deserialize<T>(body, _jsonOptions)
            ?? throw new InvalidOperationException("Не удалось разобрать ответ Jira.");
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
