using System.Text.Json.Serialization;

namespace JiraSprintDashboard;

public class AppSettings
{
    public string JiraBaseUrl { get; set; } = "";
    public string JiraUsername { get; set; } = "";
    public string Token { get; set; } = "";
    public string ProjectName { get; set; } = "";
    public string ProjectKey { get; set; } = "";
    public int SprintId { get; set; }
    public string SprintName { get; set; } = "";
    public List<string> TeamMemberAccountIds { get; set; } = new();
}

public record TeamMember(string AccountId, string DisplayName)
{
    public override string ToString() => DisplayName;
}

public record ProjectInfo(string Id, string Key, string Name);

public record SprintInfo(int Id, string Name, string State);

public class IssueDashboardRow
{
    public string Key { get; init; } = "";
    public string Summary { get; init; } = "";
    public string Status { get; init; } = "";
    public string Assignee { get; init; } = "";
    public double PlannedHours { get; init; }
    public double SpentHours { get; init; }
}

public class JiraSearchResponse
{
    [JsonPropertyName("issues")]
    public List<JiraIssue> Issues { get; set; } = new();
}

public class JiraIssue
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("fields")]
    public JiraIssueFields Fields { get; set; } = new();
}

public class JiraIssueFields
{
    [JsonPropertyName("summary")]
    public string Summary { get; set; } = "";

    [JsonPropertyName("status")]
    public JiraStatus? Status { get; set; }

    [JsonPropertyName("assignee")]
    public JiraUser? Assignee { get; set; }

    [JsonPropertyName("subtasks")]
    public List<JiraIssueSubtask> Subtasks { get; set; } = new();

    [JsonPropertyName("worklog")]
    public JiraWorklogContainer? Worklog { get; set; }
}

public class JiraIssueSubtask
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("fields")]
    public JiraIssueSubtaskFields Fields { get; set; } = new();
}

public class JiraIssueSubtaskFields
{
    [JsonPropertyName("timeoriginalestimate")]
    public int? OriginalEstimateSeconds { get; set; }

    [JsonPropertyName("assignee")]
    public JiraUser? Assignee { get; set; }
}

public class JiraStatus
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("statusCategory")]
    public JiraStatusCategory? StatusCategory { get; set; }
}

public class JiraStatusCategory
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";
}

public class JiraUser
{
    [JsonPropertyName("accountId")]
    public string AccountId { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    public string UserId =>
        !string.IsNullOrWhiteSpace(AccountId) ? AccountId
        : !string.IsNullOrWhiteSpace(Name) ? Name
        : Key;
}

public class JiraWorklogContainer
{
    [JsonPropertyName("worklogs")]
    public List<JiraWorklogItem> Worklogs { get; set; } = new();
}

public class JiraWorklogItem
{
    [JsonPropertyName("author")]
    public JiraUser? Author { get; set; }

    [JsonPropertyName("timeSpentSeconds")]
    public int TimeSpentSeconds { get; set; }
}

public class JiraProjectSearchResponse
{
    [JsonPropertyName("values")]
    public List<JiraProjectItem> Values { get; set; } = new();

    [JsonPropertyName("projects")]
    public List<JiraProjectItem> Projects { get; set; } = new();

    public IEnumerable<JiraProjectItem> AllProjects =>
        Values.Count > 0 ? Values : Projects;
}

public class JiraProjectItem
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = "";

    [JsonPropertyName("key")]
    public string Key { get; set; } = "";

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";
}

public class JiraBoardSearchResponse
{
    [JsonPropertyName("values")]
    public List<JiraBoardItem> Values { get; set; } = new();
}

public class JiraBoardItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }
}

public class JiraSprintSearchResponse
{
    [JsonPropertyName("values")]
    public List<JiraSprintItem> Values { get; set; } = new();
}

public class JiraSprintItem
{
    [JsonPropertyName("id")]
    public int Id { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("state")]
    public string State { get; set; } = "";
}

public class JiraRoleUsersResponse
{
    [JsonPropertyName("actors")]
    public List<JiraRoleActor> Actors { get; set; } = new();
}

public class JiraRoleActor
{
    [JsonPropertyName("actorUser")]
    public JiraUser? ActorUser { get; set; }

    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("displayName")]
    public string DisplayName { get; set; } = "";

    public string? ResolveUserId()
    {
        var fromUser = ActorUser?.UserId;
        if (!string.IsNullOrWhiteSpace(fromUser))
        {
            return fromUser;
        }

        return string.IsNullOrWhiteSpace(Name) ? null : Name;
    }

    public string ResolveDisplayName() =>
        ActorUser?.DisplayName
        ?? (!string.IsNullOrWhiteSpace(DisplayName) ? DisplayName : Name);
}

public class JiraProjectRolesResponse
{
    [JsonExtensionData]
    public Dictionary<string, System.Text.Json.JsonElement> RoleUrls { get; set; } = new();
}
