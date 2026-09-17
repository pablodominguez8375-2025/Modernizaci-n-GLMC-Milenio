using Centenario.Authorization;

static void Assert(bool condition, string message)
{
    if (!condition)
    {
        throw new InvalidOperationException(message);
    }
}

var auth = new WorkshopAuthorizationService();
var now = DateTimeOffset.UtcNow;
var workshopId = Guid.NewGuid();
var userId = Guid.NewGuid();

var treasurer = new AuthorizationContext(userId, workshopId, WorkshopRole.Tesorero);
Assert(auth.IsAllowed(treasurer, WorkshopView.Tesoreria, PermissionAction.Registrar, now),
    "El Tesorero debe poder registrar Tesorería.");
Assert(!auth.IsAllowed(treasurer, WorkshopView.Hospitalaria, PermissionAction.Editar, now),
    "El Tesorero no debe editar Hospitalaria.");

var hospitalario = new AuthorizationContext(userId, workshopId, WorkshopRole.Hospitalario);
Assert(auth.IsAllowed(hospitalario, WorkshopView.AyudasHospitalarias, PermissionAction.Registrar, now),
    "Hospitalaria debe poder registrar ayudas.");
Assert(!auth.IsAllowed(hospitalario, WorkshopView.Tesoreria, PermissionAction.Editar, now),
    "Hospitalaria no debe editar Tesorería.");

var orador = new AuthorizationContext(userId, workshopId, WorkshopRole.Orador);
Assert(auth.IsAllowed(orador, WorkshopView.EleccionesVotaciones, PermissionAction.Validar, now),
    "El Orador debe controlar elecciones y votaciones.");
Assert(!auth.IsAllowed(orador, WorkshopView.Tesoreria, PermissionAction.Autorizar, now),
    "El Orador no debe autorizar Tesorería.");

var primerVigilante = new AuthorizationContext(userId, workshopId, WorkshopRole.PrimerVigilante);
Assert(auth.IsAllowed(primerVigilante, WorkshopView.DocenciaCompaneros, PermissionAction.Registrar, now),
    "El Primer Vigilante debe registrar docencia de Compañeros.");
Assert(!auth.IsAllowed(primerVigilante, WorkshopView.DocenciaAprendices, PermissionAction.Editar, now),
    "El Primer Vigilante no debe administrar docencia de Aprendices.");

var segundoVigilante = new AuthorizationContext(userId, workshopId, WorkshopRole.SegundoVigilante);
Assert(auth.IsAllowed(segundoVigilante, WorkshopView.DocenciaAprendices, PermissionAction.Registrar, now),
    "El Segundo Vigilante debe registrar docencia de Aprendices.");

var immediatePast = new AuthorizationContext(userId, workshopId, WorkshopRole.InmediatoExVenerableMaestro);
Assert(auth.IsAllowed(immediatePast, WorkshopView.DocenciaMaestros, PermissionAction.Registrar, now),
    "El Inmediato Ex-Venerable debe registrar docencia de Maestros.");
Assert(!auth.IsAllowed(immediatePast, WorkshopView.Tesoreria, PermissionAction.Autorizar, now),
    "La subrogación no debe heredarse automáticamente.");

var temporaryGrant = new TemporaryPermissionGrant(
    WorkshopView.Actas,
    PermissionAction.Firmar,
    now.AddMinutes(-5),
    now.AddHours(2),
    "Subrogación formal registrada",
    "ACTA-CONSEJO-001");

var subrogating = immediatePast with
{
    TemporaryGrants = new[] { temporaryGrant }
};

Assert(auth.IsAllowed(subrogating, WorkshopView.Actas, PermissionAction.Firmar, now),
    "Una subrogación formal puede otorgar permisos temporales explícitos.");

Assert(HospitalAidAuthorizationGuard.CanExecuteAid("CA-2026-001", null),
    "Una ayuda hospitalaria puede ejecutarse por acuerdo del Consejo.");
Assert(HospitalAidAuthorizationGuard.CanExecuteAid(null, "VM-2026-001"),
    "Una ayuda hospitalaria puede ejecutarse por aprobación del Venerable.");
Assert(!HospitalAidAuthorizationGuard.CanExecuteAid(null, null),
    "Una ayuda hospitalaria no puede ejecutarse sin autorización válida.");

Assert(WithdrawalSignatureGuard.HasRequiredSignatures(new[]
{
    WorkshopRole.VenerableMaestro,
    WorkshopRole.Tesorero,
    WorkshopRole.Orador,
    WorkshopRole.Secretario
}), "CRV/CRF requiere los cuatro firmantes institucionales.");

Assert(!WithdrawalSignatureGuard.HasRequiredSignatures(new[]
{
    WorkshopRole.VenerableMaestro,
    WorkshopRole.Orador,
    WorkshopRole.Secretario
}), "CRV/CRF no debe completarse si falta el Tesorero.");

Console.WriteLine("Centenario.Authorization smoke tests: OK");
