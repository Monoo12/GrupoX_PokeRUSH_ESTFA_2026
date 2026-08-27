using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using SQLite;
using UnityEngine;
using SQLiteConnection = SQLite.SQLiteConnection;

// Fila de la tabla 'settings': un par clave/valor por opcion.
//
// Va en la MISMA base que el resto del save (pokerush.db) a proposito. La
// alternativa era PlayerPrefs, que hubiera sido un segundo sistema de
// persistencia en paralelo al que ya existe. Es una tabla aparte de 'player'
// porque volumen y controles son de la maquina, no de la partida: borrar el
// save no deberia devolverte los controles a los de fabrica.
[Table("settings")]
public class SettingRow
{
    [PrimaryKey]
    public string Key { get; set; }
    public string Value { get; set; }
}

// Lectura/escritura de las opciones del juego. Escribe al toque (no hay un
// "aplicar" que se pueda perder) y cachea en memoria para que leer una opcion
// desde un Update no vaya a la base.
public class SettingsService
{
    private static SettingsService _instance;
    public static SettingsService Instance => _instance ??= new SettingsService();

    private readonly Dictionary<string, string> _cache = new Dictionary<string, string>();
    private bool _loaded;

    private SQLiteConnection _db => DatabaseService.Instance.Connection;

    private SettingsService() { }

    private void EnsureLoaded()
    {
        if (_loaded) return;
        _loaded = true;

        try
        {
            // Idempotente: si DatabaseService ya la creo, no hace nada. Esta aca
            // ademas para que un proyecto con la base vieja no explote.
            _db.CreateTable<SettingRow>();
            foreach (SettingRow row in _db.Table<SettingRow>().ToList())
                if (row.Key != null) _cache[row.Key] = row.Value;
        }
        catch (System.Exception e)
        {
            // Sin base seguimos andando con los valores por defecto en memoria:
            // que no se pueda guardar el volumen no puede impedir jugar.
            Debug.LogWarning($"[Settings] No se pudo leer la tabla de opciones: {e.Message}");
        }
    }

    public string GetString(string key, string fallback = null)
    {
        EnsureLoaded();
        return _cache.TryGetValue(key, out string value) && value != null ? value : fallback;
    }

    public void SetString(string key, string value)
    {
        EnsureLoaded();
        _cache[key] = value;
        try
        {
            _db.InsertOrReplace(new SettingRow { Key = key, Value = value });
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[Settings] No se pudo guardar '{key}': {e.Message}");
        }
    }

    public void Delete(string key)
    {
        EnsureLoaded();
        _cache.Remove(key);
        try
        {
            _db.Delete<SettingRow>(key);
        }
        catch (System.Exception)
        {
            // Borrar algo que no estaba no es un problema.
        }
    }

    // InvariantCulture a proposito: guardado en una maquina con coma decimal y
    // leido en otra con punto, un float con ToString() local se lee mal.
    public float GetFloat(string key, float fallback)
    {
        string texto = GetString(key, null);
        if (texto == null) return fallback;
        return float.TryParse(texto, NumberStyles.Float, CultureInfo.InvariantCulture, out float v)
            ? v : fallback;
    }

    public void SetFloat(string key, float value)
        => SetString(key, value.ToString(CultureInfo.InvariantCulture));

    public bool GetBool(string key, bool fallback)
    {
        string texto = GetString(key, null);
        if (texto == null) return fallback;
        return texto == "1" || texto.Equals("true", System.StringComparison.OrdinalIgnoreCase);
    }

    public void SetBool(string key, bool value) => SetString(key, value ? "1" : "0");
}
