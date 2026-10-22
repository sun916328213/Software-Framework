# WPFControls agent rules

## Minimal, non-showy implementation

- Deliver the smallest complete change for the explicit request. Do not turn a local fix into an architecture rewrite, general-purpose API, product redesign, or speculative cleanup.
- Before editing, state the intended behavior, likely files/modules, existing mechanisms to reuse, and what is explicitly out of scope. For trivial edits, keep this to one sentence. Ask first if ambiguity materially changes scope.
- Inspect nearby code and module README first. Prefer existing models, Workspace/Session, factories, property editors, result views, configuration formats, test fixtures, and commands. Do not create parallel infrastructure merely because it seems cleaner or more reusable.
- Do not add abstractions, interfaces, projects, dependencies, configuration switches, background schedulers, generic frameworks, dedicated views, placeholder UI, compatibility layers, or broad renames unless requested or evidence shows the existing design cannot meet the requirement. Explain the concrete necessity for every addition.
- Preserve algorithms, domain meaning, backend differences, public APIs, serialization, defaults, errors, cancellation, and ownership unless they are part of the request. Never invent capabilities or silently normalize meaningful behavior.
- Every new type, API, field, control, and test must map to a stated requirement or a concrete risk. If it does not, omit it or ask first. Tests should verify behavior at the existing seam, not implementation details.
- Review the diff before finishing and remove unrelated edits and speculative complexity. Put larger improvements in a separate recommendation; do not implement them as a side effect.
- Report what changed and what was actually verified. Do not imply tests/builds ran when they did not.Apply `Software Frame/AGENTS.md` and read `ARCHITECTURE.md` before changes affecting composition or ownership.

- Reuse the existing feature structure, Prism composition, shared controls, queries and ViewModels. Do not add Application layers, duplicate host menus/navigation, or parallel service registration for a local feature.
- Shared controls register reusable pages but do not choose machine-specific menus, database entities, hardware connection or production startup. Preserve the MachineApp integration boundary.
- Preserve persistence aliases and public XAML/resource URIs. Check dynamic theme resources, Loaded/Unloaded subscriptions, DataContext and ownership without changing unrelated UI.
- Test existing logical and WPF/STA seams; do not claim production host or hardware acceptance from module tests.

