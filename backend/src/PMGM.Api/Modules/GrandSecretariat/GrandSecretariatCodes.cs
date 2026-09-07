namespace PMGM.Api.Modules.GrandSecretariat;

public static class GrandSecretariatCodes
{
    public static class SpaceType
    {
        public const string Temple = "temple";
        public const string SecretariatRoom = "secretariat_room";
    }

    public static class SpaceStatus
    {
        public const string Active = "active";
        public const string Inactive = "inactive";
    }

    public static class ReservationStatus
    {
        public const string Reserved = "reserved";
        public const string Cancelled = "cancelled";
    }

    public static class DocumentType
    {
        public const string Decree = "decree";
        public const string Communication = "communication";
        public const string CeremonyAuthorization = "ceremony_authorization";
    }

    public static class DocumentStatus
    {
        public const string Issued = "issued";
        public const string Voided = "voided";
    }
}
