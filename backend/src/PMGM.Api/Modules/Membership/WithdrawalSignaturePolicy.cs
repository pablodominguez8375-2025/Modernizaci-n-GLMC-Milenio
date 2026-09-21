namespace PMGM.Api.Modules.Membership;

public static class WithdrawalSignaturePolicy
{
    public const string Venerable = "venerable";
    public const string Treasurer = "treasurer";
    public const string Orator = "orator";
    public const string Secretary = "secretary";

    public static bool IsValidRole(string role)
        => role is Venerable or Treasurer or Orator or Secretary;

    public static bool IsComplete(
        string? venerable,
        string? treasurer,
        string? orator,
        string? secretary)
        => venerable is not null && treasurer is not null && orator is not null && secretary is not null;
}
