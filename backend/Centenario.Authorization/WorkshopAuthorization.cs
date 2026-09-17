namespace Centenario.Authorization;

public enum WorkshopRole
{
    VenerableMaestro,
    InmediatoExVenerableMaestro,
    PrimerVigilante,
    SegundoVigilante,
    Orador,
    Secretario,
    Tesorero,
    Hospitalario
}

public enum PermissionAction
{
    Ver,
    Crear,
    Editar,
    Registrar,
    Revisar,
    Validar,
    Aprobar,
    Autorizar,
    Firmar,
    Remitir,
    Informar,
    Inspeccionar
}

public enum PermissionBasis
{
    NormativaDirecta,
    ProtocoloInstitucional,
    ControlOperativo
}

public enum WorkshopView
{
    DashboardTaller,
    FichaTaller,
    Hermanos,
    GestionLogial,
    Tenidas,
    Actas,
    ConsejoAdministracion,
    Insinuaciones,
    Ceremonias,
    RetirosTraslados,
    Tesoreria,
    Presupuesto,
    Cuotas,
    Morosidad,
    CuadroMensualTesoreria,
    Hospitalaria,
    TroncoBeneficencia,
    AyudasHospitalarias,
    VisitasHospitalarias,
    ReposicionesHospitalarias,
    DocenciaMaestros,
    DocenciaCompaneros,
    DocenciaAprendices,
    AsistenciaMaestros,
    AsistenciaCompaneros,
    AsistenciaAprendices,
    TrabajosMaestros,
    TrabajosCompaneros,
    TrabajosAprendices,
    EvaluacionColumna,
    BibliotecaVirtual,
    Normativa,
    EleccionesVotaciones,
    MemoriaAnual,
    Decretos,
    Documentos,
    Agenda,
    Notificaciones,
    Reportes,
    BandejaPendientes
}

public sealed record WorkshopPermission(
    WorkshopRole Role,
    WorkshopView View,
    PermissionAction Action,
    PermissionBasis Basis,
    string Source,
    bool Sensitive = false);

public sealed record TemporaryPermissionGrant(
    WorkshopView View,
    PermissionAction Action,
    DateTimeOffset ValidFrom,
    DateTimeOffset ValidUntil,
    string Reason,
    string AuthorityReference)
{
    public bool IsActive(DateTimeOffset at) => at >= ValidFrom && at <= ValidUntil;
}

public sealed record AuthorizationContext(
    Guid UserId,
    Guid WorkshopId,
    WorkshopRole Role,
    IReadOnlyCollection<TemporaryPermissionGrant>? TemporaryGrants = null);

public sealed class WorkshopAuthorizationService
{
    private readonly IReadOnlyCollection<WorkshopPermission> _permissions;

    public WorkshopAuthorizationService(IReadOnlyCollection<WorkshopPermission>? permissions = null)
    {
        _permissions = permissions ?? WorkshopPermissionCatalog.All;
    }

    public bool IsAllowed(
        AuthorizationContext context,
        WorkshopView view,
        PermissionAction action,
        DateTimeOffset at)
    {
        if (_permissions.Any(p =>
                p.Role == context.Role &&
                p.View == view &&
                p.Action == action))
        {
            return true;
        }

        return context.TemporaryGrants?.Any(g =>
                   g.View == view &&
                   g.Action == action &&
                   g.IsActive(at)) == true;
    }

    public IReadOnlyCollection<WorkshopView> GetVisibleViews(
        AuthorizationContext context,
        DateTimeOffset at)
    {
        var baseline = _permissions
            .Where(p => p.Role == context.Role)
            .Select(p => p.View);

        var temporary = context.TemporaryGrants?
                            .Where(g => g.IsActive(at))
                            .Select(g => g.View)
                        ?? Enumerable.Empty<WorkshopView>();

        return baseline
            .Concat(temporary)
            .Distinct()
            .OrderBy(v => v.ToString())
            .ToArray();
    }

    public IReadOnlyCollection<WorkshopPermission> GetRolePermissions(WorkshopRole role) =>
        _permissions.Where(p => p.Role == role).ToArray();
}

public static class HospitalAidAuthorizationGuard
{
    public static bool CanExecuteAid(
        string? councilResolutionId,
        string? venerableApprovalId) =>
        !string.IsNullOrWhiteSpace(councilResolutionId) ||
        !string.IsNullOrWhiteSpace(venerableApprovalId);
}

public static class WithdrawalSignatureGuard
{
    private static readonly WorkshopRole[] Required =
    [
        WorkshopRole.VenerableMaestro,
        WorkshopRole.Tesorero,
        WorkshopRole.Orador,
        WorkshopRole.Secretario
    ];

    public static IReadOnlyCollection<WorkshopRole> RequiredRoles => Required;

    public static bool HasRequiredSignatures(IEnumerable<WorkshopRole> signedRoles)
    {
        var set = signedRoles.ToHashSet();
        return Required.All(set.Contains);
    }
}

public static class WorkshopPermissionCatalog
{
    public static readonly IReadOnlyCollection<WorkshopPermission> All =
    [
        // Venerable Maestro — Art. 11.2 + controles operativos acordados
        N(WorkshopRole.VenerableMaestro, WorkshopView.DashboardTaller, PermissionAction.Ver, "Art. 11.2; supervisión general del Taller"),
        N(WorkshopRole.VenerableMaestro, WorkshopView.FichaTaller, PermissionAction.Ver, "Art. 11.2; responsabilidad general del Taller"),
        N(WorkshopRole.VenerableMaestro, WorkshopView.GestionLogial, PermissionAction.Ver, "Art. 11.2 a-c"),
        N(WorkshopRole.VenerableMaestro, WorkshopView.GestionLogial, PermissionAction.Revisar, "Art. 11.2 a-c"),
        N(WorkshopRole.VenerableMaestro, WorkshopView.Tenidas, PermissionAction.Ver, "Art. 11.2 b"),
        N(WorkshopRole.VenerableMaestro, WorkshopView.Actas, PermissionAction.Revisar, "Art. 11.2 i"),
        N(WorkshopRole.VenerableMaestro, WorkshopView.Actas, PermissionAction.Firmar, "Art. 11.2 i"),
        N(WorkshopRole.VenerableMaestro, WorkshopView.ConsejoAdministracion, PermissionAction.Ver, "Art. 11.2 b; Art. 10.1"),
        P(WorkshopRole.VenerableMaestro, WorkshopView.Insinuaciones, PermissionAction.Ver, "Protocolo institucional 2026"),
        P(WorkshopRole.VenerableMaestro, WorkshopView.Insinuaciones, PermissionAction.Revisar, "Protocolo institucional 2026"),
        P(WorkshopRole.VenerableMaestro, WorkshopView.Ceremonias, PermissionAction.Revisar, "Formulario Solicitud de Ceremonias 2026"),
        P(WorkshopRole.VenerableMaestro, WorkshopView.Ceremonias, PermissionAction.Firmar, "Formulario Solicitud de Ceremonias 2026"),
        P(WorkshopRole.VenerableMaestro, WorkshopView.RetirosTraslados, PermissionAction.Firmar, "Formularios CRV/CRF 2026"),
        N(WorkshopRole.VenerableMaestro, WorkshopView.Tesoreria, PermissionAction.Inspeccionar, "Art. 11.2 h", true),
        O(WorkshopRole.VenerableMaestro, WorkshopView.Tesoreria, PermissionAction.Autorizar, "Control operativo acordado para egresos de Tesorería", true),
        N(WorkshopRole.VenerableMaestro, WorkshopView.Hospitalaria, PermissionAction.Inspeccionar, "Art. 11.2 h", true),
        N(WorkshopRole.VenerableMaestro, WorkshopView.AyudasHospitalarias, PermissionAction.Autorizar, "Art. 12.13 c: socorros acordados por Consejo o Venerable", true),
        O(WorkshopRole.VenerableMaestro, WorkshopView.Documentos, PermissionAction.Ver, "Control operativo para documentos bajo su firma"),
        N(WorkshopRole.VenerableMaestro, WorkshopView.Documentos, PermissionAction.Firmar, "Art. 11.2 i y formularios institucionales aplicables"),
        O(WorkshopRole.VenerableMaestro, WorkshopView.Agenda, PermissionAction.Ver, "Supervisión operativa de actividades del Taller"),
        O(WorkshopRole.VenerableMaestro, WorkshopView.Reportes, PermissionAction.Ver, "Control operativo de supervisión"),
        O(WorkshopRole.VenerableMaestro, WorkshopView.BandejaPendientes, PermissionAction.Ver, "Control operativo de workflow"),

        // Secretario — Art. 12.11
        O(WorkshopRole.Secretario, WorkshopView.DashboardTaller, PermissionAction.Ver, "Control operativo de Secretaría"),
        N(WorkshopRole.Secretario, WorkshopView.FichaTaller, PermissionAction.Ver, "Art. 12.11; colaboración administrativa"),
        O(WorkshopRole.Secretario, WorkshopView.FichaTaller, PermissionAction.Editar, "Implementación operativa de funciones administrativas"),
        O(WorkshopRole.Secretario, WorkshopView.Hermanos, PermissionAction.Ver, "Necesario para comunicaciones y registros administrativos"),
        O(WorkshopRole.Secretario, WorkshopView.Hermanos, PermissionAction.Editar, "Solo datos administrativos dentro de su competencia"),
        N(WorkshopRole.Secretario, WorkshopView.Tenidas, PermissionAction.Crear, "Art. 12.11 a"),
        N(WorkshopRole.Secretario, WorkshopView.Tenidas, PermissionAction.Editar, "Art. 12.11 a"),
        N(WorkshopRole.Secretario, WorkshopView.Tenidas, PermissionAction.Registrar, "Art. 12.11 a"),
        N(WorkshopRole.Secretario, WorkshopView.Actas, PermissionAction.Crear, "Art. 12.11 a-b"),
        N(WorkshopRole.Secretario, WorkshopView.Actas, PermissionAction.Editar, "Art. 12.11 a-b"),
        N(WorkshopRole.Secretario, WorkshopView.Actas, PermissionAction.Firmar, "Art. 12.11 b"),
        O(WorkshopRole.Secretario, WorkshopView.ConsejoAdministracion, PermissionAction.Registrar, "Secretaría y trazabilidad de acuerdos del Consejo"),
        P(WorkshopRole.Secretario, WorkshopView.Insinuaciones, PermissionAction.Crear, "Formulario de Insinuación 2026"),
        P(WorkshopRole.Secretario, WorkshopView.Insinuaciones, PermissionAction.Editar, "Formulario de Insinuación 2026; corrección del mismo expediente"),
        P(WorkshopRole.Secretario, WorkshopView.Insinuaciones, PermissionAction.Remitir, "Formulario/Protocolo 2026"),
        P(WorkshopRole.Secretario, WorkshopView.Ceremonias, PermissionAction.Crear, "Formulario Solicitud de Ceremonias 2026"),
        P(WorkshopRole.Secretario, WorkshopView.Ceremonias, PermissionAction.Editar, "Formulario Solicitud de Ceremonias 2026"),
        P(WorkshopRole.Secretario, WorkshopView.Ceremonias, PermissionAction.Firmar, "Formulario Solicitud de Ceremonias 2026"),
        P(WorkshopRole.Secretario, WorkshopView.Ceremonias, PermissionAction.Remitir, "Protocolo institucional 2026"),
        N(WorkshopRole.Secretario, WorkshopView.RetirosTraslados, PermissionAction.Registrar, "Art. 12.11 h"),
        P(WorkshopRole.Secretario, WorkshopView.RetirosTraslados, PermissionAction.Firmar, "Formularios CRV/CRF 2026"),
        N(WorkshopRole.Secretario, WorkshopView.Documentos, PermissionAction.Crear, "Art. 12.11 i"),
        N(WorkshopRole.Secretario, WorkshopView.Documentos, PermissionAction.Editar, "Art. 12.11 i"),
        N(WorkshopRole.Secretario, WorkshopView.Documentos, PermissionAction.Remitir, "Art. 12.11 c-d"),
        O(WorkshopRole.Secretario, WorkshopView.Agenda, PermissionAction.Crear, "Control operativo de calendario del Taller"),
        O(WorkshopRole.Secretario, WorkshopView.Agenda, PermissionAction.Editar, "Control operativo de calendario del Taller"),
        O(WorkshopRole.Secretario, WorkshopView.Notificaciones, PermissionAction.Crear, "Control operativo de comunicaciones"),
        O(WorkshopRole.Secretario, WorkshopView.Notificaciones, PermissionAction.Remitir, "Control operativo de comunicaciones"),
        O(WorkshopRole.Secretario, WorkshopView.BandejaPendientes, PermissionAction.Ver, "Control operativo de workflow"),

        // Tesorero — Art. 12.12
        O(WorkshopRole.Tesorero, WorkshopView.DashboardTaller, PermissionAction.Ver, "Panel financiero operativo", true),
        N(WorkshopRole.Tesorero, WorkshopView.Tesoreria, PermissionAction.Ver, "Art. 12.12", true),
        N(WorkshopRole.Tesorero, WorkshopView.Tesoreria, PermissionAction.Registrar, "Art. 12.12 c-f", true),
        N(WorkshopRole.Tesorero, WorkshopView.Tesoreria, PermissionAction.Editar, "Art. 12.12 c-f", true),
        N(WorkshopRole.Tesorero, WorkshopView.Presupuesto, PermissionAction.Crear, "Art. 12.12 b", true),
        N(WorkshopRole.Tesorero, WorkshopView.Presupuesto, PermissionAction.Editar, "Art. 12.12 b", true),
        N(WorkshopRole.Tesorero, WorkshopView.Presupuesto, PermissionAction.Remitir, "Art. 12.12 b; consideración del Consejo", true),
        N(WorkshopRole.Tesorero, WorkshopView.Cuotas, PermissionAction.Registrar, "Art. 12.12 c-d", true),
        N(WorkshopRole.Tesorero, WorkshopView.Cuotas, PermissionAction.Editar, "Art. 12.12 c-d", true),
        N(WorkshopRole.Tesorero, WorkshopView.Morosidad, PermissionAction.Ver, "Art. 12.12 e", true),
        N(WorkshopRole.Tesorero, WorkshopView.Morosidad, PermissionAction.Informar, "Art. 12.12 e", true),
        N(WorkshopRole.Tesorero, WorkshopView.CuadroMensualTesoreria, PermissionAction.Crear, "Art. 12.12 f-g", true),
        N(WorkshopRole.Tesorero, WorkshopView.CuadroMensualTesoreria, PermissionAction.Remitir, "Art. 12.12 d,g-h", true),
        P(WorkshopRole.Tesorero, WorkshopView.Ceremonias, PermissionAction.Ver, "Protocolo 2026: antecedente financiero aplicable", true),
        P(WorkshopRole.Tesorero, WorkshopView.RetirosTraslados, PermissionAction.Firmar, "Formularios CRV/CRF 2026"),
        O(WorkshopRole.Tesorero, WorkshopView.Documentos, PermissionAction.Crear, "Comprobantes y rendiciones de Tesorería", true),
        O(WorkshopRole.Tesorero, WorkshopView.Documentos, PermissionAction.Ver, "Comprobantes y rendiciones de Tesorería", true),
        N(WorkshopRole.Tesorero, WorkshopView.ConsejoAdministracion, PermissionAction.Informar, "Art. 12.12 b,e,g", true),
        O(WorkshopRole.Tesorero, WorkshopView.BandejaPendientes, PermissionAction.Ver, "Control operativo financiero", true),

        // Hospitalario — Art. 12.13
        O(WorkshopRole.Hospitalario, WorkshopView.DashboardTaller, PermissionAction.Ver, "Panel hospitalario operativo", true),
        N(WorkshopRole.Hospitalario, WorkshopView.Hospitalaria, PermissionAction.Ver, "Art. 12.13", true),
        N(WorkshopRole.Hospitalario, WorkshopView.Hospitalaria, PermissionAction.Registrar, "Art. 12.13 a-g", true),
        N(WorkshopRole.Hospitalario, WorkshopView.Hospitalaria, PermissionAction.Editar, "Art. 12.13 a-g", true),
        N(WorkshopRole.Hospitalario, WorkshopView.TroncoBeneficencia, PermissionAction.Registrar, "Art. 12.13 b-d", true),
        N(WorkshopRole.Hospitalario, WorkshopView.AyudasHospitalarias, PermissionAction.Crear, "Art. 12.13 c,f", true),
        N(WorkshopRole.Hospitalario, WorkshopView.AyudasHospitalarias, PermissionAction.Registrar, "Art. 12.13 c,f", true),
        N(WorkshopRole.Hospitalario, WorkshopView.AyudasHospitalarias, PermissionAction.Editar, "Art. 12.13 c,f", true),
        N(WorkshopRole.Hospitalario, WorkshopView.VisitasHospitalarias, PermissionAction.Registrar, "Art. 12.13 f", true),
        O(WorkshopRole.Hospitalario, WorkshopView.ReposicionesHospitalarias, PermissionAction.Ver, "Control operativo institucional de reposiciones", true),
        O(WorkshopRole.Hospitalario, WorkshopView.ReposicionesHospitalarias, PermissionAction.Registrar, "Control operativo institucional de reposiciones", true),
        P(WorkshopRole.Hospitalario, WorkshopView.Ceremonias, PermissionAction.Ver, "Protocolo 2026: requisito hospitalario aplicable", true),
        O(WorkshopRole.Hospitalario, WorkshopView.Documentos, PermissionAction.Crear, "Rendiciones y respaldos de Hospitalaria", true),
        O(WorkshopRole.Hospitalario, WorkshopView.Documentos, PermissionAction.Ver, "Rendiciones y respaldos de Hospitalaria", true),
        N(WorkshopRole.Hospitalario, WorkshopView.ConsejoAdministracion, PermissionAction.Informar, "Art. 12.13 e,g", true),
        O(WorkshopRole.Hospitalario, WorkshopView.BandejaPendientes, PermissionAction.Ver, "Control operativo hospitalario", true),

        // Orador — Art. 12.8 y 12.9
        O(WorkshopRole.Orador, WorkshopView.DashboardTaller, PermissionAction.Ver, "Panel normativo operativo"),
        N(WorkshopRole.Orador, WorkshopView.Normativa, PermissionAction.Ver, "Art. 12.8-12.9"),
        N(WorkshopRole.Orador, WorkshopView.GestionLogial, PermissionAction.Revisar, "Art. 12.8-12.9"),
        N(WorkshopRole.Orador, WorkshopView.Actas, PermissionAction.Revisar, "Art. 12.9 e"),
        N(WorkshopRole.Orador, WorkshopView.Actas, PermissionAction.Firmar, "Art. 12.9 e"),
        N(WorkshopRole.Orador, WorkshopView.EleccionesVotaciones, PermissionAction.Validar, "Art. 12.9 f"),
        N(WorkshopRole.Orador, WorkshopView.EleccionesVotaciones, PermissionAction.Registrar, "Art. 12.9 f"),
        N(WorkshopRole.Orador, WorkshopView.MemoriaAnual, PermissionAction.Crear, "Art. 12.9 d"),
        N(WorkshopRole.Orador, WorkshopView.MemoriaAnual, PermissionAction.Editar, "Art. 12.9 d"),
        N(WorkshopRole.Orador, WorkshopView.MemoriaAnual, PermissionAction.Remitir, "Art. 12.9 d"),
        N(WorkshopRole.Orador, WorkshopView.Decretos, PermissionAction.Ver, "Art. 12.9 c"),
        P(WorkshopRole.Orador, WorkshopView.Insinuaciones, PermissionAction.Revisar, "Protocolo 2026: intervención en balotaje"),
        P(WorkshopRole.Orador, WorkshopView.RetirosTraslados, PermissionAction.Firmar, "Formularios CRV/CRF 2026"),
        O(WorkshopRole.Orador, WorkshopView.Documentos, PermissionAction.Revisar, "Control normativo de documentos aplicables"),
        O(WorkshopRole.Orador, WorkshopView.ConsejoAdministracion, PermissionAction.Ver, "Art. 10.1 y función normativa"),
        O(WorkshopRole.Orador, WorkshopView.BandejaPendientes, PermissionAction.Ver, "Control operativo normativo"),

        // Primer Vigilante — Art. 12.3, 12.5, 12.6
        O(WorkshopRole.PrimerVigilante, WorkshopView.DashboardTaller, PermissionAction.Ver, "Panel docente operativo"),
        N(WorkshopRole.PrimerVigilante, WorkshopView.DocenciaCompaneros, PermissionAction.Crear, "Art. 12.5"),
        N(WorkshopRole.PrimerVigilante, WorkshopView.DocenciaCompaneros, PermissionAction.Editar, "Art. 12.5"),
        N(WorkshopRole.PrimerVigilante, WorkshopView.DocenciaCompaneros, PermissionAction.Registrar, "Art. 12.5-12.6"),
        N(WorkshopRole.PrimerVigilante, WorkshopView.AsistenciaCompaneros, PermissionAction.Registrar, "Art. 12.6"),
        N(WorkshopRole.PrimerVigilante, WorkshopView.TrabajosCompaneros, PermissionAction.Registrar, "Art. 12.6"),
        N(WorkshopRole.PrimerVigilante, WorkshopView.EvaluacionColumna, PermissionAction.Registrar, "Art. 12.6"),
        N(WorkshopRole.PrimerVigilante, WorkshopView.EvaluacionColumna, PermissionAction.Informar, "Art. 12.6; Art. 4.5 cuando corresponda"),
        O(WorkshopRole.PrimerVigilante, WorkshopView.Tenidas, PermissionAction.Ver, "Necesario para su función en la Columna"),
        O(WorkshopRole.PrimerVigilante, WorkshopView.ConsejoAdministracion, PermissionAction.Ver, "Art. 10.1"),
        O(WorkshopRole.PrimerVigilante, WorkshopView.BibliotecaVirtual, PermissionAction.Ver, "Acceso por grado/permisos"),
        O(WorkshopRole.PrimerVigilante, WorkshopView.BandejaPendientes, PermissionAction.Ver, "Control operativo docente"),

        // Segundo Vigilante — Art. 12.3, 12.5, 12.6
        O(WorkshopRole.SegundoVigilante, WorkshopView.DashboardTaller, PermissionAction.Ver, "Panel docente operativo"),
        N(WorkshopRole.SegundoVigilante, WorkshopView.DocenciaAprendices, PermissionAction.Crear, "Art. 12.5"),
        N(WorkshopRole.SegundoVigilante, WorkshopView.DocenciaAprendices, PermissionAction.Editar, "Art. 12.5"),
        N(WorkshopRole.SegundoVigilante, WorkshopView.DocenciaAprendices, PermissionAction.Registrar, "Art. 12.5-12.6"),
        N(WorkshopRole.SegundoVigilante, WorkshopView.AsistenciaAprendices, PermissionAction.Registrar, "Art. 12.6"),
        N(WorkshopRole.SegundoVigilante, WorkshopView.TrabajosAprendices, PermissionAction.Registrar, "Art. 12.6"),
        N(WorkshopRole.SegundoVigilante, WorkshopView.EvaluacionColumna, PermissionAction.Registrar, "Art. 12.6"),
        N(WorkshopRole.SegundoVigilante, WorkshopView.EvaluacionColumna, PermissionAction.Informar, "Art. 12.6; Art. 4.5 cuando corresponda"),
        O(WorkshopRole.SegundoVigilante, WorkshopView.Tenidas, PermissionAction.Ver, "Necesario para su función en la Columna"),
        O(WorkshopRole.SegundoVigilante, WorkshopView.ConsejoAdministracion, PermissionAction.Ver, "Art. 10.1"),
        O(WorkshopRole.SegundoVigilante, WorkshopView.BibliotecaVirtual, PermissionAction.Ver, "Acceso por grado/permisos"),
        O(WorkshopRole.SegundoVigilante, WorkshopView.BandejaPendientes, PermissionAction.Ver, "Control operativo docente"),

        // Inmediato Ex-Venerable — Art. 12.2 y 12.5
        O(WorkshopRole.InmediatoExVenerableMaestro, WorkshopView.DashboardTaller, PermissionAction.Ver, "Panel docente y de Consejo"),
        N(WorkshopRole.InmediatoExVenerableMaestro, WorkshopView.DocenciaMaestros, PermissionAction.Crear, "Art. 12.5"),
        N(WorkshopRole.InmediatoExVenerableMaestro, WorkshopView.DocenciaMaestros, PermissionAction.Editar, "Art. 12.5"),
        N(WorkshopRole.InmediatoExVenerableMaestro, WorkshopView.DocenciaMaestros, PermissionAction.Registrar, "Art. 12.5"),
        N(WorkshopRole.InmediatoExVenerableMaestro, WorkshopView.AsistenciaMaestros, PermissionAction.Registrar, "Art. 12.5-12.6 por función docente"),
        N(WorkshopRole.InmediatoExVenerableMaestro, WorkshopView.TrabajosMaestros, PermissionAction.Registrar, "Art. 12.5-12.6 por función docente"),
        O(WorkshopRole.InmediatoExVenerableMaestro, WorkshopView.Tenidas, PermissionAction.Ver, "Art. 12.2; preparación para subrogación"),
        O(WorkshopRole.InmediatoExVenerableMaestro, WorkshopView.ConsejoAdministracion, PermissionAction.Ver, "Art. 10.1"),
        O(WorkshopRole.InmediatoExVenerableMaestro, WorkshopView.BibliotecaVirtual, PermissionAction.Ver, "Acceso autorizado"),
        O(WorkshopRole.InmediatoExVenerableMaestro, WorkshopView.BandejaPendientes, PermissionAction.Ver, "Control operativo docente/Consejo")
    ];

    private static WorkshopPermission N(
        WorkshopRole role,
        WorkshopView view,
        PermissionAction action,
        string source,
        bool sensitive = false) =>
        new(role, view, action, PermissionBasis.NormativaDirecta, source, sensitive);

    private static WorkshopPermission P(
        WorkshopRole role,
        WorkshopView view,
        PermissionAction action,
        string source,
        bool sensitive = false) =>
        new(role, view, action, PermissionBasis.ProtocoloInstitucional, source, sensitive);

    private static WorkshopPermission O(
        WorkshopRole role,
        WorkshopView view,
        PermissionAction action,
        string source,
        bool sensitive = false) =>
        new(role, view, action, PermissionBasis.ControlOperativo, source, sensitive);
}
