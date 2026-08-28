using UnityEngine;

// Que puede tocar en la ruleta.
public enum RoulettePrizeKind
{
    // Casilla vacia: se pierde la apuesta y nada mas.
    Nada,
    Dinero,
    Objeto,
    Poken,
    // ---- penalizaciones ----
    PerderDinero,
    PerderObjeto,
    // El equipo sale golpeado. Nunca deja a nadie debilitado: la ruleta no
    // puede dejar al jugador sin equipo en un piso donde hay que pelear.
    DanioAlEquipo
}

// Una casilla de la ruleta.
//
// Todo lo que define el balance vive aca y en un solo lugar: el peso, que da y
// cuanto. No hay probabilidades repartidas por varios scripts.
[System.Serializable]
public class RoulettePrize
{
    public string nombre = "Premio";
    [Tooltip("Color de la porcion en la rueda.")]
    public Color color = new Color(0.30f, 0.34f, 0.44f, 1f);

    [Tooltip("Cuanto sale esta casilla comparada con las demas. 0 = nunca.")]
    [Min(0)] public int peso = 10;

    public RoulettePrizeKind tipo = RoulettePrizeKind.Dinero;

    [Header("Segun el tipo")]
    [Tooltip("Dinero que da o saca. En DanioAlEquipo es el % de PS que quita.")]
    public int monto = 100;
    public HealItemDefinition objeto;
    [Min(1)] public int cantidad = 1;
    public PokenDefinition poken;
    [Min(1)] public int nivel = 15;

    [Header("Progresion")]
    [Tooltip("Este premio cumple el objetivo del piso. El flag lo dice la mesa.")]
    public bool cumpleObjetivo;

    [Tooltip("Cartel al salir. {0} = el detalle (cuanto dinero, que objeto...).")]
    [TextArea(2, 3)] public string mensaje = "";
}

// La mesa de ruleta: cuanto cuesta jugar, que casillas tiene y que objetivo
// desbloquea.
[CreateAssetMenu(fileName = "NuevaRuleta", menuName = "PokeRush/Casino/Mesa de Ruleta")]
public class RouletteTableDefinition : ScriptableObject
{
    [Header("Identidad")]
    public string mesaNombre = "RULETA";

    [Header("Apuesta")]
    [Tooltip("Lo que cuesta cada giro.")]
    [Min(0)] public int costoPorGiro = 50;

    [Header("Casillas")]
    public RoulettePrize[] premios;

    [Header("Progresion")]
    [Tooltip("Flag que se activa cuando sale una casilla marcada como objetivo. " +
             "Es lo que el ascensor del piso 2 mira para dejar subir.")]
    public StoryFlag flagObjetivo = StoryFlag.Casino_Ruleta_ObjetivoCumplido;

    [Header("Presentacion")]
    [Min(0.5f)] public float duracionDelGiro = 2.5f;
    [Tooltip("Vueltas enteras antes de frenar.")]
    [Min(1)] public int vueltas = 4;

    public int PesoTotal
    {
        get
        {
            if (premios == null) return 0;
            int total = 0;
            foreach (RoulettePrize p in premios)
                if (p != null && p.peso > 0) total += p.peso;
            return total;
        }
    }

    public bool Validar(out string error)
    {
        if (premios == null || premios.Length < 2)
        { error = "necesita al menos dos casillas"; return false; }

        if (PesoTotal <= 0)
        { error = "todas las casillas tienen peso 0: nunca saldria ninguna"; return false; }

        for (int i = 0; i < premios.Length; i++)
        {
            RoulettePrize p = premios[i];
            if (p == null) { error = $"la casilla {i} esta vacia"; return false; }

            if (p.tipo == RoulettePrizeKind.Objeto && p.objeto == null)
            { error = $"la casilla '{p.nombre}' da un objeto pero no tiene ninguno asignado"; return false; }

            if (p.tipo == RoulettePrizeKind.Poken && p.poken == null)
            { error = $"la casilla '{p.nombre}' da un Poken pero no tiene especie asignada"; return false; }
        }

        error = null;
        return true;
    }
}
