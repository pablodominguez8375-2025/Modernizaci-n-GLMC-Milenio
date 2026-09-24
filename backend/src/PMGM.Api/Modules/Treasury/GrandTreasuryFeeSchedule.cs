namespace PMGM.Api.Modules.Treasury;

public static class GrandTreasuryFeeSchedule
{
    public const string SourceReference = "Decreto N.º 1.759 — cuotas 2026";
    public static readonly DateOnly EffectiveFrom = new(2026, 1, 1);

    public const string Santiago = "santiago";
    public const string OtherOriente = "other_oriente";
    public const string Peru = "peru";
    public const string PastActiveMembershipType = "past_active";

    public static bool IsValidTerritory(string value) => value is Santiago or OtherOriente or Peru;

    public static (decimal Amount, string Currency)? Resolve(string feeType, string territory, DateOnly asOf)
    {
        if (asOf < EffectiveFrom) return null;
        if (feeType == TreasuryCodes.LodgeFeeType.PastActive) return (0m, "CLP");

        // The decree states USD 6 for ordinary dues in Peru, but the current
        // statement/payment model is CLP-only. Do not convert or create a CLP charge.
        if (territory == Peru) return null;

        if (territory is not (Santiago or OtherOriente)) return null;
        var amount = feeType switch
        {
            TreasuryCodes.LodgeFeeType.Normal => territory == Santiago ? 21000m : 15000m,
            TreasuryCodes.LodgeFeeType.Spouse => territory == Santiago ? 13000m : 10000m,
            TreasuryCodes.LodgeFeeType.Senior => territory == Santiago ? 10000m : 8000m,
            TreasuryCodes.LodgeFeeType.Student => 8000m,
            _ => (decimal?)null
        };
        return amount is null ? null : (amount.Value, "CLP");
    }

    public static IReadOnlyList<object> Rates =>
    [
        new { feeType = TreasuryCodes.LodgeFeeType.Normal, territory = Santiago, amount = 21000m, currency = "CLP" },
        new { feeType = TreasuryCodes.LodgeFeeType.Normal, territory = OtherOriente, amount = 15000m, currency = "CLP" },
        new { feeType = TreasuryCodes.LodgeFeeType.Normal, territory = Peru, amount = 6m, currency = "USD" },
        new { feeType = TreasuryCodes.LodgeFeeType.Spouse, territory = Santiago, amount = 13000m, currency = "CLP" },
        new { feeType = TreasuryCodes.LodgeFeeType.Spouse, territory = OtherOriente, amount = 10000m, currency = "CLP" },
        new { feeType = TreasuryCodes.LodgeFeeType.Senior, territory = Santiago, amount = 10000m, currency = "CLP" },
        new { feeType = TreasuryCodes.LodgeFeeType.Senior, territory = OtherOriente, amount = 8000m, currency = "CLP" },
        new { feeType = TreasuryCodes.LodgeFeeType.Student, territory = Santiago, amount = 8000m, currency = "CLP" },
        new { feeType = TreasuryCodes.LodgeFeeType.Student, territory = OtherOriente, amount = 8000m, currency = "CLP" }
    ];

    public static IReadOnlyList<object> CeremonyRights =>
    [
        new { ceremonyType = "initiation", label = "Iniciación", amount = 41000m, currency = "CLP" },
        new { ceremonyType = "wage_increase", label = "Aumento de salario", amount = 31000m, currency = "CLP" },
        new { ceremonyType = "exaltation", label = "Exaltación", amount = 41000m, currency = "CLP" },
        new { ceremonyType = "affiliation", label = "Afiliación", amount = 26000m, currency = "CLP" },
        new { ceremonyType = "incorporation", label = "Incorporación", amount = 31000m, currency = "CLP" }
    ];

    public static (decimal Amount, string Currency)? ResolveCeremonyRight(string ceremonyType, DateOnly asOf)
    {
        if (asOf < EffectiveFrom) return null;
        var amount = ceremonyType switch
        {
            "initiation" => 41000m,
            "wage_increase" => 31000m,
            "exaltation" => 41000m,
            "affiliation" => 26000m,
            "incorporation" => 31000m,
            _ => (decimal?)null
        };
        return amount is null ? null : (amount.Value, "CLP");
    }
}
