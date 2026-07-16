// Valores individuales (IVs) de un Poken: 0-31 por estadistica, como en la saga.
// Se generan al crear el Poken y persisten en el save (serializados como texto).
[System.Serializable]
public struct IvSet
{
    public const int MaxIv = 31;

    public int hp;
    public int attack;
    public int defense;
    public int specialAttack;
    public int specialDefense;
    public int speed;

    public static IvSet GenerateRandom(System.Random random)
    {
        return new IvSet
        {
            hp = random.Next(0, MaxIv + 1),
            attack = random.Next(0, MaxIv + 1),
            defense = random.Next(0, MaxIv + 1),
            specialAttack = random.Next(0, MaxIv + 1),
            specialDefense = random.Next(0, MaxIv + 1),
            speed = random.Next(0, MaxIv + 1)
        };
    }

    // Formato del save: "hp,atk,def,spa,spd,spe" (ej: "31,12,0,5,20,31")
    public string Serialize()
        => $"{hp},{attack},{defense},{specialAttack},{specialDefense},{speed}";

    // Devuelve false si el texto es null/invalido (ej: save viejo sin columna Ivs).
    public static bool TryParse(string text, out IvSet ivs)
    {
        ivs = default;
        if (string.IsNullOrEmpty(text)) return false;

        string[] parts = text.Split(',');
        if (parts.Length != 6) return false;

        int[] values = new int[6];
        for (int i = 0; i < 6; i++)
        {
            if (!int.TryParse(parts[i], out values[i])) return false;
            if (values[i] < 0) values[i] = 0;
            if (values[i] > MaxIv) values[i] = MaxIv;
        }

        ivs = new IvSet
        {
            hp = values[0],
            attack = values[1],
            defense = values[2],
            specialAttack = values[3],
            specialDefense = values[4],
            speed = values[5]
        };
        return true;
    }
}
