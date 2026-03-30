# Detailed Design: Contract Validation Module

**Module:** `SeamQ.Validator`
**Target Framework:** .NET 8
**Status:** Draft
**Last Updated:** 2026-03-30

---

## 1. Overview

The Contract Validation module verifies that consumer workspaces correctly implement the contracts defined by provider workspaces. After seam detection has identified the interface boundaries between workspaces, the validator applies a configurable set of rules against each consumer to determine compliance.

Validation is:

- **Rule-based** -- each aspect of contract compliance (interface implementation, injection tokens, input/output bindings) is encapsulated in its own `IValidationRule` implementation.
- **Per-consumer** -- results are scoped to each consumer workspace so that teams can identify exactly which consumers are out of compliance and why.
- **Severity-aware** -- each finding is classified as Error, Warning, or Info so that CI pipelines can gate on errors while surfacing warnings for human review.

### Validation Pipeline

```
Seam (provider contract)
  |
  v
ContractValidator (orchestrator)
  |--- for each consumer workspace
  |       |--- for each IValidationRule
  |       |       |--- InterfaceImplementationRule
  |       |       |--- InjectionTokenRule
  |       |       |--- InputOutputRule
  |       |       v
  |       |    RuleFinding[]
  |       v
  |    ValidationResult (per consumer)
  v
ValidationReport (aggregate)
```

---

## 2. Component Diagrams

### 2.1 Validator Class Diagram

![Validator Class Diagram](diagrams/class-validator.png)

### 2.2 Validation Result Class Diagram

![Validation Result Class Diagram](diagrams/class-validation-result.png)

### 2.3 C4 Component Diagram

![C4 Component Diagram - Validator Internals](diagrams/c4-component-validator.png)

---

## 3. Sequence Diagrams

### 3.1 Validate Seam Sequence

![Validate Seam Sequence](diagrams/seq-validate-seam.png)

### 3.2 Interface Implementation Rule Sequence

![Interface Implementation Rule Sequence](diagrams/seq-interface-rule.png)

---

## 4. State Diagram

### 4.1 Validation Progress States

![Validation Progress State Diagram](diagrams/state-validation.png)

---

## 5. Component Descriptions

### 5.1 IContractValidator / ContractValidator

**Responsibility:** Top-level orchestrator that runs all registered validation rules against every consumer of a given seam and assembles the final report.

`IContractValidator` defines the contract:

```csharp
public interface IContractValidator
{
    Task<ValidationReport> ValidateAsync(
        DetectedSeam seam,
        IReadOnlyList<WorkspaceModel> consumerWorkspaces,
        ValidatorOptions options,
        CancellationToken ct);

    Task<ValidationReport> ValidateAllAsync(
        SeamRegistry registry,
        IReadOnlyList<WorkspaceModel> allWorkspaces,
        ValidatorOptions options,
        CancellationToken ct);
}
```

`ContractValidator` implements the interface:

1. Accepts a `DetectedSeam` (the provider contract) and the list of consumer workspaces that reference it.
2. Iterates over each consumer workspace.
3. For each consumer, iterates over the registered `IValidationRule` instances and invokes `EvaluateAsync`.
4. Collects all `RuleFinding` objects returned by each rule.
5. Builds a `ValidationResult` for the consumer containing the aggregated findings.
6. After all consumers are processed, assembles a `ValidationReport` with summary counts.

The `ValidateAllAsync` overload iterates the full `SeamRegistry` and calls `ValidateAsync` for each seam, merging the results into a single comprehensive report.

Rules are injected via the constructor as `IEnumerable<IValidationRule>`, allowing rules to be added, removed, or reordered through DI configuration.

### 5.2 IValidationRule

**Responsibility:** Common interface for all contract compliance checks.

```csharp
public interface IValidationRule
{
    string RuleId { get; }
    string DisplayName { get; }
    Task<IReadOnlyList<RuleFinding>> EvaluateAsync(
        DetectedSeam seam,
        WorkspaceModel consumerWorkspace,
        CancellationToken ct);
}
```

Each rule:

- Has a stable `RuleId` (e.g., `"CV001"`) used in structured output and suppression configuration.
- Has a human-readable `DisplayName` for report rendering.
- Receives the full `DetectedSeam` (which contains the provider contract surface) and one consumer `WorkspaceModel`.
- Returns zero or more `RuleFinding` objects describing compliance issues or confirmations.

Rules are stateless and thread-safe, enabling parallel evaluation when multiple consumers are validated concurrently.

### 5.3 InterfaceImplementationRule

**Responsibility:** Checks that consumer workspaces implement all interfaces defined in the provider contract.

**Rule ID:** `CV001`

Evaluation steps:

1. Extracts the list of required interfaces from the seam's contract surface (e.g., `IPluginModule`, `IRouteProvider`).
2. Searches the consumer workspace's parsed AST for classes that declare `implements <InterfaceName>`.
3. For each required interface:
   - If no implementing class is found, emits an **Error** finding: "Consumer does not implement required interface `IFoo`."
   - If an implementing class is found, compares the class's declared methods and properties against the interface's members.
   - For each missing method, emits an **Error** finding: "Class `BarComponent` is missing method `initialize(config: PluginConfig): void` required by `IPluginModule`."
   - For each method present but with a mismatched signature (different parameter types or return type), emits a **Warning** finding with details of the expected vs. actual signature.
4. If all interfaces are fully implemented, emits an **Info** finding confirming compliance.

Type matching uses structural comparison of the parsed type strings. Generic type parameters are compared by position and constraint where available.

### 5.4 InjectionTokenRule

**Responsibility:** Checks that consumer workspaces provide all required injection tokens declared in the provider contract.

**Rule ID:** `CV002`

Evaluation steps:

1. Extracts the list of `InjectionToken` declarations from the seam's contract surface. These are tokens the provider expects consumers to supply (e.g., `PLUGIN_CONFIG`, `FEATURE_FLAGS`).
2. Searches the consumer workspace for provider registrations:
   - `providers: [{ provide: TOKEN_NAME, useValue: ... }]` in component/module metadata.
   - `{ provide: TOKEN_NAME, useFactory: ... }` patterns.
   - `provideIn: 'root'` services that satisfy the token.
   - `inject(TOKEN_NAME)` usage that implies the token must be provided upstream.
3. For each required token:
   - If no provider registration is found, emits an **Error** finding: "Consumer does not provide required injection token `PLUGIN_CONFIG`."
   - If a provider is found but the provided type does not match the token's declared type, emits a **Warning** finding with the expected vs. actual type.
   - If `useFactory` is detected but the return type cannot be statically determined, emits an **Info** finding noting that runtime verification may be needed.
4. Tokens marked as optional in the contract (via `@Optional()` decorator usage in the provider) produce **Warning** findings instead of **Error** when missing.

### 5.5 InputOutputRule

**Responsibility:** Checks that consumer workspaces correctly bind to component inputs and outputs declared in the provider contract.

**Rule ID:** `CV003`

Evaluation steps:

1. Extracts the list of component `@Input()` and `@Output()` declarations from the seam's contract surface. This includes:
   - Decorator-based inputs/outputs (`@Input()`, `@Output()`).
   - Signal-based inputs/outputs (`input()`, `input.required()`, `output()`).
   - Two-way binding models (`model()`).
2. Searches consumer templates (inline or external) for usages of the provider component's selector.
3. For each template usage:
   - Checks that all `required` inputs are bound (via `[inputName]="..."` or signal binding syntax). Missing required inputs emit an **Error** finding.
   - Checks that bound input names match declared input names (accounting for aliases). Unknown input names emit a **Warning** finding: "Consumer binds to unknown input `foo` on component `<app-widget>`."
   - Checks output event handler bindings (`(outputName)="..."`) reference declared outputs. Unknown output names emit a **Warning** finding.
   - For inputs with declared types, performs basic type compatibility checking between the bound expression's inferred type and the declared input type. Mismatches emit a **Warning** finding.
4. If the consumer does not use the provider component at all (no template references to its selector), emits an **Info** finding noting no bindings to validate.

Type checking for template expressions is best-effort since full template type-checking requires the Angular compiler. The rule focuses on structural mismatches detectable from static analysis of the parsed AST.

### 5.6 ValidationResult

**Responsibility:** Holds the validation outcome for a single consumer workspace against a single seam.

```csharp
public record ValidationResult
{
    public string SeamId { get; init; }
    public string ConsumerWorkspace { get; init; }
    public IReadOnlyList<RuleFinding> Findings { get; init; }
    public bool Passed => Findings.All(f => f.Severity != ValidationSeverity.Error);
    public int ErrorCount => Findings.Count(f => f.Severity == ValidationSeverity.Error);
    public int WarningCount => Findings.Count(f => f.Severity == ValidationSeverity.Warning);
    public int InfoCount => Findings.Count(f => f.Severity == ValidationSeverity.Info);
}
```

`RuleFinding` is a record containing:

```csharp
public record RuleFinding
{
    public string RuleId { get; init; }
    public string RuleName { get; init; }
    public ValidationSeverity Severity { get; init; }
    public string Message { get; init; }
    public string? ContractElement { get; init; }
    public string? ConsumerFile { get; init; }
    public int? ConsumerLine { get; init; }
    public string? ExpectedSignature { get; init; }
    public string? ActualSignature { get; init; }
}
```

Each finding links back to the specific contract element (interface name, token name, input/output name) and optionally references the consumer source location (file path and line number) where the issue was detected or where the implementation should exist.

### 5.7 ValidationSeverity

**Responsibility:** Classifies the urgency of each finding.

```csharp
public enum ValidationSeverity
{
    Error,
    Warning,
    Info
}
```

| Level | Meaning | CI Gate |
|---|---|---|
| `Error` | Contract violation that will cause runtime failure. Consumer must fix before integration. | Fails the pipeline. |
| `Warning` | Potential issue that may indicate a problem but does not guarantee runtime failure. Examples include type mismatches that could be resolved by implicit conversions, or optional tokens that are not provided. | Reported but does not fail. |
| `Info` | Informational note confirming compliance or noting a condition that could not be fully verified statically. | Reported only in verbose mode. |

### 5.8 ValidationReport

**Responsibility:** Aggregates all `ValidationResult` entries across consumers and seams into a single report with summary statistics.

```csharp
public record ValidationReport
{
    public DateTime GeneratedAt { get; init; }
    public IReadOnlyList<ValidationResult> Results { get; init; }
    public int TotalSeams { get; init; }
    public int TotalConsumers { get; init; }
    public int PassedCount => Results.Count(r => r.Passed);
    public int FailedCount => Results.Count(r => !r.Passed);
    public int TotalErrors => Results.Sum(r => r.ErrorCount);
    public int TotalWarnings => Results.Sum(r => r.WarningCount);
    public int TotalInfos => Results.Sum(r => r.InfoCount);
    public bool AllPassed => Results.All(r => r.Passed);
}
```

The report also provides convenience methods:

```csharp
public IEnumerable<ValidationResult> GetResultsForSeam(string seamId);
public IEnumerable<ValidationResult> GetResultsForConsumer(string workspaceName);
public IEnumerable<RuleFinding> GetAllFindings(ValidationSeverity? severity = null);
```

The report is serialisable to JSON for machine consumption and is also rendered in human-readable table format by the CLI `validate` command.

---

## 6. Rule Extensibility

The validation engine is designed for extensibility. New rules can be added by:

1. Implementing `IValidationRule`.
2. Registering the implementation in the DI container.

No changes to `ContractValidator` are required. The orchestrator discovers all registered rules at construction time.

Rules can also be selectively enabled or disabled via `ValidatorOptions`:

```csharp
public class ValidatorOptions
{
    public HashSet<string> EnabledRuleIds { get; set; }
    public HashSet<string> SuppressedRuleIds { get; set; }
    public ValidationSeverity MinimumSeverity { get; set; } = ValidationSeverity.Info;
    public bool TreatWarningsAsErrors { get; set; } = false;
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;
}
```

When `EnabledRuleIds` is non-empty, only those rules run. `SuppressedRuleIds` excludes specific rules. `TreatWarningsAsErrors` promotes all warnings to errors for stricter CI gates.

---

## 7. Error Handling Strategy

The validator uses a **fail-soft** approach consistent with the scanner module:

- Individual rule failures (unexpected exceptions) are caught and recorded as an **Error** finding with the message "Rule `{RuleId}` threw an unexpected exception: {message}". Validation continues with remaining rules.
- If a consumer workspace's AST is incomplete (e.g., some files failed to parse during scanning), rules operate on the available data and emit **Warning** findings noting that the analysis may be incomplete.
- If the seam's contract surface is empty (no interfaces, tokens, or inputs/outputs to validate), the validator emits a single **Info** finding and returns a passing result.

---

## 8. Concurrency

- Consumer workspaces are validated in parallel using `Parallel.ForEachAsync` with a configurable `MaxDegreeOfParallelism` from `ValidatorOptions`.
- All `IValidationRule` implementations are required to be stateless and thread-safe.
- `RuleFinding` and `ValidationResult` are immutable records, eliminating shared mutable state.
- The final `ValidationReport` is assembled on a single thread after all parallel work completes.

---

## 9. Configuration

Relevant settings from `seamq.config.json` under the `validation` key:

| Setting | Type | Default | Description |
|---|---|---|---|
| `enabledRules` | `string[]` | `[]` (all rules) | Rule IDs to enable. Empty means all. |
| `suppressedRules` | `string[]` | `[]` | Rule IDs to suppress. |
| `minimumSeverity` | `string` | `"Info"` | Minimum severity to include in reports. |
| `treatWarningsAsErrors` | `bool` | `false` | Promote warnings to errors. |
| `maxParallelism` | `int` | CPU count | Max concurrent consumer validations. |

---

## 10. Dependencies

| Dependency | Purpose |
|---|---|
| `SeamQ.Scanner` | Provides `WorkspaceModel` and parsed AST types. |
| `SeamQ.Detector` | Provides `DetectedSeam` and `SeamRegistry` types. |
| `Microsoft.Extensions.Logging` | Structured logging throughout the validation pipeline. |
| `Microsoft.Extensions.DependencyInjection` | Rule registration and discovery via DI. |
| `System.Text.Json` | Serialisation of `ValidationReport` to JSON output. |
