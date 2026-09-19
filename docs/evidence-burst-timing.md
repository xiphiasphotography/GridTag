# Evidence, bursts en timing

## Evidence

De model- en driverbronnen worden alleen toegepast wanneer de eerste nummer-match niet `auto` is. Een bestaande automatische nummer-match wordt dus niet door aanvullende evidence gewijzigd.

- `CarModelEvidence` vergelijkt een modelobservatie met `Entry.Car`.
- `DriverNameEvidence` vergelijkt gelezen namen met de drivers van een entry.
- `TimingCrossCheckEvidence` vergelijkt een capturetijd met passing times uit CSV.
- `CompositeEvidence` combineert onafhankelijke bronnen zonder entrylist-candidates te creëren.

Vision kent de entrylist niet. `ICarModelClassifier` en `IDriverNameReader` leveren alleen observaties; Core maakt daar weights van en `NumberMatcher` beslist.

Er zijn geen thresholds gewijzigd. Zonder modelgewichten of echte classifierimplementaties blijven deze bronnen injecteerbaar/fake-driven.

## Timing CSV

Formaat:

```text
number;time
69;2026-09-18 13:52:05
```

Gebruik in de CLI:

```text
gridtag eval --labels work/labels.csv --entrylist samples/entrylist.csv --session samples/session.example.json --timing-csv work/passing-times.csv --clock-offset 00:00:05
```

De offset wordt bij de fototijd opgeteld. Een passing binnen de standaardtolerantie van twee seconden versterkt de kandidaat; een bekende kandidaat die buiten de tolerantie valt geeft conflictgewicht. Ontbrekende timingdata blijft neutraal.

## Burst-propagatie

`BurstPropagation.Propose` gebruikt EXIF-tijdverschil en een aangeleverde similarityscore. Het resultaat is altijd een `BurstNumberProposal` met reden `burst_propagation_review`. Een voorstel wordt nooit als `auto` teruggegeven en moet opnieuw door de gewone nummer/evidence-validatie of handmatige review.

## Metingen

De bestaande sample/eval-run vóór deze uitbreiding had geen automatische detecties: `auto precision 0%`, `recall 0%`, `review rate 0%`, en `no_car_detected` voor alle 241 labels. Zonder modelconfiguratie blijven de na-metingen gelijk; de nieuwe evidencebronnen worden pas meetbaar zodra echte modelobservaties of timingdata worden aangesloten. Thresholds zijn niet aangepast.
