using UnityEngine;

// BANCO DE PRUEBAS. No es parte del juego.
//
// Le mete objetos a la partida cargada para poder probar la MOCHILA en combate
// (y el boton CURAR de la fase de liberacion, que usa la misma mochila). Sin
// objetos esos dos caminos no se pueden ejercitar: el menu abre vacio.
//
// Va en la escena demo, nunca en una escena del juego real: ahi los objetos
// salen de la partida guardada.
public class DemoInventorySeeder : MonoBehaviour
{
    [System.Serializable]
    public class Entrada
    {
        public HealItemDefinition objeto;
        public int cantidad = 5;
    }

    [SerializeField] private Entrada[] objetos;

    // Corre despues del SceneBootstrapper (que es el que crea la partida si
    // falta). Start del bootstrap y este comparten frame, asi que se espera un
    // frame para no depender del orden de ejecucion.
    private void Start() => StartCoroutine(SembrarAlFinalDelFrame());

    private System.Collections.IEnumerator SembrarAlFinalDelFrame()
    {
        yield return null;

        PlayerModel player = GameManager.Instance?.CurrentPlayer;
        if (player == null)
        {
            Debug.LogWarning("[Demo] No hay partida cargada: no se pueden sembrar objetos.");
            yield break;
        }

        int total = 0;
        foreach (Entrada e in objetos)
        {
            if (e?.objeto == null || e.cantidad <= 0) continue;
            player.Inventory.AddHealItem(e.objeto, e.cantidad);
            total += e.cantidad;
        }

        Debug.Log($"[Demo] Mochila cargada con {total} objetos para probar.");
    }
}
