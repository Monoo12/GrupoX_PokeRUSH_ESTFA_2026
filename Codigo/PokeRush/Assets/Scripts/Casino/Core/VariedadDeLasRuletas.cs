using System.Collections.Generic;

// Que Pokemon trae cada gajo de las ruletas de premios (playtest del 5-oct:
// "mas variedad, que no te toquen repetidos del equipo, conocidos y epicos,
// pero que no se vayan de chetados en las Poke Balls normales").
//
// Las tablas de premios siguen diciendo CUANTOS gajos son Pokemon, con que
// peso, a que nivel y de que rareza (el icono de la ball: Poke, Super, Ultra o
// Master). Lo que cambia es la especie: cada vez que se abre una ruleta, cada
// gajo de Pokemon sortea una del pozo de su rareza que el jugador no tenga (ni
// en el equipo ni en el PC) y que no este ya en otro gajo de la misma rueda.
// Si el pozo se agota, queda la especie que traia la tabla.
//
// Los pozos van por nombre y se cargan con DefinitionRepository recien cuando
// salen: cargar todas las especies de una vez son cientos de MB (ver
// IndiceDeDefiniciones).
public static class VariedadDeLasRuletas
{
    public enum Rareza { Comun, Buena, Rara, MuyRara }

    // Poke Ball: iniciales de todas las regiones y los mas conocidos, en su
    // primera forma. Nada que se vaya de mambo al nivel de las ruletas.
    public static readonly string[] Comunes =
    {
        "Bulbasaur", "Charmander", "Squirtle", "Pikachu", "Eevee",
        "Chikorita", "Cyndaquil", "Totodile", "Treecko", "Torchic", "Mudkip",
        "Turtwig", "Chimchar", "Piplup", "Snivy", "Tepig", "Oshawott",
        "Chespin", "Fennekin", "Froakie", "Rowlet", "Litten", "Popplio",
        "Grookey", "Scorbunny", "Sobble", "Sprigatito", "Fuecoco", "Quaxly",
        "Togepi", "Shinx", "Houndour", "Gastly", "Abra", "Machop", "Magnemite",
        "Swablu", "Snorunt", "Litwick", "Rockruff", "Fletchling", "Drilbur", "Sneasel"
    };

    // Super Ball: los que todos quieren tener, todavia sin evolucionar.
    public static readonly string[] Buenas =
    {
        "Riolu", "Ralts", "Axew", "Scyther", "Porygon", "Honedge", "Mimikyu",
        "Absol", "Munchlax", "Charcadet", "Noibat", "Toxel", "Tinkatink",
        "Rookidee", "Sableye", "Larvesta", "Hatenna", "Aron"
    };

    // Ultra Ball: los pseudolegendarios en su primera forma y dos clasicos.
    public static readonly string[] Raras =
    {
        "Dratini", "Larvitar", "Bagon", "Beldum", "Gible", "Deino", "Goomy",
        "Jangmo-o", "Dreepy", "Frigibax", "Lapras", "Snorlax"
    };

    // Master Ball (la mas rara de todas): los epicos, ya evolucionados.
    public static readonly string[] MuyRaras =
    {
        "Lucario", "Charizard", "Greninja", "Blaziken", "Gengar", "Gardevoir",
        "Umbreon", "Sylveon", "Infernape", "Decidueye"
    };

    public static string[] Pozo(Rareza rareza)
    {
        switch (rareza)
        {
            case Rareza.Buena: return Buenas;
            case Rareza.Rara: return Raras;
            case Rareza.MuyRara: return MuyRaras;
            default: return Comunes;
        }
    }

    // La rareza la dice el icono del gajo (icon_pokeball, icon_greatball...).
    public static Rareza RarezaDe(Prize premio)
    {
        string icono = premio != null && premio.icon != null ? premio.icon.name.ToLowerInvariant() : "";
        if (icono.Contains("master")) return Rareza.MuyRara;
        if (icono.Contains("ultra")) return Rareza.Rara;
        if (icono.Contains("great") || icono.Contains("super")) return Rareza.Buena;
        return Rareza.Comun;
    }

    // Las especies que el jugador ya tiene (equipo y PC), por nombre.
    public static HashSet<string> EspeciesDelJugador(PlayerModel jugador)
    {
        var tiene = new HashSet<string>();
        if (jugador == null) return tiene;
        foreach (PokenModel p in jugador.Team)
            if (p?.Definition != null) tiene.Add(p.Definition.pokenName);
        if (jugador.Storage != null)
            for (int caja = 0; caja < jugador.Storage.Cajas; caja++)
                for (int lugar = 0; lugar < PokenStorageModel.LugaresPorCaja; lugar++)
                {
                    PokenModel p = jugador.Storage.Get(caja, lugar);
                    if (p?.Definition != null) tiene.Add(p.Definition.pokenName);
                }
        return tiene;
    }

    // Copia de los premios con la especie de cada gajo de Pokemon sorteada.
    // Los premios que no son Pokemon quedan como estaban (los mismos objetos).
    // 'cargar' trae una especie por nombre (null = no existe); sin el, no se
    // sortea nada y solo se evitan repetidos con lo que hay en la tabla.
    public static Prize[] Preparar(Prize[] premios, PlayerModel jugador,
                                   System.Func<string, PokenDefinition> cargar, System.Random rng)
    {
        if (premios == null) return null;
        rng ??= new System.Random();

        HashSet<string> tomadas = EspeciesDelJugador(jugador);
        var resultado = new Prize[premios.Length];

        for (int i = 0; i < premios.Length; i++)
        {
            Prize original = premios[i];
            if (original == null || original.poken == null) { resultado[i] = original; continue; }

            PokenDefinition especie = cargar != null ? Sortear(RarezaDe(original), tomadas, cargar, rng) : null;
            // Sin pozo disponible: la de la tabla, aunque se repita (mejor un
            // repetido que un gajo vacio).
            if (especie == null) especie = original.poken;

            Prize copia = original.Copiar();
            copia.poken = especie;
            copia.prizeName = especie.pokenName;
            copia.shiny = original.shiny && especie == original.poken;
            tomadas.Add(especie.pokenName);
            resultado[i] = copia;
        }
        return resultado;
    }

    private static PokenDefinition Sortear(Rareza rareza, HashSet<string> tomadas,
                                           System.Func<string, PokenDefinition> cargar, System.Random rng)
    {
        var libres = new List<string>();
        foreach (string nombre in Pozo(rareza))
            if (!tomadas.Contains(nombre)) libres.Add(nombre);

        while (libres.Count > 0)
        {
            int i = rng.Next(libres.Count);
            PokenDefinition def = cargar(libres[i]);
            if (def != null) return def;
            libres.RemoveAt(i);   // no esta en el juego: se prueba otro
        }
        return null;
    }
}
