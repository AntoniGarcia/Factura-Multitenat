using Facturacion.Shared.Operador;

namespace Facturacion.Pruebas;

public sealed class PermisosGranularesDeOperadorPruebas
{
    [Fact]
    public void Crear_paquetes_no_autoriza_editar_ni_retirar()
    {
        var permisos = PermisosDePanel.DesdeAsignacionesGuardadas(
            [PermisosDePanel.VerPaquetes, PermisosDePanel.CrearPaquetes]);

        Assert.True(PermisosDePanel.Autoriza(permisos, PermisosDePanel.CrearPaquetes));
        Assert.False(PermisosDePanel.Autoriza(permisos, PermisosDePanel.EditarPaquetes));
        Assert.False(PermisosDePanel.Autoriza(permisos, PermisosDePanel.RetirarPaquetes));
    }

    [Fact]
    public void Los_permisos_agrupados_anteriores_se_pueden_separar_al_guardar()
    {
        var permisos = PermisosDePanel.DesdeAsignacionesGuardadas(
            [PermisosDePanel.VerCompras, PermisosDePanel.AcreditarCompras]);
        Assert.True(PermisosDePanel.Autoriza(permisos, PermisosDePanel.SoloAcreditarCompras));
        Assert.True(PermisosDePanel.Autoriza(permisos, PermisosDePanel.DescartarCompras));
        Assert.DoesNotContain(PermisosDePanel.AcreditarCompras, permisos);

        var guardados = PermisosDePanel.DesdeAsignacionesGuardadas(permisos.Except([PermisosDePanel.DescartarCompras]));
        Assert.True(PermisosDePanel.Autoriza(guardados, PermisosDePanel.SoloAcreditarCompras));
        Assert.False(PermisosDePanel.Autoriza(guardados, PermisosDePanel.DescartarCompras));
    }
}
