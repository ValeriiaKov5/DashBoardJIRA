using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JiraSprintDashboard;

public class JiraClient : IDisposable
{
    private static readonly DateTime MinSprintCreatedDate = new(2026, 1, 1);

    private readonly HttpClient _httpClient;
    private readonly string _apiPrefix;
    private readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public JiraClient(string baseUrl, string? username, string token)
    {
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            throw new InvalidOperationException("Укажите URL Jira.");
        }

        if (string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("Укажите пароль или API-токен Jira.");
        }

        var trimmedUrl = baseUrl.TrimEnd('/');
        _apiPrefix = trimmedUrl.Contains("atlassian.net", StringComparison.OrdinalIgnoreCase)
            ? "rest/api/3"
            : "rest/api/2";

        _httpClient = new HttpClient
        {
            BaseAddress = new Uri(trimmedUrl + "/")
        };
        _httpClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        _httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("JiraSprintDashboard/1.0");

        var trimmedToken = token.Trim();
        var trimmedUsername = username?.Trim();
        if (!string.IsNullOrWhiteSpace(trimmedUsername))
        {
            var credentials = Convert.ToBase64String(
                Encoding.UTF8.GetBytes($"{trimmedUsername}:{trimmedToken}"));
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Basic", credentials);
        }
        else
        {
            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue("Bearer", trimmedToken);
        }
    }

    public Task VerifyConnectionAsync(CancellationToken ct) =>
        GetAsync<JsonElement>($"{_apiPrefix}/myself", ct);

    public async Task<ProjectInfo?> FindProjectByNameAsync(string projectName, CancellationToken ct)
    {
        if (_apiPrefix == "rest/api/3")
        {
            var response = await GetAsync<JiraProjectSearchResponse>(
                $"{_apiPrefix}/project/search?maxResults=200", ct);
            var match = response.AllProjects.FirstOrDefault(
                p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
            return match is null ? null : new ProjectInfo(match.Id, match.Key, match.Name);
        }

        try
        {
            var searchResponse = await GetAsync<JiraProjectSearchResponse>(
                $"{_apiPrefix}/project/search?maxResults=200", ct);
            var searchMatch = searchResponse.AllProjects.FirstOrDefault(
                p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase));
            if (searchMatch is not null)
            {
                return new ProjectInfo(searchMatch.Id, searchMatch.Key, searchMatch.Name);
            }
        }
        catch
        {
            // Старые версии Jira Server могут не поддерживать project/search.
        }

        var projects = await GetAsync<List<JiraProjectItem>>($"{_apiPrefix}/project", ct);
        var listMatch = projects.FirstOrDefault(
            p => p.Name.Equals(projectName, StringComparison.OrdinalIgnoreCase)
                || p.Key.Equals(projectName, StringComparison.OrdinalIgnoreCase));
        return listMatch is null ? null : new ProjectInfo(listMatch.Id, listMatch.Key, listMatch.Name);
    }

    public async Task<List<SprintInfo>> GetSprintsAsync(string projectKey, CancellationToken ct)
    {
        var boardResponse = await GetAsync<JiraBoardSearchResponse>(
            $"rest/agile/1.0/board?projectKeyOrId={Uri.EscapeDataString(projectKey)}&maxResults=50", ct);

        var boardId = boardResponse.Values.FirstOrDefault()?.Id
            ?? throw new InvalidOperationException("Для проекта не найдена scrum/kanban доска.");

        var sprintsJson = await GetAsync<JsonElement>(
            $"rest/agile/1.0/board/{boardId}/sprint?state=active,future,closed&maxResults=200", ct);

        if (!sprintsJson.TryGetProperty("values", out var values) || values.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var result = new List<SprintInfo>();
        foreach (var sprint in values.EnumerateArray())
        {
            if (await TryMapSprintAfterCutoffAsync(sprint, ct, out var sprintInfo))
            {
                result.Add(sprintInfo);
            }
        }

        return result
            .OrderByDescending(s => s.Id)
            .ToList();
    }

    private async Task<bool> TryMapSprintAfterCutoffAsync(
        JsonElement sprint,
        CancellationToken ct,
        out SprintInfo sprintInfo)
    {
        sprintInfo = default!;
        var id = sprint.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
        var name = sprint.TryGetProperty("name", out var nameEl) ? nameEl.GetString() ?? "" : "";
        var state = sprint.TryGetProperty("state", out var stateEl) ? stateEl.GetString() ?? "" : "";

        if (id == 0)
        {
            return false;
        }

        if (!TryGetSprintReferenceDate(sprint, name, out var referenceDate))
        {
            try
            {
                var detail = await GetAsync<JsonElement>($"rest/agile/1.0/sprint/{id}", ct);
                if (!TryGetSprintReferenceDate(detail, name, out referenceDate))
                {
                    return false;
                }
            }
            catch
            {
                return false;
            }
        }

        if (referenceDate.Date <= MinSprintCreatedDate)
        {
            return false;
        }

        sprintInfo = new SprintInfo(id, name, state);
        return true;
    }

    private static bool TryGetSprintReferenceDate(JsonElement sprint, string name, out DateTime referenceDate)
    {
        foreach (var field in new[] { "createdDate", "startDate", "activatedDate", "completeDate", "endDate" })
        {
            if (sprint.TryGetProperty(field, out var dateEl)
                && TryParseJiraDateElement(dateEl, out referenceDate))
            {
                return true;
            }
        }

        if (TryParseDateFromSprintName(name, out referenceDate))
        {
            return true;
        }

        if (TryParseYearFromSprintName(name, out referenceDate))
        {
            return true;
        }

        referenceDate = default;
        return false;
    }

    private static bool TryParseYearFromSprintName(string name, out DateTime date)
    {
        date = default;
        var match = Regex.Match(name, @"\b(20[2-9]\d)\b");
        if (!match.Success || !int.TryParse(match.Groups[1].Value, out var year) || year < 2026)
        {
            return false;
        }

        date = new DateTime(year, 1, 2);
        return true;
    }

    private static bool TryParseJiraDateElement(JsonElement element, out DateTime date)
    {
        date = default;
        if (element.ValueKind != JsonValueKind.String)
        {
            return false;
        }

        var text = element.GetString();
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        if (DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var dto))
        {
            date = dto.Date;
            return true;
        }

        return DateTime.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out date);
    }

    private static bool TryParseDateFromSprintName(string name, out DateTime date)
    {
        date = default;
        DateTime? best = null;

        foreach (Match match in Regex.Matches(name, @"\b(\d{2})\.(\d{2})\.(\d{4})\b"))
        {
            if (DateTime.TryParse(
                    $"{match.Groups[3].Value}-{match.Groups[2].Value}-{match.Groups[1].Value}",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                best = best is null || parsed < best ? parsed : best;
            }
        }

        foreach (Match match in Regex.Matches(name, @"\b(\d{4})-(\d{2})-(\d{2})\b"))
        {
            if (DateTime.TryParse(
                    $"{match.Groups[1].Value}-{match.Groups[2].Value}-{match.Groups[3].Value}",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out var parsed))
            {
                best = best is null || parsed < best ? parsed : best;
            }
        }

        if (best is null)
        {
            return false;
        }

        date = best.Value;
        return true;
    }

    public async Task<List<TeamMember>> GetProjectUsersAsync(string projectKey, CancellationToken ct)
    {
        var result = new Dictionary<string, TeamMember>(StringComparer.OrdinalIgnoreCase);

        var roles = await GetAsync<JiraProjectRolesResponse>(
            $"{_apiPrefix}/project/{Uri.EscapeDataString(projectKey)}/role", ct);

        foreach (var role in roles.RoleUrls)
        {
            var roleUrl = role.Value.ValueKind == JsonValueKind.String
                ? role.Value.GetString()
                : role.Value.ToString();
            if (string.IsNullOrWhiteSpace(roleUrl)
                || !Uri.TryCreate(roleUrl, UriKind.Absolute, out var roleUri))
            {
                continue;
            }

            var roleResponse = await GetAbsoluteAsync<JiraRoleUsersResponse>(roleUri, ct);
            foreach (var actor in roleResponse.Actors)
            {
                var userId = actor.ResolveUserId();
                if (string.IsNullOrWhiteSpace(userId))
                {
                    continue;
                }

                var member = new TeamMember(userId, actor.ResolveDisplayName());
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
        var jql = $"project = {EscapeJqlValue(projectKey)} AND sprint = {sprintId} ORDER BY key ASC";
        var payload = JsonSerializer.Serialize(new JiraSearchRequest
        {
            Jql = jql,
            MaxResults = 1000,
            Fields = ["summary", "status", "assignee", "subtasks", "worklog"]
        }, _jsonOptions);

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{_apiPrefix}/search");
        request.Content = new StringContent(payload, Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureJsonResponse(body, response.IsSuccessStatusCode ? null : (int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Jira API error ({(int)response.StatusCode}): {TrimForDisplay(body)}");
        }

        var data = JsonSerializer.Deserialize<JiraSearchResponse>(body, _jsonOptions)
            ?? new JiraSearchResponse();

        var result = new List<IssueDashboardRow>(data.Issues.Count);
        foreach (var issue in data.Issues)
        {
            var plannedSeconds = issue.Fields.Subtasks
                .Where(st => string.IsNullOrWhiteSpace(memberAccountId)
                    || st.Fields.Assignee?.UserId == memberAccountId)
                .Sum(st => st.Fields.OriginalEstimateSeconds ?? 0);

            var spentSeconds = issue.Fields.Worklog?.Worklogs
                .Where(wl => string.IsNullOrWhiteSpace(memberAccountId)
                    || wl.Author?.UserId == memberAccountId)
                .Sum(wl => wl.TimeSpentSeconds) ?? 0;

            if (!string.IsNullOrWhiteSpace(memberAccountId)
                && plannedSeconds == 0
                && spentSeconds == 0
                && issue.Fields.Assignee?.UserId != memberAccountId)
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

    private static string EscapeJqlValue(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "''";
        }

        return value.All(c => char.IsLetterOrDigit(c) || c == '_' || c == '-')
            ? value
            : $"'{value.Replace("'", "\\'", StringComparison.Ordinal)}'";
    }

    private async Task<T> GetAsync<T>(string relativeUrl, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(relativeUrl, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureJsonResponse(body, response.IsSuccessStatusCode ? null : (int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Jira API error ({(int)response.StatusCode}): {TrimForDisplay(body)}");
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, _jsonOptions)
                ?? throw new InvalidOperationException("Не удалось разобрать ответ Jira.");
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Jira вернула неожиданный ответ. {TrimForDisplay(body)}", ex);
        }
    }

    private async Task<T> GetAbsoluteAsync<T>(Uri absoluteUri, CancellationToken ct)
    {
        using var response = await _httpClient.GetAsync(absoluteUri, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        EnsureJsonResponse(body, response.IsSuccessStatusCode ? null : (int)response.StatusCode);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Jira API error ({(int)response.StatusCode}): {TrimForDisplay(body)}");
        }

        return JsonSerializer.Deserialize<T>(body, _jsonOptions)
            ?? throw new InvalidOperationException("Не удалось разобрать ответ Jira.");
    }

    private static void EnsureJsonResponse(string body, int? statusCode)
    {
        var trimmed = body.TrimStart();
        if (!trimmed.StartsWith("<", StringComparison.Ordinal))
        {
            return;
        }

        var hint = statusCode switch
        {
            401 => "Неверный логин или пароль.",
            403 => "Доступ запрещён. Попробуйте: 1) логин + пароль от Jira; 2) логин + API-токен (токен вместо пароля); "
                + "3) оставьте логин пустым и вставьте только API-токен (Bearer). "
                + "Токен создаётся в Jira: Профиль → Personal Access Tokens.",
            _ => "Проверьте URL Jira и доступ к REST API."
        };
        throw new InvalidOperationException(
            $"Jira вернула HTML вместо JSON ({(statusCode?.ToString() ?? "нет кода")}). {hint}");
    }

    private static string TrimForDisplay(string body)
    {
        var oneLine = body.Replace('\r', ' ').Replace('\n', ' ').Trim();
        return oneLine.Length <= 300 ? oneLine : oneLine[..300] + "...";
    }

    public void Dispose()
    {
        _httpClient.Dispose();
    }
}
