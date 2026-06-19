using System.Collections.Generic;

public class StoryModel
{
    public HashSet<StoryFlag> ActiveFlags { get; private set; }

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
        ActiveFlags.Add(flag);
    }
}