# Cigarettes (Vintage Story mod)

Grow three tobacco varieties, dry and shred the leaves, roll cigarettes by hand or in batches on furniture, smoke cigars and pipes, vape temporal-gear-powered e-liquid, buy into nineteen parody brands (one of them rotting), climb a 71-achievement Smoker's Ledger that hands out character traits, and flick the butts on the ground or stub them out in an ashtray. See [DESIGN.md](DESIGN.md) for the full pipeline and roadmap.

Layout follows the official `dotnet new vsmod` template (VintageStory.Mod.Templates 1.22.0).

```
Cigarettes.sln
build.sh / build.ps1        packages Releases/cigarettes_<version>.zip via CakeBuild
CakeBuild/                  build + JSON validation + packaging
Cigarettes/
  modinfo.json
  CigarettesModSystem.cs    registers the item, block and block entity classes
  Items/                    ItemSmokable (shared smoking), ItemCigarette, ItemPipe, ItemCigarettePack
  Smoking/SmokingSystem.cs  timed buffs, nicotine level, withdrawal, coughing (server)
  Smoking/AchievementSystem.cs  the Smoker's Ledger: achievements, ranks, /smokestats (server, config/achievements.json)
  Entities/EntityFlickedButt.cs  a flicked butt that lands as ground storage
  Blocks/                   drying rack and frame, ashtray, batch furniture (tobacco cutter, rolling table)
  assets/cigarettes/        itemtypes, blocktypes, recipes, shapes, textures, lang, config/achievements.json, config/handbook (11 guide pages), patches/traits.json
```

## Building

1. Install the .NET 10 SDK and Vintage Story 1.22.
2. Set `VINTAGE_STORY` to the game folder (e.g. `%AppData%\Vintagestory` on Windows, `~/ApplicationData/vintagestory` on Linux).
3. `dotnet build Cigarettes/Cigarettes.csproj` to compile, or `./build.sh` to produce the release zip.
4. Run the "Client" launch profile to start the game with the mod loaded from source.

## Status

v0.8, not yet compiled: the C# was written against the 1.22 API docs without a game install available, so expect a few fixes on the first build. All JSON parses, and every texture, shape, drop, recipe ingredient and lang key resolves. Art is placeholder.
