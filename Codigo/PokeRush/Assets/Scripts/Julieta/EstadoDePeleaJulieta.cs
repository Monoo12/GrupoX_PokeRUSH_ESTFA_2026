using System.Collections.Generic;
using UnityEngine;

// Lo que el guion necesita saber de la pelea para decidir que momento suena.
//
// Clase pura: no toca la escena ni la UI. La llena PeleaJulieta (mirando el
// BattleModel y contando lo que pasa) y la lee GuionEnCurso. Asi las reglas de
// "cuando cambia de fase" se prueban enteras en JulietaSmokeTests sin abrir un
// combate.
public class EstadoDePeleaJulieta
{
    public FaseDeJulieta Fase { get; private set; } = FaseDeJulieta.Analisis;

    // Ya se paso por el chequeo del arranque (los momentos AlEmpezar sonaron).
    public bool Empezo { get; private set; }

    // El turno del combate que esta por jugarse (BattleModel.CurrentTurn).
    public int TurnoActual { get; private set; } = 1;
    private int _turnoAlEntrarALaFase = 1;
    public int TurnosEnLaFase => Mathf.Max(0, TurnoActual - _turnoAlEntrarALaFase);

    // Del rival y del jugador (se refrescan con Mirar).
    public int RivalLeQuedan;
    public int RivalActivoPorcentaje = 100;   // 0-100
    public bool RivalEnElUltimo;
    public int JugadorCaidos;

    // Contadores de lo que paso.
    public int LecturasDeNestor;
    public int GirosEnLaFase;
    public int GirosTotales;
    public int VidaDelPJPorcentaje = 100;     // 0-100
    public int AtaquesAlPJ;
    public bool ControladoCayo;
    public bool RivalSalvado;

    public void Mirar(BattleModel modelo)
    {
        if (modelo == null) return;

        TurnoActual = modelo.CurrentTurn;

        RivalLeQuedan = 0;
        foreach (PokenModel p in modelo.Enemy.Team)
            if (p != null && !p.IsFainted) RivalLeQuedan++;

        PokenModel activo = modelo.ActiveEnemyPoken;
        RivalActivoPorcentaje = activo != null && activo.MaxHP > 0
            ? Mathf.CeilToInt(100f * activo.CurrentHP / activo.MaxHP)
            : 0;
        RivalEnElUltimo = BattleTeamRules.EsElUltimo(modelo.Enemy.Team, modelo.ActiveEnemyIndex);

        JugadorCaidos = 0;
        foreach (PokenModel p in modelo.Player.Team)
            if (p != null && p.IsFainted) JugadorCaidos++;
    }

    public void MarcarEmpezado() => Empezo = true;

    // Para los tests y para una pelea que arranca sin BattleModel todavia.
    public void PonerTurno(int turno) => TurnoActual = Mathf.Max(1, turno);

    public void CambiarDeFase(FaseDeJulieta fase)
    {
        Fase = fase;
        _turnoAlEntrarALaFase = TurnoActual;
        GirosEnLaFase = 0;
    }

    // Para probar: hace de cuenta que la fase actual ya lleva 'n' turnos mas.
    // En el proximo chequeo suenan los momentos por turnos de la fase (y el
    // cambio de fase, que es uno de ellos). Se pierde al cambiar de fase.
    public void AdelantarTurnosDeLaFase(int n) => _turnoAlEntrarALaFase -= Mathf.Max(0, n);

    public void ContarGiro()
    {
        GirosEnLaFase++;
        GirosTotales++;
    }

    public void Reiniciar()
    {
        Fase = FaseDeJulieta.Analisis;
        Empezo = false;
        TurnoActual = 1;
        _turnoAlEntrarALaFase = 1;
        RivalLeQuedan = 0;
        RivalActivoPorcentaje = 100;
        RivalEnElUltimo = false;
        JugadorCaidos = 0;
        LecturasDeNestor = 0;
        GirosEnLaFase = 0;
        GirosTotales = 0;
        VidaDelPJPorcentaje = 100;
        AtaquesAlPJ = 0;
        ControladoCayo = false;
        RivalSalvado = false;
    }

    public string Resumen() =>
        $"fase {Fase} (turno {TurnoActual}, {TurnosEnLaFase} en la fase), rival {RivalLeQuedan} en pie " +
        $"({RivalActivoPorcentaje}% el activo), jugador {JugadorCaidos} caidos, Nestor {LecturasDeNestor}, " +
        $"giros {GirosEnLaFase}/{GirosTotales}, PJ {VidaDelPJPorcentaje}% ({AtaquesAlPJ} golpes)" +
        (RivalSalvado ? ", rival salvado" : "") + (ControladoCayo ? ", controlado caido" : "");
}

// Que momento del guion suena ahora. Cada momento suena UNA vez por pelea.
//
// Clase pura, como el estado. PeleaJulieta la consulta en los chequeos (al
// arrancar y antes de cada turno) y reproduce lo que devuelve; como un momento
// puede cambiar la fase, vuelve a preguntar hasta que no quede ninguno.
public class GuionEnCurso
{
    private readonly MomentoDeJulieta[] _momentos;
    private readonly HashSet<int> _sonaron = new HashSet<int>();

    public GuionEnCurso(MomentoDeJulieta[] momentos)
    {
        _momentos = momentos ?? new MomentoDeJulieta[0];
    }

    public int Sonaron => _sonaron.Count;

    public void Reiniciar() => _sonaron.Clear();

    public bool YaSono(string nombre)
    {
        for (int i = 0; i < _momentos.Length; i++)
            if (_momentos[i] != null && _momentos[i].nombre == nombre) return _sonaron.Contains(i);
        return false;
    }

    // El primer momento pendiente que se cumple, y lo marca como sonado. Null si
    // no hay ninguno.
    public MomentoDeJulieta Siguiente(EstadoDePeleaJulieta estado)
    {
        for (int i = 0; i < _momentos.Length; i++)
        {
            MomentoDeJulieta m = _momentos[i];
            if (m == null || _sonaron.Contains(i)) continue;
            if (!m.cualquierFase && m.fase != estado.Fase) continue;
            if (!SeCumple(m, estado)) continue;

            _sonaron.Add(i);
            return m;
        }
        return null;
    }

    public static bool SeCumple(MomentoDeJulieta momento, EstadoDePeleaJulieta estado)
    {
        if (momento?.cuando == null) return false;
        foreach (CondicionDeJulieta c in momento.cuando)
            if (c != null && Cumple(c, estado)) return true;
        return false;
    }

    public static bool Cumple(CondicionDeJulieta c, EstadoDePeleaJulieta e)
    {
        switch (c.cuando)
        {
            case DisparadorDeJulieta.AlEmpezar:           return !e.Empezo;
            case DisparadorDeJulieta.AlEntrarALaFase:     return e.TurnosEnLaFase == 0;
            case DisparadorDeJulieta.TurnosEnLaFase:      return e.TurnosEnLaFase >= c.valor;
            case DisparadorDeJulieta.AlRivalLeQuedan:     return e.RivalLeQuedan <= c.valor;
            case DisparadorDeJulieta.RivalActivoBajo:     return e.RivalActivoPorcentaje <= c.valor;
            case DisparadorDeJulieta.UltimoDelRival:      return e.RivalEnElUltimo;
            case DisparadorDeJulieta.JugadorPerdio:       return e.JugadorCaidos >= Mathf.Max(1, c.valor);
            case DisparadorDeJulieta.NestorLeyo:          return e.LecturasDeNestor >= Mathf.Max(1, c.valor);
            case DisparadorDeJulieta.RuletaGiroEnLaFase:  return e.GirosEnLaFase >= Mathf.Max(1, c.valor);
            case DisparadorDeJulieta.VidaDelPJBaja:       return e.VidaDelPJPorcentaje <= c.valor;
            case DisparadorDeJulieta.ControladoAtacoAlPJ: return e.AtaquesAlPJ >= Mathf.Max(1, c.valor);
            case DisparadorDeJulieta.ControladoCayo:      return e.ControladoCayo;
            case DisparadorDeJulieta.RivalSalvado:        return e.RivalSalvado;
            default: return false;
        }
    }
}
