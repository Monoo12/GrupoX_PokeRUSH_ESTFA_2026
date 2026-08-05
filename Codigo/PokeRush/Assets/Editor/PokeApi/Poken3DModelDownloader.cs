using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using UnityEditor;
using UnityEngine;

// Baja los modelos 3D de los Pokens y los engancha a su PokenDefinition.
//
// Los modelos vienen del repo Pokemon-3D-api/assets, que los publica en .glb
// nombrados por numero de Pokedex nacional (models/opt/regular/25.glb = Pikachu).
// Eso calza exacto con PokenDefinition.dexNumber, asi que la asignacion es
// automatica y no hace falta emparejar nada a mano.
//
// Los .glb NO se versionan (ver .gitignore): cada integrante corre esto una vez.
// Una especie sin modelo no es un error: PokenView cae al sprite solo.
//
// Requiere el paquete com.unity.cloud.gltfast, que es el que convierte los .glb
// en prefabs importados de Unity.
namespace PokeRush.EditorTools.PokeApi
{
    public static class Poken3DModelDownloader
    {
        public const string BaseUrl =
            "https://raw.githubusercontent.com/Pokemon-3D-api/assets/main/models/opt/regular";

        public const string ModelsFolder = "Assets/Models/Pokens";
        private const string PokensFolder = "Assets/Resources/Definitions/Pokens";

        // Pausa entre descargas reales, por cortesia con el servidor.
        private const int DelayBetweenRequestsMs = 100;

        private static readonly HttpClient Http = CreateClient();

        private static HttpClient CreateClient()
        {
            var client = new HttpClient();
            client.Timeout = TimeSpan.FromSeconds(120); // hay modelos de 10 MB
            client.DefaultRequestHeaders.UserAgent.ParseAdd(
                "PokeRush-Importer/1.0 (proyecto academico)");
            return client;
        }

        public struct ModelReport
        {
            public int Downloaded;   // .glb bajados en esta corrida
            public int Reused;       // ya estaban en disco
            public int Assigned;     // asignados a un battleModel
            public int NotFound;     // el repo no tiene modelo para ese dex (404)
            public bool Cancelled;
            public List<string> Warnings;
        }

        // Baja y asigna los modelos de las especies cuyo dexNumber caiga en el
        // rango. El rango existe para no traerse ~971 modelos (varios GB) de una.
        public static ModelReport DownloadAndAssign(
            int minDex, int maxDex, bool forceDownload,
            Action<string, float> progress, Func<bool> cancelled)
        {
            var report = new ModelReport { Warnings = new List<string>() };

            List<PokenDefinition> species = LoadSpecies(minDex, maxDex);
            if (species.Count == 0)
            {
                report.Warnings.Add(
                    $"No hay ninguna PokenDefinition con dexNumber entre {minDex} y {maxDex} " +
                    $"en {PokensFolder}. ¿Corriste antes el importador de PokeAPI?");
                return report;
            }

            EnsureModelsFolder();

            for (int i = 0; i < species.Count; i++)
            {
                if (cancelled != null && cancelled())
                {
                    report.Cancelled = true;
                    break;
                }

                PokenDefinition poken = species[i];
                progress?.Invoke($"{poken.pokenName} (#{poken.dexNumber})",
                    (float)i / species.Count);

                string assetPath = $"{ModelsFolder}/{poken.dexNumber}.glb";
                string fullPath = Path.GetFullPath(assetPath);
                bool onDisk = File.Exists(fullPath);

                if (onDisk && !forceDownload)
                {
                    report.Reused++;
                }
                else
                {
                    switch (TryDownload(poken.dexNumber, fullPath, out string error))
                    {
                        case DownloadResult.Ok:
                            report.Downloaded++;
                            AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);
                            break;
                        case DownloadResult.NotFound:
                            // Normal: el repo no cubre todas las especies ni todas
                            // las formas. Esa especie se queda con su sprite.
                            report.NotFound++;
                            continue;
                        default:
                            report.Warnings.Add($"#{poken.dexNumber} {poken.pokenName}: {error}");
                            continue;
                    }
                }

                if (AssignModel(poken, assetPath, report.Warnings)) report.Assigned++;
            }

            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            return report;
        }

        // Asigna los .glb que ya estan en Assets/Models/Pokens/ sin bajar nada.
        // Sirve para dos casos: modelos puestos a mano (Spriters Resource,
        // Project Pokemon) y para re-asignar despues de que un cambio de paquetes
        // arregle la importacion (p.ej. agregar Draco), cuando los archivos ya
        // estaban en disco pero no habian producido un GameObject.
        [MenuItem("PokeRush/3D/Asignar modelos ya descargados")]
        public static void AssignExistingModels()
        {
            var assigned = 0;
            var failed = 0;
            var warnings = new List<string>();

            foreach (PokenDefinition poken in LoadSpecies(0, int.MaxValue))
            {
                string assetPath = $"{ModelsFolder}/{poken.dexNumber}.glb";
                if (!File.Exists(Path.GetFullPath(assetPath))) continue;

                // Re-import forzado: si el .glb ya estaba en disco, Unity no lo
                // vuelve a procesar solo porque cambiaron los paquetes. Sin esto,
                // un modelo que fallo por falta de Draco sigue sin dar GameObject.
                AssetDatabase.ImportAsset(assetPath, ImportAssetOptions.ForceUpdate);

                if (AssignModel(poken, assetPath, warnings)) assigned++;
                else failed++;
            }

            AssetDatabase.SaveAssets();

            string message = $"Modelos asignados: {assigned}. Sin importar: {failed}.";
            if (failed > 0)
                message += "\n\nLos que fallan casi siempre son .glb que Unity no logro " +
                           "convertir en GameObject. Revisar la consola.";
            Debug.Log($"[PokeRush] {message}");
            foreach (string warning in warnings.Take(10)) Debug.LogWarning($"[PokeRush] {warning}");
        }

        private enum DownloadResult { Ok, NotFound, Failed }

        private static DownloadResult TryDownload(int dexNumber, string fullPath, out string error)
        {
            error = null;
            try
            {
                string url = $"{BaseUrl}/{dexNumber}.glb";
                using HttpResponseMessage response = Http.GetAsync(url).GetAwaiter().GetResult();

                if (response.StatusCode == HttpStatusCode.NotFound)
                    return DownloadResult.NotFound;

                if (!response.IsSuccessStatusCode)
                {
                    error = $"HTTP {(int)response.StatusCode} bajando {url}";
                    return DownloadResult.Failed;
                }

                byte[] bytes = response.Content.ReadAsByteArrayAsync().GetAwaiter().GetResult();
                File.WriteAllBytes(fullPath, bytes);
                Thread.Sleep(DelayBetweenRequestsMs);
                return DownloadResult.Ok;
            }
            catch (Exception e)
            {
                error = e.Message;
                return DownloadResult.Failed;
            }
        }

        // El .glb importado queda como un GameObject (lo convierte glTFast). Si
        // vuelve null casi siempre es que falta el paquete.
        private static bool AssignModel(PokenDefinition poken, string assetPath, List<string> warnings)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (model == null)
            {
                warnings.Add(
                    $"{assetPath} no se importo como GameObject. " +
                    "Revisar que com.unity.cloud.gltfast este instalado.");
                return false;
            }

            poken.battleModel = model;
            poken.battleIdleClip = FindIdleClip(assetPath);
            EditorUtility.SetDirty(poken);
            return true;
        }

        // Prioridad de nombres para el clip de espera. Los modelos sacados de los
        // juegos originales usan el nombrado de Game Freak
        // (pm0150_00_00_00001_battlewait01_loop), asi que "battlewait" gana; despues
        // van los nombres genericos. Si no matchea ninguno se usa el primer clip,
        // que es mejor que dejar el modelo congelado.
        private static readonly string[] IdleKeywords =
            { "battlewait", "defaultwait", "wait01", "aidle", "defaultidle", "idle", "wait", "loop" };

        private static AnimationClip FindIdleClip(string assetPath)
        {
            AnimationClip[] clips = AssetDatabase.LoadAllAssetsAtPath(assetPath)
                .OfType<AnimationClip>()
                .Where(c => c != null && !c.name.StartsWith("__preview__"))
                .ToArray();

            if (clips.Length == 0) return null;

            foreach (string keyword in IdleKeywords)
            {
                AnimationClip match = clips.FirstOrDefault(
                    c => c.name.ToLowerInvariant().Contains(keyword));
                if (match != null) return match;
            }
            return clips[0];
        }

        private static List<PokenDefinition> LoadSpecies(int minDex, int maxDex)
            => AssetDatabase.FindAssets("t:PokenDefinition", new[] { PokensFolder })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<PokenDefinition>)
                .Where(p => p != null && p.dexNumber >= minDex && p.dexNumber <= maxDex)
                .OrderBy(p => p.dexNumber)
                .ToList();

        private static void EnsureModelsFolder()
        {
            if (!AssetDatabase.IsValidFolder("Assets/Models"))
                AssetDatabase.CreateFolder("Assets", "Models");
            if (!AssetDatabase.IsValidFolder(ModelsFolder))
                AssetDatabase.CreateFolder("Assets/Models", "Pokens");
        }
    }
}
