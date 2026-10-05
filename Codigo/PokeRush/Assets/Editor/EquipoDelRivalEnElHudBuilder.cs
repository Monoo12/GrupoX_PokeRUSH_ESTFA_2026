using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

// Las Poke Balls chiquitas del equipo rival en el HUD de combate (playtest
// del 5-oct: "como en los juegos originales, unas pequeñitas pokebolas que
// indiquen cuantos pokemons tiene y le quedan al enemigo").
//
//   PokeRush > Combate > Agregar las Poke Balls del rival
//
// Van debajo de la barra del databox rival, del lado izquierdo, donde el
// sprite es transparente (la lengueta oscura de la derecha arranca en x 150).
// Los iconos son los del pack (icon_ball, _status, _faint) a la mitad: el arte
// es 2x, asi que a 15 px no se pierde ningun pixel (Sprites/UI/Battle/
// icon_ball_small*). Las prende y colorea BattleHudView.UpdateEnemyTeamBalls.
//
// Se puede volver a correr: borra la fila anterior y la rehace.
public static class EquipoDelRivalEnElHudBuilder
{
    private const string Prefab = "Assets/Prefabs/Combate/SistemaDeCombate.prefab";
    private const string Sprites = "Assets/Sprites/UI/Battle/";
    private const string Nombre = "EquipoRival";

    private const int Lugares = 6;
    private const float Lado = 15f;
    private const float Separacion = 2f;
    // Desde la esquina de arriba a la izquierda del databox (70 de alto): la
    // barra termina en la fila 55 del sprite y el nombre arranca en x 14. Se
    // pasa un pixel del databox por abajo; el escudo del nucleo (y -72) queda
    // a un pixel.
    private static readonly Vector2 Esquina = new Vector2(14f, -56f);

    [MenuItem("PokeRush/Combate/Agregar las Poke Balls del rival")]
    public static void Agregar()
    {
        GameObject raiz = PrefabUtility.LoadPrefabContents(Prefab);
        try
        {
            var hud = raiz.GetComponentInChildren<BattleHudView>(true);
            if (hud == null) { Debug.LogError($"[Combate] {Prefab} no tiene BattleHudView."); return; }

            var so = new SerializedObject(hud);
            var databox = so.FindProperty("enemyBox").objectReferenceValue as GameObject;
            if (databox == null) { Debug.LogError("[Combate] El HUD no tiene el databox rival."); return; }

            Sprite sana = Cargar("icon_ball_small.png");
            Sprite conEstado = Cargar("icon_ball_small_status.png");
            Sprite debilitada = Cargar("icon_ball_small_faint.png");
            if (sana == null || conEstado == null || debilitada == null) return;

            Transform vieja = databox.transform.Find(Nombre);
            if (vieja != null) Object.DestroyImmediate(vieja.gameObject);

            var fila = new GameObject(Nombre, typeof(RectTransform));
            var rf = (RectTransform)fila.transform;
            rf.SetParent(databox.transform, false);
            rf.anchorMin = rf.anchorMax = new Vector2(0f, 1f);
            rf.pivot = new Vector2(0f, 1f);
            rf.anchoredPosition = Esquina;
            rf.sizeDelta = new Vector2(Lugares * Lado + (Lugares - 1) * Separacion, Lado);

            var bolas = new Image[Lugares];
            for (int i = 0; i < Lugares; i++)
            {
                var go = new GameObject($"Bola{i + 1}", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                var rt = (RectTransform)go.transform;
                rt.SetParent(rf, false);
                rt.anchorMin = rt.anchorMax = new Vector2(0f, 1f);
                rt.pivot = new Vector2(0f, 1f);
                rt.anchoredPosition = new Vector2(i * (Lado + Separacion), 0f);
                rt.sizeDelta = new Vector2(Lado, Lado);
                Image img = go.GetComponent<Image>();
                img.sprite = sana;
                img.raycastTarget = false;
                // Apagadas hasta que el HUD sepa cuantos trae el rival: asi no
                // se ven seis bolas contra un salvaje.
                go.SetActive(false);
                bolas[i] = img;
            }

            SerializedProperty lista = so.FindProperty("enemyTeamBalls");
            lista.arraySize = Lugares;
            for (int i = 0; i < Lugares; i++)
                lista.GetArrayElementAtIndex(i).objectReferenceValue = bolas[i];
            so.FindProperty("teamBallHealthy").objectReferenceValue = sana;
            so.FindProperty("teamBallStatus").objectReferenceValue = conEstado;
            so.FindProperty("teamBallFainted").objectReferenceValue = debilitada;
            so.ApplyModifiedPropertiesWithoutUndo();

            PrefabUtility.SaveAsPrefabAsset(raiz, Prefab);
            Debug.Log($"[Combate] Poke Balls del rival agregadas a {Prefab}.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(raiz);
        }
    }

    private static Sprite Cargar(string archivo)
    {
        Sprite s = AssetDatabase.LoadAssetAtPath<Sprite>(Sprites + archivo);
        if (s == null) Debug.LogError($"[Combate] Falta {Sprites}{archivo}.");
        return s;
    }
}
