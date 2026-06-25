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
        _connection = new SQLiteConnection(dbPath);
        CreateTables();
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
    }

    public void Close()
    {
        _connection?.Close();
    }
}