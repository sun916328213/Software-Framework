# SecsGem agent rules

Apply `Software Frame/AGENTS.md` minimal-change rules.

- Reuse existing protocol Core/Runtime/Controls boundaries, message/session machinery and secs4net conventions. Do not build a parallel protocol stack or introduce broad generic abstractions for a single message.
- Preserve wire formats, message semantics, compatibility and connection/session ownership. Keep vendor/protocol-specific details explicit rather than flattening meaningful differences.
- Test protocol behavior with deterministic fixtures/fakes and existing suites. Do not connect production equipment or send live messages without explicit authorization.
