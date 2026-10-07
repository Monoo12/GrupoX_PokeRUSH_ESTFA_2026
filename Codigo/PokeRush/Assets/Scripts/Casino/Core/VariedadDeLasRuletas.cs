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

    // Desde el rebalanceo del 6-oct-2026 todos los pozos son FORMAS FINALES: el
    // juego no tiene evolucion y es corto, asi que un Bulbasaur de la ruleta
    // quedaba Bulbasaur toda la partida. La rareza ahora dice cuan fuerte es la
    // forma final, y el nivel lo sigue poniendo la tabla.

    // Poke Ball: formas finales conocidas de poder parejo (Eeveelutions,
    // Raichu, Scizor...). Ninguna supera a un inicial final.
    public static readonly string[] Comunes =
    {
        "Raichu", "Machamp", "Alakazam", "Vaporeon", "Jolteon", "Flareon", "Espeon",
        "Umbreon", "Leafeon", "Glaceon", "Sylveon", "Luxray", "Houndoom", "Scizor",
        "Heracross", "Altaria", "Absol", "Breloom", "Talonflame", "Lycanroc",
        "Toxtricity", "Corviknight", "Mimikyu", "Krookodile", "Hawlucha", "Donphan",
        "Froslass", "Roserade", "Scolipede", "Drednaw"
    };

    // Super Ball: los iniciales finales de todas las regiones y los favoritos.
    public static readonly string[] Buenas =
    {
        "Venusaur", "Charizard", "Blastoise", "Meganium", "Typhlosion", "Feraligatr",
        "Sceptile", "Blaziken", "Swampert", "Torterra", "Infernape", "Empoleon",
        "Serperior", "Emboar", "Samurott", "Chesnaught", "Delphox", "Greninja",
        "Decidueye", "Incineroar", "Primarina", "Rillaboom", "Cinderace", "Inteleon",
        "Meowscarada", "Skeledirge", "Quaquaval", "Lucario", "Gengar", "Gardevoir",
        "Gallade", "Togekiss", "Chandelure", "Aegislash", "Lapras", "Snorlax",
        "Milotic", "Magnezone", "Weavile", "Mamoswine", "Excadrill", "Conkeldurr",
        "Haxorus", "Annihilape", "Kingambit", "Ceruledge", "Armarouge"
    };

    // Ultra Ball: los pseudolegendarios ya evolucionados y Volcarona.
    public static readonly string[] Raras =
    {
        "Dragonite", "Tyranitar", "Salamence", "Metagross", "Garchomp", "Hydreigon",
        "Goodra", "Kommo-o", "Dragapult", "Baxcalibur", "Volcarona"
    };

    // Master Ball (la mas rara de todas): legendarios. Ninguno de los de
    // portada (Mewtwo, Rayquaza...): esos son de Julieta.
    public static readonly string[] MuyRaras =
    {
        "Articuno", "Zapdos", "Moltres", "Raikou", "Entei", "Suicune", "Latias",
        "Latios", "Heatran", "Cobalion", "Terrakion", "Virizion", "Zeraora",
        "Tapu Koko", "Tapu Lele"
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
