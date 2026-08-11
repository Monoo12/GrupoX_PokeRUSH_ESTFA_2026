using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// Banco de pruebas de las animaciones de ataque: recorre una lista de
// movimientos en loop llamando a BattleFxView.PlayAttack, sin combate ni
// reglas de por medio. Sirve para mirar tiempos y tamaños sin tener que jugar
// hasta que un Poken aprenda el movimiento.
//
// La escena la arma PokeRush > Armar escena de animaciones.
public class AnimationShowcaseView : MonoBehaviour
{
    [Header("Referencias")]
    [SerializeField] private BattleFxView fx;
    [SerializeField] private PokenView attacker;
    [SerializeField] private PokenView defender;
    [SerializeField] private Text label;

    [Header("Movimientos a mostrar")]
    [Tooltip("Se reproducen en orden y vuelve a empezar. Vacio = los busca por " +
             "nombre en Resources segun defaultMoveNames.")]
    [SerializeField] private MoveDefinition[] moves;

    [Header("Ritmo")]
    [Tooltip("Pausa entre un ataque y el siguiente.")]
    [SerializeField] private float pauseBetween = 0.9f;
    [Tooltip("Arrancar solo al entrar en Play.")]
    [SerializeField] private bool playOnStart = true;

    // Fallback por si la lista quedo vacia: cubre los tres tipos de ataque y
    // mezcla animaciones propias con genericas por tipo.
    private static readonly string[] DefaultMoveNames =
    {
        "0033_Placaje",       // Physical, generica de Normal
        "0075_Hoja Afilada",  // Physical, generica de Grass
        "0053_Lanzallamas",   // Special, propia: chorro que viaja
        "0126_Llamarada",     // Special, propia: estallido sobre el objetivo
        "0052_Ascuas",        // Special, generica de Fire
        "0014_Danza Espada"   // Status, sobre el usuario
    };

    private void Start()
    {
        if (moves == null || moves.Length == 0) LoadDefaults();
        if (playOnStart) StartCoroutine(Loop());
    }

    private void LoadDefaults()
    {
        var found = new System.Collections.Generic.List<MoveDefinition>();
        foreach (string name in DefaultMoveNames)
        {
            var move = Resources.Load<MoveDefinition>("Definitions/Moves/" + name);
            if (move != null) found.Add(move);
            else Debug.LogWarning($"[Showcase] No encontre el movimiento {name}");
        }
        moves = found.ToArray();
    }

    private IEnumerator Loop()
    {
        if (fx == null || attacker == null || defender == null)
        {
            Debug.LogError("[Showcase] Faltan referencias en el inspector.");
            yield break;
        }
        if (moves.Length == 0)
        {
            Debug.LogError("[Showcase] No hay movimientos para mostrar.");
            yield break;
        }

        yield return new WaitForSeconds(0.4f);

        int i = 0;
        while (true)
        {
            MoveDefinition move = moves[i % moves.Length];
            if (move != null)
            {
                if (label != null) label.text = Describe(move);
                yield return fx.PlayAttack(attacker, defender, move);
                yield return new WaitForSeconds(pauseBetween);
            }
            i++;
        }
    }

    // Texto de ayuda: que estamos viendo y de donde sale la animacion
    private static string Describe(MoveDefinition move)
    {
        MoveAnimationDefinition anim = move.animationOverride;
        string origen;
        if (anim == null) origen = "generica de " + move.type;
        else if (anim.projectileFrames != null && anim.projectileFrames.Length > 0)
            origen = $"propia · {anim.projectileFrames.Length} frames que viajan";
        else if (anim.impactFrames != null && anim.impactFrames.Length > 0)
            origen = $"propia · {anim.impactFrames.Length} frames de impacto";
        else origen = "sin secuencia";

        return $"{move.moveName}   [{move.attackType} · {move.type}]\n{origen}";
    }
}
