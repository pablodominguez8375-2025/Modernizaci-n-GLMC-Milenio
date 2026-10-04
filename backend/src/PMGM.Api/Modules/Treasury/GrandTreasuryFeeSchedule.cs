namespace PMGM.Api.Modules.Treasury;

public static class GrandTreasuryFeeSchedule
{
    public const string Santiago = "santiago";
    public const string OtherOriente = "other_oriente";
    public const string Peru = "peru";
    public const string PastActiveMembershipType = "past_active";
    public const string Clp = "CLP";
    public const string Usd = "USD";
    public static bool IsValidTerritory(string value) => value is Santiago or OtherOriente or Peru;
    public static bool HasOrdinaryDues(string membershipType) => membershipType != PastActiveMembershipType;
    public static bool IsOrdinaryFeeType(string feeType) => feeType is TreasuryCodes.LodgeFeeType.Normal or
        TreasuryCodes.LodgeFeeType.Student or TreasuryCodes.LodgeFeeType.Senior or TreasuryCodes.LodgeFeeType.Spouse;
    public static bool IsChargedCeremony(string type) => type is "initiation" or "wage_increase" or "exaltation" or "affiliation" or "incorporation";
}
