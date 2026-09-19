namespace PMGM.Api.Modules.LodgeManagement;

public static class LodgeMeetingClosurePolicy
{
    public static string? Validate(
        string meetingStatus,
        string? ceremonyType,
        Guid? extractDocumentVersionId,
        Guid? ceremonyAuthorizationDocumentId)
    {
        if (meetingStatus == LodgeManagementCodes.MeetingStatus.Cancelled)
            return "Una Tenida cancelada no puede cerrarse.";

        if (meetingStatus == LodgeManagementCodes.MeetingStatus.Closed)
            return "La Tenida ya se encuentra cerrada.";

        if (meetingStatus != LodgeManagementCodes.MeetingStatus.Held)
            return "La Tenida debe estar Realizada antes de iniciar su cierre documental.";

        if (extractDocumentVersionId is null)
            return "Debe adjuntar el Extracto de Acta en PDF antes de cerrar la Tenida.";

        if (ceremonyType is not null && ceremonyAuthorizationDocumentId is null)
            return "Una Tenida ceremonial requiere la Plancha de Autorización de Ceremonia emitida por Gran Secretaría antes de cerrarse.";

        return null;
    }
}
