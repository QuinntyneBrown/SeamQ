# Contributing to SeamQ

Thank you for your interest in contributing to SeamQ! This guide will help you get started.

## Getting Started

### Prerequisites

- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) or later
- Git

### Setup

1. Fork the repository
2. Clone your fork:
   ```bash
   git clone https://github.com/<your-username>/SeamQ.git
   cd SeamQ
   ```
3. Build the solution:
   ```bash
   dotnet build
   ```
4. Run the tests:
   ```bash
   dotnet test
   ```

## Development Workflow

1. Create a branch from `main`:
   ```bash
   git checkout -b feature/your-feature-name
   ```
2. Make your changes
3. Ensure the build passes with zero warnings (warnings are treated as errors):
   ```bash
   dotnet build
   ```
4. Run all tests:
   ```bash
   dotnet test
   ```
5. Commit your changes with a clear message
6. Push to your fork and open a pull request

## Project Structure

```
src/
  SeamQ.Cli/          CLI entry point (System.CommandLine, one file per command)
  SeamQ.Core/         Core domain models and abstractions (no external dependencies)
  SeamQ.Scanner/      Workspace discovery and TypeScript parsing
  SeamQ.Detector/     Seam detection engine (strategy pattern)
  SeamQ.Generator/    ICD document generation (Markdown, HTML)
  SeamQ.Renderer/     PlantUML and C4 diagram generation
  SeamQ.Differ/       Baseline comparison
  SeamQ.Validator/    Contract compliance checking

test/
  SeamQ.Tests.Unit/         Unit tests
  SeamQ.Tests.Integration/  Pipeline integration tests
  SeamQ.Tests.E2E/          End-to-end CLI tests
  fixtures/                 Angular workspace test fixtures
```

## Coding Guidelines

- Target .NET 8 with C# latest language version
- Nullable reference types are enabled -- avoid `null` where possible
- Warnings are treated as errors -- fix all warnings before submitting
- Follow existing code style and naming conventions
- Keep `SeamQ.Core` free of external NuGet dependencies
- Use constructor injection via `Microsoft.Extensions.DependencyInjection`
- One file per CLI command in `SeamQ.Cli/Commands/`
- One file per detection strategy in `SeamQ.Detector/Strategies/`
- One file per ICD section in `SeamQ.Generator/Sections/`
- One file per validation rule in `SeamQ.Validator/Rules/`

## Adding a New Seam Detection Strategy

1. Create a class implementing `ISeamDetectionStrategy` in `SeamQ.Detector/Strategies/`
2. Register it in `DetectorServiceCollectionExtensions.cs`
3. Add unit tests in `test/SeamQ.Tests.Unit/Detector/`

## Adding a New ICD Section

1. Create a class implementing `IIcdSection` in `SeamQ.Generator/Sections/`
2. Register it in `GeneratorServiceCollectionExtensions.cs`
3. Add unit tests in `test/SeamQ.Tests.Unit/Generator/Sections/`

## Adding a New Validation Rule

1. Create a class implementing `IValidationRule` in `SeamQ.Validator/Rules/`
2. Register it in `ValidatorServiceCollectionExtensions.cs`
3. Add unit tests in `test/SeamQ.Tests.Unit/Validator/`

## Test Fixtures

Test fixtures are Angular workspaces in `test/fixtures/`. When adding new detection capabilities, consider whether an existing fixture covers your case or a new one is needed.

## Reporting Issues

- Use GitHub Issues to report bugs or request features
- Include steps to reproduce, expected vs. actual behavior, and your .NET version

## Pull Requests

- Keep PRs focused on a single change
- Reference any related issues
- Ensure all tests pass before requesting review
- Add tests for new functionality

## License

By contributing, you agree that your contributions will be licensed under the [MIT License](LICENSE).
