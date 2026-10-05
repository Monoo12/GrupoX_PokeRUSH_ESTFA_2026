using System.Collections.Generic;

// El ataque especial basico de cada tipo, para el Pokemon que a su nivel no
// aprende ninguno (PokenModel.AsegurarUnAtaqueEspecial, playtest del 5-oct).
//
// Son los primeros especiales de cada tipo en la saga, de poca potencia: no
// tienen que hacer mas fuerte a nadie, solo darle algo con que pegar del lado
// especial. Los busca por nombre en DefinitionRepository, que los carga recien
// cuando se piden.
public static class AtaquesEspecialesDeRespaldo
{
    private static readonly Dictionary<PokenType, string> PorTipo = new Dictionary<PokenType, string>
    {
        { PokenType.Normal,   "Meteoros" },          // 60, nunca falla
        { PokenType.Fire,     "Ascuas" },            // 40
        { PokenType.Water,    "Pistola Agua" },      // 40
        { PokenType.Grass,    "Hoja Mágica" },       // 60, nunca falla
        { PokenType.Electric, "Impactrueno" },       // 40
        { PokenType.Ice,      "Nieve Polvo" },       // 40
        { PokenType.Fighting, "Onda Vacío" },        // 40, prioridad
        { PokenType.Poison,   "Ácido" },             // 40
        { PokenType.Ground,   "Disparo Lodo" },      // 55
        { PokenType.Flying,   "Tornado" },           // 40
        { PokenType.Psychic,  "Confusión" },         // 50
        { PokenType.Bug,      "Estoicismo" },        // 50
        { PokenType.Rock,     "Poder Pasado" },      // 60
        { PokenType.Ghost,    "Infortunio" },        // 65
        { PokenType.Dragon,   "Dragoaliento" },      // 60
        { PokenType.Dark,     "Alarido" },           // 55
        { PokenType.Steel,    "Cañón Resplandor" },  // 80: el unico especial de Acero que hay
        { PokenType.Fairy,    "Viento Feérico" },    // 40
    };

    public static string NombreDelTipo(PokenType tipo)
        => PorTipo.TryGetValue(tipo, out string nombre) ? nombre : null;

    public static MoveDefinition DelTipo(PokenType tipo)
    {
        string nombre = NombreDelTipo(tipo);
        return string.IsNullOrEmpty(nombre) ? null : DefinitionRepository.Instance.GetMove(nombre);
    }
}
