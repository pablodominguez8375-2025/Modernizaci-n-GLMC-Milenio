namespace PMGM.Api.Modules.Treasury;

public static class MemberTreasuryCodes
{
    public const string LodgeTreasuryRole = "lodge_treasury";

    public static class Currency
    {
        public const string Clp = "CLP";
        public static bool IsValid(string value)
            => string.Equals(value, Clp, StringComparison.OrdinalIgnoreCase);
    }

    public static class ChargeType
    {
        public const string OrdinaryDue = "ordinary_due";
        public const string Other = "other";

        public static bool IsValid(string value)
            => value is OrdinaryDue or Other;
    }

    public static class ChargeStatus
    {
        public const string Open = "open";
        public const string Paid = "paid";
        public const string Void = "void";

        public static bool IsValid(string value)
            => value is Open or Paid or Void;
    }

    public static class PaymentMethod
    {
        public const string Cash = "cash";
        public const string Transfer = "transfer";
        public const string Card = "card";
        public const string Other = "other";

        public static bool IsValid(string value)
            => value is Cash or Transfer or Card or Other;
    }
}
