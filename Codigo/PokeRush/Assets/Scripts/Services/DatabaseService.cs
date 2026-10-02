using SQLite;
using SQLiteConnection = SQLite.SQLiteConnection;
using System.IO;
using UnityEngine;

public class DatabaseService
{
    private static DatabaseService _instance;
    public static DatabaseService Instance => _instance ??= new DatabaseService();

    private SQLiteConnection _connection;

    private DatabaseService()
    {
        string dbPath = Path.Combine(Application.persistentDataPath, "pokerush.db");
        TraerDeLaCarpetaVieja(dbPath);
        _connection = new SQLiteConnection(dbPath);
        CreateTables();
    }

    // Hasta el 2-oct-2026 la empresa del proyecto era "DefaultCompany", y la
    // partida vivia en AppData/LocalLow/DefaultCompany/PokeRush. Con el nombre
    // de release ("LevelUP Creators") la carpeta cambio: la primera vez se trae
    // la base vieja, asi nadie pierde su partida. Si ya hay una nueva, no se toca.
    private static void TraerDeLaCarpetaVieja(string dbPath)
    {
        try
        {
            if (File.Exists(dbPath)) return;
            // persistentDataPath = .../LocalLow/<empresa>/<producto>
            DirectoryInfo empresa = Directory.GetParent(Application.persistentDataPath);
            DirectoryInfo localLow = empresa != null ? empresa.Parent : null;
            if (localLow == null) return;
            string vieja = Path.Combine(localLow.FullName, "DefaultCompany", Application.productName, "pokerush.db");
            if (!File.Exists(vieja) || Path.GetFullPath(vieja) == Path.GetFullPath(dbPath)) return;

            Directory.CreateDirectory(Path.GetDirectoryName(dbPath));
            File.Copy(vieja, dbPath);
            Debug.Log($"[BD] Partida traida de la carpeta anterior: {vieja}");
        }
        catch (System.Exception e)
        {
            Debug.LogWarning($"[BD] No se pudo traer la partida de la carpeta anterior: {e.Message}");
        }
    }

    public SQLiteConnection Connection => _connection;

    private void CreateTables()
    {
        _connection.CreateTable<PlayerRow>();
        _connection.CreateTable<PlayerPokenRow>();
        _connection.CreateTable<PlayerPokenMoveRow>();
        _connection.CreateTable<PlayerInventoryRow>();
        _connection.CreateTable<PlayerCosmeticRow>();
        _connection.CreateTable<ActiveFlagRow>();
        _connection.CreateTable<DialogueRow>();
        // Opciones del juego (volumen, controles). Ver SettingsService.
        _connection.CreateTable<SettingRow>();
    }

    public void Close()
    {
        _connection?.Close();
    }
}