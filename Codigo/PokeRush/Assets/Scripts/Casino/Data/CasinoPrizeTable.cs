using UnityEngine;

// Una tabla de premios para la ruleta, como asset.
//
// Asi los porcentajes y los Pokemon se tocan desde el Inspector sin abrir la
// escena ni recompilar, y la misma tabla la comparten todos los NPCs que dan
// esa ruleta en los tres pisos.
[CreateAssetMenu(fileName = "RuletaDePremios", menuName = "PokeRush/Casino/Tabla de premios de ruleta")]
public class CasinoPrizeTable : ScriptableObject
{
    [Tooltip("Hasta 8 premios, uno por gajo, en el orden en que se dibujan. " +
             "El peso ('probability') es relativo: no hace falta que sumen 100.")]
    public Prize[] premios = new Prize[0];

    [Tooltip("Solo para la ruleta de los NPCs: el flag de 'vencido' de cada NPC " +
             "que la puede dar. La regla de maximo dos por partida se calcula " +
             "sobre esta lista (ver CasinoRuletaDeNpcs).")]
    public StoryFlag[] npcsQueLaPuedenDar = new StoryFlag[0];

    // Porcentaje (0-1) del peso total que se lleva lo que cumpla 'que'.
    public float Porcentaje(System.Predicate<Prize> que)
    {
        if (premios == null || que == null) return 0f;

        float total = 0f, parte = 0f;
        foreach (Prize p in premios)
        {
            if (p == null) continue;
            float w = Mathf.Max(0f, p.probability);
            total += w;
            if (que(p)) parte += w;
        }
        return total > 0f ? parte / total : 0f;
    }
}
