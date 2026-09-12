from pathlib import Path


def replace_once(path: str, old: str, new: str) -> None:
    file = Path(path)
    text = file.read_text(encoding="utf-8")
    if old not in text:
        raise SystemExit(f"Expected block not found in {path}")
    file.write_text(text.replace(old, new, 1), encoding="utf-8")


endpoint = "backend/src/PMGM.Api/Modules/Ceremonies/CeremonyEndpoints.cs"
queue = "backend/src/PMGM.Api/Modules/InstitutionalProjections/CeremonyReviewQueueEndpoints.cs"
test = "backend/tests/PMGM.Api.Tests/Integration/PostgreSqlHttpWorkflowTests.cs"

replace_once(
    endpoint,
    """        var treasury = await db.FinancialRegularitySnapshots\n""",
    """        var grandMaster = await db.CeremonyValidations\n            .AsNoTracking()\n            .Where(x => x.CeremonyRequestId == requestId &&\n                        x.ValidationType == CeremonyCodes.ValidationType.GrandMaster)\n            .OrderByDescending(x => x.RecordedAtUtc)\n            .FirstOrDefaultAsync(cancellationToken);\n\n        var treasury = await db.FinancialRegularitySnapshots\n""",
)

replace_once(
    endpoint,
    """            hospitalaria?.Status,\n            evidence);\n\n        return new EligibilityContext(ceremony, internalAffairs, treasury, hospitalaria, publicationSnapshot, decision, today);\n""",
    """            hospitalaria?.Status,\n            grandMaster?.Status,\n            evidence);\n\n        return new EligibilityContext(ceremony, internalAffairs, treasury, hospitalaria, grandMaster, publicationSnapshot, decision, today);\n""",
)

replace_once(
    endpoint,
    """            hospitalariaSnapshotId = context.Hospitalaria?.Id,\n            publication = context.Publication\n""",
    """            hospitalariaSnapshotId = context.Hospitalaria?.Id,\n            granMaestriaValidationId = context.GrandMaster?.Id,\n            publication = context.Publication\n""",
)

replace_once(
    endpoint,
    """        HospitalariaRegularitySnapshot? Hospitalaria,\n        CandidatePublicationSnapshot? Publication,\n""",
    """        HospitalariaRegularitySnapshot? Hospitalaria,\n        CeremonyValidation? GrandMaster,\n        CandidatePublicationSnapshot? Publication,\n""",
)

replace_once(
    queue,
    """                CanAuthorize: !isFinal && access.CanAuthorizeCeremonies(user));\n""",
    """                CanAuthorize: !isFinal && decision.CanAuthorize && access.CanAuthorizeCeremonies(user));\n""",
)

replace_once(
    test,
    """        var eligibilityResponse = await client.GetAsync(\n            $\"/api/ceremonias/solicitudes/{ceremonyId}/elegibilidad\",\n            cancellationToken);\n        Assert.Equal(HttpStatusCode.OK, eligibilityResponse.StatusCode);\n\n        var eligibilityJson = await eligibilityResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);\n        Assert.True(eligibilityJson.GetProperty(\"canAuthorize\").GetBoolean());\n\n        var queueBeforeResponse = await client.GetAsync(\n""",
    """        var eligibilityBeforeGrandMasterResponse = await client.GetAsync(\n            $\"/api/ceremonias/solicitudes/{ceremonyId}/elegibilidad\",\n            cancellationToken);\n        Assert.Equal(HttpStatusCode.OK, eligibilityBeforeGrandMasterResponse.StatusCode);\n\n        var eligibilityBeforeGrandMasterJson = await eligibilityBeforeGrandMasterResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);\n        Assert.False(eligibilityBeforeGrandMasterJson.GetProperty(\"canAuthorize\").GetBoolean());\n\n        var queueBeforeResponse = await client.GetAsync(\n""",
)

replace_once(
    test,
    """        Assert.True(queueBeforeItem.GetProperty(\"eligibility\").GetProperty(\"canAuthorize\").GetBoolean());\n        Assert.True(queueBeforeItem.GetProperty(\"actions\").GetProperty(\"canValidateInternalAffairs\").GetBoolean());\n        Assert.True(queueBeforeItem.GetProperty(\"actions\").GetProperty(\"canAuthorize\").GetBoolean());\n""",
    """        Assert.False(queueBeforeItem.GetProperty(\"eligibility\").GetProperty(\"canAuthorize\").GetBoolean());\n        Assert.True(queueBeforeItem.GetProperty(\"actions\").GetProperty(\"canValidateInternalAffairs\").GetBoolean());\n        Assert.False(queueBeforeItem.GetProperty(\"actions\").GetProperty(\"canAuthorize\").GetBoolean());\n""",
)

replace_once(
    test,
    """        Assert.False(queueBeforeItem.TryGetProperty(\"institutionalNumber\", out _));\n\n        var authorizeResponse = await client.PostAsync(\n""",
    """        Assert.False(queueBeforeItem.TryGetProperty(\"institutionalNumber\", out _));\n\n        var grandMasterResponse = await client.PostAsJsonAsync(\n            $\"/api/ceremonias/solicitudes/{ceremonyId}/validaciones/gran-maestria\",\n            new\n            {\n                status = CeremonyCodes.ValidationStatus.Approved,\n                sourceReference = \"CI-HTTP-GM\",\n                notes = \"Visto bueno institucional de integración.\"\n            },\n            cancellationToken);\n        Assert.Equal(HttpStatusCode.OK, grandMasterResponse.StatusCode);\n\n        var eligibilityAfterGrandMasterResponse = await client.GetAsync(\n            $\"/api/ceremonias/solicitudes/{ceremonyId}/elegibilidad\",\n            cancellationToken);\n        Assert.Equal(HttpStatusCode.OK, eligibilityAfterGrandMasterResponse.StatusCode);\n        var eligibilityAfterGrandMasterJson = await eligibilityAfterGrandMasterResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);\n        Assert.True(eligibilityAfterGrandMasterJson.GetProperty(\"canAuthorize\").GetBoolean());\n\n        var queueReadyResponse = await client.GetAsync(\n            \"/api/institutional/ceremonias/bandeja\",\n            cancellationToken);\n        Assert.Equal(HttpStatusCode.OK, queueReadyResponse.StatusCode);\n        var queueReadyJson = await queueReadyResponse.Content.ReadFromJsonAsync<JsonElement>(cancellationToken: cancellationToken);\n        var queueReadyItem = queueReadyJson.GetProperty(\"items\")\n            .EnumerateArray()\n            .Single(x => x.GetProperty(\"id\").GetGuid() == ceremonyId);\n        Assert.True(queueReadyItem.GetProperty(\"eligibility\").GetProperty(\"canAuthorize\").GetBoolean());\n        Assert.True(queueReadyItem.GetProperty(\"actions\").GetProperty(\"canAuthorize\").GetBoolean());\n\n        var authorizeResponse = await client.PostAsync(\n""",
)

replace_once(
    test,
    """            Assert.Contains(\"ceremony.internal_affairs_validation.recorded\", auditActions);\n            Assert.Contains(\"ceremony.authorization.approved\", auditActions);\n""",
    """            Assert.Contains(\"ceremony.internal_affairs_validation.recorded\", auditActions);\n            Assert.Contains(\"ceremony.grand_master_validation.recorded\", auditActions);\n            Assert.Contains(\"ceremony.authorization.approved\", auditActions);\n""",
)

print("Ceremony eligibility consistency patch applied.")
