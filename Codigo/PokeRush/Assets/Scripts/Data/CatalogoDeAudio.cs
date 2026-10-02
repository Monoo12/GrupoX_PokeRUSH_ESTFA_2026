using System;
using System.Collections.Generic;
using UnityEngine;

// Un sonido del juego: el clip y cuanto hay que subirlo o bajarlo para que
// suene parejo con el resto. La ganancia la calcula el armador del catalogo
// midiendo el volumen real del archivo (PokeRush > Audio > Armar catalogo de
// audio); el campo 'volumen' es el retoque a mano encima de eso.
[Serializable]
public class Sonido
{
    public AudioClip clip;
    [Tooltip("Retoque a mano. 1 = como lo dejo el armador.")]
    [Range(0f, 2f)] public float volumen = 1f;
    [Tooltip("La pone el armador del catalogo midiendo el archivo. No tocar.")]
    public float ganancia = 1f;

    public bool Hay => clip != null;
    public float Volumen => volumen * ganancia;
}

// Todo lo que suena en el juego, en un solo lugar.
//
// Vive en Resources/Audio/CatalogoDeAudio.asset y lo lee el codigo cuando
// hace falta: ninguna escena tiene que arrastrar clips. Asi una escena nueva
// (la arena de Julieta, por ejemplo) ya tiene musica con solo llamarse como
// dice la tabla de 'escenas'.
//
// Lo arma PokeRush > Audio > Armar catalogo de audio a partir de las carpetas
// de Assets/Audio (ver Documentacion/Documentacion del Codigo/Audio_Del_Juego.md).
// Despues se puede retocar a mano desde el Inspector: el armador no pisa los
// 'volumen' que alguien haya cambiado.
[CreateAssetMenu(fileName = "CatalogoDeAudio", menuName = "PokeRush/Audio/Catalogo de audio")]
public class CatalogoDeAudio : ScriptableObject
{
    public const string RutaEnResources = "Audio/CatalogoDeAudio";

    [Serializable]
    public class MusicaDeUnaEscena
    {
        [Tooltip("Nombre de la escena, sin .unity (el de Build Settings).")]
        public string escena;
        public Sonido musica = new Sonido();
        [Tooltip("Si esta escena tiene musica de combate propia para TODAS sus peleas. " +
                 "Vacio = la del rival, o una de las peleas comunes.")]
        public Sonido combate = new Sonido();
        [Tooltip("Superficie de los pasos en esta escena (una de 'superficies').")]
        public string superficie;
    }

    [Serializable]
    public class MusicaDeRival
    {
        [Tooltip("Nombre del asset del rival (EnemyDefinition o BossDefinition). Por nombre y no por " +
                 "referencia: asi el catalogo no carga su equipo al arrancar.")]
        public string rival;
        public Sonido musica = new Sonido();
    }

    [Serializable]
    public class Superficie
    {
        public string nombre;
        public Sonido[] pasos = new Sonido[0];
    }

    // Especies y movimientos van por NOMBRE y no por referencia: una referencia
    // haria que cargar el catalogo cargue las 445 especies con sus tiras (ver
    // IndiceDeDefiniciones).
    [Serializable]
    public class GritoDeEspecie
    {
        [Tooltip("Nombre del asset de la especie (0001_Bulbasaur).")]
        public string especie;
        public AudioClip grito;
    }

    [Serializable]
    public class SonidoDeAtaque
    {
        [Tooltip("englishName del movimiento (el slug de PokeAPI: thunder-punch).")]
        public string movimiento;
        [Tooltip("Suena al cargar el ataque. Vacio en los de un solo sonido.")]
        public AudioClip carga;
        [Tooltip("Suena en el impacto (o al usarlo, si es de estado).")]
        public AudioClip impacto;
    }

    [Header("Musica por escena")]
    public List<MusicaDeUnaEscena> escenas = new List<MusicaDeUnaEscena>();
    [Tooltip("Volumen de la musica antes del que elige el jugador en Opciones.")]
    [Range(0f, 1f)] public float volumenBaseDeMusica = 0.5f;
    [Tooltip("Segundos del fundido cruzado entre una cancion y la siguiente.")]
    [Min(0f)] public float fundidoEntreCanciones = 0.8f;

    [Header("Musica de combate")]
    [Tooltip("Rivales con tema propio (los jefes). Manda sobre todo lo demas.")]
    public List<MusicaDeRival> rivales = new List<MusicaDeRival>();
    [Tooltip("Las peleas comunes: sale una al azar, sin repetir la anterior.")]
    public Sonido[] peleasComunes = new Sonido[0];
    public Sonido victoriaComun = new Sonido();
    [Tooltip("Al ganarle a un jefe (Nestor, Reymar).")]
    public Sonido victoriaImportante = new Sonido();

    [Header("Jefe final (Julieta)")]
    public Sonido julietaFase1 = new Sonido();
    [Tooltip("Control del sistema y control del vinculo.")]
    public Sonido julietaFase2 = new Sonido();
    [Tooltip("La final. Desde que fase arranca lo decide PeleaJulieta.faseDeLaMusicaFinal.")]
    public Sonido julietaFinal = new Sonido();
    [Tooltip("Despues de ganarle, durante el dialogo final.")]
    public Sonido julietaDespues = new Sonido();

    [Header("Creditos")]
    public Sonido creditos = new Sonido();

    [Header("Menus")]
    public Sonido cursor = new Sonido();
    public Sonido confirmar = new Sonido();
    public Sonido cancelar = new Sonido();
    public Sonido abrirMenu = new Sonido();
    public Sonido cerrarMenu = new Sonido();
    public Sonido error = new Sonido();
    public Sonido guardar = new Sonido();
    public Sonido comprar = new Sonido();
    public Sonido pcPrender = new Sonido();
    public Sonido pcApagar = new Sonido();
    public Sonido pcAgarrar = new Sonido();
    public Sonido pcSoltar = new Sonido();

    [Header("Mundo")]
    public Sonido curar = new Sonido();
    public Sonido objetoEncontrado = new Sonido();
    public Sonido objetoRecibido = new Sonido();
    public Sonido pokemonRecibido = new Sonido();
    public Sonido teletransporte = new Sonido();
    public Sonido puertaEntrar = new Sonido();
    public Sonido puertaSalir = new Sonido();
    public Sonido ascensorViaja = new Sonido();
    public Sonido ascensorLlega = new Sonido();
    public Sonido exclamacion = new Sonido();
    public Sonido choque = new Sonido();

    [Header("Combate")]
    public Sonido lanzarBall = new Sonido();
    public Sonido pokeballSale = new Sonido();
    public Sonido retirar = new Sonido();
    public Sonido debilitado = new Sonido();
    public Sonido golpeNormal = new Sonido();
    public Sonido golpeSuperEficaz = new Sonido();
    public Sonido golpePocoEficaz = new Sonido();
    public Sonido subirNivel = new Sonido();
    public Sonido huir = new Sonido();
    public Sonido usarObjeto = new Sonido();
    public Sonido statSube = new Sonido();
    public Sonido statBaja = new Sonido();
    public Sonido quemado = new Sonido();
    public Sonido envenenado = new Sonido();
    public Sonido paralizado = new Sonido();
    public Sonido dormido = new Sonido();
    public Sonido congelado = new Sonido();
    public Sonido confundido = new Sonido();
    [Tooltip("Volumen de los gritos y de los sonidos de ataque.")]
    [Range(0f, 1.5f)] public float volumenDeGritos = 0.8f;
    [Range(0f, 1.5f)] public float volumenDeAtaques = 0.75f;
    public List<GritoDeEspecie> gritos = new List<GritoDeEspecie>();
    public List<SonidoDeAtaque> ataques = new List<SonidoDeAtaque>();
    [Tooltip("Sonido de ataque por tipo, para los movimientos sin sonido propio. " +
             "El indice es el valor de PokenType.")]
    public AudioClip[] ataquePorTipo = new AudioClip[0];

    [Header("Casino")]
    public Sonido slotsGiro = new Sonido();
    public Sonido slotsFrena = new Sonido();
    public Sonido slotsMoneda = new Sonido();
    public Sonido premioChico = new Sonido();
    public Sonido premioGrande = new Sonido();
    public Sonido ruletaGiro = new Sonido();
    public Sonido ruedaGiro = new Sonido();
    public Sonido fichas = new Sonido();
    public Sonido carta = new Sonido();
    public Sonido mezclar = new Sonido();

    [Serializable]
    public class Medicion
    {
        public AudioClip clip;
        public float rmsDb;
        public float pico;
    }

    [Header("Mediciones (las llena el armador; no tocar)")]
    [Tooltip("Volumen medido de cada archivo, para no volver a medirlo en cada armado.")]
    public List<Medicion> mediciones = new List<Medicion>();

    [Header("Pasos")]
    public List<Superficie> superficies = new List<Superficie>();
    [Tooltip("La que se usa en una escena que no figura en la tabla.")]
    public string superficiePorDefecto = "laminado";
    [Range(0f, 1.5f)] public float volumenDePasos = 0.55f;
    [Tooltip("Los NPCs que caminan suenan mas bajo que el jugador.")]
    [Range(0f, 1f)] public float volumenDePasosDeNpc = 0.5f;

    // ---------- lectura ----------

    private static CatalogoDeAudio _actual;
    private static bool _buscado;

    // Null si el asset no existe (todo el audio queda mudo, sin errores).
    public static CatalogoDeAudio Actual
    {
        get
        {
            if (_actual == null && !_buscado)
            {
                _buscado = true;
                _actual = Resources.Load<CatalogoDeAudio>(RutaEnResources);
                if (_actual == null)
                    Debug.LogWarning("[Audio] No esta Resources/Audio/CatalogoDeAudio.asset: el juego va a estar mudo. " +
                                     "Correr PokeRush > Audio > Armar catalogo de audio.");
            }
            return _actual;
        }
    }

    // El proyecto no recarga el dominio al entrar a Play: sin esto el catalogo
    // de la sesion anterior (o su ausencia) quedaria pegado.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        _actual = null;
        _buscado = false;
    }

    private Dictionary<string, MusicaDeUnaEscena> _porEscena;
    private Dictionary<string, AudioClip> _gritos;
    private Dictionary<string, SonidoDeAtaque> _ataques;
    private Dictionary<string, Superficie> _superficies;
    private Dictionary<AudioClip, float> _ganancias;

    private void OnEnable()
    {
        _porEscena = null;
        _gritos = null;
        _ataques = null;
        _superficies = null;
        _ganancias = null;
    }

    public MusicaDeUnaEscena DeLaEscena(string escena)
    {
        if (_porEscena == null)
        {
            _porEscena = new Dictionary<string, MusicaDeUnaEscena>(StringComparer.OrdinalIgnoreCase);
            foreach (MusicaDeUnaEscena e in escenas)
                if (e != null && !string.IsNullOrEmpty(e.escena)) _porEscena[e.escena] = e;
        }
        return escena != null && _porEscena.TryGetValue(escena, out MusicaDeUnaEscena m) ? m : null;
    }

    public AudioClip GritoDe(PokenDefinition especie)
    {
        if (especie == null) return null;
        if (_gritos == null)
        {
            _gritos = new Dictionary<string, AudioClip>();
            foreach (GritoDeEspecie g in gritos)
                if (g != null && !string.IsNullOrEmpty(g.especie) && g.grito != null) _gritos[g.especie] = g.grito;
        }
        return _gritos.TryGetValue(especie.name, out AudioClip c) ? c : null;
    }

    public SonidoDeAtaque AtaqueDe(MoveDefinition movimiento)
    {
        if (movimiento == null || string.IsNullOrEmpty(movimiento.englishName)) return null;
        if (_ataques == null)
        {
            _ataques = new Dictionary<string, SonidoDeAtaque>();
            foreach (SonidoDeAtaque a in ataques)
                if (a != null && !string.IsNullOrEmpty(a.movimiento)) _ataques[a.movimiento] = a;
        }
        return _ataques.TryGetValue(movimiento.englishName, out SonidoDeAtaque s) ? s : null;
    }

    public AudioClip AtaqueDelTipo(PokenType tipo)
    {
        int i = (int)tipo;
        return ataquePorTipo != null && i >= 0 && i < ataquePorTipo.Length ? ataquePorTipo[i] : null;
    }

    public Superficie SuperficieLlamada(string nombre)
    {
        if (_superficies == null)
        {
            _superficies = new Dictionary<string, Superficie>(StringComparer.OrdinalIgnoreCase);
            foreach (Superficie s in superficies)
                if (s != null && !string.IsNullOrEmpty(s.nombre)) _superficies[s.nombre] = s;
        }
        return nombre != null && _superficies.TryGetValue(nombre, out Superficie sup) ? sup : null;
    }

    // La ganancia medida de cualquier clip de musica que figure en el catalogo
    // (para que una escena con su propio MusicaDeEscena suene igual de pareja).
    public float GananciaDe(AudioClip clip)
    {
        if (clip == null) return 1f;
        if (_ganancias == null)
        {
            _ganancias = new Dictionary<AudioClip, float>();
            void Sumar(Sonido s) { if (s != null && s.clip != null) _ganancias[s.clip] = s.Volumen; }
            foreach (MusicaDeUnaEscena e in escenas) { if (e == null) continue; Sumar(e.musica); Sumar(e.combate); }
            foreach (MusicaDeRival r in rivales) if (r != null) Sumar(r.musica);
            foreach (Sonido s in peleasComunes) Sumar(s);
            Sumar(victoriaComun); Sumar(victoriaImportante);
            Sumar(julietaFase1); Sumar(julietaFase2); Sumar(julietaFinal); Sumar(julietaDespues);
            Sumar(creditos);
        }
        return _ganancias.TryGetValue(clip, out float g) ? g : 1f;
    }
}
