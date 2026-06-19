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

    // Evita cargar todo de nuevo si LoadAll() se llama mas de una vez
    private bool _loaded = false;

    // Constructor privado: nadie puede crear esta clase con "new", solo via Instance
    private DefinitionRepository() { }

    // Carga todos los ScriptableObjects desde las carpetas Resources la primera vez que se llama
    public void LoadAll()
    {
        if (_loaded) return;

        // Resources.LoadAll<T>("carpeta") busca todos los assets de tipo T dentro de Assets/Resources/carpeta
        // .ToDictionary(p => p.pokenName) crea un diccionario usando el nombre como clave
        _pokens = Resources.LoadAll<PokenDefinition>("Definitions/Pokens").ToDictionary(p => p.pokenName);
        _moves = Resources.LoadAll<MoveDefinition>("Definitions/Moves").ToDictionary(m => m.moveName);
        _healItems = Resources.LoadAll<HealItemDefinition>("Definitions/Items").ToDictionary(i => i.itemName);
        _battleItems = Resources.LoadAll<BattleItemDefinition>("Definitions/Items").ToDictionary(i => i.itemName);
        _specialItems = Resources.LoadAll<SpecialItemDefinition>("Definitions/Items").ToDictionary(i => i.itemName);
        _cosmetics = Resources.LoadAll<CosmeticDefinition>("Definitions/Cosmetics").ToDictionary(c => c.cosmeticName);

        _loaded = true;
    }

    // Metodos de busqueda: dado un nombre, devuelven el ScriptableObject correspondiente o null si no existe
    // GetValueOrDefault devuelve null automaticamente si la clave no esta en el diccionario, sin tirar error
    public PokenDefinition GetPoken(string name) => _pokens.GetValueOrDefault(name);
    public MoveDefinition GetMove(string name) => _moves.GetValueOrDefault(name);
    public HealItemDefinition GetHealItem(string name) => _healItems.GetValueOrDefault(name);
    public BattleItemDefinition GetBattleItem(string name) => _battleItems.GetValueOrDefault(name);
    public SpecialItemDefinition GetSpecialItem(string name) => _specialItems.GetValueOrDefault(name);
    public CosmeticDefinition GetCosmetic(string name) => _cosmetics.GetValueOrDefault(name);
}
