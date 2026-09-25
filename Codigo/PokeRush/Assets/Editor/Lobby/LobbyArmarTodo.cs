using UnityEditor;
using UnityEngine;

// Todo lo de la Quarcho Cueva de una, en el orden en que depende:
//
//   1. Las MT de cada especie y las descripciones de los movimientos (el tutor
//      de Nestor las necesita).
//   2. Las pantallas de la tienda y del tutor (el lobby las abre).
//   3. La escena de la cueva.
//   4. El recorrido: creacion de personaje y "Continuar" van a la cueva.
//   5. Los smoke tests del lobby.
//
// Se puede correr desde el menu o en batchmode:
//   Unity -batchmode -quit -projectPath ... -executeMethod LobbyArmarTodo.EnBatch
// En batchmode sale con codigo 1 si fallo algun test.
public static class LobbyArmarTodo
{
    [MenuItem("PokeRush/Quarcho Cueva/Armar todo y probar")]
    public static void ArmarTodo()
    {
        PokeRush.EditorTools.PokeApi.MtYDescripciones.Menu();
        LobbyScreensBuilder.Construir();
        QuarchoCuevaBuilder.Armar();
        QuarchoCuevaBuilder.ConectarRecorrido();
        PokeRush.EditorTools.LobbySmokeTests.RunAll();
    }

    public static void EnBatch()
    {
        ArmarTodo();
        if (Application.isBatchMode && PokeRush.EditorTools.LobbySmokeTests.Fallos > 0)
            EditorApplication.Exit(1);
    }
}
