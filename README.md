# VQE Ancients - Archogen Injector Whitelist

Choose which genes Vanilla Quests Expanded - Ancients' archogen injector can give. RimWorld 1.7 only.

**Upstream behaviour.** `Building_ArchogenInjector` gives the injected pawn a random archite gene (any gene with an
archite cost) as the reward and a random gene with a metabolism value of zero or more as the side effect; the pawn-kind
patch for Ancients' experiment pawns rolls the same two kinds of gene. Every candidate goes through
`Utils.IsValidGeneForInjection`, which only rejects VRE morph genes, android genes, Alpha Genes animal genes,
extraction-disabled genes and whatever an `InjectionBlacklistDef` names. So with many gene mods the reward can be any
mod's archite gene and the side effect can be nearly anything.

**What this does.** A Harmony postfix on `Utils.IsValidGeneForInjection` answers from the whitelist instead: a gene is
valid only when it is on a default list or the player ticked it, and the player has not unticked it. How genes are
picked, offered in the choice window and applied stays Ancients' code; nothing of it is reimplemented here. A listed
gene is allowed even where Ancients' own rules would reject it, as in the original. The check only runs when a pawn is
injected or an experiment pawn is generated.

**Default lists.** `1.7/Defs/InjectionWhitelist.xml` holds an `ArchogenInjectorWhitelist.InjectionWhitelistDef` with gene
defNames. All loaded defs of that type are merged, so another mod can add its own list; patch the def to change or
replace it. Genes that are not loaded are skipped.

**Settings.** Options > Mod settings lists every gene Ancients could roll (archite genes, and genes with metabolism zero
or more, that its own rules accept) plus everything on a default list, with its icon, a search box (name, defName or
mod), Show all/allowed/blocked and Kind all/archite/side effects switches, and Allow shown / Block shown / Reset to
defaults. Only departures from the defaults are saved, so later changes to a default list still apply. Changes take
effect straight away; no restart. The menu warns when no archite gene or no side-effect gene is left.

| Path | Contents |
|---|---|
| `1.7/Assemblies/ArchogenInjectorWhitelist.dll` | net10.0 build |
| `1.7/Defs/InjectionWhitelist.xml` | the default list (Too Many Mods' original, 103 genes: Ancients' 33 archite genes plus Alpha Genes' super-adaptive biology as rewards, about 70 drawback genes as side effects; not yet checked against current gene mods) |
| `Source/` | `InjectionFilter.cs` (def, filter patch, allowed-gene cache), `WhitelistMod.cs` (settings and menu). Not deployed. |

**Build.** `dotnet build -c Release` in `Source`. VQE - Ancients has no 1.7 build yet, so the csproj references its 1.6
DLL; repoint and rebuild once it ships.

**Origin.** Split out of Too Many Mods - Compats and Rebalances by Warachia (`TMM_VQEA.dll` and the
`TMM_VQE_GeneInjectionWhitelist` def), where a Harmony prefix skipped Ancients' check whenever a whitelist def existed.
Rewritten 2026-10-13 as a postfix with a settings menu. The def class and field were renamed
(`TooManyMods.InjectionWhitelistDef` / `whitelistedGenes` -> `ArchogenInjectorWhitelist.InjectionWhitelistDef` / `genes`).
Untested in-game.
