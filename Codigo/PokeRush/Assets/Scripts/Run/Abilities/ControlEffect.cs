using UnityEngine;

// Estados de control al estilo UNITE: aturdir, ralentizar, empujar, inmovilizar.
//
// Son OTRA COSA que los StatusEffect clasicos de Pokemon (quemado, paralizado,
// dormido). Aquellos duran turnos y se aplican por probabilidad; estos duran
// segundos y se aplican al impactar. Conviven: un Poken puede estar quemado y
// aturdido a la vez, y cada sistema lleva su propia cuenta.
public enum ControlKind
{
    Ninguno,
    // Pierde el control unos segundos. No se mueve ni ataca.
    Aturdir,
    // Se mueve mas lento, pero sigue pudiendo atacar.
    Ralentizar,
    // Sale despedido en la direccion del golpe.
    Empujar,
    // No se puede mover, pero si atacar.
    Inmovilizar,
}

[System.Serializable]
public struct ControlEffect
{
    public ControlKind tipo;

    [Tooltip("Duracion en segundos.")]
    public float duracion;

    [Tooltip("Para Ralentizar, cuanto queda de su velocidad (0.5 = la mitad). " +
             "Para Empujar, cuantas unidades lo desplaza.")]
    public float fuerza;

    public bool EsAlguno => tipo != ControlKind.Ninguno && duracion > 0f;
}
