# GridTag

GridTag herkent startnummers op raceauto's in RAW-foto's, controleert de match tegen de entrylist en aanvullende visuele aanwijzingen zoals automerk/-model en logo's, en voegt daarna automatisch de juiste auto-specifieke IPTC-metadata toe in **Lightroom Classic**: headline, beschrijving, alt-tekst, keywords en rijdersnamen. GridTag komt pas in actie nadat de foto's handmatig zijn geselecteerd en bewerkt.

> Status: projectstart. Deze README en `AGENTS.md` zijn bedoeld om het project met Codex in VS Code op te bouwen. De code wordt stap voor stap gemaakt volgens de takenlijst in `AGENTS.md` (§15).

## Implementation status

The .NET 10 scaffold is available. Core provides entry-list loading and lookup,
field generation, number matching, JSON contracts, and the fake-backed tagging
pipeline. The CLI provides `run`, `check-entrylist`, `fields`, `version`, and
`eval`, with documented exit codes 0-3. The evaluation harness reports
precision, recall, review rate, reasons, confusions and timing without tuning
thresholds. The Lightroom adapter now contains the task 7 plugin workflow, but
its SDK-dependent behavior still requires the manual checks in
`docs/lightroom-test-plan.md`. Vision contains the local preview and optional
ONNX/DirectML detector/plate-reader adapters; model weights remain external.

## Evaluatie-data

Uit de sample-data valt geen meetbare testset te maken: er zijn geen foto's en
de paden in `samples/labels.example.csv` bestaan niet. Daarom zijn er twee
praktische routes:

- `work/labels.smoke.csv`: smoke-input voor de vier voorbeeldrijen. Dit test
  alleen of `gridtag eval` het formaat leest; verwacht bij de meting alleen
  `no_preview` als er geen echte foto's of providers beschikbaar zijn.
- `tools/make_labels.py`: maakt een echte `work/labels.csv` uit een eigen
  archief met RAW-bestanden en XMP-sidecars. Het script leest alleen en past
  geen foto's of sidecars aan. Het vereist Python 3.8+ en geen extra packages.

Gebruik:

```text
python tools/make_labels.py "D:\\Archief\\<map met RAW + sidecars van een eerder evenement>" ^
    --entrylist samples/entrylist.csv --max-per-number 12 --sample 400 --out work/labels.csv
```

Het script herkent nummer-keywords zoals `#69` in `dc:subject` en schrijft:

- `work/labels.csv`: foto's met precies één nummer-keyword;
- `work/labels.review.csv`: meerdere nummers of nummers die niet in de
  entrylist staan. Zet bij meerdere nummers de hoofdauto vooraan en verplaats
  de gecorrigeerde rij daarna naar `labels.csv`, omdat eval het eerste nummer
  als primair gebruikt;
- geen rij voor foto's zonder nummer-keyword. Gebruik alleen
  `--include-untagged` voor mappen waarvan zeker is dat er geen auto op staat;
  zulke rijen krijgen een leeg nummer.

Tips voor een bruikbare testset:

- gebruik een eerder evenement met de bijbehorende entrylist;
- gebruik `--max-per-number` zodat één auto de meting niet domineert;
- meng sessies, lichtomstandigheden en camerahoeken;
- gebruik foto's die niet in een trainingsset zitten;
- laat `work/` in `.gitignore` staan, omdat de bestanden lokale padinformatie
  bevatten.

Als sidecars een andere notatie gebruiken dan `#69`, pas dan de regex
`NUMBER_KEYWORD` bovenin `tools/make_labels.py` aan. Het script is getest op de
twee XMP-voorbeelden en afgeleide gevallen: één nummer, meerdere nummers,
geen nummer, onbekend nummer en een sidecar zonder RAW.

Verify with `dotnet restore GridTag.slnx`, `dotnet build GridTag.slnx`, and
`dotnet test GridTag.slnx`. No Lightroom or image files are needed for these checks.

## Workflow

1. **Photo Mechanic**: basis-IPTC per evenement/sessie (map `yyyy-mm-dd - event\raw`).
2. **FastStone Viewer**: selectie verplaatsen naar `yyyy-mm-dd - event`.
3. **Lightroom Classic**: zelf bewerken; zet daarna de definitieve beelden op **Pick**.
4. **GridTag** (menu in Lightroom): alleen die Picks worden geanalyseerd en krijgen auto-specifieke metadata.
5. **Review in Lightroom**: foto's die niet automatisch lukken staan in de collectie `GridTag Review` (of `GridTag GeenAuto`). Typ daar zelf het nummer in het veld *Startnummer (handmatig)* en start "verwerk handmatige nummers".
6. **Export** vanuit Lightroom.

GridTag schrijft rechtstreeks in de Lightroom-catalogus. Er is dus geen "metadata opslaan" of "metadata lezen" nodig tussen de stappen, en er worden geen XMP-sidecars door de tool aangepast. GridTag doet nadrukkelijk geen selectie, rating, beeldbewerking of export; dat blijft handwerk in de bestaande workflow.

## Hoe het werkt

```
Lightroom-plugin (Lua)                     gridtag.exe (.NET)
  verzamelt Picks                            leest manifest.json
  schrijft manifest.json  ───────────►       leest entrylist.csv + session.json
  start gridtag.exe                          per foto: sessie → preview → auto → nummer → evidence → validatie
  leest results.json      ◄───────────       schrijft results.json
  past metadata toe in de catalogus
```

- **Validatie tegen de entrylist:** de tool kiest uit de nummers die echt bestaan, in plaats van vrije OCR te vertrouwen. Verwarbare nummers (bijv. 59/66/69/96/99) en deelnummers (5 in 55) gaan naar review.
- **Nummer is primair, merk/model is controle:** een herkend automerk/-model kan een twijfelachtig nummer versterken of juist een conflict signaleren. Logoherkenning kan later als extra, zwakker bewijs worden gebruikt. Bij conflict gaat de foto naar review; GridTag verzint nooit zelf een deelnemer.
- **Precisie boven recall:** liever een foto niet taggen dan verkeerd taggen.
- **Alles lokaal:** geen cloud, geen uploads.

Zie `AGENTS.md` voor de volledige regels, contracten en het matching-algoritme.


## Technische keuze

GridTag bestaat bewust uit twee kleine, gescheiden delen:

- **C#/.NET (`gridtag.exe`)** bevat alle echte logica: RAW-preview, vision, entrylist, matching, confidence/evidence en veldgeneratie.
- **Lightroom Classic plug-in (Lua)** blijft een dunne adapter: Picks ophalen, `gridtag.exe` starten, resultaten lezen en metadata in de Lightroom-catalogus zetten.

De eigenaar programmeert voornamelijk in JavaScript en deels in C#. Daarom blijft de C#-code eenvoudig en expliciet opgebouwd. JavaScript/Node/Electron zijn geen runtime-onderdeel van GridTag; er komt geen aparte webinterface of service bij zolang Lightroom zelf voldoende UI biedt.

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
  GridTag.Vision   RAW-preview, autodetectie, nummerlezer; later merk/model- en logo-evidence
  GridTag.Cli      gridtag.exe
tests/GridTag.Core.Tests   xUnit, incl. golden tests op de twee XMP-voorbeelden
lightroom/GridTag.lrdevplugin   de Lightroom-plugin (Lua)
tools/       Python-scripts voor training (later)
```

## Vereisten

- Windows, **.NET SDK 10**. `TargetFramework` blijft `net10.0` in `Directory.Build.props`; installeer bij een oudere SDK de .NET 10 SDK.
- **Visual Studio Code** met de C# Dev Kit en een Lua-extensie (bijv. *Lua* van sumneko). Codex-extensie of Codex CLI.
- **Lightroom Classic**. Voor de alt-tekstvelden is SDK-versie 13.2 of nieuwer nodig, oudere versies slaan die velden over.
- Later voor de herkenning: bij voorkeur een GPU. De inference-backend blijft verwisselbaar (bijv. ONNX Runtime/DirectML of WinML).

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


## Herkenningsstrategie

De eerste bruikbare versie wordt bewust in lagen opgebouwd:

1. auto detecteren;
2. startnummer lezen en alleen tegen geldige nummers uit de entrylist matchen;
3. automerk/-model herkennen als eerste extra controle op de nummermatch;
4. optioneel logo's gebruiken als aanvullende evidence;
5. alleen automatisch taggen wanneer de gecombineerde evidence voldoende betrouwbaar en onderling consistent is; anders `GridTag Review`.

Merk/model of een logo vervangt dus nooit de entrylist. Het doel is vooral fouten zoals een overtuigend gelezen `69` op een auto die visueel duidelijk bij nummer `96` uit de entrylist hoort, tegen te houden.

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
