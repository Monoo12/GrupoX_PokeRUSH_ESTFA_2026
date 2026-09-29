using System.Collections.Generic;
using UnityEngine;

// Un dialogo del mapa. Lo muestra DialogueController.StartDialogue.
//
// Lo basico es un hablante y sus lineas. Todo lo demas es opcional y sirve para
// contar la historia desde el asset, sin tocar escenas ni codigo:
//
//   Varios hablantes   Una linea que empieza con [NOMBRE] la dice NOMBRE, no el
//                      hablante del asset: "[TIXI] Hola." / "[PJ] ¿Qué pasó?".
//                      [PJ] es el jugador; [] es un narrador sin nombre. El
//                      retrato sale de 'otrosHablantes' (o del asset si el
//                      nombre es el del hablante).
//   Marcas en el texto {PJ} = el nombre del jugador, {INICIAL} = el primer
//                      Pokemon del equipo. Valen en lineas, preguntas y opciones.
//   Variantes          Otras lineas segun los flags de la partida: la primera
//                      variante que se cumple reemplaza a 'lines'. Asi un NPC
//                      cambia lo que dice despues de un evento (Nestor vencido,
//                      el servidor caido, un NPC de otro nivel con el que ya se
//                      hablo). Si ninguna se cumple, van las 'lines'.
//   Objeto clave       'otorgaObjeto' se entrega al terminar las lineas (una
//                      sola vez: si ya lo tiene, no se duplica).
//   Que sigue          Una 'pregunta' con 'respuestas' (cada una puede pedir un
//                      flag u objeto para aparecer, encender un flag y seguir
//                      con otro dialogo), o directamente 'siguiente'. Asi se
//                      arman charlas con ramas y eventos de varios pasos.
//
// Una definicion que no tiene nada que decir (sin lineas y sin variante que se
// cumpla) no abre la caja: pasa directo a lo que sigue. Sirve para meter un
// tramo que solo suena si paso algo antes.
[CreateAssetMenu(fileName = "NewDialogue", menuName = "PokeRush/DialogueDefinition")]
public class DialogueDefinition : ScriptableObject
{
    [Header("Hablante")]
    public string speakerName;
    public Sprite speakerPortrait;

    [Header("Lineas")]
    [TextArea(2, 5)]
    //Nota: [TextArea(2, 5)] hace que cada línea de diálogo se edite en un campo de texto grande en el Inspector en lugar de una línea chica — más cómodo para escribir diálogos largos. El primer número es el mínimo de líneas visibles y el segundo el máximo antes de aparecer el scroll.    
    public string[] lines;

    [Header("Flag que activa este dialogo (opcional)")]
    public StoryFlag requiredFlag;

    [Header("Flag que otorga al terminar (opcional)")]
    public StoryFlag grantsFlag;

    [System.Serializable]
    public class Hablante
    {
        [Tooltip("Como aparece entre corchetes al principio de la linea. Ej: SISTEMA")]
        public string nombre;
        public Sprite retrato;
    }

    [System.Serializable]
    public class Variante
    {
        [Tooltip("Para quien lee el Inspector. Ej: despues de vencer a Nestor")]
        public string nota;
        [Tooltip("La partida tiene que tener este flag. None = no pide nada.")]
        public StoryFlag requiere;
        [Tooltip("Y tambien este. None = no pide nada.")]
        public StoryFlag requiereTambien;
        [Tooltip("La partida NO tiene que tener este flag. None = no excluye nada.")]
        public StoryFlag excluye;
        [TextArea(2, 5)]
        public string[] lineas;
        [Tooltip("Se enciende al terminar ESTAS lineas (en vez del grantsFlag del asset).")]
        public StoryFlag otorgaFlag;
    }

    [System.Serializable]
    public class Respuesta
    {
        [Tooltip("Lo que se lee en la lista. Corto: con dos opciones va en la ventanita del Si/No.")]
        public string opcion;
        [Tooltip("Solo aparece con este flag. None = siempre.")]
        public StoryFlag requiere;
        [Tooltip("Solo aparece teniendo este objeto clave. Vacio = siempre.")]
        public SpecialItemDefinition requiereObjeto;
        [Tooltip("Se enciende al elegirla.")]
        public StoryFlag otorgaFlag;
        [Tooltip("Lo que se dice despues. Vacio = termina la charla.")]
        public DialogueDefinition siguiente;
    }

    [Header("Otros que hablan en las lineas [NOMBRE] (opcional)")]
    public Hablante[] otrosHablantes;

    [Header("Variantes segun la historia (opcional)")]
    public Variante[] variantes;

    [Header("Objeto clave que entrega al terminar (opcional)")]
    public SpecialItemDefinition otorgaObjeto;

    [Header("Que sigue (opcional)")]
    [Tooltip("Pregunta al terminar las lineas. Vacia = no pregunta.")]
    [TextArea(2, 3)]
    public string pregunta;
    public Respuesta[] respuestas;
    [Tooltip("Que respuesta elige Escape (indice en 'respuestas'). -1 = la ultima. " +
             "Que no sea la mas drastica: con Cruz, Escape es huir, no atacar.")]
    public int cancelarElige = -1;
    [Tooltip("Si no hay pregunta: el dialogo que sigue. Vacio = termina.")]
    public DialogueDefinition siguiente;

    // ---------- lo que se dice segun la partida ----------

    // La variante que corresponde a esta partida, o null si no se cumple
    // ninguna (entonces van las 'lines').
    public Variante VarianteQueToca(StoryModel story)
    {
        if (variantes == null) return null;
        foreach (Variante v in variantes)
            if (v != null && SeCumple(v, story)) return v;
        return null;
    }

    public static bool SeCumple(Variante v, StoryModel story)
    {
        if (v == null) return false;
        if (!Tiene(story, v.requiere) || !Tiene(story, v.requiereTambien)) return false;
        return v.excluye == StoryFlag.None || story == null || !story.HasFlag(v.excluye);
    }

    // None se cumple siempre; cualquier otro flag pide una partida que lo tenga.
    private static bool Tiene(StoryModel story, StoryFlag flag)
        => flag == StoryFlag.None || (story != null && story.HasFlag(flag));

    public string[] LineasPara(StoryModel story)
    {
        Variante v = VarianteQueToca(story);
        return v != null ? v.lineas : lines;
    }

    public StoryFlag FlagPara(StoryModel story)
    {
        Variante v = VarianteQueToca(story);
        return v != null ? v.otorgaFlag : grantsFlag;
    }

    // Tiene algo para decir en esta partida (una linea que no este vacia).
    public bool TieneQueDecir(StoryModel story)
    {
        string[] l = LineasPara(story);
        if (l == null) return false;
        foreach (string s in l)
            if (!string.IsNullOrWhiteSpace(s)) return true;
        return false;
    }

    // Las respuestas que se pueden elegir con esta partida y este jugador.
    public List<Respuesta> RespuestasDisponibles(StoryModel story, PlayerModel player)
    {
        var lista = new List<Respuesta>();
        if (respuestas == null) return lista;
        foreach (Respuesta r in respuestas)
        {
            if (r == null || string.IsNullOrEmpty(r.opcion)) continue;
            if (!Tiene(story, r.requiere)) continue;
            if (r.requiereObjeto != null &&
                (player == null || !player.Inventory.HasSpecialItem(r.requiereObjeto))) continue;
            lista.Add(r);
        }
        return lista;
    }

    public bool TienePregunta => !string.IsNullOrWhiteSpace(pregunta) && respuestas != null && respuestas.Length > 0;

    public Sprite RetratoDe(string nombre)
    {
        if (string.IsNullOrEmpty(nombre)) return null;
        if (string.Equals(nombre, speakerName, System.StringComparison.OrdinalIgnoreCase)) return speakerPortrait;
        if (otrosHablantes == null) return null;
        foreach (Hablante h in otrosHablantes)
            if (h != null && string.Equals(h.nombre, nombre, System.StringComparison.OrdinalIgnoreCase))
                return h.retrato;
        return null;
    }
}
