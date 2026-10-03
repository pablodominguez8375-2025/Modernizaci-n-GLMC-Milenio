namespace PMGM.Api.Modules.Admissions;

public static class WithdrawalLetterPolicy
{
    // Evaluated at case creation. Legacy dates are never inferred.
    public static string? ModeAtCreation(DateOnly? grantedDate, DateOnly today)
    {
        if (grantedDate is null || grantedDate > today) return null;
        // Avoid overflow near DateOnly.MaxValue while retaining the inclusive rule.
        if (grantedDate > DateOnly.MaxValue.AddMonths(-3))
            return AdmissionCodes.AffiliationMode.Simple;
        return today <= grantedDate.Value.AddMonths(3)
            ? AdmissionCodes.AffiliationMode.Simple
            : AdmissionCodes.AffiliationMode.Activation;
    }
}
