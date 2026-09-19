namespace GridTag.Core;

/// <summary>Processes one manifest item through manual, no-preview, detection and matching stages.</summary>
public sealed class TaggingPipeline
{
    private readonly EntryList entryList;
    private readonly EventContext eventContext;
    private readonly IRawPreviewProvider rawPreviewProvider;
    private readonly ICarDetector carDetector;
    private readonly IPlateReader plateReader;
    private readonly NumberMatcher matcher;
    private readonly FieldBuilder fieldBuilder;
    private readonly Func<ManifestPhoto, IEvidence?>? evidenceProvider;

    /// <summary>Creates a pipeline from the event context and the vision interfaces.</summary>
    public TaggingPipeline(
        EntryList entryList,
        EventContext eventContext,
        IRawPreviewProvider rawPreviewProvider,
        ICarDetector carDetector,
        IPlateReader plateReader,
        NumberMatcher? matcher = null,
        FieldBuilder? fieldBuilder = null,
        Func<ManifestPhoto, IEvidence?>? evidenceProvider = null)
    {
        this.entryList = entryList ?? throw new ArgumentNullException(nameof(entryList));
        this.eventContext = eventContext ?? throw new ArgumentNullException(nameof(eventContext));
        this.rawPreviewProvider = rawPreviewProvider ?? throw new ArgumentNullException(nameof(rawPreviewProvider));
        this.carDetector = carDetector ?? throw new ArgumentNullException(nameof(carDetector));
        this.plateReader = plateReader ?? throw new ArgumentNullException(nameof(plateReader));
        this.matcher = matcher ?? new NumberMatcher();
        this.fieldBuilder = fieldBuilder ?? new FieldBuilder();
        this.evidenceProvider = evidenceProvider;
    }

    /// <summary>Processes an entire manifest and returns the result file.</summary>
    public ResultFile Process(Manifest manifest)
    {
        ArgumentNullException.ThrowIfNull(manifest);
        var photos = manifest.Photos.Select(ProcessPhoto).ToArray();
        return new ResultFile(1, "0.1.0", DateTimeOffset.UtcNow, photos);
    }

    /// <summary>Processes a single manifest photo in the same way the CLI would.</summary>
    public PhotoResult ProcessPhoto(ManifestPhoto photo)
    {
        ArgumentNullException.ThrowIfNull(photo);

        var session = ResolveSession(photo);
        try
        {
            if (!string.IsNullOrWhiteSpace(photo.ManualNumber))
                return ProcessManualPhoto(photo, session);

            var preview = rawPreviewProvider.GetPreview(photo.Path);
            if (preview is null)
                return new PhotoResult(photo.Id, "review", ["no_preview"], session, []);

            var detections = carDetector.Detect(preview).ToArray();
            if (detections.Length == 0)
                return new PhotoResult(photo.Id, "noCar", ["no_car_detected"], session, []);

            var orderedDetections = detections.OrderByDescending(d => d.Confidence).ToArray();
            var resolvedCars = new List<CarCandidate>();
            var reasons = new List<string>();
            var largestDetection = orderedDetections[0];
            var largestResolved = false;

            foreach (var detection in orderedDetections)
            {
                var hypotheses = plateReader.ReadNumbers(preview, detection);
                if (hypotheses.Count == 0)
                {
                    if (detection == largestDetection)
                        reasons.Add("largest_car_unresolved");
                    continue;
                }

                var match = matcher.Match(entryList, hypotheses);
                if (match.Status != MatchStatus.Auto && evidenceProvider?.Invoke(photo) is { } evidence)
                    match = matcher.Match(entryList, hypotheses, evidence);
                if (match.Status == MatchStatus.Auto)
                {
                    if (!entryList.TryGetEntry(match.BestNumber!, out var entry))
                        continue;

                    var isPrimary = detection == largestDetection;
                    if (isPrimary)
                        largestResolved = true;
                    resolvedCars.Add(new CarCandidate(match.BestNumber!, match.BestProbability, "ocr", isPrimary));
                    continue;
                }

                if (detection == largestDetection)
                    reasons.AddRange(match.Reasons);
            }

            if (resolvedCars.Count == 0)
            {
                if (reasons.Count == 0)
                    reasons.Add("largest_car_unresolved");
                return new PhotoResult(photo.Id, "review", reasons, session, []);
            }

            if (!largestResolved)
            {
                return new PhotoResult(photo.Id, "review", reasons.Count > 0 ? reasons : ["largest_car_unresolved"], session, resolvedCars);
            }

            var primary = resolvedCars.OrderByDescending(car => car.Confidence).First();
            if (!entryList.TryGetEntry(primary.Number, out var primaryEntry))
                return new PhotoResult(photo.Id, "review", ["no_entry_match"], session, resolvedCars);

            var fields = BuildCombinedFields(resolvedCars, session);
            return new PhotoResult(
                photo.Id,
                "auto",
                [],
                session,
                resolvedCars.Select(car => new CarCandidate(car.Number, car.Confidence, car.Source, car.Number == primary.Number)).ToArray(),
                fields);
        }
        catch (Exception ex)
        {
            return new PhotoResult(photo.Id, "error", [$"exception:{ex.GetType().Name}:{ex.Message}"], session, []);
        }
    }

    private PhotoResult ProcessManualPhoto(ManifestPhoto photo, string? session)
    {
        var values = photo.ManualNumber!
            .Split([',', ';', ' '], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var recognized = new List<CarCandidate>();
        var reasons = new List<string>();

        foreach (var value in values)
        {
            var normalized = NumberNormalizer.Normalize(value);
            if (!entryList.TryGetEntry(normalized, out var entry))
            {
                reasons.Add($"unknown_number:{normalized}");
                continue;
            }

            recognized.Add(new CarCandidate(entry.Number, 1.0, "manual", recognized.Count == 0));
        }

        if (recognized.Count == 0)
            return new PhotoResult(photo.Id, "review", reasons, session, []);

        var primaryEntry = entryList.TryGetEntry(recognized[0].Number, out var resolvedPrimary)
            ? resolvedPrimary
            : throw new InvalidOperationException($"Manual number '{recognized[0].Number}' was accepted but was not found in the entry list.");

        var fields = BuildCombinedFields(recognized, session);
        return new PhotoResult(
            photo.Id,
            "manual",
            reasons,
            session,
            recognized,
            fields);
    }

    private GeneratedFields BuildCombinedFields(IReadOnlyList<CarCandidate> cars, string? session)
    {
        var primary = cars.First(car => car.Primary);
        var primaryEntry = entryList.TryGetEntry(primary.Number, out var entry)
            ? entry
            : throw new InvalidOperationException($"Resolved number '{primary.Number}' was not found.");
        var primaryFields = fieldBuilder.Build(primaryEntry, eventContext, session);
        var keywords = new List<string>();
        var persons = new List<string>();
        var seenKeywords = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPersons = new HashSet<string>(StringComparer.Ordinal);
        var classes = new List<string>();
        var seenClasses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var car in cars)
        {
            if (!entryList.TryGetEntry(car.Number, out var carEntry))
                continue;
            AddKeyword(carEntry.Team);
            AddKeyword(carEntry.Car);
            foreach (var driver in carEntry.Drivers)
            {
                AddKeyword(driver.Name);
                if (seenPersons.Add(driver.Name))
                    persons.Add(driver.Name);
            }
            AddKeyword($"#{carEntry.Number}");
            if (seenClasses.Add(carEntry.Class))
                classes.Add(carEntry.Class);
        }

        var sessionName = eventContext.Sessions.FirstOrDefault(item => string.Equals(item.Code, session, StringComparison.OrdinalIgnoreCase))?.Name ?? session;
        if (!string.IsNullOrWhiteSpace(sessionName))
            AddKeyword(sessionName);
        foreach (var @class in classes)
            AddKeyword(@class);

        return primaryFields with { Keywords = keywords.ToArray(), Persons = persons.ToArray() };

        void AddKeyword(string keyword)
        {
            if (seenKeywords.Add(keyword))
                keywords.Add(keyword);
        }
    }

    private string? ResolveSession(ManifestPhoto photo)
    {
        var resolver = new SessionResolver();
        var capture = photo.CaptureTime;
        return resolver.Resolve(capture, eventContext);
    }
}

/// <summary>Contract for a preview provider used by the pipeline.</summary>
public interface IRawPreviewProvider
{
    /// <summary>Retrieves the preview object for a photo path, or null when no preview is available.</summary>
    object? GetPreview(string path);
}

/// <summary>Contract for the car detector used by the pipeline.</summary>
public interface ICarDetector
{
    /// <summary>Detects cars inside a preview.</summary>
    IReadOnlyList<DetectedCar> Detect(object preview);
}

/// <summary>Contract for the number-reading step on a detected car.</summary>
public interface IPlateReader
{
    /// <summary>Reads all candidate numbers for one detected car from the preview.</summary>
    IReadOnlyList<NumberHypothesis> ReadNumbers(object preview, DetectedCar detectedCar);
}
