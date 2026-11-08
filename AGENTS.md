# AGENTS.md

Ground rules and context for working on LudumLinguarum.

## Language and tooling

* The project is written in **F#** and runs on **.NET** (see `global.json` for the SDK version).
* Use the standard `dotnet` tooling for everything: `dotnet build`, `dotnet run`, and `dotnet test` (tests included). Do not introduce alternative build or test runners.

## Purpose

LudumLinguarum extracts localized text from games, stores it in a local database, and exports it into formats compatible with flash card / spaced repetition learning apps (Anki, SuperMemo, Mnemosyne, AnyMemo).

## Copyright / game content

* **The project must never contain text or data from the supported games.** The user is expected to have their own legal copy of a game in order to use the program.
* No tests or source code may include actual game text or data. Where a test needs to verify behavior that depends on game-specific content, use **representative neutral synthetic text** instead.

## Testing

* Tests exist to clarify behavior and to keep the code working even when newer versions of games or their localizations are released.
* Use **property-based tests via FsCheck** (through Expecto.FsCheck) as appropriate, in addition to example-based tests.

## Reusable engine/framework utilities

* If functionality relates more to a particular **game engine or framework** than to a particular game, extract it into a separate utility package so it can be reused elsewhere.
* When adding such functionality, first review the existing game extraction logic to see whether any of it can be simplified by using the new shared code.

## Documentation

* Keep `CHANGELOG.md` and `README.md` up to date whenever new features or newly supported games are added.
