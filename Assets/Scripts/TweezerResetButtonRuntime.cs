using Oculus.Interaction;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class TweezerResetButtonRuntime
{
    const string ButtonName = "TweezerResetButton";

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Initialize()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (scene.name != "_Task_1" && scene.name != "_Task_2" && scene.name != "_Task_3")
            return;

        if (FindGameObject(scene, ButtonName))
            return;

        GameObject template = FindGameObject(scene, "RedoButton");
        TweezersResettable tweezers = FindComponent<TweezersResettable>(scene);

        if (!template || !tweezers)
        {
            Debug.LogWarning($"Could not create the tweezer reset button in {scene.name}.");
            return;
        }

        bool templateWasActive = template.activeSelf;
        template.SetActive(false);
        GameObject resetButton = Object.Instantiate(template, template.transform.parent);
        template.SetActive(templateWasActive);

        resetButton.name = ButtonName;
        Vector3 position = resetButton.transform.localPosition;
        position.x = 123f;
        resetButton.transform.localPosition = position;

        TMP_Text[] labels = resetButton.GetComponentsInChildren<TMP_Text>(true);
        for (int i = 0; i < labels.Length; i++)
            labels[i].text = "Reset";

        InteractableUnityEventWrapper resetEvents = resetButton.GetComponent<InteractableUnityEventWrapper>();
        if (!resetEvents)
        {
            Object.Destroy(resetButton);
            Debug.LogWarning($"The reset button template in {scene.name} has no event wrapper.");
            return;
        }

        for (int i = 0; i < resetEvents.WhenSelect.GetPersistentEventCount(); i++)
            resetEvents.WhenSelect.SetPersistentListenerState(i, UnityEngine.Events.UnityEventCallState.Off);

        resetEvents.WhenSelect.RemoveAllListeners();
        resetEvents.WhenSelect.AddListener(tweezers.ResetPose);
        resetButton.SetActive(true);

        Debug.Log($"Tweezer reset button added to {scene.name}.");
    }

    static GameObject FindGameObject(Scene scene, string objectName)
    {
        GameObject[] objects = Resources.FindObjectsOfTypeAll<GameObject>();
        for (int i = 0; i < objects.Length; i++)
        {
            GameObject current = objects[i];
            if (current.scene == scene && current.name == objectName)
                return current;
        }

        return null;
    }

    static T FindComponent<T>(Scene scene) where T : Component
    {
        T[] components = Resources.FindObjectsOfTypeAll<T>();
        for (int i = 0; i < components.Length; i++)
        {
            T current = components[i];
            if (current.gameObject.scene == scene)
                return current;
        }

        return null;
    }
}
