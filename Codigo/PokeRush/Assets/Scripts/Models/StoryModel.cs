using System.Collections.Generic;

public class StoryModel
{
    public HashSet<StoryFlag> ActiveFlags { get; private set; }

    // Se dispara cuando un flag pasa de apagado a encendido (una sola vez por
    // flag). Lo usan las cosas del mapa que tienen que reaccionar en el momento,
    // como las barreras que se apagan al tocar un boton, sin tener que
    // preguntar por el flag en cada frame.
    public event System.Action<StoryFlag> FlagActivated;

    public StoryModel()
    {
        ActiveFlags = new HashSet<StoryFlag>();
    }

    public bool HasFlag(StoryFlag flag)
    {
        return ActiveFlags.Contains(flag);
    }

    public void ActivateFlag(StoryFlag flag)
    {
        if (flag == StoryFlag.None) return;

        // Add devuelve false si ya estaba: asi el evento no se repite.
        if (!ActiveFlags.Add(flag)) return;

        FlagActivated?.Invoke(flag);
    }
}
