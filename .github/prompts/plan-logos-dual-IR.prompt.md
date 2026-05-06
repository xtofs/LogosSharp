## Plan: Introduce Dual IR Layers in Logos

Refactor the generator into three explicit stages (Roslyn extraction -> input IR -> output/code IR -> renderer) so Roslyn concerns and text rendering are isolated. Start with a minimal, behavior-preserving migration, then choose between S-expression IR and typed IR per layer using explicit decision gates. This reduces coupling, improves testability, and enables transformation passes without changing generated semantics.

**Steps**

1. Phase 1 - Baseline and seam mapping
1. Capture a golden baseline of generated output from current demo inputs and representative token definitions to lock external behavior before refactor.
1. Document the current seams and contract points in the generator pipeline: symbol extraction, pattern normalization, regex parsing, char-class optimization, and final text emission.
1. Define invariants that must remain unchanged: token ordering, skip behavior, diagnostics, namespace/type names, and deterministic output.

1. Phase 2 - Input IR introduction (depends on Phase 1)
1. Introduce an explicit input IR immediately after Roslyn extraction, replacing direct downstream dependence on Roslyn symbol objects.
1. Split current input processing into two responsibilities: Roslyn adapter (symbol/attribute access only) and IR normalizer/validator (token and regex semantics only).
1. Preserve existing regex model reuse by embedding or referencing parsed regex structures in the input IR rather than duplicating parser logic.
1. Add serialization/debug views for input IR to aid diagnostics and golden testing.

1. Phase 3 - Output IR introduction (depends on Phase 2)
1. Introduce an explicit output/code IR that models generated constructs (types, methods, statements/expressions, constants, control flow) before rendering text.
1. Refactor emission routines to produce output IR nodes first, then render through a dedicated C# renderer.
1. Keep a compatibility fallback node kind for raw statement/text fragments to reduce migration risk for complex constructs.
1. Ensure rendering remains deterministic through stable ordering rules and explicit formatting policies.

1. Phase 4 - SExpr versus typed IR decision gates (parallel analysis with Phase 2 design details)
1. Evaluate three architecture options for both layers using prototypes and objective criteria.
1. Option A: SExpr for both input and output IR.
1. Option B: typed input IR plus typed output IR.
1. Option C: hybrid, typed input IR plus SExpr output IR (or inverse only if strongly justified).
1. Make independent decisions per layer because optimal choice may differ between input and output concerns.

1. Phase 5 - Incremental migration and risk control (depends on Phases 3 and 4)
1. Migrate one emitter slice first (for example, literal token matching) to validate IR shape and rendering strategy.
1. Migrate regex-based emitters and char-class emission next; keep parity checks at each step.
1. Remove direct string-writing paths only after parity is proven for all generator outputs.
1. Retain temporary dual-path comparison mode during rollout to detect regressions early.

1. Phase 6 - Testing and verification hardening (parallelizable after Phase 3 starts)
1. Add input-IR unit tests for attribute mapping, validation rules, and normalized model shape.
1. Add output-IR tests for control-flow structure and renderer correctness.
1. Add end-to-end snapshot tests comparing generated C# against approved baselines.
1. Add deterministic-output checks across repeated runs and different machine environments.

1. Phase 7 - Documentation and adoption (depends on Phase 5)
1. Document the new pipeline and extension points for future token pattern features.
1. Document IR schema/conventions, especially if SExpr is chosen (node naming, required fields, canonical ordering).
1. Add contributor guidance on where to place new logic: Roslyn adapter vs input normalization vs output transformation vs renderer.

**Relevant files**

- /Users/christof/code/logos/src/Logos/LogosGenerator.cs - primary decomposition point; split Roslyn adapter, input IR construction, output IR construction, and rendering.
- /Users/christof/code/logos/src/Logos/RegexModel.cs - reused as part of normalized input model or wrapped by input IR nodes.
- /Users/christof/code/logos/src/Logos/RegexParser.cs - remains parsing authority; fed by input IR normalization stage.
- /Users/christof/code/logos/src/Logos/AsciiCharClassBuilder.cs - consumed by output IR construction/optimization pass.
- /Users/christof/code/logos/src/spike/SExpr.cs - candidate reusable core for SExpr-backed IR(s); likely needs move or duplication policy decision.
- /Users/christof/code/logos/samples/demo/TokenKind.cs - representative fixture for baseline/snapshot comparisons.
- /Users/christof/code/logos/samples/demo/Program.cs - execution fixture to sanity-check tokenizer behavior post-refactor.
- /Users/christof/code/logos/docs/regex-compilation-strategy.md - place to align IR decisions with compilation strategy documentation.

**Verification**

1. Build the solution and ensure no diagnostic behavior changes for existing attributes and invalid patterns.
2. Run snapshot/golden tests before and after each migration slice and diff generated outputs.
3. Validate deterministic output by regenerating multiple times and confirming byte-for-byte equality.
4. Run functional demo checks to ensure tokenization semantics are unchanged for literals, regexes, skip pattern, and end token handling.
5. If dual-path mode is enabled, compare old and new generated code paths for representative enums until parity is reached.

**Decisions**

- Included scope: internal generator architecture refactor and quality gates.
- Excluded scope: public attribute API changes, regex language expansion, runtime tokenizer semantics changes.
- Decision gate 1 (input IR form):
  A) SExpr input IR: very flexible and easy to inspect/serialize; weaker static guarantees and more runtime validation.
  B) typed input IR: stronger compile-time guarantees, clearer intent, better IDE refactors; more boilerplate.
  Recommendation: typed input IR unless frequent schema mutation is expected.
- Decision gate 2 (output IR form):
  A) SExpr output IR: excellent for rewrite passes and debug dumps; renderer relies on convention-heavy nodes.
  B) typed output IR: safer transforms and easier maintenance for C#-specific constructs; more classes/visitor code.
  C) hybrid output IR: typed top-level constructs with SExpr expression subtree for complex pattern logic.
  Recommendation: hybrid or typed-first for maintainability; SExpr can be introduced selectively for transformation-heavy subtrees.
- SExpr source decision:
  A) Reuse and move /src/spike/SExpr.cs into /src/Logos with cleanup.
  B) Re-implement minimal SExpr core in /src/Logos to avoid coupling to spike experiments.
  Recommendation: copy and harden into Logos scope, then retire spike dependency to keep production boundaries clear.

**Further Considerations**

1. Memory/perf tradeoff: IR allocation overhead is acceptable for compile-time generators, but should still be measured on large enums before fully committing to highly granular nodes.
2. Diagnostics ownership: keep source-location data anchored in input IR so output IR/rendering can emit precise errors without Roslyn symbol reach-through.
3. Migration safety: preserve a temporary old-path renderer switch until parity and determinism checks pass in CI.
