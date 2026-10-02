using System;
using System.Collections.Generic;
using UnityEngine;

// Donde esta cada especie y cada movimiento dentro de Resources, por nombre.
//
// Existe para no cargar las 445 especies al arrancar: cada PokenDefinition
// arrastra sus tiras animadas (frente, espalda y shiny, sin comprimir para no
// tocar el pixel art) y cargarlas todas eran varios GB. Con el indice,
// DefinitionRepository carga cada una recien cuando se la pide (la partida
// guardada, el equipo, los rivales).
//
// Lo arma PokeRush > Release > Armar indice de definiciones, y se rearma solo
// antes de cada build. Si falta un nombre (alguien agrego una especie y no lo
// rearmo), DefinitionRepository cae a la carga completa de antes: no se rompe
// nada, solo vuelve a gastar la memoria.
public class IndiceDeDefiniciones : ScriptableObject
{
    public const string RutaEnResources = "Definitions/IndiceDeDefiniciones";

    [Serializable]
    public class Entrada
    {
        public string nombre;   // pokenName / moveName (la clave de siempre)
        public string ruta;     // relativa a Resources, sin extension
    }

    public List<Entrada> pokens = new List<Entrada>();
    public List<Entrada> movimientos = new List<Entrada>();
}
