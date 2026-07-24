using System.Collections;
using UnityEngine;

/// <summary>
/// Controls a fullscreen CanvasGroup for fade transitions between levels.
/// Lives as a child of the persistent GameManager object so it survives scene loads
/// the same way GameManager does — nothing extra needed here for that.
///
/// GameManager calls FadeOut()/FadeIn() directly and waits for them to finish before
/// proceeding, since scene loading needs a strict order (fade to white, THEN load,
/// THEN fade out) rather than a fire-and-forget event.
/// </summary>
public class SceneFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup; // on a fullscreen white Image
    [SerializeField] private float fadeDuration = 0.4f;

    private void Awake()
    {
        canvasGroup.alpha = 0f;
        canvasGroup.blocksRaycasts = false;
    }

    /// <summary>Fades the screen TO white, covering whatever's behind it.</summary>
    public IEnumerator FadeOut()
    {
        canvasGroup.blocksRaycasts = true;
        yield return Fade(canvasGroup.alpha, 1f);
    }

    /// <summary>Fades FROM white, revealing the new scene.</summary>
    public IEnumerator FadeIn()
    {
        yield return Fade(canvasGroup.alpha, 0f);
        canvasGroup.blocksRaycasts = false;
    }

    private IEnumerator Fade(float from, float to)
    {
        float t = 0f;
        canvasGroup.alpha = from;

        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Lerp(from, to, t / fadeDuration);
            yield return null;
        }

        canvasGroup.alpha = to;
    }
}