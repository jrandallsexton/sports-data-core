# Claude Code Configuration

This directory contains configuration files for [Claude Code](https://claude.ai/claude-code), Anthropic's CLI tool for AI-assisted software development.

## Files

### `settings.local.json`

Local settings that configure Claude Code's behavior for this repository. This file is typically **not committed to version control** as it may contain user-specific preferences.

## Permissions Configuration

The `permissions.allow` array defines which commands Claude Code can execute without prompting for user confirmation each time.

### Format

```json
{
  "permissions": {
    "allow": [
      "ToolName(command:pattern)"
    ]
  }
}
```

### Current Permissions

| Permission | Description |
|------------|-------------|
| `Bash(dotnet build:*)` | Allows `dotnet build` commands with any arguments |
| `Bash(dotnet test:*)` | Allows `dotnet test` commands with any arguments |

### Pattern Syntax

- `*` - Wildcard matching any characters
- Patterns match the beginning of commands
- Multiple patterns can be specified in the array

### Common Examples

```json
{
  "permissions": {
    "allow": [
      "Bash(dotnet build:*)",
      "Bash(dotnet test:*)",
      "Bash(npm run build:*)",
      "Bash(npm test:*)",
      "Bash(git status:*)",
      "Bash(git diff:*)"
    ]
  }
}
```

## Adding/Removing Entries

1. Open `settings.local.json` in your editor
2. Add new patterns to the `permissions.allow` array
3. Save the file - changes take effect immediately

**To add a permission:**
```json
"Bash(your-command:*)"
```

**To remove a permission:**
Simply delete the line from the array.

## Security Considerations

- **Review before committing**: If you choose to commit this file, review all permissions carefully
- **Principle of least privilege**: Only allow commands that are necessary for your workflow
- **Avoid broad patterns**: Prefer specific command patterns over overly permissive wildcards
- **Sensitive operations**: Never auto-allow commands that could expose secrets or modify critical infrastructure
- **Team awareness**: Ensure team members understand what permissions are granted

### Commands to NEVER auto-allow

- `Bash(rm -rf:*)` - Dangerous file deletion
- `Bash(git push --force:*)` - Destructive git operations
- `Bash(*secret*:*)` - Commands involving secrets
- `Bash(*password*:*)` - Commands involving passwords

## Workflow Notes

### Who should edit this file?

- Individual developers for personal workflow preferences
- Team leads when establishing shared baseline permissions (if committed)

### When is this file used?

- Claude Code reads this file when starting a session in this repository
- Changes are applied immediately without restart

### Local vs Committed

**Option 1: Keep local (recommended)**
- Add `.claude/settings.local.json` to `.gitignore`
- Each developer maintains their own preferences

**Option 2: Commit shared settings**
- Establish team-agreed baseline permissions
- Review in code review like any other configuration
- Individual devs can override with additional local settings

### Resetting to defaults

Delete the file to reset to default behavior (prompt for all commands):

```bash
rm .claude/settings.local.json
```

## Additional Resources

- [Claude Code Documentation](https://docs.anthropic.com/en/docs/claude-code)
- [Claude Code GitHub](https://github.com/anthropics/claude-code)
