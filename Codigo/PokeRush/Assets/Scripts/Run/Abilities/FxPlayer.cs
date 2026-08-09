using UnityEngine;

// Pone en el mundo un efecto de los packs (Hovl "Magic effects" y Jean Moreno
// "Cartoon FX Remaster") y lo limpia solo cuando termina.
//
// Son prefabs de ParticleSystem de verdad: traen su propio movimiento, su color
// y su curva de vida. Eso es lo que los separa de los FX de UNITE, que son
// mallas estaticas -el juego original las anima desde un motor de efectos que no
// viaja en el dump- y por eso quedaban como una lamina congelada en el aire por
// mas que se las hiciera girar a mano.
//
// Aca no se le toca el arte a nadie. Solo hacen falta dos cosas que los prefabs
// no pueden traer resueltas, porque dependen de la habilidad que los use:
//
//   TAMAÑO. Cada efecto viene autorado a la escala de la demo de su pack, que no
//   tiene nada que ver con el radio de un golpe nuestro. Se mide cuanto ocupa
//   -de sus propios parametros, no a ojo- y se lo lleva al tamaño pedido.
//
//   LIMPIEZA. Nadie destruye la instancia, asi que sin esto la escena se llena
//   de sistemas apagados.
public static class FxPlayer
{
    // Techo de vida de una instancia. Los efectos de bucle (auras, fuego
    // continuo) declaran duracion infinita, y sin un tope quedarian para siempre.
    private const float VidaMaxima = 8f;

    /// Reproduce un efecto del catalogo, que ya trae su medida real horneada.
    public static GameObject Reproducir(FxCatalog.Efecto efecto, Vector3 posicion, Quaternion rotacion,
                                        float tamaño = 2f, Transform seguir = null)
        => Reproducir(efecto.prefab, posicion, rotacion, tamaño, seguir, efecto.medida);

    /// Reproduce el efecto en el mundo.
    ///
    /// `tamaño` es cuanto tiene que MEDIR de punta a punta en unidades de mundo:
    /// normalmente el diametro del area del golpe, para que el efecto y la
    /// hitbox se vean del mismo tamaño. Si `seguir` no es null, el efecto queda
    /// colgado de ese transform y lo acompaña -eso es lo que hace falta para los
    /// FX de lanzamiento, que nacen del Poken y tienen que moverse con el.
    ///
    /// `medidaNativa` es cuanto mide el prefab a escala 1, medido de verdad por
    /// el cableador del catalogo. Con 0 se cae a una estimacion, que es bastante
    /// mala: ver el comentario de FactorDeEscala.
    public static GameObject Reproducir(GameObject prefab, Vector3 posicion, Quaternion rotacion,
                                        float tamaño = 2f, Transform seguir = null, float medidaNativa = 0f)
    {
        if (prefab == null) return null;

        GameObject instancia = Object.Instantiate(prefab, posicion, rotacion);
        if (seguir != null) instancia.transform.SetParent(seguir, true);

        var sistemas = instancia.GetComponentsInChildren<ParticleSystem>(true);

        PrepararEscalado(sistemas);

        float objetivo = Mathf.Max(0.1f, tamaño);
        float nativa = medidaNativa > 0.01f ? medidaNativa : Estimar(sistemas);
        instancia.transform.localScale = Vector3.one * (nativa > 0.01f ? objetivo / nativa : 1f);

        foreach (Renderer render in instancia.GetComponentsInChildren<Renderer>(true))
            render.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        Object.Destroy(instancia, Duracion(sistemas));
        return instancia;
    }

    // Sin esto el efecto NO escala.
    //
    // En modo Local un ParticleSystem ignora la escala de sus padres: agrandar
    // el objeto raiz mueve los hijos de lugar pero deja las particulas del mismo
    // tamaño y viajando la misma distancia, asi que el efecto se desarma en vez
    // de crecer. Los packs vienen en Local porque en sus demos cada efecto se
    // usa a su tamaño original. Nosotros lo escalamos por habilidad, asi que
    // necesitamos Hierarchy, que es el modo que si hereda la escala.
    private static void PrepararEscalado(ParticleSystem[] sistemas)
    {
        foreach (ParticleSystem sistema in sistemas)
        {
            ParticleSystem.MainModule principal = sistema.main;
            principal.scalingMode = ParticleSystemScalingMode.Hierarchy;
        }
    }

    // Cuanto mide el efecto, ESTIMADO de los parametros del sistema.
    //
    // Es el ultimo recurso, para prefabs que no pasaron por el cableador. Da
    // mal, y conviene saber por que: supone que una particula viaja
    // velocidad x vida en linea recta, y casi ningun efecto hace eso -tienen
    // rozamiento, gravedad, curvas de velocidad. Medido contra la realidad se
    // pasa de largo en unos ("Star hit" predice 15 y ocupa 9, asi que el efecto
    // queda encogido a una bolita) y se queda corto en otros. La medida buena
    // sale de simular, y eso solo se puede en el editor: por eso el catalogo la
    // trae horneada.
    private static float Estimar(ParticleSystem[] sistemas)
    {
        float radio = 0f;

        foreach (ParticleSystem sistema in sistemas)
        {
            ParticleSystem.MainModule principal = sistema.main;

            float vida = principal.startLifetime.constantMax;
            float alcance = sistema.shape.radius
                            + principal.startSpeed.constantMax * vida
                            + principal.startSize.constantMax * 0.5f;

            radio = Mathf.Max(radio, alcance * Maxima(sistema.transform.localScale));
        }

        return radio * 2f;
    }

    private static float Duracion(ParticleSystem[] sistemas)
    {
        float duracion = 0.5f;

        foreach (ParticleSystem sistema in sistemas)
        {
            ParticleSystem.MainModule principal = sistema.main;
            if (principal.loop) return VidaMaxima;

            duracion = Mathf.Max(duracion,
                principal.startDelay.constantMax + principal.duration + principal.startLifetime.constantMax);
        }

        return Mathf.Min(duracion, VidaMaxima);
    }

    private static float Maxima(Vector3 v)
        => Mathf.Max(Mathf.Abs(v.x), Mathf.Max(Mathf.Abs(v.y), Mathf.Abs(v.z)));
}
