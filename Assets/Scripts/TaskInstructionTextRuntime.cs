using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class TaskInstructionTextRuntime
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Initialize()
    {
        ApplyInstructions(SceneManager.GetActiveScene());
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        ApplyInstructions(scene);
    }

    static void ApplyInstructions(Scene scene)
    {
        if (!TryGetInstruction(scene.name, out string title, out string body))
            return;

        foreach (var text in Object.FindObjectsByType<Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ApplyToText(text, title, body);

        foreach (var text in Object.FindObjectsByType<TMP_Text>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            ApplyToText(text, title, body);
    }

    static bool TryGetInstruction(string sceneName, out string title, out string body)
    {
        switch (sceneName)
        {
            case "_Task_0":
                title = "Task 0";
                body = "Use the tweezers to pick up each wafer and place it in the end location.\n\nComplete <b>3 wafers</b> to finish this task.";
                return true;

            case "_Task_1":
                title = "Task 1";
                body = "Use the tweezers to transfer each wafer to the end location.\n\nComplete <b>3 wafers</b>. If a wafer breaks, wait for the replacement and continue.";
                return true;

            case "_Task_2":
                title = "Task 2";
                body = "Use the tweezers to move each wafer to the end location with controlled handling.\n\nComplete <b>3 wafers</b> to unlock the next task.";
                return true;

            case "_Task_3":
                title = "Task 3";
                body = "Use the spray bottle to clean the dirt from the wafer, then use the tweezers to place the clean wafer in the end location.\n\nOnly a clean wafer is accepted. Complete <b>3 wafers</b>.";
                return true;

            default:
                title = "";
                body = "";
                return false;
        }
    }

    public static bool TryGetBodyForActiveScene(out string body)
    {
        if (TryGetInstruction(SceneManager.GetActiveScene().name, out _, out body))
            return true;

        body = "";
        return false;
    }

    static void ApplyToText(Text text, string title, string body)
    {
        if (text == null)
            return;

        if (IsInitialTitle(text.text))
            text.text = FormatTitle(title);
        else if (IsInitialBody(text.text, text.gameObject.name))
            text.text = body;
    }

    static void ApplyToText(TMP_Text text, string title, string body)
    {
        if (text == null)
            return;

        if (IsInitialTitle(text.text))
            text.text = FormatTitle(title);
        else if (IsInitialBody(text.text, text.gameObject.name))
            text.text = body;
    }

    static bool IsInitialTitle(string value)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return value.Contains("<b>Task") && !value.Contains("Complete</b>");
    }

    static bool IsInitialBody(string value, string objectName)
    {
        if (string.IsNullOrEmpty(value))
            return false;

        return objectName == "Information" &&
               (value.Contains("Use the Tweezers") ||
                value.Contains("Use the tweezers") ||
                value.Contains("wafer"));
    }

    static string FormatTitle(string title)
    {
        return $"-----------------------------------------------------------\n\n<b>{title}</b>\n\n-----------------------------------------------------------";
    }
}
