using System;
using UnityEditor;
using UnityEngine;

// Ventana del importador: PokeRush > Importador PokeAPI.
// Flujo: 1) Descargar cache (necesita internet, ~5 min la primera vez),
//        2) Generar assets desde el cache (offline, re-ejecutable).
namespace PokeRush.EditorTools.PokeApi
{
    public class PokeApiImporterWindow : EditorWindow
    {
        private bool _forceDownload;
        private string _lastResult = "";
        private Vector2 _scroll;

        // Rango de Pokedex para los modelos 3D. Por defecto la primera
        // generacion, que es con la que se esta probando el combate.
        private int _minDex = 1;
        private int _maxDex = 151;

        [MenuItem("PokeRush/Importador PokeAPI")]
        public static void ShowWindow()
        {
            var window = GetWindow<PokeApiImporterWindow>("Importador PokeAPI");
            window.minSize = new Vector2(420, 300);
        }

        private void OnGUI()
        {
            GUILayout.Label("Importador de datos de PokeAPI", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "La lista de especies se edita en Tools/PokeApiCache/species_selection.json " +
                "(numeros de Pokedex nacional; las lineas evolutivas se expanden solas).\n\n" +
                "1. Descargar cache: baja los JSON de PokeAPI a Tools/PokeApiCache/ (requiere internet).\n" +
                "2. Generar assets: crea/actualiza los MoveDefinition y PokenDefinition en " +
                "Resources/Definitions/ a partir del cache (no toca sprites ni animators).",
                MessageType.Info);

            EditorGUILayout.Space();
            _forceDownload = EditorGUILayout.ToggleLeft(
                "Forzar re-descarga (ignora el cache existente)", _forceDownload);
            EditorGUILayout.Space();

            if (GUILayout.Button("1. Descargar cache desde PokeAPI", GUILayout.Height(30)))
                RunDownload();

            if (GUILayout.Button("2. Generar assets desde el cache", GUILayout.Height(30)))
                RunGenerate();

            if (GUILayout.Button("Todo (descargar + generar)", GUILayout.Height(30)))
            {
                if (RunDownload()) RunGenerate();
            }

            EditorGUILayout.Space();
            DrawModelSection();

            EditorGUILayout.Space();
            if (!string.IsNullOrEmpty(_lastResult))
            {
                GUILayout.Label("Ultimo resultado:", EditorStyles.boldLabel);
                _scroll = EditorGUILayout.BeginScrollView(_scroll);
                EditorGUILayout.HelpBox(_lastResult, MessageType.None);
                EditorGUILayout.EndScrollView();
            }
        }

        // Los modelos 3D son opcionales y van por separado: pesan mucho (hasta
        // 10 MB por especie), no se versionan, y una especie sin modelo funciona
        // igual porque PokenView cae al sprite.
        private void DrawModelSection()
        {
            GUILayout.Label("Modelos 3D (opcional)", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox(
                "Baja los .glb de Pokemon-3D-api/assets y los asigna al campo " +
                "'Battle Model' de cada especie, emparejando por numero de Pokedex.\n\n" +
                "Los modelos NO se versionan: cada integrante corre esto una vez. " +
                "Las especies sin modelo se siguen dibujando con su sprite.",
                MessageType.Info);

            EditorGUILayout.BeginHorizontal();
            _minDex = EditorGUILayout.IntField("Dex desde", _minDex);
            _maxDex = EditorGUILayout.IntField("hasta", _maxDex);
            EditorGUILayout.EndHorizontal();

            if (GUILayout.Button($"Descargar modelos 3D (#{_minDex}-#{_maxDex})", GUILayout.Height(30)))
                RunDownloadModels();
        }

        private void RunDownloadModels()
        {
            bool cancelled = false;
            try
            {
                var report = Poken3DModelDownloader.DownloadAndAssign(
                    _minDex, _maxDex, _forceDownload,
                    (msg, t) => cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "Descargando modelos 3D", msg, t),
                    () => cancelled);

                string warnings = report.Warnings.Count == 0
                    ? ""
                    : $"\n\nAvisos ({report.Warnings.Count}):\n- " +
                      string.Join("\n- ", report.Warnings.GetRange(0, Math.Min(30, report.Warnings.Count)));

                _lastResult = (report.Cancelled ? "Descarga de modelos CANCELADA.\n" : "") +
                    $"Modelos: {report.Downloaded} bajados, {report.Reused} ya estaban, " +
                    $"{report.Assigned} asignados.\n" +
                    $"Sin modelo en el repo: {report.NotFound} (esas especies usan su sprite)." +
                    warnings;
                Debug.Log($"[PokeApi] {_lastResult}");
            }
            catch (Exception e)
            {
                _lastResult = $"Error bajando modelos: {e.Message}";
                Debug.LogError($"[PokeApi] {e}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private bool RunDownload()
        {
            bool cancelled = false;
            try
            {
                var report = PokeApiDownloader.DownloadAll(
                    _forceDownload,
                    (msg, t) => cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "Descargando de PokeAPI", msg, t),
                    () => cancelled);

                _lastResult = report.Cancelled
                    ? "Descarga cancelada (lo ya bajado queda en el cache)."
                    : $"Descarga completa: {report.SpeciesCount} especies, {report.MoveCount} movimientos, " +
                      $"{report.AbilityCount} habilidades en Tools/PokeApiCache/.";
                Debug.Log($"[PokeApi] {_lastResult}");
                return !report.Cancelled;
            }
            catch (Exception e)
            {
                _lastResult = $"Error en la descarga: {e.Message}";
                Debug.LogError($"[PokeApi] {e}");
                return false;
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }

        private void RunGenerate()
        {
            bool cancelled = false;
            try
            {
                var report = PokeApiAssetGenerator.GenerateAll(
                    (msg, t) => cancelled = EditorUtility.DisplayCancelableProgressBar(
                        "Generando assets", msg, t),
                    () => cancelled);

                string warnings = report.Warnings.Count == 0
                    ? ""
                    : $"\n\nAvisos ({report.Warnings.Count}):\n- " +
                      string.Join("\n- ", report.Warnings.GetRange(0, Math.Min(30, report.Warnings.Count)));

                _lastResult = (report.Cancelled ? "Generacion CANCELADA a mitad de camino.\n" : "") +
                    $"Movimientos: {report.MovesCreated} creados, {report.MovesUpdated} actualizados " +
                    $"({report.MovesNotImplemented} marcados NotImplemented).\n" +
                    $"Pokens: {report.PokensCreated} creados, {report.PokensUpdated} actualizados." +
                    warnings;
                Debug.Log($"[PokeApi] {_lastResult}");
            }
            catch (Exception e)
            {
                _lastResult = $"Error generando assets: {e.Message}";
                Debug.LogError($"[PokeApi] {e}");
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }
        }
    }
}
