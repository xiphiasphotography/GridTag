# GridTag

GridTag herkent startnummers op raceauto's in RAW-foto's en voegt daarmee automatisch de juiste IPTC-metadata toe in **Lightroom Classic**: headline, beschrijving, alt-tekst, keywords en rijdersnamen, gebaseerd op een entrylist.

> Status: projectstart. Deze README en `AGENTS.md` zijn bedoeld om het project met Codex in VS Code op te bouwen. De code wordt stap voor stap gemaakt volgens de takenlijst in `AGENTS.md` (§15).

## Workflow

1. **Photo Mechanic**: basis-IPTC per evenement/sessie (map `yyyy-mm-dd - event\raw`).
2. **FastStone Viewer**: selectie verplaatsen naar `yyyy-mm-dd - event`.
3. **Lightroom Classic**: bewerken en **Pick** zetten.
4. **GridTag** (menu in Lightroom): alleen de Picks krijgen de auto-specifieke metadata.
5. **Review in Lightroom**: foto's die niet automatisch lukken staan in de collectie `GridTag Review` (of `GridTag GeenAuto`). Typ daar zelf het nummer in het veld *Startnummer (handmatig)* en start "verwerk handmatige nummers".
6. **Export** vanuit Lightroom.

GridTag schrijft rechtstreeks in de Lightroom-catalogus. Er is dus geen "metadata opslaan" of "metadata lezen" nodig tussen de stappen, en er worden geen XMP-sidecars door de tool aangepast.

## Hoe het werkt

```
Lightroom-plugin (Lua)                     gridtag.exe (.NET)
  verzamelt Picks                            leest manifest.json
  schrijft manifest.json  ───────────►       leest entrylist.csv + session.json
  start gridtag.exe                          per foto: sessie → preview → auto → nummer → validatie
  leest results.json      ◄───────────       schrijft results.json
  past metadata toe in de catalogus
```

- **Validatie tegen de entrylist:** de tool kiest uit de nummers die echt bestaan, in plaats van vrije OCR te vertrouwen. Verwarbare nummers (bijv. 59/66/69/96/99) en deelnummers (5 in 55) gaan naar review.
- **Precisie boven recall:** liever een foto niet taggen dan verkeerd taggen.
- **Alles lokaal:** geen cloud, geen uploads.

Zie `AGENTS.md` voor de volledige regels, contracten en het matching-algoritme.

## Wat GridTag wel en niet schrijft

| GridTag schrijft | Photo Mechanic / jij |
|---|---|
| Headline, beschrijving, alt-tekst, uitgebreide alt-beschrijving | Creator, credit, copyright, contactinfo |
| Keywords: team, auto, rijders, `#nummer`, sessie, klasse | Locatie, evenement, organisatie, algemene keywords |
| Rijders (Person Shown) | Rating, kleurlabel, ontwikkelinstellingen |

## Projectstructuur (doel)

```
AGENTS.md  README.md  GridTag.slnx  Directory.Build.props
docs/        contracts, architectuur, open vragen, reference/*.xmp (goede voorbeelden)
samples/     entrylist.csv, session.example.json, manifest/results voorbeelden
src/
  GridTag.Core     domein, entrylist, matching, veldgeneratie, pipeline
  GridTag.Vision   RAW-preview, autodetectie, nummerlezer (ONNX)
  GridTag.Cli      gridtag.exe
tests/GridTag.Core.Tests   xUnit, incl. golden tests op de twee XMP-voorbeelden
lightroom/GridTag.lrdevplugin   de Lightroom-plugin (Lua)
tools/       Python-scripts voor training (later)
```

## Vereisten

- Windows, **.NET SDK 10** (LTS). Heb je een oudere SDK, pas dan `TargetFramework` aan in `Directory.Build.props`.
- **Visual Studio Code** met de C# Dev Kit en een Lua-extensie (bijv. *Lua* van sumneko). Codex-extensie of Codex CLI.
- **Lightroom Classic**. Voor de alt-tekstvelden is SDK-versie 13.2 of nieuwer nodig, oudere versies slaan die velden over.
- Later voor de herkenning: bij voorkeur een GPU (DirectML).

## Aan de slag met Codex

1. Maak een lege map, `git init`, en zet er `AGENTS.md` en deze `README.md` in.
2. Zet je bronbestanden klaar:
   - `samples/entrylist.csv` (je `_entrylist.csv`, UTF-8 met BOM, `;`-gescheiden)
   - `docs/reference/003-Mercedes_-_AMG_Team_Verstappen_Racing.xmp`
   - `docs/reference/069-Emil_Frey_Racing.xmp`
3. Open de map in VS Code en start Codex. Codex leest `AGENTS.md` automatisch.
4. Werk de taken één voor één af. Goede eerste prompts:

```
Voer taak 1 uit van AGENTS.md §15 (scaffold). Bouw en toon dat `dotnet build` slaagt.
```
```
Voer taak 2 en 3 uit (domein + FieldBuilder). De golden tests op de twee XMP-referentiebestanden moeten slagen.
```
```
Voer taak 4 uit (NumberMatcher) met tests voor: sterke enkele hypothese, verwarbaar cluster 59/66/69/96/99, substring 5 vs 55, out-of-list.
```
5. Laat Codex na elke taak vertellen wat er is gewijzigd, hoe het is gecontroleerd (commando's) en wat openstaat.

## Lightroom-plugin installeren (na taak 7)

1. Lightroom Classic → **Bestand → Plug-in Manager → Toevoegen** → kies `lightroom/GridTag.lrdevplugin`.
2. Menu **Bibliotheek → Plug-in-extra's → GridTag: instellingen…**: pad naar `gridtag.exe`, `entrylist.csv` en `session.json`.
3. Selecteer de foto's van de map, gebruik **GridTag: tag Picks**.

## Belangrijke open punten (te testen in Lightroom)

Vastgelegd in `docs/open-questions.md`, onder andere:

- Werkt **Person Shown** met twee rijders (en met welk scheidingsteken)?
- Windows-quoting bij het starten van `gridtag.exe` vanuit de plugin.
- Gedrag van collecties (`GridTag Review`) en de write-gates bij honderden foto's.

## Meetlat voor de herkenning

Voor je in modellen investeert, bouw `gridtag eval` (taak 8) en meet op een eigen testset:

- ≥ 80 % van de foto's met leesbaar nummer correct,
- ≤ 2 % foute automatische tags,
- ≤ 2 s per foto.

Halen twee of meer criteria niet, dan is een lichtere variant (handmatig invullen met entrylist-autocompletion) waarschijnlijk zinvoller.

## Licenties en privacy

- Controleer de licentie van elk model en pakket (Ultralytics YOLO is AGPL-3.0; kies voor commercieel gebruik een ruimhartig gelicentieerd alternatief).
- `json.lua` (rxi) is MIT; bewaar de licentietekst in het bestand.
- Foto's en entrylists verlaten je machine niet.
