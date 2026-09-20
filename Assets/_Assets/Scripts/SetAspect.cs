using UnityEngine;

public class SetAspect : MonoBehaviour
{
    private const float TargetAspect = 9f / 20f;
    private Camera targetCamera;

    private void Awake()
    {
        targetCamera = GetComponent<Camera>();
        ApplyAspect();
    }

    private void Update()
    {
        ApplyAspect();
    }

    private void ApplyAspect()
    {
        if (targetCamera == null)
        {
            return;
        }

        var windowAspect = (float)Screen.width / Screen.height;
        var scaleHeight = windowAspect / TargetAspect;

        if (scaleHeight < 1f)
        {
            var rect = targetCamera.rect;
            rect.width = 1f;
            rect.height = scaleHeight;
            rect.x = 0f;
            rect.y = (1f - scaleHeight) * 0.5f;
            targetCamera.rect = rect;
            return;
        }

        var scaleWidth = 1f / scaleHeight;
        var letterboxRect = targetCamera.rect;
        letterboxRect.width = scaleWidth;
        letterboxRect.height = 1f;
        letterboxRect.x = (1f - scaleWidth) * 0.5f;
        letterboxRect.y = 0f;
        targetCamera.rect = letterboxRect;
    }
}
