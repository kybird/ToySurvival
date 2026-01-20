using System.Collections.Generic;
using Protocol;
using UnityEditor;
using UnityEngine;

public class DebugMenu
{
    [MenuItem("Debug/Test LevelUp")]
    public static void TestLevelUp()
    {
        if (!Application.isPlaying)
        {
            Debug.LogError("Must be in Play Mode to test LevelUpUI");
            return;
        }

        if (LevelUpUI.Instance == null)
        {
            Debug.LogError("LevelUpUI Instance is null!");
            return;
        }

        List<LevelUpOption> options = new List<LevelUpOption>();
        for (int i = 0; i < 3; i++)
        {
            LevelUpOption opt = new LevelUpOption();
            opt.OptionId = i;
            opt.Name = $"Test Option {i}";
            opt.Desc = $"Description for option {i}";
            opt.IsNew = (i == 0);
            options.Add(opt);
        }

        LevelUpUI.Instance.Show(options, 30f, 5.0f);
        Debug.Log("Forced LevelUpUI Show");
    }
}
