from p11_lib import patch, ensure_using

E = "tests/Modernization.Scoring.Engine.Tests/"
C = "tests/Modernization.Sources.ContractTests/"
A = "tests/Modernization.Api.Tests/"

# ------------------------------------------------------------------ motor
patch(E + "GoldenScoreTests.cs", [
    ('Assert.Equal("No Data Available", metric.RawValueDisplay);', 'Assert.Equal(NoticeCodes.ValueNoData, metric.Display.Code);'),
])
patch(E + "GoldenScoreTests.cs", [
    ("Assert.NotEmpty(metric.ExclusionReason);", "Assert.NotNull(metric.Exclusion);"),
], replace_all=True)
ensure_using(E + "GoldenScoreTests.cs", "Modernization.Domain")

patch(E + "RoadmapTests.cs", [
    ('Assert.Contains("4", alert.Evidence, StringComparison.Ordinal);', 'Assert.Contains("4", alert.Evidence.Args.Values);'),
    ('Assert.Contains("2", alert.Evidence, StringComparison.Ordinal);', 'Assert.Contains("2", alert.Evidence.Args.Values);'),
])

patch(E + "WhatIfTests.cs", [
    ("Assert.All(catalog.NotActionable, l => Assert.NotEmpty(l.NotActionableReason!));",
     "Assert.All(catalog.NotActionable, l => Assert.NotNull(l.NotActionableReason));"),
])

# ------------------------------------------------------------- contratos
patch(C + "DatabaseSnapshotProviderTests.cs", [
    ('e => e.Contains("eai_master.csv", StringComparison.Ordinal)', "e => e.Code == NoticeCodes.UploadMissingMaster"),
], replace_all=True)
ensure_using(C + "DatabaseSnapshotProviderTests.cs", "Modernization.Domain")

patch(C + "DummySnapshotProviderTests.cs", [
    ('Assert.Contains("no existe en el maestro", r.Reason, StringComparison.Ordinal)', "Assert.Equal(NoticeCodes.RowUnknownEai, r.Reason.Code)"),
])
ensure_using(C + "DummySnapshotProviderTests.cs", "Modernization.Domain")

patch(C + "FileSnapshotProviderTests.cs", [
    ('Assert.Contains("Invest_Count", rejection.Reason, StringComparison.Ordinal);', 'Assert.Equal("Invest_Count", rejection.Reason.Args["column"]);'),
    ('Assert.Contains("columna clave", Assert.Single(outcome.Rejections).Reason, StringComparison.Ordinal)',
     "Assert.Equal(NoticeCodes.RowMissingKey, Assert.Single(outcome.Rejections).Reason.Code)"),
])
ensure_using(C + "FileSnapshotProviderTests.cs", "Modernization.Domain")

patch(C + "SnapshotProviderContract.cs", [
    ("string.IsNullOrWhiteSpace(rejection.Reason)", "string.IsNullOrWhiteSpace(rejection.Reason.Code)"),
])

patch(C + "SourceFileMatcherTests.cs", [
    ('Assert.Contains("mas de una fuente", match.Problem, StringComparison.Ordinal);', "Assert.Equal(NoticeCodes.MatchAmbiguous, match.Problem?.Code);"),
    ('Assert.Contains("no existe en el catalogo", match.Problem, StringComparison.Ordinal);', "Assert.Equal(NoticeCodes.MatchAssignmentUnknown, match.Problem?.Code);"),
    ('Assert.Contains("encabezado", match.Problem, StringComparison.Ordinal);', "Assert.Equal(NoticeCodes.CsvNoHeader, match.Problem?.Code);"),
])
ensure_using(C + "SourceFileMatcherTests.cs", "Modernization.Domain")

patch(C + "UploadHardeningTests.cs", [
    ('Assert.Contains(concurrent.Errors, e => e.Contains("otra carga", StringComparison.Ordinal));',
     "Assert.Contains(concurrent.Errors, e => e.Code == NoticeCodes.UploadBusy);"),
    ('Assert.DoesNotContain(after.Errors, e => e.Contains("otra carga", StringComparison.Ordinal));',
     "Assert.DoesNotContain(after.Errors, e => e.Code == NoticeCodes.UploadBusy);"),
    ('Assert.Contains("campo", ex.Message, StringComparison.Ordinal);', "Assert.Equal(NoticeCodes.CsvFieldTooLong, ex.Notice?.Code);"),
])
ensure_using(C + "UploadHardeningTests.cs", "Modernization.Domain")

# ------------------------------------------------------------------- API
patch(A + "AuthorizationScopeTests.cs", [
    ('Assert.Contains("ingestion.write", view.DisabledReason ?? string.Empty, StringComparison.Ordinal);',
     'Assert.Equal("ingestion.write", view.DisabledReason?.Args["role"]);'),
])

patch(A + "LayeredEndToEndTests.cs", [
    ("Assert.All(view.Excluded, m => Assert.NotEmpty(m.Reason));", "Assert.All(view.Excluded, m => Assert.NotEmpty(m.Reason.Code));"),
])

patch(A + "QueryExperienceTests.cs", [
    ('Assert.Contains("Asigna su fuente", result.Summary, StringComparison.Ordinal);',
     "Assert.Equal(NoticeCodes.UploadRejectedUnidentified, result.Summary.Code);"),
    ('Assert.Contains(result.Errors, e => e.Contains("forma de nombre de archivo", StringComparison.Ordinal));',
     "Assert.Contains(result.Errors, e => e.Code == NoticeCodes.FileAssignmentShape);"),
])
ensure_using(A + "QueryExperienceTests.cs", "Modernization.Domain")

patch(A + "RoadmapEndToEndTests.cs", [
    ("Assert.NotEmpty(view.Outlook);", "Assert.Equal(NoticeCodes.OutlookProjected, view.Outlook.Code);"),
    ('Assert.Contains("Maintain (Patch)", view.Outlook, StringComparison.Ordinal);', 'Assert.Equal("Maintain (Patch)", view.Outlook.Args["roadmap"]);'),
    ('Assert.Contains("79.67", view.Outlook, StringComparison.Ordinal);', 'Assert.Equal("79.67", view.Outlook.Args["baseline"]);'),
])
ensure_using(A + "RoadmapEndToEndTests.cs", "Modernization.Domain")

patch(A + "UploadFlowTests.cs", [
    ('e => e.Contains("eai_master.csv", StringComparison.Ordinal)', "e => e.Code == NoticeCodes.UploadMissingMaster"),
], replace_all=True)
patch(A + "UploadFlowTests.cs", [
    ('Assert.Contains("Carga publicada", result.Summary, StringComparison.Ordinal);', "Assert.Equal(NoticeCodes.UploadPublished, result.Summary.Code);"),
    ('Assert.Contains(result.Errors, e => e.Contains("EAI_CODE", StringComparison.Ordinal));',
     'Assert.Contains(result.Errors, e => e.Args.Values.Any(v => v.Contains("EAI_CODE", StringComparison.Ordinal)));'),
    ('Assert.Contains("quedaron fuera", result.Summary, StringComparison.Ordinal);', 'Assert.NotEqual("0", result.Summary.Args["rejectedRows"]);'),
])
ensure_using(A + "UploadFlowTests.cs", "Modernization.Domain")

patch(A + "UploadSecurityTests.cs", [
    ('Assert.Contains(result.Errors, e => e.Contains(".csv", StringComparison.Ordinal));',
     "Assert.Contains(result.Errors, e => e.Code == NoticeCodes.FileExtension);"),
    ('Assert.Contains(result.Errors, e => e.Contains("text/html", StringComparison.Ordinal));',
     'Assert.Contains(result.Errors, e => e.Args.Values.Any(v => v.Contains("text/html", StringComparison.Ordinal)));'),
    ('Assert.Contains(result.Errors, e => e.Contains("no admite cargas", StringComparison.Ordinal));',
     "Assert.Contains(result.Errors, e => e.Code == NoticeCodes.UploadNotSupported);"),
])
ensure_using(A + "UploadSecurityTests.cs", "Modernization.Domain")

patch(A + "WhatIfEndToEndTests.cs", [
    ('Assert.Contains("suma de las partes", view.InteractionNote, StringComparison.Ordinal);',
     'Assert.StartsWith("scenario.", view.InteractionNote!.Code, StringComparison.Ordinal);'),
    ("Assert.NotEmpty(view.Explanation);", "Assert.NotEmpty(view.Explanation.Code);"),
    ('Assert.Contains("ya supera", view.Explanation, StringComparison.Ordinal);', "Assert.Equal(NoticeCodes.TargetAlreadyMet, view.Explanation.Code);"),
])
ensure_using(A + "WhatIfEndToEndTests.cs", "Modernization.Domain")
