---
name: spec
description: When asked to do any kind of code generation or design work, follow the Spec process to generate requirements and implement them with a team of agents.
---

# Spec Skill

When asked to do any kind of code generation or design work, follow this process:

## Step 0: Determine Task Type

Classify the request as one of:

- **Code** — generating, modifying, or building software (source files, tests, configs)
- **Design** — creating or modifying visual designs in `.pen` files (screens, layouts, components, pages)

This classification drives which workflow applies in Step 2.

## Step 1: Requirements Generation

1. Create `docs/specs` folder in the project root if it does not already exist
2. If `docs/specs/L1.md` and `docs/specs/L2.md` already exist, read them first — you will update them rather than overwrite them
3. Create or update `docs/specs/L1.md` containing high-level requirements for the ask
   - L1 requirements are extreme high-level descriptions of what the system shall do
   - Each L1 requirement has a unique identifier (e.g., `L1-1`, `L1-2`)
   - If the file already exists, update it in place — do not recreate from scratch
4. Create or update `docs/specs/L2.md` containing detailed requirements that trace to L1 requirements
   - Each L2 requirement traces to exactly one L1 requirement via its identifier
   - Each L2 requirement includes acceptance criteria defining done
   - For design tasks, acceptance criteria should describe visual and structural outcomes rather than automated test assertions
   - If the file already exists, update it in place — do not recreate from scratch
5. Changes to existing requirements are **append-only**:
   - Never delete existing L1 or L2 requirements
   - Never remove existing acceptance criteria
   - Existing requirement text may be broadened but never narrowed
   - New requirements and acceptance criteria may be added freely

## Step 2: Team-Based Implementation Process

For each L1 requirement, create a team of agents:

### Code Tasks

#### Implementor Agent

Implement the L1 requirement:

- Systematically implement each L2 requirement for the assigned L1 and then mark as complete
- Create a failing acceptance test for each acceptance criteria for the L2 requirement
- Implement the minimal code to make the acceptance test pass

#### Quality Assurance Agent

For each completed L2 requirement marked as complete:

- Verify the implementation is complete — no TODOs or placeholder code
- Verify all acceptance tests pass
- Mark the L2 as verified, or reject it with feedback so the Implementor can fix

### Design Tasks

#### Implementor Agent

Implement the L1 requirement in the `.pen` file:

- Systematically implement each L2 requirement for the assigned L1 using the Pencil MCP tools and then mark as complete
- Use `get_guidelines`, `get_style_guide_tags`, and `get_style_guide` to follow proper design conventions
- Build the design using `batch_design` operations (insert, update, copy, replace, etc.)
- Take a screenshot with `get_screenshot` after completing each L2 to visually confirm the result

#### Quality Assurance Agent

For each completed L2 requirement marked as complete:

- Use `get_screenshot` to visually inspect the design
- Use `batch_get` and `snapshot_layout` to verify structural correctness (layout, spacing, hierarchy)
- Verify all acceptance criteria are met by visual and structural inspection
- Verify there are no layout problems (overlapping elements, clipped content, misalignment)
- Mark the L2 as verified, or reject it with specific visual/structural feedback so the Implementor can fix

Note: For design tasks, automated failing tests do not apply. Quality assurance is performed through visual inspection and structural verification instead.

### Iteration

Iterate until all L2 requirements under the L1 are verified by the QA agent.