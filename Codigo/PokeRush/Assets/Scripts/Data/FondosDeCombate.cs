using System;
using System.Collections.Generic;
using UnityEngine;

// El fondo de combate de cada lugar del juego (playtest del 5-oct: "mejora los
// fondos de las peleas, elegir otros para la mayor parte").
//
// Antes, la pelea que no traia fondo propio caia en el del prefab
// (Pradera_bg): los entrenadores del casino y del Data Center peleaban todos
// en un pasto. Ahora el BattleController elige asi:
//
//   1. El fondo del rival (EnemyDefinition / BossDefinition.battleBackground):
//      los jefes tienen el suyo (Nestor, Reymar, Julieta, el rival del tutorial).
//   2. El de la escena donde se pelea, de esta tabla.
//   3. El del prefab.
//
// Vive en Resources/Definitions/Combate/FondosDeCombate.asset y lo arma
// PokeRush > Combate > Elegir los fondos de combate. Una escena nueva entra
// agregando una fila desde el Inspector.
[CreateAssetMenu(fileName = "FondosDeCombate", menuName = "PokeRush/Combate/Fondos de combate")]
public class FondosDeCombate : ScriptableObject
{
    public const string RutaEnResources = "Definitions/Combate/FondosDeCombate";

    [Serializable]
    public class FondoDeUnaEscena
    {
        [Tooltip("Nombre de la escena, sin .unity (el de Build Settings).")]
        public string escena;
        public Sprite fondo;
    }

    public List<FondoDeUnaEscena> porEscena = new List<FondoDeUnaEscena>();

    private static FondosDeCombate _actual;
    private static bool _buscado;

    public static FondosDeCombate Actual
    {
        get
        {
            if (_actual == null && !_buscado)
            {
                _buscado = true;
                _actual = Resources.Load<FondosDeCombate>(RutaEnResources);
            }
            return _actual;
        }
    }

    // El proyecto no recarga el dominio al entrar a Play (ver CatalogoDeAudio).
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void Reiniciar()
    {
        _actual = null;
        _buscado = false;
    }

    // Null si la escena no esta en la tabla (o no hay tabla).
    public static Sprite DeLaEscena(string escena)
    {
        FondosDeCombate tabla = Actual;
        if (tabla == null || string.IsNullOrEmpty(escena)) return null;
        foreach (FondoDeUnaEscena fila in tabla.porEscena)
            if (fila != null && fila.fondo != null
                && string.Equals(fila.escena, escena, StringComparison.OrdinalIgnoreCase))
                return fila.fondo;
        return null;
    }
}
