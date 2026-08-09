using System.Collections.Generic;
using UnityEngine;

// Que efectos le tocan a cada tipo de Pokemon, y cuanto mide cada uno.
//
// La idea es que el golpe se lea sin mirar el HUD: si es fuego se ve fuego, si
// es electrico se ven chispas. Antes eso se intentaba TIÑENDO un efecto gris,
// porque los FX de UNITE vienen en escala de grises y su motor les pone el
// color. Tiñendo se pierde todo lo que hace que un efecto se vea bien y queda un
// manchon de un solo tono. Con los packs de Hovl y de Cartoon FX ya no hace
// falta: cada tipo apunta a un efecto autorado en su color.
//
// Un golpe se cuenta con DOS efectos, y hacen falta los dos:
//
//   EL ARCO es la estela del movimiento. Sale una sola vez, del propio Poken,
//   cuando abre la ventana de daño. Es lo que convierte una patada en un ataque.
//   Ademas es lo unico que se ve cuando el golpe sale al aire, y ver que fallaste
//   es informacion, no un premio que haya que esconder. Tiene que ser un TAJO,
//   no un area: un anillo horizontal alrededor del cuerpo no se lee como un
//   golpe, se lee como un adorno.
//
//   EL IMPACTO sale una vez POR RIVAL alcanzado, sobre su cuerpo. Pegarle a tres
//   tiene que verse como tres golpes.
[CreateAssetMenu(fileName = "CatalogoFx", menuName = "PokeRush/Catalogo de FX")]
public class FxCatalog : ScriptableObject
{
    // Un efecto y CUANTO MIDE de verdad.
    //
    // La medida no se puede adivinar de los parametros del sistema de
    // particulas. Se intento -radio de la forma + velocidad x vida + tamaño de
    // particula- y da cualquier cosa: para "Star hit" predecia 15 unidades
    // cuando en pantalla ocupa 9, y el efecto terminaba encogido a 0,6 unidades,
    // una bolita. La unica medida confiable sale de SIMULAR el efecto y mirar el
    // bounds de sus renderers, y eso solo se puede hacer en el editor. Por eso
    // viaja horneada en el asset: ver FxCatalogBuilder.
    [System.Serializable]
    public struct Efecto
    {
        public GameObject prefab;

        [Tooltip("Cuanto mide de punta a punta en unidades de mundo, con el " +
                 "prefab a escala 1. Lo mide el cableador simulando el efecto. " +
                 "0 = que FxPlayer lo estime (peor).")]
        public float medida;

        public bool Existe => prefab != null;
    }

    [System.Serializable]
    public class Entrada
    {
        public PokenType tipo = PokenType.Normal;

        [Tooltip("Estela del golpe. Sale del Poken al abrir la ventana de daño, " +
                 "haya pegado o no.")]
        public Efecto arco;

        [Tooltip("Estalla sobre cada rival alcanzado.")]
        public Efecto impacto;

        [Tooltip("Sale al arrancar la animacion, antes del golpe. Opcional: la " +
                 "mayoria de los ataques fisicos no llevan nada aca.")]
        public Efecto lanzamiento;
    }

    [Tooltip("Un juego de efectos por tipo. Lo llena PokeRush > 3D > Cablear catalogo de FX.")]
    public List<Entrada> entradas = new List<Entrada>();

    [Header("Respaldos")]
    [Tooltip("Para los tipos que todavia no tienen efecto propio.")]
    public Efecto arcoPorDefecto;
    public Efecto impactoPorDefecto;

    public Efecto Arco(PokenType tipo)
    {
        Entrada entrada = Buscar(tipo);
        return entrada != null && entrada.arco.Existe ? entrada.arco : arcoPorDefecto;
    }

    public Efecto Impacto(PokenType tipo)
    {
        Entrada entrada = Buscar(tipo);
        return entrada != null && entrada.impacto.Existe ? entrada.impacto : impactoPorDefecto;
    }

    public Efecto Lanzamiento(PokenType tipo)
    {
        Entrada entrada = Buscar(tipo);
        return entrada != null ? entrada.lanzamiento : default;
    }

    private Entrada Buscar(PokenType tipo)
    {
        for (int i = 0; i < entradas.Count; i++)
            if (entradas[i] != null && entradas[i].tipo == tipo) return entradas[i];

        return null;
    }
}
