using Oculus.Interaction;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;

[InitializeOnLoad]
public static class InstallTweezerResetButton
{
    const string TablePrefabPath = "Assets/Prefabs/Table.prefab";
    const string ButtonName = "TweezerResetButton";

    static InstallTweezerResetButton()
    {
        EditorApplication.delayCall += Install;
    }

    static void Install()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
        {
            EditorApplication.delayCall += Install;
            return;
        }

        GameObject root = PrefabUtility.LoadPrefabContents(TablePrefabPath);
        try
        {
            if (FindChild(root.transform, ButtonName))
            {
                Debug.Log("Persistent tweezer reset button is already installed.");
                return;
            }

            Transform template = FindChild(root.transform, "RedoButton");
            if (!template)
            {
                Debug.LogError("Could not find RedoButton in the Table prefab.");
                return;
            }

            GameObject resetButton = Object.Instantiate(template.gameObject, template.parent);
            resetButton.name = ButtonName;

            Vector3 position = resetButton.transform.localPosition;
            position.x = 123f;
            resetButton.transform.localPosition = position;

            TMP_Text[] labels = resetButton.GetComponentsInChildren<TMP_Text>(true);
            for (int i = 0; i < labels.Length; i++)
                labels[i].text = "Reset";

            InteractableUnityEventWrapper events = resetButton.GetComponent<InteractableUnityEventWrapper>();
            while (events.WhenSelect.GetPersistentEventCount() > 0)
                UnityEventTools.RemovePersistentListener(events.WhenSelect, 0);

            TweezerResetButtonAction action = resetButton.AddComponent<TweezerResetButtonAction>();
            UnityEventTools.AddPersistentListener(events.WhenSelect, action.ResetTweezers);

            PrefabUtility.SaveAsPrefabAsset(root, TablePrefabPath);
            Debug.Log("Installed persistent TweezerResetButton in Assets/Prefabs/Table.prefab.");
        }
        finally
        {
            PrefabUtility.UnloadPrefabContents(root);
        }
    }

    static Transform FindChild(Transform parent, string childName)
    {
        Transform[] children = parent.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < children.Length; i++)
        {
            if (children[i].name == childName)
                return children[i];
        }

        return null;
    }
}
