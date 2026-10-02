using System.Collections.Generic;
using UnityEngine;

// Los efectos de sonido del juego, uno por momento: Sonidos.Confirmar(),
// Sonidos.Curar(), Sonidos.Grito(poken)...
//
// Cada uno busca su clip en el CatalogoDeAudio y lo hace sonar con el volumen
// medido. Si falta el catalogo o el clip, no suena nada y no hay error: el
// codigo que llama no tiene que chequear nada.
//
// Hay tres clases de sonido:
//   - efecto corto (PlayOneShot): se pueden pisar, como dos golpes seguidos;
//   - jingle (Curar, ObjetoRecibido...): la musica baja mientras suena;
//   - largo (la ruleta girando): devuelve un SfxEnCurso para cortarlo.
public static class Sonidos
{
    private static CatalogoDeAudio C => CatalogoDeAudio.Actual;
    private static AudioService Audio => AudioService.Instance;

    public static void Tocar(Sonido s, float volumenExtra = 1f)
    {
        if (s == null || !s.Hay) return;
        Audio.PlaySFX(s.clip, s.Volumen * volumenExtra);
    }

    public static void Jingle(Sonido s)
    {
        if (s == null || !s.Hay) return;
        Audio.PlayJingle(s.clip, Mathf.Clamp01(s.Volumen));
    }

    public static SfxEnCurso Largo(Sonido s, bool loop = false)
    {
        if (s == null || !s.Hay) return null;
        return Audio.PlayLargo(s.clip, Mathf.Clamp01(s.Volumen), loop);
    }

    // ---------- menus ----------

    public static void Cursor() => Tocar(C?.cursor);
    public static void Confirmar() => Tocar(C?.confirmar);
    public static void Cancelar() => Tocar(C?.cancelar);
    public static void AbrirMenu() => Tocar(C?.abrirMenu);
    public static void CerrarMenu() => Tocar(C?.cerrarMenu);
    public static void Error() => Tocar(C?.error);
    public static void Guardar() => Jingle(C?.guardar);
    public static void Comprar() => Tocar(C?.comprar);
    public static void PcPrender() => Tocar(C?.pcPrender);
    public static void PcApagar() => Tocar(C?.pcApagar);
    public static void PcAgarrar() => Tocar(C?.pcAgarrar);
    public static void PcSoltar() => Tocar(C?.pcSoltar);

    // ---------- mundo ----------

    public static void Curar() => Jingle(C?.curar);
    public static void ObjetoEncontrado() => Jingle(C?.objetoEncontrado);
    public static void ObjetoRecibido() => Jingle(C?.objetoRecibido);
    public static void PokemonRecibido() => Jingle(C?.pokemonRecibido);
    public static void Teletransporte() => Tocar(C?.teletransporte);
    public static void PuertaEntrar() => Tocar(C?.puertaEntrar);
    public static void PuertaSalir() => Tocar(C?.puertaSalir);
    public static void AscensorLlega() => Tocar(C?.ascensorLlega);
    public static SfxEnCurso AscensorViaja() => Largo(C?.ascensorViaja);
    public static void Exclamacion() => Tocar(C?.exclamacion);

    // Chocar contra una pared: como en la saga, suena mientras se insiste, pero
    // no en cada frame.
    private static float _ultimoChoque = -10f;
    public static void Choque()
    {
        if (Time.unscaledTime - _ultimoChoque < 0.38f) return;
        _ultimoChoque = Time.unscaledTime;
        Tocar(C?.choque);
    }

    // ---------- combate ----------

    public static void LanzarBall() => Tocar(C?.lanzarBall);
    public static void PokeballSale() => Tocar(C?.pokeballSale);
    public static void Retirar() => Tocar(C?.retirar);
    public static void Debilitado() => Tocar(C?.debilitado);
    public static void Huir() => Tocar(C?.huir);

    public static void CambioDeStat(bool sube) => Tocar(sube ? C?.statSube : C?.statBaja);

    public static void Estado(StatusEffect estado)
    {
        CatalogoDeAudio c = C;
        if (c == null) return;
        switch (estado)
        {
            case StatusEffect.Burned: Tocar(c.quemado); break;
            case StatusEffect.Poisoned: Tocar(c.envenenado); break;
            case StatusEffect.Paralyzed: Tocar(c.paralizado); break;
            case StatusEffect.Asleep: Tocar(c.dormido); break;
            case StatusEffect.Frozen: Tocar(c.congelado); break;
        }
    }

    public static void DelEvento(SonidoDelEvento sonido)
    {
        CatalogoDeAudio c = C;
        if (c == null) return;
        switch (sonido)
        {
            case SonidoDelEvento.Golpe: Tocar(c.golpeNormal); break;
            case SonidoDelEvento.GolpeSuperEficaz: Tocar(c.golpeSuperEficaz); break;
            case SonidoDelEvento.GolpePocoEficaz: Tocar(c.golpePocoEficaz); break;
            case SonidoDelEvento.Curacion: Tocar(c.usarObjeto); break;
            case SonidoDelEvento.SubirNivel: Jingle(c.subirNivel); break;
        }
    }

    public static void Grito(PokenModel poken, float tono = 1f)
    {
        CatalogoDeAudio c = C;
        if (c == null || poken == null) return;
        AudioClip grito = c.GritoDe(poken.Definition);
        if (grito == null) return;
        Audio.PlaySFX(Cargado(grito), c.volumenDeGritos, tono);
    }

    public static AudioClip CargaDe(MoveDefinition movimiento)
        => C?.AtaqueDe(movimiento)?.carga;

    public static AudioClip ImpactoDe(MoveDefinition movimiento)
    {
        CatalogoDeAudio c = C;
        if (c == null) return null;
        CatalogoDeAudio.SonidoDeAtaque propio = c.AtaqueDe(movimiento);
        if (propio != null && propio.impacto != null) return propio.impacto;
        // Sin sonido propio: el de su tipo (Forcejeo y compania, sin movimiento: Normal).
        return c.AtaqueDelTipo(movimiento != null ? movimiento.type : PokenType.Normal);
    }

    public static void Ataque(AudioClip clip)
    {
        CatalogoDeAudio c = C;
        if (c == null || clip == null) return;
        Audio.PlaySFX(Cargado(clip), c.volumenDeAtaques, 1f);
    }

    // Los gritos y los ataques se importan sin precargar (son cientos): se
    // cargan los de la pelea al empezar y se sueltan al terminar.
    private static readonly List<AudioClip> _cargadosParaLaPelea = new List<AudioClip>();

    public static void PrecargarPelea(PlayerModel jugador, EnemyModel rival)
    {
        CatalogoDeAudio c = C;
        if (c == null) return;
        LiberarPelea();
        if (jugador != null) foreach (PokenModel p in jugador.Team) Precargar(c, p);
        if (rival != null) foreach (PokenModel p in rival.Team) Precargar(c, p);
    }

    private static void Precargar(CatalogoDeAudio c, PokenModel poken)
    {
        if (poken == null) return;
        Sumar(c.GritoDe(poken.Definition));
        foreach (MoveModel slot in poken.Moves)
        {
            if (slot == null || slot.Definition == null) continue;
            CatalogoDeAudio.SonidoDeAtaque a = c.AtaqueDe(slot.Definition);
            if (a == null) continue;
            Sumar(a.carga);
            Sumar(a.impacto);
        }
    }

    private static void Sumar(AudioClip clip)
    {
        if (clip == null || _cargadosParaLaPelea.Contains(clip)) return;
        if (clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        _cargadosParaLaPelea.Add(clip);
    }

    public static void LiberarPelea()
    {
        foreach (AudioClip clip in _cargadosParaLaPelea)
            if (clip != null && !clip.preloadAudioData) clip.UnloadAudioData();
        _cargadosParaLaPelea.Clear();
    }

    // Uno que no se precargo (un movimiento aprendido a mitad de pelea) se carga
    // al vuelo: tarda un instante la primera vez.
    private static AudioClip Cargado(AudioClip clip)
    {
        if (clip != null && clip.loadState == AudioDataLoadState.Unloaded) clip.LoadAudioData();
        return clip;
    }

    // ---------- casino ----------

    public static SfxEnCurso SlotsGiro() => Largo(C?.slotsGiro);
    public static void SlotsFrena() => Tocar(C?.slotsFrena);
    public static void SlotsMoneda() => Tocar(C?.slotsMoneda);
    public static void PremioChico() => Jingle(C?.premioChico);
    public static void PremioGrande() => Jingle(C?.premioGrande);
    public static SfxEnCurso RuletaGiro() => Largo(C?.ruletaGiro);
    public static SfxEnCurso RuedaGiro() => Largo(C?.ruedaGiro);
    public static void Fichas() => Tocar(C?.fichas);
    public static void Carta() => Tocar(C?.carta);
    public static void Mezclar() => Tocar(C?.mezclar);
}
