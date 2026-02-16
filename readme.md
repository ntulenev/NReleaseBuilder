# NReleaseBuilder

NReleaseBuilder is a .NET console application that checks deployed component versions from a CSV file against Bitbucket tags, then enriches newer versions with Jira task and status information.

## What It Does

1. Reads a CSV file (expects `container` and `image` columns).
2. Extracts repository and current version from each image tag.
3. Loads tags from Bitbucket for each repository.
4. Detects versions newer than the current one.
5. Extracts Jira task keys from related commit messages.
6. Resolves Jira statuses for those tasks.
7. Renders a console report with:
   - per-component status table
   - summary counters
   - Slack-ready text block
   - unique Jira tasks by status chart

## Configuration

Edit `src/NReleaseBuilder/appsettings.json`.

Example structure:

```json
{
  "CsvFilePath": "C:\\path\\to\\components.csv",
  "Bitbucket": {
    "BaseUrl": "https://api.bitbucket.org/2.0",
    "Workspace": "your-workspace",
    "ProjectNames": [ "AAA", "BBB" ],
    "AuthEmail": "you@example.com",
    "AuthApiToken": "your-bitbucket-token",
    "PageLen": 50,
    "RetryCount": 4,
    "MaxParallelRequests": 4,
    "UseTruncatedRepositoryNameFallback": true,
    "RepositoryNameOverrides": {}
  },
  "Jira": {
    "BaseUrl": "https://your-company.atlassian.net",
    "Email": "you@example.com",
    "ApiToken": "your-jira-token",
    "AllowedTaskStatuses": [],
    "RetryCount": 6,
    "MaxParallelRequests": 1
  }
}
```

Notes:

- `Bitbucket.ProjectNames` defines Jira project keys to detect in commit messages (for example `AAA-123`).
- `Jira.AllowedTaskStatuses` is optional:
  - empty: show all rows
  - non-empty: keep only rows where all detected Jira statuses are in the allow-list
- `RepositoryNameOverrides` lets you map CSV repository names to actual Bitbucket repository names.

## Expected CSV Format

Example with fictional names:

```csv
container,image
demo-engine,registry.invalid/galaxy.orbit.engine:1.2.3
sample-worker,registry.invalid/moonlight.task.runner:2.4.0
```

Only `container` and `image` are required.