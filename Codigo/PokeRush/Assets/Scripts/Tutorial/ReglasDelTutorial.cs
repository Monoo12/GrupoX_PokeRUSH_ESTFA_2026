using System.Collections.Generic;

// Las reglas del tutorial de la habitacion, sin nada de escena: que partes
// faltan, cuando se habilita la pelea y cuando abre la puerta.
//
// Las usan TutorialParte, TutorialDeLaHabitacion, la puerta y los smoke tests
// (que arman partes de mentira sin abrir ninguna escena).
public static class ReglasDelTutorial
{
    // Lo que las reglas necesitan saber de una parte. TutorialParte lo cumple.
    public interface IParte
    {
        // Como se nombra en "te falta ...": "la mochila", "el PC".
        string Nombre { get; }
        // Se activa al terminarla. None = mal configurada: no cuenta.
        StoryFlag Flag { get; }
        // Cuenta para abrir la puerta.
        bool Obligatoria { get; }
        // Se habilita recien cuando no falta ninguna OTRA obligatoria (la pelea).
        bool EsperaALasDemas { get; }
    }

    public static bool Hecha(IParte parte, StoryModel story)
        => parte != null && story != null && parte.Flag != StoryFlag.None && story.HasFlag(parte.Flag);

    // Las obligatorias que faltan, en el orden en que vienen. Una parte sin flag
    // no cuenta: si contara, la puerta no abriria nunca (TutorialDeLaHabitacion
    // avisa al arrancar).
    public static List<IParte> Faltantes(IEnumerable<IParte> partes, StoryModel story)
    {
        var faltan = new List<IParte>();
        if (partes == null) return faltan;

        foreach (IParte p in partes)
            if (p != null && p.Obligatoria && p.Flag != StoryFlag.None && !Hecha(p, story))
                faltan.Add(p);
        return faltan;
    }

    public static bool Completo(IEnumerable<IParte> partes, StoryModel story)
        => story != null && Faltantes(partes, story).Count == 0;

    // Las otras obligatorias que tienen que estar hechas antes de 'parte'. Vacio
    // = se puede hacer ya. Solo pesa para las que esperan a las demas.
    public static List<IParte> FaltanAntesDe(IParte parte, IEnumerable<IParte> partes, StoryModel story)
    {
        var antes = new List<IParte>();
        if (parte == null || !parte.EsperaALasDemas) return antes;

        foreach (IParte p in Faltantes(partes, story))
            if (p != parte && p.Flag != parte.Flag && !p.EsperaALasDemas)
                antes.Add(p);
        return antes;
    }

    public static bool Disponible(IParte parte, IEnumerable<IParte> partes, StoryModel story)
        => FaltanAntesDe(parte, partes, story).Count == 0;

    // La puerta abre con todo hecho, o si ya se habia terminado u omitido antes
    // (un flag que viene del guardado manda sobre como esten las partes).
    public static bool PuedeSalir(IEnumerable<IParte> partes, StoryModel story)
    {
        if (story == null) return false;
        if (story.HasFlag(StoryFlag.Tutorial_Completado) || story.HasFlag(StoryFlag.Tutorial_Omitido)) return true;
        return Completo(partes, story);
    }

    // "la mochila" / "la mochila y el PC" / "la mochila, el PC y la pelea".
    public static string Enumerar(IList<string> nombres)
    {
        if (nombres == null || nombres.Count == 0) return "";
        if (nombres.Count == 1) return nombres[0];

        var primeros = new List<string>(nombres);
        string ultimo = primeros[primeros.Count - 1];
        primeros.RemoveAt(primeros.Count - 1);
        return string.Join(", ", primeros) + " y " + ultimo;
    }

    public static string Enumerar(IEnumerable<IParte> partes)
    {
        var nombres = new List<string>();
        if (partes != null)
            foreach (IParte p in partes)
                if (p != null) nombres.Add(p.Nombre);
        return Enumerar(nombres);
    }

    // Deja al equipo como nuevo, igual que la enfermera del casino: PS al
    // maximo, sin estados y con los PP llenos. La pelea de practica cura antes
    // (para que arranque siempre) y despues (para que nadie salga del cuarto
    // golpeado).
    public static void CurarEquipo(PlayerModel player)
    {
        if (player == null) return;
        foreach (PokenModel poken in player.Team)
        {
            poken.SetCurrentHP(poken.MaxHP);
            poken.CureStatus();
            foreach (MoveModel move in poken.Moves) move.SetCurrentPP(move.Definition.maxPP);
        }
    }
}
