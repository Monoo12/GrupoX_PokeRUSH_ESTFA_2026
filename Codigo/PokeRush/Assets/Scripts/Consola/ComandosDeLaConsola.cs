using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

// El interprete de la consola de administrador: "dar blaziken 50" -> lo hace y
// devuelve que paso. No dibuja nada (eso es ConsolaDeAdministrador) ni sabe de
// teclas, asi se prueba entero sin escena (ConsolaSmokeTests). Los botones de
// la consola mandan estos mismos comandos: hay un solo camino para cada cosa y
// el registro le enseña al que prueba que escribir.
//
// Lo que toca del juego entra por los delegados de abajo, que por defecto van a
// lo de verdad (GameManager, la pelea en curso, SceneManager). Los tests ponen
// los suyos. Para entregar Pokemon usa PlayerModel.RecibirPokemon, el mismo
// camino que las ruletas y los liberados (equipo si hay lugar, si no el PC).
public class ComandosDeLaConsola
{
    public enum Tipo { Normal, Ok, Error, Eco, Titulo }

    public struct Linea
    {
        public string Texto;
        public Tipo Tipo;
        public Linea(string texto, Tipo tipo) { Texto = texto; Tipo = tipo; }
    }

    public class Comando
    {
        public string Nombre;
        public string[] Alias = new string[0];
        public string Grupo;
        public string Uso;
        public string Descripcion;
        public Action<string[]> Hacer;
        // Lo que se ofrece al autocompletar el argumento. Null = nada.
        public Func<IEnumerable<string>> Argumentos;
    }

    // ---------- lo que toca del juego ----------

    public Func<PlayerModel> Jugador = () => GameManager.Instance != null ? GameManager.Instance.CurrentPlayer : null;
    public Func<StoryModel> Historia = () => GameManager.Instance != null ? GameManager.Instance.Story : null;
    // Si no hay partida (la pantalla de titulo), arranca una de prueba.
    public Func<bool> AsegurarPartida = AsegurarPartidaDePrueba;
    public Func<BattleController> PeleaEnCurso = BuscarPeleaEnCurso;
    public Func<PeleaJulieta> PeleaDeJulieta = () => UnityEngine.Object.FindAnyObjectByType<PeleaJulieta>(FindObjectsInactive.Include);
    public Action<string> CargarEscena = CargarEscenaDelJuego;
    public Func<IList<string>> EscenasDelBuild = EscenasDelBuildSettings;
    public Action Guardar = () => GameManager.Instance?.SaveGame();
    public Func<float> Velocidad = () => Time.timeScale;
    public Action<float> PonerVelocidad = v => Time.timeScale = v;
    // La vista borra su registro.
    public Action Limpiar;

    // Lo que dibuja la vista aparte del registro.
    public bool MostrarFps;

    public const int NivelMaximo = 100;

    // El equipo de "kit": el mismo de la arena de prueba de Julieta
    // (PeleaJulietaBuilder), seis formas finales de Nv 47-50.
    public static readonly (string especie, int nivel)[] Kit =
    {
        ("Lucario", 50), ("Blaziken", 48), ("Garchomp", 50),
        ("Gardevoir", 49), ("Gengar", 49), ("Basculegion", 47)
    };

    private readonly List<Comando> _comandos = new List<Comando>();
    private readonly List<Linea> _salida = new List<Linea>();

    public IReadOnlyList<Comando> Comandos => _comandos;

    public ComandosDeLaConsola()
    {
        Registrar();
    }

    // ---------- ejecutar ----------

    public List<Linea> Ejecutar(string linea)
    {
        _salida.Clear();
        linea = (linea ?? string.Empty).Trim();
        if (linea.Length == 0) return new List<Linea>();

        Escribir("> " + linea, Tipo.Eco);
        string[] partes = Partir(linea);
        Comando comando = Buscar(partes[0]);
        if (comando == null)
        {
            Error($"No conozco '{partes[0]}'. Escribí 'ayuda' para ver los comandos.");
            List<string> parecidos = SugerenciasDeComando(partes[0], 3);
            if (parecidos.Count > 0) Info("¿Quisiste decir " + string.Join(", ", parecidos) + "?");
            return new List<Linea>(_salida);
        }

        try
        {
            comando.Hacer(partes.Skip(1).ToArray());
        }
        catch (Exception e)
        {
            // Una herramienta de prueba no puede tirar el juego abajo.
            Error($"'{comando.Nombre}' falló: {e.Message}");
            Debug.LogException(e);
        }
        return new List<Linea>(_salida);
    }

    public Comando Buscar(string nombre)
    {
        string n = Normalizar(nombre);
        foreach (Comando c in _comandos)
            if (c.Nombre == n || c.Alias.Contains(n)) return c;
        return null;
    }

    // ---------- autocompletar ----------

    // Lo que se ofrece para lo que se lleva escrito: comandos si todavia no
    // hay un espacio, o el argumento del comando si ya lo hay.
    public List<string> Sugerencias(string linea, int maximo = 8)
    {
        linea = linea ?? string.Empty;
        int espacio = linea.IndexOf(' ');
        if (espacio < 0) return SugerenciasDeComando(linea, maximo);

        Comando comando = Buscar(linea.Substring(0, espacio));
        if (comando?.Argumentos == null) return new List<string>();
        string arg = linea.Substring(espacio + 1).TrimStart();
        return Filtrar(comando.Argumentos(), arg, maximo);
    }

    // Tab: si hay una sola opcion la completa entera; si hay varias, hasta
    // donde coinciden todas. Manda lo que EMPIEZA con lo escrito: "ve" es
    // velocidad aunque "nivel" tambien lo contenga.
    public string Completar(string linea)
    {
        linea = linea ?? string.Empty;
        int espacio = linea.IndexOf(' ');
        string antes = espacio < 0 ? string.Empty : linea.Substring(0, espacio + 1);
        string escrito = espacio < 0 ? linea : linea.Substring(espacio + 1).TrimStart();

        List<string> opciones = Sugerencias(linea, 200);
        List<string> empiezan = opciones.Where(o => Normalizar(o).StartsWith(Normalizar(escrito))).ToList();
        if (empiezan.Count > 0) opciones = empiezan;
        if (opciones.Count == 0) return linea;
        if (opciones.Count == 1) return antes + opciones[0] + " ";

        string comun = PrefijoComun(opciones);
        return comun.Length > escrito.Length ? antes + comun : linea;
    }

    private List<string> SugerenciasDeComando(string texto, int maximo)
        => Filtrar(_comandos.Select(c => c.Nombre), texto, maximo);

    // Primero los que empiezan con lo escrito, despues los que lo contienen.
    private static List<string> Filtrar(IEnumerable<string> opciones, string escrito, int maximo)
    {
        string buscado = Normalizar(escrito);
        var empiezan = new List<string>();
        var contienen = new List<string>();
        foreach (string o in opciones)
        {
            string n = Normalizar(o);
            if (n.StartsWith(buscado)) empiezan.Add(o);
            else if (buscado.Length > 1 && n.Contains(buscado)) contienen.Add(o);
        }
        empiezan.Sort(StringComparer.OrdinalIgnoreCase);
        contienen.Sort(StringComparer.OrdinalIgnoreCase);
        return empiezan.Concat(contienen).Take(maximo).ToList();
    }

    private static string PrefijoComun(List<string> textos)
    {
        string p = textos[0];
        foreach (string t in textos)
        {
            int i = 0;
            while (i < p.Length && i < t.Length && char.ToLowerInvariant(p[i]) == char.ToLowerInvariant(t[i])) i++;
            p = p.Substring(0, i);
        }
        return p;
    }

    // ---------- los comandos ----------

    private void Registrar()
    {
        // Equipo
        Agregar("equipo", "Equipo", "equipo", "Muestra el equipo y cuántos hay en el PC.", _ => VerEquipo());
        Agregar("dar", "Equipo", "dar <especie|número> [nivel] [shiny]",
            "Da un Pokémon (al equipo si hay lugar, si no al PC). Sin nivel, el más alto del equipo.",
            Dar, Especies, "pokemon", "agregar");
        Agregar("quitar", "Equipo", "quitar <lugar>", "Manda al PC al Pokémon de ese lugar del equipo (1-6).", Quitar);
        Agregar("kit", "Equipo", "kit", "Equipo de prueba: seis formas finales Nv 47-50 (el de ahora va al PC).", _ => DarKit());
        Agregar("curar", "Equipo", "curar", "Cura todo el equipo (PS, estados y PP).", _ => Curar(), null, "heal");
        Agregar("nivel", "Equipo", "nivel <N> [lugar]", "Sube a nivel N a todo el equipo o a uno (solo sube).", Nivel, null, "lvl");
        Agregar("subir", "Equipo", "subir [cantidad]", "Sube todo el equipo esa cantidad de niveles (5 si no se dice).", Subir);

        // Objetos y plata
        Agregar("plata", "Objetos", "plata <cantidad>", "Suma plata (negativa para sacar).", Plata, null, "dinero", "money");
        Agregar("objeto", "Objetos", "objeto <nombre> [cantidad]", "Da un objeto. Sin nombre, la lista.", Objeto, Objetos, "item");

        // Combate
        Agregar("ganar", "Combate", "ganar", "Gana la pelea en curso (cuenta como victoria normal).", _ => Ganar(), null, "win");
        Agregar("perder", "Combate", "perder", "Pierde la pelea en curso.", _ => Perder());
        Agregar("rival1", "Combate", "rival1", "Deja al Pokémon rival en campo con 1 PS.", _ => RivalEnUnPs(), null, "1ps");
        Agregar("dios", "Combate", "dios [si|no]", "Modo dios: tu equipo no recibe daño.", Dios, SiNo, "god");
        Agregar("fase", "Combate", "fase", "Contra Julieta: la fase actual cambia en el próximo turno.", _ => Fase());

        // Historia y viaje
        Agregar("ir", "Historia", "ir <destino>", "Va a un punto de la historia y deja los flags como si se hubiera jugado hasta ahí.",
            Ir, DestinosDeLaConsola.Claves, "tp");
        Agregar("escena", "Historia", "escena [nombre]", "Carga cualquier escena del build sin tocar la historia.", Escena,
            () => EscenasDelBuild());
        Agregar("flag", "Historia", "flag <nombre> | flag -<nombre>", "Prende un flag de historia (con '-' lo apaga).", Flag, NombresDeFlags);
        Agregar("flags", "Historia", "flags [filtro]", "Lista los flags prendidos.", Flags, NombresDeFlags);
        Agregar("historia", "Historia", "historia", "Cuántos niveles se completaron y cuál sigue.", _ => VerHistoria());

        // Mundo
        Agregar("fantasma", "Mundo", "fantasma [si|no]", "Atravesar paredes.", Fantasma, SiNo, "noclip");
        Agregar("velocidad", "Mundo", "velocidad [x]", "Velocidad del juego (1, 2, 4...). Sin número, la actual.", CambiarVelocidad,
            () => new[] { "1", "2", "4" }, "speed");
        Agregar("guardar", "Mundo", "guardar", "Guarda la partida.", _ => GuardarPartida(), null, "save");
        Agregar("autoguardado", "Mundo", "autoguardado [si|no]", "Guardado automático al cambiar de nivel y al ganar (para no pisar la partida de verdad).",
            Autoguardado, SiNo);
        Agregar("fps", "Mundo", "fps [si|no]", "Muestra los cuadros por segundo.", Fps, SiNo);

        // Consola
        Agregar("ayuda", "Consola", "ayuda [comando]", "Los comandos, o el detalle de uno.", Ayuda,
            () => _comandos.Select(c => c.Nombre), "help", "?");
        Agregar("limpiar", "Consola", "limpiar", "Borra el registro.", _ => Limpiar?.Invoke(), null, "cls", "clear");
    }

    private void Agregar(string nombre, string grupo, string uso, string descripcion, Action<string[]> hacer,
                         Func<IEnumerable<string>> argumentos = null, params string[] alias)
    {
        _comandos.Add(new Comando
        {
            Nombre = nombre, Grupo = grupo, Uso = uso, Descripcion = descripcion,
            Hacer = hacer, Argumentos = argumentos, Alias = alias ?? new string[0]
        });
    }

    // ---------- equipo ----------

    private void VerEquipo()
    {
        PlayerModel jugador = Jugador();
        if (jugador == null) { Error("No hay partida cargada."); return; }

        Titulo($"Equipo de {jugador.PlayerName} (${jugador.Money})");
        if (jugador.Team.Count == 0) Info("(vacío)");
        for (int i = 0; i < jugador.Team.Count; i++)
        {
            PokenModel p = jugador.Team[i];
            Info($"{i + 1}. {p.Definition.pokenName}{(p.IsShiny ? " ★" : "")}  Nv {p.Level}  PS {p.CurrentHP}/{p.MaxHP}  " +
                 string.Join(", ", p.Moves.Select(m => m.Definition.moveName)));
        }
        Info($"PC: {jugador.Storage.Count} Pokémon.");
    }

    private void Dar(string[] args)
    {
        if (args.Length == 0) { Uso("dar"); return; }
        if (!AsegurarPartida()) { Error("No hay partida y no se pudo arrancar una."); return; }
        PlayerModel jugador = Jugador();

        var resto = new List<string>(args);
        bool shiny = false;
        if (resto.Count > 1 && EsShiny(resto[resto.Count - 1]))
        {
            shiny = true;
            resto.RemoveAt(resto.Count - 1);
        }

        int nivel = jugador.Team.Count > 0 ? jugador.Team.Max(p => p.Level) : 50;
        if (resto.Count > 1 && int.TryParse(resto[resto.Count - 1], out int n))
        {
            nivel = n;
            resto.RemoveAt(resto.Count - 1);
        }
        nivel = Mathf.Clamp(nivel, 1, NivelMaximo);

        PokenDefinition especie = BuscarEspecie(string.Join(" ", resto));
        if (especie == null) return;

        var poken = new PokenModel(especie, nivel);
        if (shiny) poken.SetShiny(true);
        PokenRecibido recibido = jugador.RecibirPokemon(poken);
        string quien = $"{especie.pokenName}{(shiny ? " shiny" : "")} Nv {nivel}";
        switch (recibido.Destino)
        {
            case DestinoDePokemon.Equipo: Ok($"{quien} se sumó al equipo."); break;
            case DestinoDePokemon.Pc: Ok($"{quien} fue al PC (caja {recibido.Caja + 1}): el equipo está lleno."); break;
            default: Error($"No hay lugar para {quien}: el equipo y el PC están llenos."); break;
        }
        RefrescarPelea();
    }

    private void Quitar(string[] args)
    {
        PlayerModel jugador = Jugador();
        if (jugador == null) { Error("No hay partida cargada."); return; }
        if (PeleaEnCurso() != null) { Error("No en plena pelea."); return; }
        if (args.Length == 0 || !int.TryParse(args[0], out int lugar)) { Uso("quitar"); return; }
        if (lugar < 1 || lugar > jugador.Team.Count) { Error($"El equipo tiene {jugador.Team.Count} lugares."); return; }
        if (jugador.Team.Count == 1) { Error("Es el único del equipo: el juego no anda sin ninguno."); return; }

        PokenModel poken = jugador.Team[lugar - 1];
        if (!jugador.Storage.TryAgregar(poken, out int caja, out _)) { Error("El PC está lleno."); return; }
        jugador.Team.RemoveAt(lugar - 1);
        Ok($"{poken.Definition.pokenName} fue al PC (caja {caja + 1}).");
    }

    private void DarKit()
    {
        if (!AsegurarPartida()) { Error("No hay partida y no se pudo arrancar una."); return; }
        if (PeleaEnCurso() != null) { Error("No en plena pelea."); return; }
        PlayerModel jugador = Jugador();

        var nuevos = new List<PokenModel>();
        foreach (var (nombre, nivel) in Kit)
        {
            PokenDefinition especie = DefinitionRepository.Instance.GetPoken(nombre);
            if (especie == null) { Error($"No encontré la especie {nombre}."); return; }
            nuevos.Add(new PokenModel(especie, nivel));
        }

        int alPc = 0;
        foreach (PokenModel viejo in jugador.Team.ToList())
        {
            if (!jugador.Storage.TryAgregar(viejo, out _, out _)) { Error("El PC está lleno: no hay donde guardar el equipo actual."); return; }
            jugador.Team.Remove(viejo);
            alPc++;
        }
        jugador.Team.AddRange(nuevos);
        Ok($"Equipo de prueba: {string.Join(", ", nuevos.Select(p => $"{p.Definition.pokenName} {p.Level}"))}." +
           (alPc > 0 ? $" El de antes ({alPc}) está en el PC." : ""));
    }

    private void Curar()
    {
        PlayerModel jugador = Jugador();
        if (jugador == null) { Error("No hay partida cargada."); return; }
        jugador.CurarEquipo();
        RefrescarPelea();
        Ok("Equipo curado.");
    }

    private void Nivel(string[] args)
    {
        PlayerModel jugador = Jugador();
        if (jugador == null) { Error("No hay partida cargada."); return; }
        if (args.Length == 0 || !int.TryParse(args[0], out int objetivo)) { Uso("nivel"); return; }
        objetivo = Mathf.Clamp(objetivo, 1, NivelMaximo);

        List<PokenModel> cuales = jugador.Team;
        if (args.Length > 1)
        {
            if (!int.TryParse(args[1], out int lugar) || lugar < 1 || lugar > jugador.Team.Count)
            {
                Error($"El lugar va de 1 a {jugador.Team.Count}.");
                return;
            }
            cuales = new List<PokenModel> { jugador.Team[lugar - 1] };
        }

        foreach (PokenModel p in cuales) SubirHasta(p, objetivo);
        RefrescarPelea();
        Ok(string.Join(", ", cuales.Select(p => $"{p.Definition.pokenName} Nv {p.Level}")) + ".");
    }

    private void Subir(string[] args)
    {
        PlayerModel jugador = Jugador();
        if (jugador == null) { Error("No hay partida cargada."); return; }
        int cuanto = 5;
        if (args.Length > 0 && !int.TryParse(args[0], out cuanto)) { Uso("subir"); return; }

        foreach (PokenModel p in jugador.Team) SubirHasta(p, Mathf.Clamp(p.Level + cuanto, 1, NivelMaximo));
        RefrescarPelea();
        Ok(string.Join(", ", jugador.Team.Select(p => $"{p.Definition.pokenName} Nv {p.Level}")) + ".");
    }

    // Por experiencia, como al pelear: los stats suben con la formula de
    // siempre y los PS crecen en proporcion. No aprende movimientos (eso lo
    // ofrece el combate al subir) y no baja: AddXP no sabe ir para atras.
    public static void SubirHasta(PokenModel poken, int objetivo)
    {
        ExperienceTableDefinition tabla = PokenModel.DefaultXpTable;
        if (poken == null || tabla == null) return;
        objetivo = Mathf.Min(objetivo, tabla.maxLevel);

        int vueltas = 0;
        while (poken.Level < objetivo && vueltas++ < NivelMaximo)
        {
            int falta = tabla.GetXPForLevel(poken.Level) - poken.CurrentXP;
            if (poken.AddXP(Mathf.Max(1, falta)).Count == 0) break;
        }
    }

    // ---------- objetos y plata ----------

    private void Plata(string[] args)
    {
        if (args.Length == 0 || !int.TryParse(args[0], out int cantidad)) { Uso("plata"); return; }
        if (!AsegurarPartida()) { Error("No hay partida y no se pudo arrancar una."); return; }
        PlayerModel jugador = Jugador();

        if (cantidad >= 0) jugador.AddMoney(cantidad);
        else jugador.SpendMoney(Mathf.Min(-cantidad, jugador.Money));
        Ok($"Plata: ${jugador.Money}.");
    }

    private void Objeto(string[] args)
    {
        if (args.Length == 0)
        {
            Titulo("Objetos:");
            Info(string.Join(", ", Objetos().OrderBy(o => o)));
            return;
        }
        if (!AsegurarPartida()) { Error("No hay partida y no se pudo arrancar una."); return; }
        PlayerModel jugador = Jugador();

        var resto = new List<string>(args);
        int cantidad = 1;
        if (resto.Count > 1 && int.TryParse(resto[resto.Count - 1], out int c))
        {
            cantidad = Mathf.Max(1, c);
            resto.RemoveAt(resto.Count - 1);
        }

        string nombre = Elegir(Objetos(), string.Join(" ", resto), "objeto");
        if (nombre == null) return;

        DefinitionRepository repo = DefinitionRepository.Instance;
        if (repo.GetHealItem(nombre) is HealItemDefinition cura) jugador.Inventory.AddHealItem(cura, cantidad);
        else if (repo.GetBattleItem(nombre) is BattleItemDefinition batalla) jugador.Inventory.AddBattleItem(batalla, cantidad);
        else if (repo.GetSpecialItem(nombre) is SpecialItemDefinition clave) jugador.Inventory.AddSpecialItem(clave, cantidad);
        else { Error($"No encontré el objeto {nombre}."); return; }
        Ok($"{nombre} x{cantidad} a la mochila.");
    }

    // ---------- combate ----------

    private void Ganar()
    {
        BattleController pelea = PeleaEnCurso();
        if (pelea == null) { Error("No hay ninguna pelea en curso."); return; }
        pelea.GanarAhora();
        Ok("Pelea ganada.");
    }

    private void Perder()
    {
        BattleController pelea = PeleaEnCurso();
        if (pelea == null) { Error("No hay ninguna pelea en curso."); return; }
        pelea.Abandonar();
        Ok("Pelea perdida.");
    }

    private void RivalEnUnPs()
    {
        BattleController pelea = PeleaEnCurso();
        if (pelea == null || pelea.Modelo == null) { Error("No hay ninguna pelea en curso."); return; }
        PokenModel rival = pelea.Modelo.ActiveEnemyPoken;
        rival.SetCurrentHP(1);
        pelea.RefrescarHud();
        Ok($"{rival.Definition.pokenName} rival queda con 1 PS.");
    }

    public bool ModoDios => PokenModel.NoRecibeDanio != null;

    private void Dios(string[] args)
    {
        bool prender = LeerSiNo(args, !ModoDios);
        PokenModel.NoRecibeDanio = prender ? EsDelEquipo : null;
        Ok(prender ? "Modo dios: tu equipo no recibe daño." : "Modo dios apagado.");
    }

    private bool EsDelEquipo(PokenModel poken)
    {
        PlayerModel jugador = Jugador();
        return jugador != null && jugador.Team.Contains(poken);
    }

    private void Fase()
    {
        PeleaJulieta julieta = PeleaDeJulieta();
        if (julieta == null || !julieta.Peleando) { Error("Solo sirve en plena pelea contra Julieta."); return; }
        julieta.AdelantarFase();
        Ok($"La fase {julieta.Fase} cambia en el próximo turno.");
    }

    // ---------- historia y viaje ----------

    private void Ir(string[] args)
    {
        if (args.Length == 0)
        {
            Titulo("Destinos (ir <destino>):");
            foreach (DestinosDeLaConsola.Destino d in DestinosDeLaConsola.Todos)
                Info($"{d.Clave,-12} {d.Nombre}  ({d.Escena})");
            return;
        }

        DestinosDeLaConsola.Destino destino = DestinosDeLaConsola.Buscar(string.Join(" ", args));
        if (destino == null)
        {
            Error($"No conozco el destino '{string.Join(" ", args)}'. Opciones: {string.Join(", ", DestinosDeLaConsola.Claves())}.");
            return;
        }
        if (!EstaEnElBuild(destino.Escena)) { Error($"La escena {destino.Escena} no está en Build Settings."); return; }
        if (!AsegurarPartida()) { Error("No hay partida y no se pudo arrancar una."); return; }

        DestinosDeLaConsola.MarcarHistoria(Historia(), destino);
        PlayerModel jugador = Jugador();
        foreach (string nombre in DestinosDeLaConsola.ObjetosHasta(destino))
        {
            SpecialItemDefinition objeto = DefinitionRepository.Instance.GetSpecialItem(nombre);
            if (objeto != null && jugador != null && !jugador.Inventory.HasSpecialItem(objeto))
                jugador.Inventory.AddSpecialItem(objeto);
        }
        Ok($"Historia hasta \"{destino.Nombre}\". Cargando {destino.Escena}...");
        CargarEscena(destino.Escena);
    }

    private void Escena(string[] args)
    {
        IList<string> escenas = EscenasDelBuild();
        if (args.Length == 0)
        {
            Titulo("Escenas del build:");
            for (int i = 0; i < escenas.Count; i++) Info($"{i}. {escenas[i]}");
            return;
        }

        string escena = Elegir(escenas, string.Join(" ", args), "escena");
        if (escena == null) return;
        Ok($"Cargando {escena}...");
        CargarEscena(escena);
    }

    private void Flag(string[] args)
    {
        if (args.Length == 0) { Uso("flag"); return; }
        StoryModel historia = Historia();
        if (historia == null) { Error("No hay partida cargada."); return; }

        string texto = string.Join(" ", args);
        bool apagar = texto.StartsWith("-");
        if (apagar) texto = texto.Substring(1);

        string nombre = Elegir(NombresDeFlags(), texto, "flag");
        if (nombre == null) return;
        StoryFlag flag = (StoryFlag)Enum.Parse(typeof(StoryFlag), nombre);

        if (apagar)
        {
            // No hay evento de "se apago": lo que ya reacciono en la escena
            // (una barrera abierta) queda asi hasta recargarla.
            bool estaba = historia.ActiveFlags.Remove(flag);
            Ok(estaba ? $"{nombre} apagado." : $"{nombre} ya estaba apagado.");
        }
        else
        {
            bool estaba = historia.HasFlag(flag);
            historia.ActivateFlag(flag);
            Ok(estaba ? $"{nombre} ya estaba prendido." : $"{nombre} prendido.");
        }
    }

    private void Flags(string[] args)
    {
        StoryModel historia = Historia();
        if (historia == null) { Error("No hay partida cargada."); return; }

        string filtro = Normalizar(string.Join(" ", args));
        List<string> prendidos = historia.ActiveFlags.Select(f => f.ToString())
            .Where(f => filtro.Length == 0 || Normalizar(f).Contains(filtro))
            .OrderBy(f => f).ToList();
        Titulo($"{prendidos.Count} flags prendidos{(filtro.Length > 0 ? $" con '{string.Join(" ", args)}'" : "")}:");
        if (prendidos.Count > 0) Info(string.Join(", ", prendidos));
    }

    private void VerHistoria()
    {
        StoryModel historia = Historia();
        if (historia == null) { Error("No hay partida cargada."); return; }

        RecorridoDeLaHistoria.Nivel siguiente = RecorridoDeLaHistoria.SiguienteNivel(historia);
        Titulo($"Niveles completados: {RecorridoDeLaHistoria.NivelesCompletados(historia)} de {RecorridoDeLaHistoria.Niveles.Length}.");
        Info(siguiente != null ? $"Sigue {siguiente.Nombre} ({siguiente.EscenaDeEntrada})." : "No queda ningún nivel.");
        Info($"Flags prendidos: {historia.ActiveFlags.Count}. Escena actual: {SceneManager.GetActiveScene().name}.");
    }

    // ---------- mundo ----------

    private void Fantasma(string[] args)
    {
        PlayerController.AtravesarParedes = LeerSiNo(args, !PlayerController.AtravesarParedes);
        Ok(PlayerController.AtravesarParedes ? "Atravesás paredes." : "Las paredes vuelven a frenar.");
    }

    private void CambiarVelocidad(string[] args)
    {
        if (args.Length == 0) { Info($"Velocidad x{Velocidad():0.##}."); return; }
        if (!float.TryParse(args[0].Replace(',', '.').TrimStart('x', 'X'), NumberStyles.Float, CultureInfo.InvariantCulture, out float v))
        {
            Uso("velocidad");
            return;
        }
        v = Mathf.Clamp(v, 0.1f, 10f);
        PonerVelocidad(v);
        Ok($"Velocidad x{v:0.##}.");
    }

    private void GuardarPartida()
    {
        if (Jugador() == null) { Error("No hay partida para guardar."); return; }
        Guardar();
        Ok("Partida guardada.");
    }

    private void Autoguardado(string[] args)
    {
        CasinoDialogo.GuardadoAutomatico = LeerSiNo(args, !CasinoDialogo.GuardadoAutomatico);
        Ok(CasinoDialogo.GuardadoAutomatico ? "Autoguardado prendido." : "Autoguardado apagado: el juego no guarda solo.");
    }

    private void Fps(string[] args)
    {
        MostrarFps = LeerSiNo(args, !MostrarFps);
        Ok(MostrarFps ? "FPS a la vista." : "FPS ocultos.");
    }

    // ---------- consola ----------

    private void Ayuda(string[] args)
    {
        if (args.Length > 0)
        {
            Comando c = Buscar(args[0]);
            if (c == null) { Error($"No conozco '{args[0]}'."); return; }
            Titulo(c.Uso);
            Info(c.Descripcion);
            if (c.Alias.Length > 0) Info("También: " + string.Join(", ", c.Alias));
            return;
        }

        Titulo("Comandos (Tab completa, flechas recorren lo escrito, ayuda <comando> da el detalle):");
        foreach (IGrouping<string, Comando> grupo in _comandos.GroupBy(c => c.Grupo))
            Info($"{grupo.Key}: " + string.Join("  ·  ", grupo.Select(c => c.Uso)));
    }

    // ---------- busquedas ----------

    private IEnumerable<string> Especies() => DefinitionRepository.Instance.NombresDePokens();
    private IEnumerable<string> Objetos() => DefinitionRepository.Instance.NombresDeObjetos();
    private static IEnumerable<string> NombresDeFlags()
        => Enum.GetNames(typeof(StoryFlag)).Where(n => n != nameof(StoryFlag.None));
    private static IEnumerable<string> SiNo() => new[] { "si", "no" };

    // Por numero de Pokedex o por nombre (sin acentos ni mayusculas; alcanza
    // con el principio si no es ambiguo: "blazi").
    private PokenDefinition BuscarEspecie(string texto)
    {
        string nombre;
        if (int.TryParse(texto, out int numero))
        {
            nombre = Especies().FirstOrDefault(e => DefinitionRepository.Instance.NumeroDePokedex(e) == numero);
            if (nombre == null) { Error($"No hay ninguna especie con el número {numero}."); return null; }
        }
        else
        {
            nombre = Elegir(Especies(), texto, "especie");
            if (nombre == null) return null;
        }

        PokenDefinition especie = DefinitionRepository.Instance.GetPoken(nombre);
        if (especie == null) Error($"No se pudo cargar {nombre}.");
        return especie;
    }

    // Exacto si lo hay; si no, el unico que empieza (o, en ultima, contiene)
    // con lo escrito. Si hay varios, los lista y no elige.
    private string Elegir(IEnumerable<string> opciones, string escrito, string que)
    {
        string buscado = Normalizar(escrito);
        if (buscado.Length == 0) { Error($"Falta el nombre del {que}."); return null; }

        List<string> todas = opciones.ToList();
        string exacta = todas.FirstOrDefault(o => Normalizar(o) == buscado);
        if (exacta != null) return exacta;

        List<string> empiezan = todas.Where(o => Normalizar(o).StartsWith(buscado)).ToList();
        if (empiezan.Count == 1) return empiezan[0];
        List<string> candidatas = empiezan.Count > 0 ? empiezan : todas.Where(o => Normalizar(o).Contains(buscado)).ToList();
        if (candidatas.Count == 1) return candidatas[0];

        if (candidatas.Count == 0) Error($"No encontré ningún {que} '{escrito}'.");
        else Error($"'{escrito}' puede ser: {string.Join(", ", candidatas.OrderBy(c => c).Take(10))}{(candidatas.Count > 10 ? "..." : "")}");
        return null;
    }

    private bool EstaEnElBuild(string escena)
        => EscenasDelBuild().Any(e => string.Equals(e, escena, StringComparison.OrdinalIgnoreCase));

    private void RefrescarPelea() => PeleaEnCurso()?.RefrescarHud();

    private static bool EsShiny(string texto)
    {
        string n = Normalizar(texto);
        return n == "shiny" || n == "variocolor";
    }

    // "dios" a secas da vuelta; "dios si" / "dios no" lo deja como se pide.
    private static bool LeerSiNo(string[] args, bool siNoDice)
    {
        if (args.Length == 0) return siNoDice;
        string n = Normalizar(args[0]);
        if (n == "si" || n == "on" || n == "1" || n == "true") return true;
        if (n == "no" || n == "off" || n == "0" || n == "false") return false;
        return siNoDice;
    }

    // Sin acentos, sin mayusculas y sin espacios, puntos ni guiones: "Mr. Mime"
    // y "mrmime" son lo mismo, "Poción" y "pocion" tambien.
    public static string Normalizar(string texto)
    {
        if (string.IsNullOrEmpty(texto)) return string.Empty;
        string separado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(separado.Length);
        foreach (char c in separado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsWhiteSpace(c) || c == '.' || c == '-' || c == '\'' || c == '_' || c == '’') continue;
            sb.Append(char.ToLowerInvariant(c));
        }
        return sb.ToString();
    }

    private static string[] Partir(string linea)
        => linea.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);

    // ---------- salida ----------

    private void Escribir(string texto, Tipo tipo) => _salida.Add(new Linea(texto, tipo));
    private void Ok(string texto) => Escribir(texto, Tipo.Ok);
    private void Error(string texto) => Escribir(texto, Tipo.Error);
    private void Info(string texto) => Escribir(texto, Tipo.Normal);
    private void Titulo(string texto) => Escribir(texto, Tipo.Titulo);
    private void Uso(string comando) => Error("Uso: " + Buscar(comando)?.Uso);

    // ---------- lo de verdad ----------

    private static bool AsegurarPartidaDePrueba()
    {
        GameManager gm = GameManager.Instance;
        if (gm == null) return false;
        if (gm.CurrentPlayer == null) gm.NewGame("PRUEBA");
        return gm.CurrentPlayer != null;
    }

    private static BattleController BuscarPeleaEnCurso()
    {
        foreach (BattleController b in UnityEngine.Object.FindObjectsByType<BattleController>(FindObjectsInactive.Include))
            if (b.EnCurso) return b;
        return null;
    }

    // Antes de cambiar de escena se cierra lo que haya abierto: el sistema de
    // menus sobrevive al cambio y quedaria dibujado encima de la escena nueva.
    private static void CargarEscenaDelJuego(string escena)
    {
        if (MenuManager.HasInstance) MenuManager.Instance.CloseAll();
        SceneManager.LoadScene(escena);
    }

    private static IList<string> EscenasDelBuildSettings()
    {
        var escenas = new List<string>();
        for (int i = 0; i < SceneManager.sceneCountInBuildSettings; i++)
            escenas.Add(System.IO.Path.GetFileNameWithoutExtension(SceneUtility.GetScenePathByBuildIndex(i)));
        return escenas;
    }
}
