using UnityEngine;

public class TweezerResetButtonAction : MonoBehaviour
{
    public void ResetTweezers()
    {
        TweezersResettable[] candidates = Resources.FindObjectsOfTypeAll<TweezersResettable>();
        for (int i = 0; i < candidates.Length; i++)
        {
            TweezersResettable candidate = candidates[i];
            if (candidate.gameObject.scene == gameObject.scene)
            {
                candidate.ResetPose();
                return;
            }
        }

        Debug.LogWarning($"No tweezers were found in {gameObject.scene.name}.", this);
    }
}
