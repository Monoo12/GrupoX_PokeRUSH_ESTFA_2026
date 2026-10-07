using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

// Banco de balance: juega la partida entera con el motor de verdad (TurnEngine,
// la IA de cada rival y la lectura aliada de Nestor) sin escenas ni
// animaciones, cientos de veces, y dice que tan dificil salio cada combate.
//
// Lo que juega del lado del jugador es un "jugador razonable": pega con lo que
// mas rinde contra el tipo del rival (MoveScorer, el mismo de la IA), cambia si
// esta muy mal parado, usa una pocion cuando le queda poco y elige el relevo
// por matchup. Se equivoca a proposito un 15% de las veces, como cualquiera.
//
// La partida sale de los assets del juego (equipos de los rivales, ruletas,
// Pokemon liberados, nivel de los iniciales), asi que mide EXACTAMENTE lo que
// se va a jugar. Contra Nestor juega tambien su Prediccion (PredictionState) y
// la fase de liberacion (LiberationEngine). Lo que no simula: la ruleta y la
// mano de blackjack de Reymar y el guion de Julieta. Esas peleas se juegan en
// Play (PokeRush > Balance > Playtest en Play).
//
//   PokeRush > Balance > Simular la partida
//
// El informe queda en Documentacion/Documentacion del Codigo/Balance_Simulacion.txt.
public static class SimuladorDeBalance
{
    private const string Pokens = "Assets/Resources/Definitions/Pokens/";
    private const string Defs = "Assets/Resources/Definitions/";
    public const string RutaDelInforme = "../../Documentacion/Documentacion del Codigo/Balance_Simulacion.txt";

    // ------------------------------------------------------------ un combate

    public class Opciones
    {
        public bool nestorAliado;
        // Solo contra Nestor: escudo del nucleo y si el rastreo deja cambiar.
        public BossBattleConfig jefe;
        public float torpezaDelJugador = 0.15f;
        // Pociones por combate como mucho (un jugador no se pasa la pelea curando).
        public int pocionesPorCombate = 2;
        // Si no es null, se anota el combate entero (para leer una pelea).
        public StringBuilder registro;
    }

    public class Resultado
    {
        public bool gano;
        public int turnos;
        public int caidosDelJugador;
        public int koAlRival;
        public float vidaRestante;     // 0..1, del equipo que peleo
        public int pocionesUsadas;
        public int cambios;
        public int turnoDelUltimoCambio = -10;
        // Contra Nestor: si llego a la liberacion, cuantos turnos aguanto y si la termino.
        public bool liberacion;
        public int turnosDeLiberacion;
        public bool perdioEnLaLiberacion;
    }

    // Cambios voluntarios como mucho por combate: una persona no cambia en
    // cada turno (y cada cambio le regala un golpe al rival).
    private const int CambiosPorCombate = 2;

    private static AIProfileDefinition _perfilDelJugador;

    private static AIProfileDefinition PerfilDelJugador(bool puedeCambiar, float torpeza)
    {
        if (_perfilDelJugador == null)
            _perfilDelJugador = BattleAIFactory.CreateDefaultProfile(BattleAIType.GymLeader);
        _perfilDelJugador.randomness = torpeza;
        _perfilDelJugador.canSwitch = puedeCambiar;
        _perfilDelJugador.switchGainThreshold = 0.6f;
        _perfilDelJugador.setupWeight = 0.3f;
        _perfilDelJugador.statusWeight = 0.3f;
        return _perfilDelJugador;
    }

    public static Resultado Pelear(PlayerModel jugador, NPCDefinition definicion, Opciones op, IBattleRng rng)
    {
        op ??= new Opciones();
        var rival = new EnemyModel(definicion);
        AIProfileDefinition perfilRival = rival.AiProfile != null
            ? rival.AiProfile
            : BattleAIFactory.CreateDefaultProfile(BattleAIType.Trainer);
        IBattleAI iaRival = BattleAIFactory.Create(perfilRival);

        var r = new Resultado();
        int lider = jugador.Team.FindIndex(p => !p.IsFainted);
        if (lider < 0 || rival.Team.Count == 0) return r;

        var modelo = new BattleModel(jugador, rival);
        modelo.SwitchPlayerPoken(lider);
        modelo.SwitchEnemyPoken(0);

        var motor = new TurnEngine(modelo, rng)
        {
            EnemyReplacementSelector = m => iaRival.ChooseReplacement(AiContext.ForEnemy(m, perfilRival)),
            OfrecerCambioAntesDeQueEntre = op.jefe == null
        };
        bool puedeCambiar = op.jefe == null || op.jefe.PermiteCambio;
        bool salvado = false;
        if (op.jefe != null)
        {
            modelo.EnemySide.ShieldHp = op.jefe.EscudoInicial;
            modelo.EnemySide.ShieldMaxHp = op.jefe.EscudoInicial;
            // Como en BattleController: el ultimo del boss queda en 1 PS y
            // arranca la fase de liberacion.
            motor.EnemyCannotFaint = m =>
            {
                if (!BattleTeamRules.EsElUltimo(m.Enemy.Team, m.ActiveEnemyIndex)) return false;
                salvado = true;
                return true;
            };
        }

        NestorPredictionSystem nestor = op.nestorAliado
            ? new NestorPredictionSystem
            {
                Probabilidad = 0.2f, CooldownTurnos = 2, TurnosDeGracia = 1,
                MaximoPorCombate = 2, FraccionDeGolpeFuerte = 0.45f
            }
            : null;

        // La Prediccion del boss: cada tantos turnos le anula al jugador una
        // categoria de accion (atacar, cambiar, usar objetos...).
        PredictionState prediccion = null;
        if (op.jefe != null)
        {
            prediccion = new PredictionState(op.jefe.PrediccionConAviso, op.jefe.TurnosEntrePredicciones)
            {
                PuedeCambiar = () => puedeCambiar && jugador.Team.Where((p, i) => i != modelo.ActivePlayerIndex).Any(p => !p.IsFainted),
                TieneObjetos = () => jugador.Inventory.HealItems.Exists(s => s.Quantity > 0),
                TieneMovimientoDeEstado = () => modelo.ActivePlayerPoken.Moves.Exists(mv => mv.HasPP && PredictionState.EsDeEstado(mv.Definition)),
                TieneMovimientoDeDano = () => modelo.ActivePlayerPoken.Moves.Exists(mv => mv.HasPP && PredictionState.EsDeDano(mv.Definition))
            };
        }

        AIProfileDefinition perfil = PerfilDelJugador(puedeCambiar, op.torpezaDelJugador);
        IBattleAI jugadorIA = BattleAIFactory.Create(perfil);
        var peleo = new HashSet<PokenModel>();
        int vivosAntes = jugador.Team.Count(p => !p.IsFainted);

        Anotar(op, motor.BeginBattle());
        for (int vuelta = 0; vuelta < 600 && !modelo.IsBattleOver; vuelta++)
        {
            if (modelo.AwaitingPlayerReplacement)
            {
                Anotar(op, motor.CompletePlayerReplacement(MejorRelevo(modelo)));
                continue;
            }
            if (modelo.AwaitingEnemyReplacement)
            {
                Anotar(op, motor.CompleteEnemyReplacement(-1));
                continue;
            }

            peleo.Add(modelo.ActivePlayerPoken);
            prediccion?.Avanzar(modelo, rng);
            BattleAction accion = AccionDelJugador(modelo, jugadorIA, perfil, op, r, rng);
            if (prediccion != null && Anulada(prediccion, accion, modelo))
                accion = OtraSalida(prediccion, modelo, op, r);

            AiContext ctxRival = AiContext.ForEnemy(modelo, perfilRival);
            BattleAction delRival = TurnEngine.AccionForzada(modelo.EnemyState) ?? iaRival.ChooseAction(ctxRival, rng);
            if (nestor != null)
            {
                NestorIntervention lectura = nestor.Evaluar(ctxRival, delRival, rng);
                if (lectura.Intervino) delRival = lectura.AccionFinal;
            }

            op.registro?.AppendLine($"-- turno {r.turnos + 1}: {modelo.ActivePlayerPoken.Definition.pokenName} " +
                $"{modelo.ActivePlayerPoken.CurrentHP}/{modelo.ActivePlayerPoken.MaxHP} vs " +
                $"{modelo.ActiveEnemyPoken.Definition.pokenName} {modelo.ActiveEnemyPoken.CurrentHP}/{modelo.ActiveEnemyPoken.MaxHP}");
            Anotar(op, motor.ResolveTurn(accion, delRival));
            r.turnos++;

            if (salvado && !modelo.IsBattleOver)
            {
                Liberar(modelo, op, r, rng);
                break;
            }
        }

        r.gano = modelo.IsBattleOver && modelo.IsPlayerVictory;
        r.koAlRival = rival.Team.Count(p => p.IsFainted);
        r.caidosDelJugador = vivosAntes - jugador.Team.Count(p => !p.IsFainted);
        int max = peleo.Sum(p => p.MaxHP);
        r.vidaRestante = max > 0 ? (float)peleo.Sum(p => p.CurrentHP) / max : 0f;
        return r;
    }

    // La fase de liberacion (LiberationEngine, lo mismo que hace el
    // BattleController): no atacar. El piloto se protege del golpe fuerte (cada
    // tres turnos, el primero incluido), se cura si al activo le queda poco y
    // tiene con que, y si no cambia al que este mas entero.
    private static void Liberar(BattleModel modelo, Opciones op, Resultado r, IBattleRng rng)
    {
        r.liberacion = true;
        var fase = new LiberationEngine(modelo, rng, op.jefe.TurnosLiberacion);
        PlayerModel jugador = modelo.Player;
        for (int turno = 0; turno < 60 && !fase.Terminada; turno++)
        {
            if (!jugador.HasAlivePoken) break;
            if (modelo.ActivePlayerPoken.IsFainted) modelo.SwitchPlayerPoken(MasEntero(modelo, -1f));

            PokenModel activo = modelo.ActivePlayerPoken;
            float vida = (float)activo.CurrentHP / activo.MaxHP;
            LiberationEngine.Accion accion = LiberationEngine.Accion.Proteger;
            if (turno % 3 != 0)
            {
                HealItemDefinition pocion = vida < 0.4f ? MejorPocion(jugador.Inventory, activo) : null;
                int otro = MasEntero(modelo, vida + 0.2f);
                if (pocion != null && jugador.Inventory.ConsumeHealItem(pocion, 1))
                {
                    activo.Heal(pocion.fullHeal ? activo.MaxHP : pocion.healAmount);
                    activo.CureStatus();
                    r.pocionesUsadas++;
                    accion = LiberationEngine.Accion.Curar;
                }
                else if (otro >= 0)
                {
                    modelo.SwitchPlayerPoken(otro);
                    accion = LiberationEngine.Accion.Cambiar;
                }
            }
            Anotar(op, fase.ResolveTurn(accion));
            r.turnosDeLiberacion++;
        }

        r.perdioEnLaLiberacion = !fase.Terminada;
        if (fase.Terminada) modelo.Finish(playerVictory: true);
        else modelo.Finish(playerVictory: false);
    }

    // El del banco con mas vida (en proporcion) por encima de 'minimo'; -1 si no hay.
    private static int MasEntero(BattleModel m, float minimo)
    {
        int mejor = -1;
        for (int i = 0; i < m.Player.Team.Count; i++)
        {
            PokenModel p = m.Player.Team[i];
            if (p.IsFainted || i == m.ActivePlayerIndex) continue;
            float v = (float)p.CurrentHP / p.MaxHP;
            if (v > minimo) { minimo = v; mejor = i; }
        }
        return mejor;
    }

    private static void Anotar(Opciones op, List<BattleEvent> eventos)
    {
        if (op.registro == null || eventos == null) return;
        foreach (BattleEvent e in eventos)
            if (!string.IsNullOrEmpty(e.Message)) op.registro.AppendLine("   " + e.Message);
    }

    private static BattleAction AccionDelJugador(BattleModel m, IBattleAI ia, AIProfileDefinition perfil,
                                                 Opciones op, Resultado r, IBattleRng rng)
    {
        PokenModel activo = m.ActivePlayerPoken;
        float vida = (float)activo.CurrentHP / activo.MaxHP;
        float vidaRival = (float)m.ActiveEnemyPoken.CurrentHP / m.ActiveEnemyPoken.MaxHP;

        // Curarse cuando queda poco y el rival todavia aguanta.
        if (vida < 0.25f && vidaRival > 0.3f && r.pocionesUsadas < op.pocionesPorCombate)
        {
            HealItemDefinition pocion = MejorPocion(m.Player.Inventory, activo);
            if (pocion != null)
            {
                r.pocionesUsadas++;
                return BattleAction.UseItem(pocion);
            }
        }

        var ctx = new AiContext
        {
            Self = m.PlayerState, Foe = m.EnemyState, OwnTeam = m.Player.Team,
            OwnActiveIndex = m.ActivePlayerIndex, Profile = perfil, Model = m
        };
        BattleAction accion = ia.ChooseAction(ctx, rng);
        if (accion.Kind == BattleActionKind.SwitchPoken)
        {
            bool puede = perfil.canSwitch && r.cambios < CambiosPorCombate && r.turnos - r.turnoDelUltimoCambio > 2;
            if (!puede) return BattleAction.Move(MejorMovimiento(ctx));
            r.cambios++;
            r.turnoDelUltimoCambio = r.turnos;
        }
        return accion;
    }

    private static bool Anulada(PredictionState p, BattleAction a, BattleModel m)
    {
        MoveDefinition mov = a.Kind == BattleActionKind.UseMove && a.MoveIndex >= 0 && a.MoveIndex < m.ActivePlayerPoken.Moves.Count
            ? m.ActivePlayerPoken.Moves[a.MoveIndex].Definition : null;
        return p.Bloquea(a.Kind, mov);
    }

    // Lo que hace una persona cuando le anulan lo que queria: el mejor
    // movimiento que le queda, si no cambiar, si no curarse, si no Forcejeo.
    private static BattleAction OtraSalida(PredictionState p, BattleModel m, Opciones op, Resultado r)
    {
        var ctx = new AiContext
        {
            Self = m.PlayerState, Foe = m.EnemyState, OwnTeam = m.Player.Team,
            OwnActiveIndex = m.ActivePlayerIndex, Profile = _perfilDelJugador, Model = m
        };
        int mejor = -1;
        float puntaje = float.MinValue;
        for (int i = 0; i < m.ActivePlayerPoken.Moves.Count; i++)
        {
            MoveModel mv = m.ActivePlayerPoken.Moves[i];
            if (!mv.HasPP || p.Bloquea(BattleActionKind.UseMove, mv.Definition)) continue;
            float s = MoveScorer.ScoreMove(ctx, mv.Definition);
            if (s > puntaje) { puntaje = s; mejor = i; }
        }
        if (mejor >= 0 && puntaje > 0f) return BattleAction.Move(mejor);

        int relevo = MejorRelevo(m);
        bool puedeCambiar = op.jefe == null || op.jefe.PermiteCambio;
        if (relevo >= 0 && puedeCambiar && !p.Bloquea(BattleActionKind.SwitchPoken, null)) return BattleAction.Switch(relevo);

        HealItemDefinition pocion = MejorPocion(m.Player.Inventory, m.ActivePlayerPoken);
        if (pocion != null && m.ActivePlayerPoken.CurrentHP < m.ActivePlayerPoken.MaxHP && !p.Bloquea(BattleActionKind.UseItem, null))
        {
            r.pocionesUsadas++;
            return BattleAction.UseItem(pocion);
        }
        return mejor >= 0 ? BattleAction.Move(mejor) : BattleAction.Struggle();
    }

    private static int MejorMovimiento(AiContext ctx)
    {
        float mejor = float.MinValue;
        int indice = 0;
        for (int i = 0; i < ctx.Self.Poken.Moves.Count; i++)
        {
            MoveModel mv = ctx.Self.Poken.Moves[i];
            if (!mv.HasPP) continue;
            float s = MoveScorer.ScoreMove(ctx, mv.Definition);
            if (s > mejor) { mejor = s; indice = i; }
        }
        return indice;
    }

    private static HealItemDefinition MejorPocion(InventoryModel inv, PokenModel p)
    {
        int falta = p.MaxHP - p.CurrentHP;
        HealItemDefinition mejor = null;
        foreach (string nombre in new[] { "HiperPocion", "SuperPocion", "Pocion" })
        {
            var item = AssetDatabase.LoadAssetAtPath<HealItemDefinition>($"{Defs}Items/{nombre}.asset");
            if (item == null || inv.CountHealItem(item) <= 0) continue;
            mejor = item;
            if (item.healAmount <= falta) break;   // la mas chica que no desperdicia
        }
        return mejor;
    }

    private static int MejorRelevo(BattleModel m)
    {
        int mejor = -1;
        float puntaje = float.MinValue;
        for (int i = 0; i < m.Player.Team.Count; i++)
        {
            PokenModel p = m.Player.Team[i];
            if (p.IsFainted || i == m.ActivePlayerIndex) continue;
            float s = MoveScorer.MatchupScore(p, m.EnemyState) + 0.3f * p.CurrentHP / p.MaxHP;
            if (s > puntaje) { puntaje = s; mejor = i; }
        }
        return mejor;
    }

    // ------------------------------------------------------------ la partida

    public enum TipoDePaso { Combate, Regalo, Ruleta, Objetos, Etapa }

    public class Paso
    {
        public TipoDePaso tipo;
        public string nombre;
        public NPCDefinition rival;
        public Opciones opciones;
        public PokenDefinition especie;
        public int nivel;
        public CasinoPrizeTable tabla;
        public int tiros = 1;
        public float chance = 1f;          // para regalos que no todos consiguen (Mew)
        // Variante que solo se mide: pelea una copia del equipo, asi la XP no
        // se cuenta dos veces (Nestor con distintos subsistemas cortados).
        public bool soloMedir;
        public string objeto;              // Objetos: nombre del asset en Items
        public int cantidad;
    }

    public class Plan
    {
        public string inicial;
        public List<Paso> pasos = new List<Paso>();

        public Plan Combate(string nombre, NPCDefinition rival, Opciones op = null, bool soloMedir = false)
        {
            pasos.Add(new Paso { tipo = TipoDePaso.Combate, nombre = nombre, rival = rival, opciones = op ?? new Opciones(),
                                 soloMedir = soloMedir });
            return this;
        }
        public Plan Regalo(string nombre, PokenDefinition especie, int nivel, float chance = 1f)
        {
            pasos.Add(new Paso { tipo = TipoDePaso.Regalo, nombre = nombre, especie = especie, nivel = nivel, chance = chance });
            return this;
        }
        public Plan Ruleta(string nombre, CasinoPrizeTable tabla, int tiros = 1)
        {
            pasos.Add(new Paso { tipo = TipoDePaso.Ruleta, nombre = nombre, tabla = tabla, tiros = tiros });
            return this;
        }
        public Plan Objetos(string objeto, int cantidad)
        {
            pasos.Add(new Paso { tipo = TipoDePaso.Objetos, objeto = objeto, cantidad = cantidad });
            return this;
        }
        public Plan Etapa(string nombre)
        {
            pasos.Add(new Paso { tipo = TipoDePaso.Etapa, nombre = nombre });
            return this;
        }
    }

    private static T Cargar<T>(string ruta) where T : Object
    {
        var a = AssetDatabase.LoadAssetAtPath<T>(ruta);
        if (a == null) Debug.LogError($"[Balance] No existe {ruta}");
        return a;
    }

    private static NPCDefinition Rival(string ruta) => Cargar<NPCDefinition>($"{Defs}{ruta}.asset");
    private static CasinoPrizeTable Tabla(string ruta) => Cargar<CasinoPrizeTable>($"{Defs}{ruta}.asset");

    // El recorrido de la historia, con lo que hace un jugador comun: pelea con
    // todos los NPC de cada piso, gira las ruletas que le tocan (una cada dos
    // victorias, hasta cinco), gana alguna en la mesa, le compra dos tiros al
    // empresario, uno a Martin y le gana el Mew a Tacconi la mitad de las veces.
    public static Plan PlanDelJuego(string inicial)
    {
        var plan = new Plan { inicial = inicial };
        var aliado = new Opciones { nestorAliado = true };

        plan.Etapa("Habitacion")
            .Combate("Tutorial: " + Rival("Tutorial/RivalDelTutorial").characterName, Rival("Tutorial/RivalDelTutorial"))
            .Objetos("Pocion", 2);

        plan.Etapa("Data Center (Torre 1)");
        foreach (var (prefab, nombre) in new[] { ("NPC_Basculin", "Liberado"), ("NPC_Combusken", "Liberado") })
        {
            var go = Cargar<GameObject>($"Assets/Prefabs/NPCs/{prefab}.prefab");
            var lib = go != null ? go.GetComponentInChildren<LiberarPokenTrigger>(true) : null;
            if (lib == null) continue;
            var so = new SerializedObject(lib);
            plan.Regalo(nombre, so.FindProperty("poken").objectReferenceValue as PokenDefinition,
                        so.FindProperty("nivel").intValue);
        }
        plan.Objetos("Pocion", 1).Objetos("SuperPocion", 2);
        var nadaCortado = BossBattleConfig.PorDefecto();
        var todoCortado = BossBattleConfig.PorDefecto();
        todoCortado.EscudoInicial = 0; todoCortado.PermiteCambio = true;
        // Lo comun: el rastreo (camino obligatorio) y un nodo del firewall.
        var parcial = BossBattleConfig.PorDefecto();
        parcial.EscudoInicial = BossBattleConfig.EscudoPorNodoActivo * 2; parcial.PermiteCambio = true;
        plan.Combate("Nestor (nada cortado)", Rival("Torre1/Nestor"), new Opciones { jefe = nadaCortado }, soloMedir: true);
        plan.Combate("Nestor (todo cortado)", Rival("Torre1/Nestor_SinMelmetal"), new Opciones { jefe = todoCortado }, soloMedir: true);
        plan.Combate("Nestor (rastreo y 1 nodo)", Rival("Torre1/Nestor"), new Opciones { jefe = parcial });

        plan.Etapa("Casino piso 1").Objetos("SuperPocion", 3);
        string[] piso1 = { "ApostadorCasino", "MalabaristaCasino", "MotoristaCasino", "PokemaniacoCasino" };
        AgregarPiso(plan, piso1, "Casino/RuletaDeLosNpcs", aliado);
        plan.Combate("Guardia del ascensor", Rival("Casino/GuardiaAscensor"), aliado)
            .Ruleta("Ruleta del guardia", Tabla("Casino/RuletaDelGuardia"));

        plan.Etapa("Casino piso 2");
        string[] piso2 = { "BailarinaCasino", "JovenRicaCasino", "LadronCasino", "MagnateCasino",
                           "MediumCasino", "ModeloCasino", "TahurCasino" };
        AgregarPiso(plan, piso2, "Casino/RuletaDeLosNpcsPiso2", aliado);
        plan.Ruleta("Ruleta de la mesa", Tabla("Casino/RuletaDeLaMesa"));
        plan.Combate("Guardia VIP", Rival("Casino/GuardiaVipCasino"), aliado)
            .Ruleta("Ruleta del guardia VIP", Tabla("Casino/RuletaDelGuardiaVip"));

        plan.Etapa("Casino piso 3");
        string[] piso3 = { "CrupieraCasino", "GangsterCasino", "GuayCasino", "KaratekaCasino",
                           "MacarraCasino", "VeteranoCasino" };
        AgregarPiso(plan, piso3, "Casino/RuletaDeLosNpcs", aliado);
        plan.Ruleta("Empresario", Tabla("Casino/RuletaDelEmpresario"), 2).Objetos("HiperPocion", 2);

        plan.Etapa("Casino piso FINAL");
        plan.Combate("Reymar", Rival("Casino/JefeFinalCasino"), aliado);

        plan.Etapa("Quarcho Cueva");
        plan.Ruleta("Ruleta de Martin", Tabla("Lobby/RuletaDeMartin_Pokemon"));
        plan.Regalo("Mew de Tacconi", Cargar<PokenDefinition>($"{Pokens}0151_Mew.asset"), NivelDelMewDeTacconi(), 0.5f);

        plan.Etapa("BossFinal");
        plan.Combate("Julieta", Rival("Julieta/Julieta"));
        return plan;
    }

    private static int _ruletasDeNpcs;

    // Una ruleta cada dos victorias, hasta cinco en toda la partida (CasinoRuletaDeNpcs).
    private static void AgregarPiso(Plan plan, string[] npcs, string ruleta, Opciones op)
    {
        for (int i = 0; i < npcs.Length; i++)
        {
            NPCDefinition def = Rival("Casino/" + npcs[i]);
            plan.Combate(def != null ? def.characterName : npcs[i], def, op);
            if (i % 2 == 1) plan.Ruleta("Ruleta de los NPC", Tabla(ruleta));
        }
    }

    public static int NivelDelMewDeTacconi()
    {
        string escena = File.ReadAllText("Assets/Scenes/Quarcho-Cueva.unity");
        int i = escena.IndexOf("nivelPremio:", System.StringComparison.Ordinal);
        if (i < 0) return 30;
        string linea = escena.Substring(i + 12, 6).Trim().Split('\n')[0].Trim();
        return int.TryParse(linea, out int n) ? n : 30;
    }

    public static int NivelDeLosIniciales()
    {
        string escena = File.ReadAllText("Assets/Scenes/CreacionPersonaje.unity");
        int i = escena.IndexOf("nivelInicial:", System.StringComparison.Ordinal);
        if (i < 0) return 20;
        string linea = escena.Substring(i + 13, 6).Trim().Split('\n')[0].Trim();
        return int.TryParse(linea, out int n) ? n : 20;
    }

    // ------------------------------------------------------------ el informe

    public class Estadistica
    {
        public string nombre, etapa;
        public int jugadas, ganadasDeUna, intentos, turnos, caidos, abandonos;
        public float vida, nivelJugador, nivelRival, nivelMaxJugador;
        public int pocion;
        public int perdidasEnLaLiberacion;
        // Ganadas al primer intento y jugadas, por inicial.
        public Dictionary<string, int[]> porInicial = new Dictionary<string, int[]>();
    }

    // Los IVs salen de la semilla (si no, la misma semilla daba otro informe,
    // +-4 puntos por pelea). Al terminar deja los estaticos de PokenModel como
    // estaban: el proyecto no recarga el dominio y los smoke tests cuentan con
    // que nadie los dejo puestos.
    public static string Simular(System.Func<string, Plan> armarPlan, string[] iniciales, int partidasPorInicial, int semilla)
    {
        var tablaAntes = PokenModel.DefaultXpTable;
        var movimientoAntes = PokenModel.DefaultMove;
        var especialAntes = PokenModel.EspecialDeRespaldo;
        var ivsAntes = PokenModel.IvRandom;
        PrepararElMotor();
        PokenModel.IvRandom = new System.Random(semilla);
        try
        {
            return SimularConElMotor(armarPlan, iniciales, partidasPorInicial, semilla);
        }
        finally
        {
            PokenModel.DefaultXpTable = tablaAntes;
            PokenModel.DefaultMove = movimientoAntes;
            PokenModel.EspecialDeRespaldo = especialAntes;
            PokenModel.IvRandom = ivsAntes;
        }
    }

    private static string SimularConElMotor(System.Func<string, Plan> armarPlan, string[] iniciales, int partidasPorInicial, int semilla)
    {
        var stats = new Dictionary<string, Estadistica>();
        var orden = new List<string>();
        var equiposFinales = new Dictionary<string, int>();
        var rng = new SeededRng(semilla);
        var azar = new System.Random(semilla);

        foreach (string inicial in iniciales)
        {
            for (int n = 0; n < partidasPorInicial; n++)
            {
                Plan plan = armarPlan(inicial);
                var jugador = new PlayerModel("BANCO");
                var coleccion = new List<PokenModel>
                {
                    new PokenModel(Cargar<PokenDefinition>($"{Pokens}{inicial}.asset"), NivelDeLosIniciales())
                };
                string etapa = "";
                _ruletasDeNpcs = 0;

                foreach (Paso paso in plan.pasos)
                {
                    switch (paso.tipo)
                    {
                        case TipoDePaso.Etapa: etapa = paso.nombre; break;
                        case TipoDePaso.Objetos:
                            var item = Cargar<HealItemDefinition>($"{Defs}Items/{paso.objeto}.asset");
                            if (item != null) jugador.Inventory.AddHealItem(item, paso.cantidad);
                            break;
                        case TipoDePaso.Regalo:
                            if (paso.especie != null && azar.NextDouble() < paso.chance)
                                coleccion.Add(new PokenModel(paso.especie, paso.nivel));
                            break;
                        case TipoDePaso.Ruleta:
                            if (paso.nombre == "Ruleta de los NPC")
                            {
                                if (_ruletasDeNpcs >= 5) break;
                                _ruletasDeNpcs++;
                            }
                            for (int t = 0; t < paso.tiros; t++) Girar(paso.tabla, jugador, coleccion, azar);
                            break;
                        case TipoDePaso.Combate:
                            if (paso.rival == null) break;
                            if (!stats.TryGetValue(paso.nombre, out Estadistica e))
                            {
                                e = stats[paso.nombre] = new Estadistica { nombre = paso.nombre, etapa = etapa };
                                orden.Add(paso.nombre);
                            }
                            ArmarEquipo(jugador, coleccion);
                            e.jugadas++;
                            e.nivelJugador += (float)jugador.Team.Average(p => p.Level);
                            e.nivelMaxJugador += jugador.Team.Max(p => p.Level);
                            var defEquipo = EquipoDe(paso.rival);
                            e.nivelRival += defEquipo.Length > 0 ? (float)defEquipo.Average(t => t.level) : 0f;

                            PlayerModel quienPelea = jugador;
                            if (paso.soloMedir)
                            {
                                quienPelea = new PlayerModel("COPIA");
                                foreach (PokenModel p in jugador.Team) quienPelea.Team.Add(new PokenModel(p.Definition, p.Level));
                                foreach (HealItemStack st in jugador.Inventory.HealItems)
                                    quienPelea.Inventory.AddHealItem(st.Definition, st.Quantity);
                            }
                            bool gano = false;
                            for (int intento = 1; intento <= 5 && !gano; intento++)
                            {
                                quienPelea.CurarEquipo();
                                // Despues de perder, una persona abre con el que mejor le va
                                // contra el primero del rival (ya lo vio).
                                if (intento > 1) AbrirConElMejor(quienPelea, paso.rival);
                                Resultado r = Pelear(quienPelea, paso.rival, paso.opciones, rng);
                                e.intentos++;
                                e.pocion += r.pocionesUsadas;
                                if (intento == 1)
                                {
                                    e.turnos += r.turnos;
                                    e.caidos += r.caidosDelJugador;
                                    e.vida += r.vidaRestante;
                                    if (r.gano) e.ganadasDeUna++;
                                    if (r.perdioEnLaLiberacion) e.perdidasEnLaLiberacion++;
                                    if (!e.porInicial.TryGetValue(inicial, out int[] cuenta))
                                        cuenta = e.porInicial[inicial] = new int[2];
                                    cuenta[1]++;
                                    if (r.gano) cuenta[0]++;
                                }
                                gano = r.gano;
                            }
                            if (!gano) e.abandonos++;
                            jugador.CurarEquipo();
                            break;
                    }
                }

                foreach (PokenModel p in coleccion)
                {
                    string k = $"{p.Definition.pokenName}";
                    equiposFinales[k] = equiposFinales.TryGetValue(k, out int c) ? c + 1 : 1;
                }
            }
        }

        var sb = new StringBuilder();
        int total = iniciales.Length * partidasPorInicial;
        sb.AppendLine($"Simulacion de balance: {total} partidas ({string.Join(", ", iniciales)}), semilla {semilla}.");
        sb.AppendLine("Gana1 = gana al primer intento con el equipo curado. Intentos = promedio hasta ganar (tope 5).");
        sb.AppendLine("Vida = PS que le quedan al equipo que peleo, al ganar o perder el primer intento.");
        sb.AppendLine();
        sb.AppendLine($"{"Combate",-28} {"Etapa",-22} {"NvJug",5} {"NvMax",5} {"NvRiv",5} {"Gana1",6} {"Intent",6} {"Turnos",6} {"Caidos",6} {"Vida",5} {"Pocion",6}  Gana1 por inicial");
        foreach (string k in orden)
        {
            Estadistica e = stats[k];
            float j = Mathf.Max(1, e.jugadas);
            sb.AppendLine($"{Corto(e.nombre, 28),-28} {Corto(e.etapa, 22),-22} {e.nivelJugador / j,5:0.0} {e.nivelMaxJugador / j,5:0.0} " +
                          $"{e.nivelRival / j,5:0.0} {100f * e.ganadasDeUna / j,5:0}% {e.intentos / j,6:0.00} " +
                          $"{e.turnos / j,6:0.0} {e.caidos / j,6:0.0} {100f * e.vida / j,4:0}% {e.pocion / j,6:0.0}  " +
                          string.Join(" ", e.porInicial.Select(x => $"{Corto(x.Key.Substring(5), 4)} {100 * x.Value[0] / Mathf.Max(1, x.Value[1])}%")) +
                          (e.abandonos > 0 ? $"  ({e.abandonos} no la ganaron en 5)" : "") +
                          (e.perdidasEnLaLiberacion > 0 ? $"  [{e.perdidasEnLaLiberacion} perdidas en la liberacion]" : ""));
        }
        sb.AppendLine();
        sb.AppendLine("Pokemon conseguidos en las partidas (veces): " +
                      string.Join(", ", equiposFinales.OrderByDescending(x => x.Value).Select(x => $"{x.Key} {x.Value}")));
        return sb.ToString();
    }

    private static string Corto(string s, int n) => s == null ? "" : (s.Length <= n ? s : s.Substring(0, n));

    private static EnemyTeamEntry[] EquipoDe(NPCDefinition def)
    {
        if (def is EnemyDefinition e) return e.pokenTeam ?? new EnemyTeamEntry[0];
        if (def is BossDefinition b) return b.pokenTeam ?? new EnemyTeamEntry[0];
        return new EnemyTeamEntry[0];
    }

    private static void AbrirConElMejor(PlayerModel jugador, NPCDefinition rival)
    {
        EnemyTeamEntry[] equipo = EquipoDe(rival);
        if (equipo.Length == 0 || equipo[0].species == null || jugador.Team.Count < 2) return;
        var primero = new BattlePokenState(new PokenModel(equipo[0].species, equipo[0].level), isPlayerSide: false);
        int mejor = 0;
        float puntaje = float.MinValue;
        for (int i = 0; i < jugador.Team.Count; i++)
        {
            float s = MoveScorer.MatchupScore(jugador.Team[i], primero);
            if (s > puntaje) { puntaje = s; mejor = i; }
        }
        if (mejor > 0) jugador.SwapTeamSlots(0, mejor);
    }

    // Lo que hace un jugador con el PC: pelea con los seis mejores que tiene.
    private static void ArmarEquipo(PlayerModel jugador, List<PokenModel> coleccion)
    {
        PokenModel inicial = coleccion[0];
        var mejores = coleccion.Skip(1)
            .OrderByDescending(p => p.Level * (p.Definition.baseHP + p.Definition.baseAttack + p.Definition.baseDefense
                                              + p.Definition.baseSpecialAttack + p.Definition.baseSpecialDefense
                                              + p.Definition.baseSpeed))
            .Take(PlayerModel.EquipoMaximo - 1).ToList();
        jugador.Team.Clear();
        jugador.Team.Add(inicial);
        jugador.Team.AddRange(mejores);
    }

    private static void Girar(CasinoPrizeTable tabla, PlayerModel jugador, List<PokenModel> coleccion, System.Random azar)
    {
        if (tabla == null || tabla.premios == null || tabla.premios.Length == 0) return;
        // Lo mismo que hace la rueda al abrirse: las especies salen de los pozos
        // sin repetir lo que el jugador ya tiene.
        jugador.Team.Clear();
        jugador.Team.AddRange(coleccion.Take(PlayerModel.EquipoMaximo));
        foreach (PokenModel p in coleccion.Skip(PlayerModel.EquipoMaximo)) jugador.Storage.TryAgregar(p, out _, out _);
        Prize[] premios = VariedadDeLasRuletas.Preparar(tabla.premios, jugador,
            nombre => DefinitionRepository.Instance.GetPoken(nombre), azar);
        LimpiarPc(jugador);

        float suma = premios.Sum(p => p.probability);
        double tiro = azar.NextDouble() * suma;
        Prize premio = premios[premios.Length - 1];
        foreach (Prize p in premios)
        {
            tiro -= p.probability;
            if (tiro <= 0) { premio = p; break; }
        }
        if (premio.poken != null) coleccion.Add(new PokenModel(premio.poken, Mathf.Max(1, premio.pokenLevel)));
        if (premio.item != null && premio.amount > 0) jugador.Inventory.AddHealItem(premio.item, premio.amount);
    }

    private static void LimpiarPc(PlayerModel jugador)
    {
        for (int caja = 0; caja < jugador.Storage.Cajas; caja++)
            for (int lugar = 0; lugar < PokenStorageModel.LugaresPorCaja; lugar++)
                jugador.Storage.Sacar(caja, lugar);
    }

    // Lo mismo que GameManager.InitializeServices le inyecta al modelo.
    public static void PrepararElMotor()
    {
        DefinitionRepository.Instance.LoadAll();
        PokenModel.DefaultXpTable = DefinitionRepository.Instance.ExperienceTable;
        PokenModel.DefaultMove = DefinitionRepository.Instance.GetMove("Placaje");
        PokenModel.EspecialDeRespaldo = AtaquesEspecialesDeRespaldo.DelTipo;
    }

    // ------------------------------------------------------------ menu

    [MenuItem("PokeRush/Balance/Simular la partida")]
    public static void SimularLaPartida()
    {
        // 120 por inicial: con 40, una pelea de 30% podia salir 24% o 33%.
        string informe = Simular(PlanDelJuego, new[] { "0448_Lucario", "0094_Gengar", "0658_Greninja" }, 120, 2026);
        File.WriteAllText(RutaDelInforme, informe);
        Debug.Log("[Balance]\n" + informe);
    }
}
