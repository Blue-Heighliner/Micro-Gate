# Development Conventions

## Language & Platform

- Environment is Ubuntu Linux.
- Prefer C# if practical for any given task. Use the latest available language features.

## Project Structure

- Within each project folder, source files live under a `src` subfolder (e.g. `Core/src/Thing.cs`), not directly in the project root.

## Documentation & Comments

- Do not use — character
- All `public` and `internal` types and members must have full XML documentation comments (`<summary>`, `<param>`, `<returns>`, `<exception>`, etc. as applicable).
- When a type or member implements an already-documented interface and doesn't add meaningfully beyond that documentation, use `<inheritdoc />` instead of duplicating it.
- Do not add inline comments within method bodies unless explaining something genuinely obscure (a non-obvious workaround, a subtle invariant, a surprising platform quirk). Code should otherwise be self-explanatory through naming and structure.

## Class & Type Design

- Mark all classes `sealed` unless they are explicitly designed for inheritance.
- Prefer `record` types with `required init` properties for DTOs and models.
- Default to `internal` visibility. Only use `public` when there's a clear reason to expose the type/member outside the assembly.
- One type per file, with these exceptions:
  - An interface and its implementing class are co-located in one file named after the class (e.g. `IThing` and `Thing` both live in `Thing.cs`).
  - Extension classes are co-located with the class they extend (e.g. `ThingExtensions` also lives in `Thing.cs`).
  - A standalone interface with no single implementing class in the same file is named after its concept without the `I` prefix (e.g. `IOther` with no co-located `Other` class lives in `Other.cs`).
- Group static members at the top of a type, then instance members below them. Within each group, order members by kind: constructors, fields, events, properties, operators, methods. Applies to interfaces too (events, then properties, then methods) — an interface's members are not exempt just because it has no constructors or fields.
- Always use braces for blocks — never an implicit one-line `if`/`else`/`for`/`foreach`/`while`/etc. Always write `if (...) { ... }`, never `if (...) ...`.
- Prefer expression-bodied members over full block bodies when possible and practical (e.g. `void Do() => Action();`). For methods, when the signature and expression body don't fit on one line, wrap with `=>` indented on its own line beneath the signature, not trailing at the end of the signature line. For properties, the `=>` always stays on the same line as the signature (e.g. `public int Foo => value;`), even if the expression itself then needs to wrap onto following lines — never move the `=>` itself down to its own line as with methods.
- Do not use comments to designate sections of members (e.g. `// ── Section ──` dividers). All members should simply be ordered according to the member ordering rule above — no additional grouping by topic/area via comments.
- Do not define a private static field solely to back an instance property that always returns the same value. Initialize the instance property directly instead (e.g. `public IReadOnlyList<X> Foo { get; } = [...];`) rather than adding a separate `private static readonly` field just to hold that value.
- When a property returns a reference-type value (e.g. a list, array, dictionary, or other object), prefer storing that value in the property's own backing field, computed once, rather than an expression body that constructs a new value on every access. Do `IList<int> Numbers { get; } = [5, 2, 3];`, not `IList<int> Numbers => [5, 2, 3];`.
- Prefer private instance fields over private static fields, even when the value is the same for every instance. Reserve `static` for cases that genuinely require it (e.g. backing a static member, or a true compile-time `const`).
- Do not prefix private field names with an underscore. Use plain camelCase for private fields (e.g. `engineController`); public/internal properties use PascalCase (e.g. `EngineController`) — the casing itself is what distinguishes a private field from a public property, not a leading underscore.
- For private fields holding plain internal data (not an injected DI dependency), declare the field with its concrete/plain class type rather than an interface (e.g. `private readonly Dictionary<string, UserInfo> userCodes = new();`, not `IReadOnlyDictionary<string, UserInfo>`; `private readonly List<string> users = [...];`, not `IReadOnlyList<string>`). This does not apply to DI-injected constructor/property dependencies, which continue to use their interface type per the DI convention.
- Prefer primary constructors when possible.

## Usings

- Each project has a single `Using.cs` file containing all `global using` directives for that project.
- Do not place `using` directives in individual files.

## Async & Performance

- Use `async`/`await` instead of blocking calls.
- Do not suffix async method names with `Async` — name them as you would any other method.
- Prefer `Span<T>`, `ReadOnlySpan<T>`, `Memory<T>`, and `ReadOnlyMemory<T>` over raw byte arrays where applicable.

## Variable Declarations

- Do not use `var`. Always declare the explicit variable type.

## Dependency Injection

- Use an IoC container to manage and inject dependencies.
- Configure automatic resolution of interfaces to their same-named implementation (e.g. `IThing` resolves to `Thing`) without requiring explicit registration.

## Avalonia (Linux/X11)

- In `AppBuilder.Configure<T>().UsePlatformDetect()`, always pass `.With(new X11PlatformOptions { OverlayPopups = true })` (merge into any existing `X11PlatformOptions` rather than adding a second `.With()` call). Without it, popups render as separate X11 windows sharing a GPU context with the main window; that context can get stuck and stop repainting after rapid popup open/close cycles, workspace switches, or compositor hiccups — sometimes not clearing until a full reboot. `OverlayPopups = true` renders popups inside their owning window's own surface instead, avoiding the whole bug class.

## Testing

- Create and maintain unit tests using xUnit.
- Use mocks for dependencies under test.
- For major changes in applications with a UI, use `xvfb` (Xvfb / `xvfb-run`) to run and exercise the application headlessly as part of testing, rather than skipping verification because no display is available, always target a virtual display — never the real/physical display.
