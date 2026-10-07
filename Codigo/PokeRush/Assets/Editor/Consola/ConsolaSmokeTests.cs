using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// La consola de administrador sin abrir ninguna escena: cada comando contra un
// jugador y una historia de prueba, con la carga de escenas y la velocidad
// enchufadas a dobles (ComandosDeLaConsola deja cambiar todo lo que toca).
// Lo que se ve (la ventana, la tecla) se prueba en Play: ver
// Consola_De_Administrador.md.
//
//   PokeRush > Consola > Correr smoke tests de la consola
public static class ConsolaSmokeTests
{
    private static int _passed, _failed;

    [MenuItem("PokeRush/Consola/Correr smoke tests de la consola")]
    public static void RunAll()
    {
        _passed = 0;
        _failed = 0;

        // Lo que los comandos tocan de estatico: se deja como estaba.
        var dios = PokenModel.NoRecibeDanio;
        bool paredes = PlayerController.AtravesarParedes;
        bool autoguardado = CasinoDialogo.GuardadoAutomatico;
        var tablaXp = PokenModel.DefaultXpTable;
        var respaldo = PokenModel.EspecialDeRespaldo;
        PokenModel.EspecialDeRespaldo = null;
        // Lo que hace GameManager al arrancar: sin la tabla, AddXP no sube.
        DefinitionRepository.Instance.LoadAll();
        if (PokenModel.DefaultXpTable == null) PokenModel.DefaultXpTable = DefinitionRepository.Instance.ExperienceTable;
        try
        {
            TestLosComandosEstanBienArmados();
            TestNormalizar();
            TestComandoDesconocido();
            TestDar();
            TestDarConElEquipoLleno();
            TestQuitarYKit();
            TestNivelYSubir();
            TestPlataYObjetos();
            TestFlags();
            TestLosDestinos();
            TestIr();
            TestEscena();
            TestTrucos();
            TestSinPelea();
            TestAutocompletar();
            TestAyuda();
            TestLaOpcionSeGuarda();
        }
        finally
        {
            PokenModel.NoRecibeDanio = dios;
            PlayerController.AtravesarParedes = paredes;
            CasinoDialogo.GuardadoAutomatico = autoguardado;
            PokenModel.DefaultXpTable = tablaXp;
            PokenModel.EspecialDeRespaldo = respaldo;
        }

        if (_failed == 0) Debug.Log($"[ConsolaSmokeTests] TODOS OK: {_passed} checks pasaron.");
        else Debug.LogError($"[ConsolaSmokeTests] {_failed} FALLARON, {_passed} pasaron.");
    }

    private static void Check(bool condicion, string nombre)
    {
        if (condicion) _passed++;
        else
        {
            _failed++;
            Debug.LogError($"[ConsolaSmokeTests] FALLO: {nombre}");
        }
    }

    // ---------- armado ----------

    private class Banco
    {
        public PlayerModel Jugador = new PlayerModel("PRUEBA");
        public StoryModel Historia = new StoryModel();
        public string EscenaCargada;
        public float Velocidad = 1f;
        public int Guardadas;
        public readonly ComandosDeLaConsola Consola = new ComandosDeLaConsola();

        public Banco()
        {
            Consola.Jugador = () => Jugador;
            Consola.Historia = () => Historia;
            Consola.AsegurarPartida = () => true;
            Consola.PeleaEnCurso = () => null;
            Consola.PeleaDeJulieta = () => null;
            Consola.CargarEscena = e => EscenaCargada = e;
            Consola.EscenasDelBuild = () => new List<string>
            {
                "Menu Principal", "habitacion", "Quarcho-Cueva", "NestorGOD2", "Casino Nivel 1",
                "Casino Nivel 2", "Casino nivel 3", "Casino piso FINAL", "BossFinal"
            };
            Consola.Guardar = () => Guardadas++;
            Consola.Velocidad = () => Velocidad;
            Consola.PonerVelocidad = v => Velocidad = v;
        }

        public List<ComandosDeLaConsola.Linea> Correr(string linea) => Consola.Ejecutar(linea);

        public bool Fallo(string linea) => Correr(linea).Any(l => l.Tipo == ComandosDeLaConsola.Tipo.Error);
        public bool Anduvo(string linea)
        {
            List<ComandosDeLaConsola.Linea> salida = Correr(linea);
            return salida.Any(l => l.Tipo == ComandosDeLaConsola.Tipo.Ok) &&
                   !salida.Any(l => l.Tipo == ComandosDeLaConsola.Tipo.Error);
        }
    }

    private static PokenModel Pokemon(string especie, int nivel)
        => new PokenModel(DefinitionRepository.Instance.GetPoken(especie), nivel);

    // ---------- tests ----------

    private static void TestLosComandosEstanBienArmados()
    {
        var consola = new ComandosDeLaConsola();
        Check(consola.Comandos.Count >= 20, $"Hay comandos ({consola.Comandos.Count})");
        Check(consola.Comandos.All(c => !string.IsNullOrEmpty(c.Uso) && !string.IsNullOrEmpty(c.Descripcion) && c.Hacer != null),
              "Todos tienen uso, descripcion y que hacer");

        var nombres = new HashSet<string>();
        bool repetido = false;
        foreach (ComandosDeLaConsola.Comando c in consola.Comandos)
            foreach (string n in new[] { c.Nombre }.Concat(c.Alias))
                if (!nombres.Add(n)) repetido = true;
        Check(!repetido, "Ningun nombre ni alias se repite");
        Check(consola.Comandos.All(c => c.Nombre == ComandosDeLaConsola.Normalizar(c.Nombre)),
              "Los nombres ya estan normalizados (se buscan asi)");
        Check(consola.Buscar("HELP") != null && consola.Buscar("help").Nombre == "ayuda", "Los alias andan y no importan las mayusculas");
    }

    private static void TestNormalizar()
    {
        Check(ComandosDeLaConsola.Normalizar("Poción") == "pocion", "Normalizar saca acentos");
        Check(ComandosDeLaConsola.Normalizar("Mr. Mime") == "mrmime", "Normalizar saca puntos y espacios");
        Check(ComandosDeLaConsola.Normalizar("Casino_Piso1-PaseAscensor") == "casinopiso1paseascensor", "Normalizar saca guiones");
        Check(ComandosDeLaConsola.Normalizar(null) == string.Empty, "Normalizar aguanta null");
    }

    private static void TestComandoDesconocido()
    {
        var b = new Banco();
        List<ComandosDeLaConsola.Linea> salida = b.Correr("cura");
        Check(salida.Count > 0 && salida[0].Tipo == ComandosDeLaConsola.Tipo.Eco && salida[0].Texto == "> cura",
              "Cada comando empieza con su eco");
        Check(salida.Any(l => l.Tipo == ComandosDeLaConsola.Tipo.Error), "Un comando que no existe da error");
        Check(salida.Any(l => l.Texto.Contains("curar")), "Y sugiere el parecido (cura -> curar)");
        Check(b.Correr("   ").Count == 0, "Una linea vacia no hace nada");
    }

    private static void TestDar()
    {
        var b = new Banco();
        Check(b.Anduvo("dar blaziken 44"), "dar blaziken 44");
        Check(b.Jugador.Team.Count == 1 && b.Jugador.Team[0].Definition.pokenName == "Blaziken" && b.Jugador.Team[0].Level == 44,
              "Blaziken Nv 44 al equipo");

        Check(b.Anduvo("dar 448 30"), "dar por numero de Pokedex (448)");
        Check(b.Jugador.Team.Count == 2 && b.Jugador.Team[1].Definition.pokenName == "Lucario" && b.Jugador.Team[1].Level == 30,
              "El 448 es Lucario, Nv 30");

        Check(b.Anduvo("dar GENGAR shiny"), "dar sin nivel y shiny");
        PokenModel gengar = b.Jugador.Team.Last();
        Check(gengar.Definition.pokenName == "Gengar" && gengar.IsShiny, "Gengar shiny");
        Check(gengar.Level == 44, $"Sin nivel, el mas alto del equipo (44, dio {gengar.Level})");

        Check(b.Anduvo("dar garch 200"), "Alcanza con el principio del nombre (garch)");
        Check(b.Jugador.Team.Last().Level == ComandosDeLaConsola.NivelMaximo, "El nivel se recorta al maximo");

        int antes = b.Jugador.Team.Count;
        Check(b.Fallo("dar char"), "Un nombre ambiguo no elige (char: Charmander, Charizard...)");
        Check(b.Fallo("dar noexiste"), "Una especie que no existe da error");
        Check(b.Fallo("dar"), "dar sin nada muestra el uso");
        Check(b.Jugador.Team.Count == antes, "Los errores no agregan nada");
    }

    private static void TestDarConElEquipoLleno()
    {
        var b = new Banco();
        for (int i = 0; i < PlayerModel.EquipoMaximo; i++) b.Jugador.Team.Add(Pokemon("Gengar", 40));
        List<ComandosDeLaConsola.Linea> salida = b.Correr("dar lucario 40");
        Check(b.Jugador.Team.Count == PlayerModel.EquipoMaximo, "Con el equipo lleno no se pasa de 6");
        Check(b.Jugador.Storage.Count == 1, "El que sobra va al PC (el mismo camino que las ruletas)");
        Check(salida.Any(l => l.Tipo == ComandosDeLaConsola.Tipo.Ok && l.Texto.Contains("PC")), "Y lo dice");
    }

    private static void TestQuitarYKit()
    {
        var b = new Banco();
        b.Jugador.Team.Add(Pokemon("Gengar", 40));
        Check(b.Fallo("quitar 1"), "No deja sacar al unico del equipo");
        b.Jugador.Team.Add(Pokemon("Lucario", 40));
        Check(b.Anduvo("quitar 1"), "quitar 1");
        Check(b.Jugador.Team.Count == 1 && b.Jugador.Team[0].Definition.pokenName == "Lucario" && b.Jugador.Storage.Count == 1,
              "El Gengar fue al PC y quedo el Lucario");
        Check(b.Fallo("quitar 5"), "Un lugar que no existe da error");

        Check(b.Anduvo("kit"), "kit");
        Check(b.Jugador.Team.Count == 6, "El kit son seis");
        Check(b.Jugador.Team.Select(p => p.Definition.pokenName).SequenceEqual(ComandosDeLaConsola.Kit.Select(k => k.especie)),
              "Los seis del kit, en orden");
        Check(b.Jugador.Storage.Count == 2, "El equipo de antes fue al PC");
    }

    private static void TestNivelYSubir()
    {
        var b = new Banco();
        b.Jugador.Team.Add(Pokemon("Gengar", 40));
        b.Jugador.Team.Add(Pokemon("Lucario", 45));
        int psAntes = b.Jugador.Team[0].MaxHP;

        Check(b.Anduvo("nivel 47"), "nivel 47");
        Check(b.Jugador.Team.All(p => p.Level == 47), "Todo el equipo a 47");
        Check(b.Jugador.Team[0].MaxHP > psAntes, "Los PS suben con el nivel");

        Check(b.Anduvo("nivel 50 2"), "nivel 50 2");
        Check(b.Jugador.Team[0].Level == 47 && b.Jugador.Team[1].Level == 50, "Solo el del lugar 2");

        Check(b.Anduvo("nivel 30"), "nivel 30 no rompe");
        Check(b.Jugador.Team[1].Level == 50, "Pero no baja");

        Check(b.Anduvo("subir"), "subir");
        Check(b.Jugador.Team[0].Level == 52 && b.Jugador.Team[1].Level == 55, "subir suma 5");
        Check(b.Anduvo("subir 3"), "subir 3");
        Check(b.Jugador.Team[0].Level == 55, "subir 3 suma 3");
        Check(b.Fallo("nivel"), "nivel sin numero muestra el uso");
    }

    private static void TestPlataYObjetos()
    {
        var b = new Banco();
        Check(b.Anduvo("plata 5000"), "plata 5000");
        Check(b.Jugador.Money == 5000, "Suma la plata");
        Check(b.Anduvo("plata -100000"), "plata negativa");
        Check(b.Jugador.Money == 0, "Saca, pero nunca deja menos de 0");
        Check(b.Fallo("plata mucha"), "plata sin numero muestra el uso");

        string cura = DefinitionRepository.Instance.NombresDeObjetos()
            .FirstOrDefault(n => DefinitionRepository.Instance.GetHealItem(n) != null);
        Check(cura != null, "Hay objetos de curacion para probar");
        if (cura == null) return;

        Check(b.Anduvo($"objeto {cura} 3"), $"objeto {cura} 3");
        Check(b.Jugador.Inventory.CountHealItem(DefinitionRepository.Instance.GetHealItem(cura)) == 3, "Tres a la mochila");
        Check(b.Anduvo($"item {ComandosDeLaConsola.Normalizar(cura).ToUpperInvariant()}"), "Por alias, sin acentos ni mayusculas");
        Check(b.Jugador.Inventory.CountHealItem(DefinitionRepository.Instance.GetHealItem(cura)) == 4, "Sin cantidad, uno");
        Check(b.Fallo("objeto cosaquenoexiste"), "Un objeto que no existe da error");
        Check(b.Correr("objeto").Any(l => l.Texto.Contains(cura)), "objeto sin nombre lista los objetos");
    }

    private static void TestFlags()
    {
        var b = new Banco();
        Check(b.Anduvo("flag Torre1_NestorLiberado"), "flag Torre1_NestorLiberado");
        Check(b.Historia.HasFlag(StoryFlag.Torre1_NestorLiberado), "Queda prendido");
        Check(b.Anduvo("flag -torre1nestorliberado"), "flag -... (sin mayusculas ni guiones)");
        Check(!b.Historia.HasFlag(StoryFlag.Torre1_NestorLiberado), "Queda apagado");
        Check(b.Fallo("flag casino_piso"), "Un flag ambiguo no elige");
        Check(b.Fallo("flag noexiste"), "Un flag que no existe da error");

        b.Historia.ActivateFlag(StoryFlag.Casino_MalabaristaVencido);
        Check(b.Correr("flags malabarista").Any(l => l.Texto.Contains("Casino_MalabaristaVencido")), "flags con filtro los lista");
        Check(b.Correr("flags").Any(l => l.Texto.StartsWith("1 flags")), "flags cuenta los prendidos");
    }

    private static void TestLosDestinos()
    {
        DestinosDeLaConsola.Destino[] todos = DestinosDeLaConsola.Todos;
        Check(todos.Length == 10, $"Diez destinos ({todos.Length})");

        foreach (DestinosDeLaConsola.Destino d in todos)
            Check(RecorridoDeLaHistoria.EstaEnBuildSettings(d.Escena), $"La escena de '{d.Clave}' ({d.Escena}) esta en Build Settings");

        var vistos = new HashSet<StoryFlag>();
        bool repetido = todos.SelectMany(d => d.Flags).Any(f => !vistos.Add(f));
        Check(!repetido, "Ningun flag esta en dos destinos (se acumulan)");
        Check(todos.All(d => d.Clave == ComandosDeLaConsola.Normalizar(d.Clave)), "Las claves ya estan normalizadas");
        foreach (string objeto in todos.SelectMany(d => d.Objetos))
            Check(DefinitionRepository.Instance.GetSpecialItem(objeto) != null, $"El objeto clave '{objeto}' existe");

        Check(DestinosDeLaConsola.Buscar("CUEVA")?.Clave == "cueva", "Buscar por clave exacta gana aunque haya otras que empiezan igual");
        Check(DestinosDeLaConsola.Buscar("casinof")?.Clave == "casinofinal", "Buscar por el principio");
        Check(DestinosDeLaConsola.Buscar("cas") == null, "Un principio ambiguo no elige");

        // Cada nivel del recorrido tiene su destino de entrada.
        foreach (RecorridoDeLaHistoria.Nivel nivel in RecorridoDeLaHistoria.Niveles)
            Check(todos.Any(d => d.Escena == nivel.EscenaDeEntrada), $"Hay un destino para {nivel.Nombre}");
    }

    private static void TestIr()
    {
        var b = new Banco();
        b.Historia.ActivateFlag(StoryFlag.Casino_MalabaristaVencido);

        Check(b.Anduvo("ir casino2"), "ir casino2");
        Check(b.EscenaCargada == "Casino Nivel 2", "Carga el piso 2");
        Check(b.Historia.HasFlag(StoryFlag.Lobby_TixiPresentado) && b.Historia.HasFlag(StoryFlag.Torre1_NestorLiberado) &&
              b.Historia.HasFlag(StoryFlag.Casino_Piso1_PaseAscensor), "Prende lo de antes (Tixi, Nestor, el pase del piso 1)");
        Check(!b.Historia.HasFlag(StoryFlag.Casino_Piso2_PaseAscensor), "Pero no lo de despues (el pase del piso 2)");
        Check(RecorridoDeLaHistoria.SiguienteNivel(b.Historia)?.EscenaDeEntrada == "Casino Nivel 1",
              "Para el recorrido, el siguiente nivel es el casino");

        Check(b.Anduvo("ir julieta"), "ir julieta");
        Check(b.EscenaCargada == "BossFinal" && b.Historia.HasFlag(StoryFlag.Casino_Completado) &&
              b.Historia.HasFlag(StoryFlag.Historia_TixiTransmisor), "La arena, con el casino completo y el transmisor");
        Check(RecorridoDeLaHistoria.NivelesCompletados(b.Historia) == 2, "Dos niveles completos");
        DefinitionRepository repo = DefinitionRepository.Instance;
        Check(b.Jugador.Inventory.HasSpecialItem(repo.GetSpecialItem("Tarjeta VIP")) &&
              b.Jugador.Inventory.HasSpecialItem(repo.GetSpecialItem("Tarjeta de acceso P4")) &&
              b.Jugador.Inventory.HasSpecialItem(repo.GetSpecialItem("Transmisor de Tixi")),
              "Y los objetos clave de la historia (Tarjeta VIP, la del piso 4, el transmisor)");
        b.Correr("ir julieta");
        Check(b.Jugador.Inventory.SpecialItems.All(s => s.Quantity == 1), "Ir dos veces no los duplica");

        Check(b.Anduvo("ir datacenter"), "ir datacenter (para atras)");
        Check(b.EscenaCargada == "NestorGOD2", "Carga el Data Center");
        Check(!b.Historia.HasFlag(StoryFlag.Torre1_NestorLiberado) && !b.Historia.HasFlag(StoryFlag.Casino_Completado),
              "Apaga lo de despues: Nestor vuelve a estar sin liberar");
        Check(b.Historia.HasFlag(StoryFlag.Lobby_TixiPresentado), "Y deja lo de antes");
        Check(b.Historia.HasFlag(StoryFlag.Casino_MalabaristaVencido), "Lo que no es un hito queda como estaba");

        b.EscenaCargada = null;
        b.Consola.EscenasDelBuild = () => new List<string> { "Menu Principal" };
        Check(b.Fallo("ir casino1") && b.EscenaCargada == null, "Si la escena no esta en el build, no carga nada");
        Check(b.Fallo("ir marte"), "Un destino que no existe da error");
        Check(b.Correr("ir").Any(l => l.Texto.Contains("casinofinal")), "ir solo lista los destinos");
    }

    private static void TestEscena()
    {
        var b = new Banco();
        Check(b.Anduvo("escena menu"), "escena menu");
        Check(b.EscenaCargada == "Menu Principal", "Carga por un pedazo del nombre");
        Check(b.Fallo("escena casino"), "Ambigua no carga");
        Check(b.Correr("escena").Any(l => l.Texto.Contains("BossFinal")), "Sin nombre, lista las del build");
    }

    private static void TestTrucos()
    {
        var b = new Banco();
        b.Jugador.Team.Add(Pokemon("Gengar", 40));
        PokenModel mio = b.Jugador.Team[0];
        PokenModel rival = Pokemon("Lucario", 40);

        Check(b.Anduvo("dios"), "dios");
        Check(b.Consola.ModoDios, "Modo dios prendido");
        int ps = mio.CurrentHP;
        mio.TakeDamage(50);
        Check(mio.CurrentHP == ps, "Con modo dios el equipo no recibe dano");
        int psRival = rival.CurrentHP;
        rival.TakeDamage(50);
        Check(rival.CurrentHP == psRival - 50, "El rival si");
        Check(b.Anduvo("dios no"), "dios no");
        mio.TakeDamage(50);
        Check(!b.Consola.ModoDios && mio.CurrentHP == ps - 50, "Apagado, vuelve a recibir");

        bool antes = PlayerController.AtravesarParedes;
        b.Correr("fantasma");
        Check(PlayerController.AtravesarParedes == !antes, "fantasma da vuelta atravesar paredes");
        b.Correr("noclip no");
        Check(!PlayerController.AtravesarParedes, "noclip no lo apaga");

        Check(b.Anduvo("velocidad 2") && Mathf.Approximately(b.Velocidad, 2f), "velocidad 2");
        Check(b.Anduvo("velocidad x4") && Mathf.Approximately(b.Velocidad, 4f), "velocidad x4");
        Check(b.Anduvo("velocidad 100") && Mathf.Approximately(b.Velocidad, 10f), "La velocidad se recorta a 10");
        Check(b.Anduvo("velocidad 1,5") && Mathf.Approximately(b.Velocidad, 1.5f), "Acepta coma decimal");

        b.Correr("autoguardado no");
        Check(!CasinoDialogo.GuardadoAutomatico, "autoguardado no lo apaga");
        b.Correr("autoguardado si");
        Check(CasinoDialogo.GuardadoAutomatico, "autoguardado si lo prende");

        Check(b.Anduvo("guardar") && b.Guardadas == 1, "guardar llama al guardado");
        b.Correr("fps");
        Check(b.Consola.MostrarFps, "fps los muestra");

        mio.SetCurrentHP(1);
        Check(b.Anduvo("curar") && mio.CurrentHP == mio.MaxHP, "curar cura");
    }

    private static void TestSinPelea()
    {
        var b = new Banco();
        Check(b.Fallo("ganar"), "ganar sin pelea avisa");
        Check(b.Fallo("perder"), "perder sin pelea avisa");
        Check(b.Fallo("rival1"), "rival1 sin pelea avisa");
        Check(b.Fallo("fase"), "fase sin Julieta avisa");
    }

    private static void TestAutocompletar()
    {
        var c = new Banco().Consola;
        Check(c.Completar("cur") == "curar ", "Tab completa el comando (cur -> curar)");
        Check(c.Completar("ve") == "velocidad ", "Tab completa el comando (ve -> velocidad)");
        Check(c.Completar("ir casinof") == "ir casinofinal ", "Tab completa el destino");
        Check(c.Completar("dar blazik") == "dar Blaziken ", "Tab completa la especie");
        Check(c.Completar("flag Torre1_Firewall").StartsWith("flag Torre1_FirewallNodo"),
              "Con varias opciones completa hasta donde coinciden");
        Check(c.Completar("zzz") == "zzz", "Sin opciones no toca nada");
        Check(c.Sugerencias("dar blazi").Contains("Blaziken"), "Sugiere especies");
        Check(c.Sugerencias("ir cue").Count == 3, "Sugiere las tres cuevas");
        Check(c.Sugerencias("").Count > 0, "Sin nada escrito sugiere comandos");
    }

    private static void TestAyuda()
    {
        var b = new Banco();
        Check(b.Correr("ayuda").Count > 6, "ayuda lista los grupos de comandos");
        Check(b.Correr("ayuda dar").Any(l => l.Texto.StartsWith("dar <especie")), "ayuda dar da el uso");
        Check(b.Fallo("ayuda nada"), "ayuda de algo que no existe avisa");

        int limpiezas = 0;
        b.Consola.Limpiar = () => limpiezas++;
        b.Correr("cls");
        Check(limpiezas == 1, "limpiar (cls) le pide a la vista que borre");
    }

    private static void TestLaOpcionSeGuarda()
    {
        bool antes = GameSettings.ConsolaHabilitada;
        try
        {
            GameSettings.ConsolaHabilitada = true;
            Check(GameSettings.ConsolaHabilitada, "La opcion de la consola se prende");
            GameSettings.ConsolaHabilitada = false;
            Check(!GameSettings.ConsolaHabilitada, "Y se apaga");
        }
        finally
        {
            GameSettings.ConsolaHabilitada = antes;
        }
    }
}
