using System.Collections.Generic;
using System.Linq;
using UnityEngine;

public class DefinitionRepository
{
    // Singleton: una sola instancia compartida por todo el juego
    private static DefinitionRepository _instance;
    public static DefinitionRepository Instance => _instance ??= new DefinitionRepository();

    // Diccionarios para buscar cada tipo de Definition por su nombre (string)
    private Dictionary<string, PokenDefinition> _pokens;
    private Dictionary<string, MoveDefinition> _moves;
    private Dictionary<string, HealItemDefinition> _healItems;
    private Dictionary<string, BattleItemDefinition> _battleItems;
    private Dictionary<string, SpecialItemDefinition> _specialItems;
    private Dictionary<string, CosmeticDefinition> _cosmetics;
    private Dictionary<string, AIProfileDefinition> _aiProfiles;

    // Tabla de experiencia global (Resources/Definitions/ExperienceTable.asset)
    public ExperienceTableDefinition ExperienceTable { get; private set; }

    // Evita cargar todo de nuevo si LoadAll() se llama mas de una vez
    private bool _loaded = false;

    // Especies y movimientos se cargan RECIEN cuando se piden (ver
    // IndiceDeDefiniciones): cargarlos todos al arrancar arrastraba las tiras
    // animadas de las 445 especies, varios GB de texturas. Null = sin indice,
    // se cargo todo como antes.
    private Dictionary<string, string> _rutaDePoken;
    private Dictionary<string, string> _rutaDeMove;
    private bool _pokensCompletos;
    private bool _movesCompletos;

    // Constructor privado: nadie puede crear esta clase con "new", solo via Instance
    private DefinitionRepository() { }

    // Carga todos los ScriptableObjects desde las carpetas Resources la primera vez que se llama
    public void LoadAll()
    {
        if (_loaded) return;

        // Resources.LoadAll<T>("carpeta") busca todos los assets de tipo T dentro de Assets/Resources/carpeta
        // .ToDictionary(p => p.pokenName) crea un diccionario usando el nombre como clave
        var indice = Resources.Load<IndiceDeDefiniciones>(IndiceDeDefiniciones.RutaEnResources);
        if (indice != null && indice.pokens.Count > 0 && indice.movimientos.Count > 0)
        {
            _pokens = new Dictionary<string, PokenDefinition>();
            _moves = new Dictionary<string, MoveDefinition>();
            _rutaDePoken = Indexar(indice.pokens);
            _rutaDeMove = Indexar(indice.movimientos);
        }
        else
        {
            CargarTodosLosPokens();
            CargarTodosLosMoves();
        }
        _healItems = Resources.LoadAll<HealItemDefinition>("Definitions/Items").ToDictionary(i => i.itemName);
        _battleItems = Resources.LoadAll<BattleItemDefinition>("Definitions/Items").ToDictionary(i => i.itemName);
        _specialItems = Resources.LoadAll<SpecialItemDefinition>("Definitions/Items").ToDictionary(i => i.itemName);
        _cosmetics = Resources.LoadAll<CosmeticDefinition>("Definitions/Cosmetics").ToDictionary(c => c.cosmeticName);
        // Los perfiles de IA se indexan por el nombre del asset (no tienen campo de nombre propio)
        _aiProfiles = Resources.LoadAll<AIProfileDefinition>("Definitions/AIProfiles").ToDictionary(p => p.name);

        // Tabla de experiencia unica compartida por todos los Pokens
        ExperienceTable = Resources.Load<ExperienceTableDefinition>("Definitions/ExperienceTable");

        _loaded = true;
    }

    // Metodos de busqueda: dado un nombre, devuelven el ScriptableObject correspondiente o null si no existe
    // GetValueOrDefault devuelve null automaticamente si la clave no esta en el diccionario, sin tirar error
    public PokenDefinition GetPoken(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        // Las ruletas y el ataque especial de respaldo piden por nombre fuera
        // del GameManager (smoke tests de editor): sin esto, _pokens es null.
        if (!_loaded) LoadAll();
        if (_pokens.TryGetValue(name, out PokenDefinition p)) return p;
        if (_rutaDePoken != null && _rutaDePoken.TryGetValue(name, out string ruta))
        {
            p = Resources.Load<PokenDefinition>(ruta);
            if (p != null) { _pokens[name] = p; return p; }
        }
        // No esta en el indice (una especie nueva sin rearmarlo): la carga de antes.
        if (!_pokensCompletos)
        {
            Debug.LogWarning($"[Definiciones] '{name}' no esta en el indice: se cargan todas las especies. " +
                             "Correr PokeRush > Release > Armar indice de definiciones.");
            CargarTodosLosPokens();
            return _pokens.GetValueOrDefault(name);
        }
        return null;
    }

    public MoveDefinition GetMove(string name)
    {
        if (string.IsNullOrEmpty(name)) return null;
        if (!_loaded) LoadAll();
        if (_moves.TryGetValue(name, out MoveDefinition m)) return m;
        if (_rutaDeMove != null && _rutaDeMove.TryGetValue(name, out string ruta))
        {
            m = Resources.Load<MoveDefinition>(ruta);
            if (m != null) { _moves[name] = m; return m; }
        }
        if (!_movesCompletos)
        {
            Debug.LogWarning($"[Definiciones] '{name}' no esta en el indice: se cargan todos los movimientos. " +
                             "Correr PokeRush > Release > Armar indice de definiciones.");
            CargarTodosLosMoves();
            return _moves.GetValueOrDefault(name);
        }
        return null;
    }

    private void CargarTodosLosPokens()
    {
        _pokens = Resources.LoadAll<PokenDefinition>("Definitions/Pokens").ToDictionary(p => p.pokenName);
        _pokensCompletos = true;
    }

    private void CargarTodosLosMoves()
    {
        _moves = Resources.LoadAll<MoveDefinition>("Definitions/Moves").ToDictionary(m => m.moveName);
        _movesCompletos = true;
    }

    private static Dictionary<string, string> Indexar(List<IndiceDeDefiniciones.Entrada> entradas)
    {
        var d = new Dictionary<string, string>();
        foreach (IndiceDeDefiniciones.Entrada e in entradas)
            if (e != null && !string.IsNullOrEmpty(e.nombre) && !string.IsNullOrEmpty(e.ruta)) d[e.nombre] = e.ruta;
        return d;
    }
    // ---------- listados (consola de administrador) ----------
    //
    // Para buscar y autocompletar por nombre SIN cargar las especies: con el
    // indice, los nombres salen de ahi y no se toca ningun asset.

    public IEnumerable<string> NombresDePokens()
    {
        if (!_loaded) LoadAll();
        return _rutaDePoken != null ? _rutaDePoken.Keys : _pokens.Keys;
    }

    // Numero de Pokedex sin cargar la especie: el asset se llama
    // "0257_Blaziken". 0 si no se sabe.
    public int NumeroDePokedex(string nombre)
    {
        if (!_loaded) LoadAll();
        if (string.IsNullOrEmpty(nombre)) return 0;
        if (_rutaDePoken != null && _rutaDePoken.TryGetValue(nombre, out string ruta))
        {
            string archivo = System.IO.Path.GetFileName(ruta);
            int guion = archivo.IndexOf('_');
            return guion > 0 && int.TryParse(archivo.Substring(0, guion), out int n) ? n : 0;
        }
        return _pokens.TryGetValue(nombre, out PokenDefinition p) && p != null ? p.dexNumber : 0;
    }

    public IEnumerable<string> NombresDeObjetos()
    {
        if (!_loaded) LoadAll();
        return _healItems.Keys.Concat(_battleItems.Keys).Concat(_specialItems.Keys);
    }

    public HealItemDefinition GetHealItem(string name) => _healItems.GetValueOrDefault(name);
    public BattleItemDefinition GetBattleItem(string name) => _battleItems.GetValueOrDefault(name);
    public SpecialItemDefinition GetSpecialItem(string name) => _specialItems.GetValueOrDefault(name);
    public CosmeticDefinition GetCosmetic(string name) => _cosmetics.GetValueOrDefault(name);
    public AIProfileDefinition GetAIProfile(string name) => _aiProfiles.GetValueOrDefault(name);
}
