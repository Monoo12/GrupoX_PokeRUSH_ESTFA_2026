// El orden de la historia y donde queda cada nivel.
//
//   Menu Principal -> Creacion de personaje -> (habitacion tutorial) ->
//   Quarcho Cueva -> Data Center de Nestor -> Quarcho Cueva -> Casino ->
//   Quarcho Cueva -> Jefe final
//
// La cueva es el lobby: se vuelve a ella al terminar cada nivel y desde ahi se
// sale al siguiente. QUE nivel toca lo deciden los flags de la partida y no la
// escena de donde se viene: SaveService no guarda la escena, asi que al
// "Continuar" el jugador aparece en la cueva y la salida igual sabe adonde va.
//
// Clase pura: la usan la salida de la cueva, Tixi, la vuelta automatica al
// lobby y los smoke tests, y ninguno tiene que saber nombres de escenas.
public static class RecorridoDeLaHistoria
{
    public const string EscenaDelLobby = "Quarcho-Cueva";

    // La habitacion tutorial todavia no existe: mientras tanto la creacion de
    // personaje manda directo a la cueva.
    public const string EscenaDespuesDeCrearPersonaje = EscenaDelLobby;

    public class Nivel
    {
        // Como lo nombran los carteles: "¿Salís hacia el Data Center de Néstor?"
        public string Nombre;
        // La que se carga al salir de la cueva.
        public string EscenaDeEntrada;
        // Todas las del nivel: el casino son cuatro pisos.
        public string[] Escenas;
        // Se enciende al terminar el nivel. None = no hay vuelta a la cueva
        // (el jefe final cierra el juego).
        public StoryFlag FlagCompletado;
    }

    // En orden. Agregar un nivel es sumar una entrada aca.
    public static readonly Nivel[] Niveles =
    {
        new Nivel
        {
            Nombre = "el Data Center de Néstor",
            EscenaDeEntrada = "NestorGOD2",
            Escenas = new[] { "NestorGOD2" },
            FlagCompletado = StoryFlag.Torre1_NestorLiberado
        },
        new Nivel
        {
            Nombre = "el casino",
            EscenaDeEntrada = "Casino Nivel 1",
            Escenas = new[] { "Casino Nivel 1", "Casino Nivel 2", "Casino nivel 3", "Casino piso FINAL" },
            FlagCompletado = StoryFlag.Casino_Completado
        },
        new Nivel
        {
            // La escena todavia no existe: la salida lo avisa en vez de romper.
            Nombre = "el jefe final",
            EscenaDeEntrada = "BossFinal",
            Escenas = new[] { "BossFinal" },
            FlagCompletado = StoryFlag.None
        }
    };

    // El primer nivel que falta completar. Null = no queda ninguno.
    public static Nivel SiguienteNivel(StoryModel story)
    {
        foreach (Nivel nivel in Niveles)
        {
            if (nivel.FlagCompletado == StoryFlag.None) return nivel;
            if (story == null || !story.HasFlag(nivel.FlagCompletado)) return nivel;
        }
        return null;
    }

    // El nivel al que pertenece una escena. Null = no es de ningun nivel (la
    // cueva, los menus, las escenas de prueba).
    public static Nivel NivelDeLaEscena(string escena)
    {
        if (string.IsNullOrEmpty(escena)) return null;
        foreach (Nivel nivel in Niveles)
            foreach (string e in nivel.Escenas)
                if (e == escena) return nivel;
        return null;
    }

    public static bool EsElLobby(string escena) => escena == EscenaDelLobby;

    // Cuantos niveles de la historia estan completos (para los carteles y los
    // tests). Cuenta en orden: un flag suelto de un nivel posterior no suma si
    // falta uno anterior.
    public static int NivelesCompletados(StoryModel story)
    {
        int n = 0;
        foreach (Nivel nivel in Niveles)
        {
            if (nivel.FlagCompletado == StoryFlag.None || story == null || !story.HasFlag(nivel.FlagCompletado))
                break;
            n++;
        }
        return n;
    }
}
