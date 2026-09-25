using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;

// Completa, desde el cache de PokeAPI, dos datos que usa el tutor de MTs de
// Nestor en la Quarcho Cueva:
//
//   - PokenDefinition.mtMoves: lo que cada especie aprende por MT o tutor en
//     los juegos originales, filtrado a los movimientos que existen en el
//     proyecto y que el combate implementa.
//   - MoveDefinition.descripcion: el texto en español de cada movimiento.
//
//   PokeRush > Datos > Completar MTs y descripciones de movimientos
//
// Solo LEE el cache (Tools/PokeApiCache): lo que no este cacheado se saltea con
// aviso, no se descarga. No toca ningun otro campo, asi que se puede correr
// cuantas veces haga falta. El importador completo (PokeApiAssetGenerator)
// tambien los llena, para que volver a importar no los borre.
namespace PokeRush.EditorTools.PokeApi
{
    public static class MtYDescripciones
    {
        private const string CarpetaDePokens = "Assets/Resources/Definitions/Pokens";
        private const string CarpetaDeMovimientos = "Assets/Resources/Definitions/Moves";

        public class Reporte
        {
            public int Especies, EspeciesConMt, MtsTotales, Descripciones;
            public List<string> Avisos = new List<string>();
        }

        [MenuItem("PokeRush/Datos/Completar MTs y descripciones de movimientos")]
        public static void Menu()
        {
            Reporte r = CompletarTodo();
            foreach (string aviso in r.Avisos.Take(30)) Debug.LogWarning($"[MTs] {aviso}");
            Debug.Log($"[MTs] TERMINADO. {r.Especies} especies, {r.EspeciesConMt} con MT " +
                      $"({r.MtsTotales} en total, {(r.EspeciesConMt > 0 ? r.MtsTotales / r.EspeciesConMt : 0)} de promedio). " +
                      $"{r.Descripciones} movimientos con descripción. Avisos: {r.Avisos.Count}.");
        }

        public static Reporte CompletarTodo()
        {
            var reporte = new Reporte();

            List<MoveDefinition> movimientos = Cargar<MoveDefinition>(CarpetaDeMovimientos);
            var porId = new Dictionary<int, MoveDefinition>();
            foreach (MoveDefinition m in movimientos)
                if (m != null && m.pokeApiId > 0 && !porId.ContainsKey(m.pokeApiId)) porId[m.pokeApiId] = m;

            try
            {
                AssetDatabase.StartAssetEditing();

                foreach (MoveDefinition m in movimientos)
                {
                    if (m == null || m.pokeApiId <= 0 || !PokeApiDownloader.IsCached("move", m.pokeApiId)) continue;
                    var dto = PokeApiDownloader.GetDto<MoveDto>("move", m.pokeApiId);
                    string texto = Descripcion(dto);
                    if (string.IsNullOrEmpty(texto) || texto == m.descripcion) { if (!string.IsNullOrEmpty(texto)) reporte.Descripciones++; continue; }
                    m.descripcion = texto;
                    EditorUtility.SetDirty(m);
                    reporte.Descripciones++;
                }

                foreach (PokenDefinition especie in Cargar<PokenDefinition>(CarpetaDePokens))
                {
                    if (especie == null || especie.dexNumber <= 0) continue;
                    reporte.Especies++;

                    PokemonDto pokemon = PokemonDeLaEspecie(especie.dexNumber);
                    if (pokemon == null)
                    {
                        reporte.Avisos.Add($"{especie.pokenName}: no esta en el cache, queda sin MT.");
                        continue;
                    }

                    MoveDefinition[] mts = MovimientosDeMt(pokemon, id => porId.TryGetValue(id, out MoveDefinition m) ? m : null);
                    if (mts.Length > 0) { reporte.EspeciesConMt++; reporte.MtsTotales += mts.Length; }
                    else reporte.Avisos.Add($"{especie.pokenName}: ninguna de sus MT existe en el proyecto.");

                    if (IgualA(especie.mtMoves, mts)) continue;
                    especie.mtMoves = mts;
                    EditorUtility.SetDirty(especie);
                }
            }
            finally
            {
                AssetDatabase.StopAssetEditing();
            }

            AssetDatabase.SaveAssets();
            return reporte;
        }

        // Las MT de una especie como assets: solo los que existen en el proyecto
        // y se pueden usar en combate, ordenados por nombre (asi los lista el tutor).
        public static MoveDefinition[] MovimientosDeMt(PokemonDto pokemon, Func<int, MoveDefinition> buscar)
        {
            var lista = new List<MoveDefinition>();
            foreach (int id in PokeApiMapper.ExtractMachineAndTutorMoveIds(pokemon))
            {
                MoveDefinition m = buscar(id);
                if (m == null || m.implementation == MoveImplementation.NotImplemented || lista.Contains(m)) continue;
                lista.Add(m);
            }
            lista.Sort((a, b) => string.Compare(a.moveName, b.moveName, StringComparison.CurrentCulture));
            return lista.ToArray();
        }

        public static string Descripcion(MoveDto dto)
            => dto == null ? "" : PokeApiMapper.SpanishFlavorText(dto.FlavorTextEntries);

        private static PokemonDto PokemonDeLaEspecie(int especieId)
        {
            if (!PokeApiDownloader.IsCached("pokemon-species", especieId)) return null;
            var especie = PokeApiDownloader.GetDto<PokemonSpeciesDto>("pokemon-species", especieId);
            int pokemonId = PokeApiDownloader.DefaultPokemonId(especie);
            if (!PokeApiDownloader.IsCached("pokemon", pokemonId)) return null;
            return PokeApiDownloader.GetDto<PokemonDto>("pokemon", pokemonId);
        }

        private static bool IgualA(MoveDefinition[] a, MoveDefinition[] b)
        {
            if (a == null || b == null) return a == b;
            if (a.Length != b.Length) return false;
            for (int i = 0; i < a.Length; i++) if (a[i] != b[i]) return false;
            return true;
        }

        private static List<T> Cargar<T>(string carpeta) where T : ScriptableObject
            => AssetDatabase.FindAssets($"t:{typeof(T).Name}", new[] { carpeta })
                .Select(guid => AssetDatabase.LoadAssetAtPath<T>(AssetDatabase.GUIDToAssetPath(guid)))
                .Where(a => a != null)
                .ToList();
    }
}
