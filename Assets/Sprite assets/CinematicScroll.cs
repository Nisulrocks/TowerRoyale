using UnityEngine;
using UnityEngine.UI;

public class CinematicUIScroll : MonoBehaviour
{
    [Header("Scroll Speed")]
    [SerializeField] private float scrollSpeedX = 0.1f;
    [SerializeField] private float scrollSpeedY = 0.0f;

    private RawImage rawImage;
    private Rect uvRect;

    void Start()
    {
        rawImage = GetComponent<RawImage>();

        if (rawImage != null)
        {
            // Cache the starting UV rect coordinates
            uvRect = rawImage.uvRect;
        }
        else
        {
            Debug.LogError("This script requires a RawImage component to function!");
        }
    }

    void Update()
    {
        if (rawImage == null) return;

        // Shift the X and Y positions of the UV rect over time
        uvRect.x += scrollSpeedX * Time.deltaTime;
        uvRect.y += scrollSpeedY * Time.deltaTime;

        // Apply the updated coordinates back to the Raw Image
        rawImage.uvRect = uvRect;
    }
}