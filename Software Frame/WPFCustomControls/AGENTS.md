# WPFCustomControls agent rules

Apply `Software Frame/AGENTS.md` minimal-change rules.

- Keep this library focused on genuinely reusable controls. Prefer extending an existing control/template/style to adding a parallel control for one consumer.
- Preserve public dependency properties, XAML resource keys, behavior and theme semantics. Avoid changes that force unrelated hosts to migrate.
- Test control behavior in the existing STA/WPF test project; do not add a new UI framework or test project for a single control.
