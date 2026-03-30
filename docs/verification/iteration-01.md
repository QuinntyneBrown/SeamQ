# SeamQ Verification Report -- Iteration 01

**Date:** 2026-03-30
**Branch:** main
**Build:** `dotnet build` -- 0 errors, 0 warnings
**Pack:** `dotnet pack` -- produces `SeamQ.1.0.0.nupkg` successfully
**Tool Install:** `dotnet tool install --global` -- succeeds, `seamq` command available on PATH

---

## 1. Command-Level Verification

### 1.1 `seamq --help` -- PASS

Output matches the UI design almost exactly. All 10 commands listed: `scan`, `list`, `generate`, `diagram`, `inspect`, `validate`, `diff`, `init`, `export`, `serve`. All 5 global options listed: `--verbose`, `--quiet`, `--no-color`, `--output-dir`, `--config`. Also shows `--version` and `-?, -h, --help`.

**Minor gap vs. UI design:** The description line says `seamq - static analysis tool for angular workspace interface boundaries` but the UI design uses `seamq` as the root command name whereas the actual usage line says `SeamQ.Cli [command] [options]` (assembly name instead of `seamq`). This is a System.CommandLine default behavior issue.

### 1.2 `seamq --version` -- PARTIAL

**Actual output:**
```
1.0.0+5b3b01032912e16a40d4b45759ec5f6fb456da21
```

**UI design expects:**
```
seamq 1.0.0
.net 8.0.0 | system.commandline 2.0.0
```

**Gap:** The version output uses System.CommandLine's default `--version` handler, which only prints the assembly informational version. The UI design calls for a branded `seamq 1.0.0` header followed by a framework info line. This causes E2E test `Version_ShowsVersionInfo` to fail because `HelpPage.ShowsVersion` checks for `"seamq"` in the output.

### 1.3 `seamq scan <paths>` -- PASS (with gaps)

**Actual output:**
```
[ok] scanned dashboard-framework (2 projects, 12 exports)
[ok] scanned weather-tile-plugin (1 projects, 3 exports)

found 4 seams across 2 workspaces.
```

**Working:** Scans workspaces, counts projects and exports, detects seams, shows summary.

**Gaps vs. UI design:**
1. **Missing hint line.** UI design shows `run 'seamq list' to view detected seams.` after the summary -- not present in implementation.
2. **No error handling for invalid paths.** `seamq scan /nonexistent/path` throws an unhandled `DirectoryNotFoundException` with a full stack trace instead of a user-friendly `[error]` message. This causes E2E test `Scan_WithInvalidPath_ReturnsError` to fail because the `ErrorPage` cannot parse `[error]` from the stack trace output.

### 1.4 `seamq scan --help` -- PASS

Output includes description, usage, arguments (`<paths>`), and all scan-specific options (`--save-baseline`, `--no-cache`, `--exclude`). Matches UI design.

### 1.5 `seamq scan --save-baseline` -- PASS

Creates a baseline JSON file at the specified path. E2E test `Scan_WithSaveBaseline_CreatesBaselineFile` passes.

### 1.6 `seamq list` -- FAIL (state not persisted)

**Actual output (when run as a separate CLI invocation after `scan`):**
```
[!!] No seams in registry. Run 'seamq scan' first.
```

**Root cause:** `SeamRegistry` is an in-memory `ConcurrentDictionary`. Seam data is lost between CLI invocations. The `scan` command populates the registry, but running `list` as a separate process creates a new empty registry.

**Impact:** This is the single most critical architectural gap. Every command except `scan` depends on having seams in the registry. Without cross-invocation persistence (e.g., writing scan results to `.seamq/registry.json` and loading on startup), the tool can only work if all commands are chained in a single process invocation.

**E2E test failures caused:** `List_AfterScan_ShowsDetectedSeams`, `List_WithTypeFilter_FiltersResults`, `List_WithConfidenceFilter_FiltersLowConfidence` -- all fail because E2E tests create a new `SeamQCliDriver` (and thus a new DI container with empty `SeamRegistry`) for each test.

### 1.7 `seamq inspect <seam-id>` -- FAIL (same persistence issue)

```
[error] Seam 'S1' not found. Run 'seamq scan' first.
```

**Exit code:** 0 (should be non-zero for errors -- see Section 2.5)

**E2E test failures:** `Inspect_WithValidSeamId_ShowsContractSurface`, `Inspect_ShowsInterfaces`, `Inspect_WithInvalidSeamId_ReturnsError`

### 1.8 `seamq validate <seam-id>` -- FAIL (persistence + exit code)

Same "Seam not found" error. Exit code 0 when it should be non-zero.

**E2E test failures:** `Validate_WithValidSeam_ShowsResults`, `Validate_WithViolations_ReportsErrors`

`Validate_All_ValidatesAllSeams` passes because it checks `ExitCode.Should().BeOneOf(0, 1)` and gets 0.

### 1.9 `seamq generate <seam-id>` -- FAIL (persistence)

Same "Seam not found" error. Exit code 0.

**E2E test failures:** `Generate_WithSeamId_ProducesMarkdown`, `Generate_WithMultipleFormats_ProducesAll`, `Generate_All_ProducesOutputForAllSeams`

### 1.10 `seamq diff <baseline>` -- PARTIAL FAIL

- `seamq diff /nonexistent/baseline.json` returns `[!!] No seams in registry.` with **exit code 0** instead of an error about the missing file. The command checks for empty registry first (which fails), never reaching the file-existence check.
- **E2E test failure:** `Diff_WithNonexistentBaseline_ReturnsError` expects non-zero exit code, gets 0.

### 1.11 `seamq init` -- PASS

Successfully creates `seamq.config.json` with a sensible default structure. Warns if file already exists. No interactive prompts (gap -- see UI design comparison).

### 1.12 `seamq export` / `seamq diagram` / `seamq serve` -- NOT VERIFIED END-TO-END

These commands all depend on having seams in the registry. Without persistence, they all show the "Seam not found" error. No E2E tests exist for these commands yet.

### 1.13 Unknown Command -- PASS (partial)

`seamq analzye` returns exit code 1 with message: `Unrecognized command or argument 'analzye'`.

**Gap vs. L2-5.11:** No "did you mean?" suggestion shown (L2 spec requires it). System.CommandLine's `UseTypoCorrections()` is presumably wired up (seen in stack trace), but it does not emit a suggestion for this typo.

### 1.14 Missing Required Argument -- PASS

`seamq inspect` (no seam-id) returns exit code 1 with: `Required argument missing for command: 'inspect'.` followed by usage help. This matches L2-5.11.

---

## 2. Systematic Bug List

### 2.1 CRITICAL: SeamRegistry has no cross-invocation persistence

**Files:** `src/SeamQ.Detector/SeamRegistry.cs`, `src/SeamQ.Cli/Commands/ScanCommand.cs`

The registry is purely in-memory. After `scan` completes and the process exits, all detected seams are lost. Every subsequent command (`list`, `inspect`, `validate`, `generate`, `diagram`, `diff`, `export`) requires seams to be in the registry but starts with an empty one.

**Fix:** After `scan`, serialize the registry to `.seamq/registry.json` (or a configurable path). On startup of any command, load the registry from disk if the file exists.

### 2.2 HIGH: `--no-color` global option has no effect

**File:** `src/SeamQ.Cli/CommandBuilder.cs`

The `--no-color` option is registered as a global option but is never read by any middleware or passed to the `ConsoleRenderer`. The renderer's `UseColor` property stays `true` regardless.

**Fix:** Add a middleware or invocation handler that reads the `--no-color` parse result and sets `renderer.UseColor = false`.

### 2.3 HIGH: Error conditions return exit code 0

**Files:** All command handlers in `src/SeamQ.Cli/Commands/`

When a command writes an error via `renderer.WriteError(...)` or `renderer.WriteWarning(...)` and returns early, the `SetHandler` lambda returns normally (exit code 0). L2-10.5 requires exit code 1 for partial failure and exit code 2 for fatal errors.

**Affected commands:** `inspect`, `validate`, `generate`, `diagram`, `diff`, `export` -- all return 0 on "seam not found" or "no seams in registry".

**Fix:** Set `InvocationContext.ExitCode = 1` (or 2) before returning from error paths, or throw a specific exception that the exception handler middleware catches and maps to exit codes.

### 2.4 HIGH: `scan` does not catch `DirectoryNotFoundException`

**File:** `src/SeamQ.Cli/Commands/ScanCommand.cs`

When an invalid workspace path is provided, `WorkspaceScanner.ScanAsync` throws `DirectoryNotFoundException`. The scan command handler does not catch this, so it propagates as an unhandled exception with a full stack trace. L2-1.1 requires "a clear error message with the invalid path".

**Fix:** Wrap the `scanner.ScanAsync` call in try/catch for `DirectoryNotFoundException` and display `[error] workspace directory not found: <path>`.

### 2.5 MEDIUM: `--verbose`, `--quiet`, `--output-dir`, `--config` global options not wired up

**File:** `src/SeamQ.Cli/CommandBuilder.cs`

All global options are registered but none are consumed by any middleware or command handler. `--config` should affect `ConfigLoader.Load()`. `--output-dir` should override `config.Output.Directory`. `--verbose` should increase log verbosity. `--quiet` should suppress non-error output.

### 2.6 MEDIUM: `--version` output does not match UI design

**File:** `src/SeamQ.Cli/Program.cs` (uses default System.CommandLine version handler)

Actual: `1.0.0+<hash>`
Design: `seamq 1.0.0` + `.net 8.0.0 | system.commandline 2.0.0`

### 2.7 MEDIUM: `init` command does not use interactive prompts

**File:** `src/SeamQ.Cli/Commands/InitCommand.cs`

The UI design shows an interactive wizard with `? add a workspace path:`, `? alias for this workspace:`, `? role:`, etc. The actual implementation writes a default config without any prompts (L2-5.8 requires interactive prompts).

### 2.8 MEDIUM: Confidence displayed as percentage, not decimal

**File:** `src/SeamQ.Cli/Commands/ListCommand.cs:75`, `src/SeamQ.Cli/Commands/InspectCommand.cs:51`

The list command formats confidence as `s.Confidence.ToString("P0")` which produces `95%` instead of `0.95` as shown in the UI design. The inspect command does the same.

### 2.9 LOW: Scan output missing hint line

**File:** `src/SeamQ.Cli/Commands/ScanCommand.cs`

The UI design shows `run 'seamq list' to view detected seams.` after the scan summary. This hint is not emitted.

### 2.10 LOW: Root command name shows `SeamQ.Cli` instead of `seamq`

System.CommandLine defaults to the executable name from the entry assembly. When run via `dotnet run --project src/SeamQ.Cli`, this shows as `SeamQ.Cli` in help text. When installed as a global tool, it shows the correct name.

### 2.11 LOW: No E2E tests for `diagram`, `export`, or `serve` commands

Only `scan`, `list`, `inspect`, `validate`, `generate`, `diff`, and `error-handling` have E2E test coverage. Three commands lack any test coverage.

### 2.12 LOW: Unit and Integration test projects are empty

`test/SeamQ.Tests.Unit/` and `test/SeamQ.Tests.Integration/` compile but contain no test classes. `dotnet test` reports "No test is available."

---

## 3. E2E Test Results Summary

```
Total tests: 23
     Passed:  9
     Failed: 14
```

### Passing Tests (9)

| Test | Why it passes |
|------|---------------|
| `ScanCommandTests.Scan_WithValidWorkspaces_ReturnsSuccess` | Scan works correctly |
| `ScanCommandTests.Scan_WithSaveBaseline_CreatesBaselineFile` | Baseline save works |
| `ScanCommandTests.Scan_WithNoArguments_UsesConfig` | Accepts exit codes 0/1/2 |
| `GlobalOptionsTests.Help_ShowsAllCommands` | Help text is correct |
| `GlobalOptionsTests.Help_ShowsGlobalOptions` | Global options listed |
| `ErrorHandlingTests.UnknownCommand_ShowsSuggestion` | Exit code 1 for unknown command |
| `ValidateCommandTests.Validate_All_ValidatesAllSeams` | Accepts exit codes 0/1 (gets 0) |
| `DiffCommandTests.Diff_WithBaseline_ShowsChanges` | Accepts exit codes 0/1/2 (gets 0) |
| `ErrorHandlingTests.MissingRequiredArgument_ShowsUsage` | Exit code 1 for missing arg |

### Failing Tests (14) -- Grouped by Root Cause

**Root cause: SeamRegistry not persisted across test invocations (9 tests)**

Tests that create a fresh `SeamQCliDriver` instance and call `list`/`inspect`/`validate`/`generate` without first calling `scan` in the same driver instance:

- `ListCommandTests.List_AfterScan_ShowsDetectedSeams` -- SeamCount is 0
- `ListCommandTests.List_WithTypeFilter_FiltersResults` -- empty rows
- `ListCommandTests.List_WithConfidenceFilter_FiltersLowConfidence` -- empty rows
- `InspectCommandTests.Inspect_WithValidSeamId_ShowsContractSurface` -- seam not found
- `InspectCommandTests.Inspect_ShowsInterfaces` -- seam not found
- `InspectCommandTests.Inspect_WithInvalidSeamId_ReturnsError` -- exit code 0 (not non-zero)
- `ValidateCommandTests.Validate_WithValidSeam_ShowsResults` -- seam not found
- `ValidateCommandTests.Validate_WithViolations_ReportsErrors` -- ResultSummary is null
- `GenerateCommandTests.Generate_WithSeamId_ProducesMarkdown` -- seam not found

Note: Even within a single `SeamQCliDriver` instance, each `RunAsync` call creates a new DI container, so state is not shared. The tests would need either (a) registry persistence to disk, or (b) a shared DI container across calls within the same driver instance.

**Root cause: Version output format mismatch (1 test)**

- `GlobalOptionsTests.Version_ShowsVersionInfo` -- output `1.0.0+hash` does not contain "seamq"

**Root cause: Error exit code is 0 instead of non-zero (2 tests)**

- `DiffCommandTests.Diff_WithNonexistentBaseline_ReturnsError` -- exit code 0
- `ScanCommandTests.Scan_WithInvalidPath_ReturnsError` -- ErrorPage.ErrorMessage is null (stack trace instead of `[error]` message)

**Root cause: Generate output format issue (2 tests)**

- `GenerateCommandTests.Generate_WithMultipleFormats_ProducesAll` -- no seams (persistence)
- `GenerateCommandTests.Generate_All_ProducesOutputForAllSeams` -- no seams (persistence)

---

## 4. Gaps vs. UI Design (docs/ui-design.pen)

| Screen | Gap | Severity |
|--------|-----|----------|
| `seamq --help` | Usage line shows `SeamQ.Cli` instead of `seamq` when run via `dotnet run` | Low |
| `seamq --version` | Missing branded header (`seamq 1.0.0`) and framework info line | Medium |
| `seamq scan` | Missing hint: `run 'seamq list' to view detected seams.` | Low |
| `seamq scan` | Unhandled exception for invalid paths instead of `[error]` message | High |
| `seamq list` | Cannot verify: registry not persisted. Confidence format uses `P0` (percentage) instead of decimal `0.95` | High / Medium |
| `seamq inspect` | Cannot verify: registry not persisted. Inspect header uses `WriteHeader(seam.Name)` which produces `-- tile_plugin_contract --` but design shows `-- seam S1: tile_plugin_contract --` (includes ID prefix) | High / Medium |
| `seamq validate` | Cannot verify: registry not persisted. Design shows per-consumer sections with `[ok]`/`[!!]` per element + `result: N errors, N warnings` summary. Code structure looks correct but untestable. | High |
| `seamq generate` | Cannot verify: registry not persisted. Design shows `>>` file paths and statistics. `GenerateResultPage` parses `>>` markers but `GenerateCommand` uses `WriteMuted` without `>>` prefix. | High / Medium |
| `seamq diagram` | Cannot verify: registry not persisted. Design shows categorized output (class/sequence/state/c4 with counts). | High |
| `seamq diff` | Cannot verify: registry not persisted. Design shows `[+added]`, `[~modified]`, `[-removed]` markers. Code uses `[ok] +`, `[error] -`, `[!!] ~` prefixes from `WriteSuccess`/`WriteError`/`WriteWarning`. Marker format does not match design. | High / Medium |
| `seamq init` | Not interactive. Design shows wizard prompts (`?` prefix). Implementation writes defaults without prompting. | Medium |
| `seamq export` | Cannot verify: registry not persisted. Design shows `>>` file paths for contract-surface, data-dictionary, traceability-matrix. Implementation uses `WriteMuted` without `>>` prefix. | High |
| `seamq serve` | Cannot verify end-to-end. Design shows ICD listing. Implementation is a basic file server without ICD index generation. | Medium |

---

## 5. Gaps vs. L2 Requirements

### L2-5.x CLI Commands -- Implementation Status

| L2 Req | Command | Status | Notes |
|--------|---------|--------|-------|
| L2-5.1 | `scan` | Partial | Scan works but: no graceful error on bad paths, missing hint line, `--no-cache`/`--exclude` accepted but effectiveness not verified |
| L2-5.2 | `list` | Blocked | Cannot verify due to persistence. Code structure has `--type`, `--provider`, `--confidence` filters. |
| L2-5.3 | `generate` | Blocked | Cannot verify due to persistence. Code structure supports `--format` and `--all`. |
| L2-5.4 | `diagram` | Blocked | Cannot verify due to persistence. Code structure supports `--type` and `--all`. |
| L2-5.5 | `inspect` | Blocked | Cannot verify due to persistence. Code structure shows grouped elements. |
| L2-5.6 | `validate` | Blocked | Cannot verify due to persistence. Code structure shows per-consumer results. |
| L2-5.7 | `diff` | Blocked | Cannot verify due to persistence + missing file not caught when registry empty. |
| L2-5.8 | `init` | Partial | Creates config file but is not interactive (L2 requires prompts). |
| L2-5.9 | `export` | Blocked | Cannot verify due to persistence. |
| L2-5.10 | Global options | Fail | `--verbose`, `--quiet`, `--no-color`, `--output-dir`, `--config` all registered but none are wired to actual behavior. |
| L2-5.11 | Help/errors | Partial | Help text good. Missing "did you mean?" suggestion for unknown commands. |
| L2-5.12 | `serve` | Partial | HTTP server implemented. No ICD index page generation. |

### L2-10.5 Exit Codes

| Condition | Expected | Actual |
|-----------|----------|--------|
| Success | 0 | 0 -- correct |
| Invalid path (scan) | 1 or 2 | 1 (via unhandled exception) -- accidental correctness |
| Seam not found | 1 | 0 -- WRONG |
| No seams in registry | 1 | 0 -- WRONG |
| Missing baseline file | 2 | 0 -- WRONG |
| Unknown command | 1 | 1 -- correct |
| Missing required argument | 1 | 1 -- correct |

### L2-10.3 Graceful Degradation

**Not implemented.** If one workspace in a multi-workspace scan fails, the entire scan aborts with an unhandled exception. L2 requires remaining workspaces to continue with partial results.

### Other L2 Gaps Not Testable Without Persistence

L2-1.x (scanning depth), L2-2.x (detection accuracy), L2-3.x (ICD generation quality), L2-4.x (diagram generation quality), L2-6.x (config loading from `--config`), L2-7.x (baseline diffing), L2-8.x (validation accuracy) -- all require the registry persistence fix before they can be meaningfully end-to-end verified.

---

## 6. Prioritized Fix List

1. **P0 -- Registry persistence.** Write scan results to `.seamq/registry.json`; load on startup. This unblocks ALL other commands and 9 of 14 failing E2E tests.
2. **P0 -- Exit codes.** Set `InvocationContext.ExitCode` to 1 or 2 on error paths. Fixes 2+ tests and L2-10.5.
3. **P1 -- Error handling in scan.** Catch `DirectoryNotFoundException` and show `[error]` message. Implement graceful degradation (L2-10.3). Fixes 1 test.
4. **P1 -- Wire global options.** Read `--no-color`, `--verbose`, `--quiet`, `--output-dir`, `--config` and apply them. L2-5.10.
5. **P2 -- Version output.** Custom `--version` handler for branded output. Fixes 1 test.
6. **P2 -- Confidence format.** Change `"P0"` to `"F2"` for decimal display. L2-5.2 / UI design alignment.
7. **P2 -- Inspect header.** Include seam ID in header: `seam S1: tile_plugin_contract`. UI design alignment.
8. **P2 -- Scan hint line.** Add `run 'seamq list' to view detected seams.` after scan summary.
9. **P2 -- Generate/export `>>` file markers.** Use `>>` prefix on file paths per UI design.
10. **P2 -- Diff change markers.** Use `[+added]`, `[~modified]`, `[-removed]` per UI design.
11. **P3 -- Interactive init.** Add prompts for workspace paths, aliases, roles, output settings.
12. **P3 -- Missing E2E tests.** Add tests for `diagram`, `export`, `serve` commands.
13. **P3 -- Unit/Integration tests.** Populate the empty test projects.
14. **P3 -- "Did you mean?" suggestions.** Verify System.CommandLine typo correction middleware works.
