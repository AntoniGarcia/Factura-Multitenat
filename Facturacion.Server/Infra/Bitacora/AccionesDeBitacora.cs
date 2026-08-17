namespace Facturacion.Server.Infra.Bitacora;

/// <summary>
/// Acciones de la bitácora. Son cadenas fijas para poder filtrarlas después sin adivinar
/// cómo las escribió cada quien.
/// </summary>
public static class AccionesDeBitacora
{
    public const string InicioSesion = "inicio_sesion";
    public const string InicioSesionFallido = "inicio_sesion_fallido";
    public const string CierreSesion = "cierre_sesion";
    public const string TokenRotado = "token_rotado";
    public const string FamiliaInvalidada = "familia_invalidada";
    public const string EmpresaCambiada = "empresa_cambiada";

    /// <summary>
    /// Reutilización de un refresh token ya consumido: la señal de que alguien copió la
    /// cookie. Se registra aparte del resto para poder buscarla sola.
    /// </summary>
    public const string ReutilizacionDeToken = "reutilizacion_de_token";

    // ── Empresa, certificados y folios (fase 4) ────────────────────────────────────────
    public const string EmpresaActualizada = "empresa_actualizada";
    public const string ConfiguracionActualizada = "configuracion_actualizada";
    public const string LogoActualizado = "logo_actualizado";
    public const string LogoQuitado = "logo_quitado";
    public const string CsdCargado = "csd_cargado";
    public const string CsdReemplazado = "csd_reemplazado";

    /// <summary>
    /// Entrega de la llave privada al módulo de timbrado. Se registra <b>cada</b> llamada:
    /// es el único rastro de quién pidió el material del sello y cuándo (CLAUDE.md §4).
    /// </summary>
    public const string CsdEntregadoParaTimbrar = "csd_entregado_para_timbrar";

    public const string SerieCreada = "serie_creada";
    public const string SerieActualizada = "serie_actualizada";
    public const string FolioReservado = "folio_reservado";
    public const string FolioConfirmado = "folio_confirmado";

    /// <summary>
    /// Folio apartado que no llegó a usarse. Es lo que justifica un hueco en la numeración
    /// ante una revisión; el folio no se recicla (CLAUDE.md §5).
    /// </summary>
    public const string FolioAbandonado = "folio_abandonado";

    // ── Clientes (fase 5) ──────────────────────────────────────────────────────────────
    public const string ClienteCreado = "cliente_creado";
    public const string ClienteActualizado = "cliente_actualizado";
    public const string ClienteDesactivado = "cliente_desactivado";
    public const string ClienteReactivado = "cliente_reactivado";

    // ── Productos (fase 6) ─────────────────────────────────────────────────────────────
    public const string ProductoCreado = "producto_creado";
    public const string ProductoActualizado = "producto_actualizado";
    public const string ProductoDesactivado = "producto_desactivado";
    public const string ProductoReactivado = "producto_reactivado";

    // ── Timbres (fase 7) ───────────────────────────────────────────────────────────────
    // Todo lo que toca dinero deja registro, incluida la reserva: es la única forma de
    // explicar después por qué el saldo bajó sin que se emitiera ningún comprobante.
    public const string CompraSolicitada = "compra_solicitada";
    public const string CompraAcreditada = "compra_acreditada";
    public const string TimbreReservado = "timbre_reservado";
    public const string TimbreConsumido = "timbre_consumido";
    public const string TimbreDevuelto = "timbre_devuelto";
    public const string ReservaAbandonada = "reserva_de_timbre_abandonada";

    // ── Usuarios e invitaciones (fase 8) ───────────────────────────────────────────────
    public const string UsuarioInvitado = "usuario_invitado";
    public const string AccesoOtorgadoDirecto = "acceso_otorgado_directo";
    public const string InvitacionReenviada = "invitacion_reenviada";
    public const string InvitacionRevocada = "invitacion_revocada";
    public const string InvitacionAceptada = "invitacion_aceptada";
    public const string PermisosActualizados = "permisos_actualizados";
    public const string UsuarioDesactivado = "usuario_desactivado";
    public const string UsuarioReactivado = "usuario_reactivado";
    public const string PerfilActualizado = "perfil_actualizado";
    public const string ContrasenaCambiada = "contrasena_cambiada";
}

/// <summary>Entidades sobre las que registra la bitácora.</summary>
public static class EntidadesDeBitacora
{
    public const string Sesion = "Sesion";
    public const string RefreshToken = "RefreshToken";
    public const string Empresa = "Empresa";
    public const string ConfiguracionEmpresa = "ConfiguracionEmpresa";
    public const string CertificadoCsd = "CertificadoCsd";
    public const string Serie = "Serie";
    public const string ReservaFolio = "ReservaFolio";
    public const string Cliente = "Cliente";
    public const string Producto = "Producto";
    public const string CompraTimbres = "CompraTimbres";
    public const string ReservaTimbre = "ReservaTimbre";
    public const string Usuario = "Usuario";
    public const string UsuarioEmpresa = "UsuarioEmpresa";
    public const string Invitacion = "Invitacion";
    public const string Perfil = "Perfil";
}
