# Commit Conventions

This project follows a structured commit message format to maintain a clear and readable git history. Following these conventions helps with automated changelog generation, easier code reviews, and better collaboration.

## Format

All commit messages should follow this structure:

```
<type>(<scope>): <subject>

[optional body]

[optional footer]
```

Total commit size should stay under 50 words for most commits, but **always** under 100 words even for the most complex commit.

## Types

Use one of the following types to categorize your commit:

- **feat**: A new feature for the user
- **fix**: A bug fix in production code
- **docs**: Documentation changes only
- **style**: Code style changes (formatting, missing semicolons, etc.) with no logic changes
- **refactor**: Code changes that neither fix a bug nor add a feature
- **perf**: Performance improvements
- **test**: Adding, updating, fixing, or refactoring tests (use this for all test-related changes)
- **build**: Changes to build system, dependencies, or project configuration (e.g., NuGet packages, npm dependencies, .csproj files)
- **revert**: Reverts a previous commit

**Note**: Use `test` type for all test changes, including new tests, fixing broken tests, and refactoring test code. The scope (core/aspnetcore/efcore) indicates which type of test.

## Scopes

Use these project-specific scopes to identify the area of change:

### Project Scopes

Scope should correspond to the project where the majority of the change was made. For commits which cross scope, use the scope closest to the user.

- **core**: NuxtIdentity.Core library
- **aspnetcore**: NuxtIdentity.AspNetCore library
- **efcore**: NuxtIdentity.EntityFrameworkCore library
- **tenancy**: NuxtIdentity.Tenancy library (if exists)
- **samples**: Sample applications
- **samples(local)**: Local authentication sample
- **playground**: Playground applications
- **tests(core)**: Core unit tests
- **tests(aspnetcore)**: ASP.NET Core tests
- **tests(efcore)**: Entity Framework Core tests
- **scripts**: PowerShell automation scripts
- **ci**: Changes to CI/CD configuration files and scripts, including release deployment
- **deps**: Dependency updates

## Subject Line

The subject line should:

- Use imperative mood ("add" not "added" or "adds")
- Not capitalize the first letter (makes grep easier)
- Not end with a period
- Be limited to 72 characters
- Be concise but descriptive

### Examples

✅ **Good**:
```
feat(core): add jwt token generation with rotation support
fix(aspnetcore): resolve null reference in controller base
docs(readme): update getting started instructions
feat(efcore): add refresh token entity configuration
feat(aspnetcore): implement user session endpoint
refactor(core): simplify token validation logic
```

❌ **Bad**:
```
Added new feature.
Fixed bug
Update files
```

## Body (Optional)

Include a body when the commit needs additional explanation:

- Separate from subject with a blank line
- Wrap lines at 72 characters
- Explain **what** and **why**, not **how**
- Use bullet points for multiple items

### Example

```
refactor(core): simplify jwt token service configuration

- Extract token validation parameters to separate method
- Remove unused token generation options
- Add XML documentation for public members

This improves testability and makes the service easier to maintain.
```

## Footer (Optional)

Use the footer for:

### Breaking Changes

Prefix with `BREAKING CHANGE:` followed by a description:

```
feat(core)!: redesign token service interface

BREAKING CHANGE: IJwtTokenService.GenerateAccessTokenAsync() now returns
Result<string> instead of string. Update all callers to handle the Result
pattern.
```

Note: The `!` after the type/scope is a visual indicator of a breaking change.

### Issue References

Reference issues that this commit addresses:

```
fix(aspnetcore): correct validation logic for login endpoint

Fixes #123
Closes #456
```

### Co-authors

Credit co-authors when pair programming:

```
feat(core): implement token refresh with rotation

Co-authored-by: Jane Doe <jane@example.com>
```

## Complete Examples

### Simple Feature

```
feat(core): add refresh token service interface
```

### Bug Fix with Details

```
fix(aspnetcore): prevent duplicate token generation on refresh

Check for active refresh tokens before creating new ones to avoid
race conditions in concurrent refresh requests.

Fixes #78
```

### Test Changes

```
test(core): add validation tests for jwt token service
test(aspnetcore): fix flaky controller integration test
test(core): refactor test fixture setup for better performance
```

### Refactoring with Multiple Changes

```
refactor(aspnetcore): restructure authentication controller

- Move token generation logic to helper method
- Extract user info mapping to separate service
- Improve error handling with problem details
- Add comprehensive integration tests

This improves code maintainability and testability while
maintaining the same external API.
```

### Documentation Update

```
docs(readme): update installation instructions for nuget packages
```

### Infrastructure Change

```
build(ci): add automated nuget deployment workflow

Implements continuous deployment to NuGet on release creation.
Includes version synchronization for all library packages.
```

### Sample Application

```
feat(samples): add local authentication sample with sqlite

Demonstrates basic usage of NuxtIdentity with ASP.NET Core
Identity and local authentication provider.
```

## Best Practices

1. **Make atomic commits**: Each commit should represent a single logical change
2. **Commit early and often**: Don't wait until you have a massive changeset
3. **Write meaningful messages**: Future you (and your team) will thank you
4. **Use the body**: Don't be afraid to explain the context and reasoning
5. **Reference issues**: Link commits to issue tracking for better traceability
6. **Review before pushing**: Use `git log` to review your commit messages

## Tools

Consider using these tools to enforce commit conventions:

- **[Commitizen](https://github.com/commitizen/cz-cli)**: Interactive commit message builder
- **[commitlint](https://commitlint.js.org/)**: Lint commit messages
- **[Husky](https://typicode.github.io/husky/)**: Git hooks to enforce conventions

## Resources

- [Conventional Commits Specification](https://www.conventionalcommits.org/)
- [Angular Commit Guidelines](https://github.com/angular/angular/blob/main/CONTRIBUTING.md#commit)
- [How to Write a Git Commit Message](https://chris.beams.io/posts/git-commit/)
