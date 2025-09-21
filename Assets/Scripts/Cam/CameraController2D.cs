using UnityEngine;

public class CameraController2D : MonoBehaviour
{
    [Header("References")]
    public Camera cam; // assign Main Camera in Inspector

    [Header("Zoom Settings")]
    public float zoomSpeed = 5f;
    public float minZoom = 3f;
    public float maxZoom = 12f;

    [Header("Pan Settings")]
    [Tooltip("Multiply the world-space drag delta. Try 1.0 as a starting value.")]
    public float panSpeed = 1f;
    [Tooltip("If true, the camera moves with the mouse drag. If false, it moves opposite (map-style drag).")]
    public bool dragFollowsMouse = true;

    [Header("Confiner (required for clamping)")]
    public BoxCollider2D confiner; // assign a GameObject with BoxCollider2D that defines the playable area

    Vector3 lastMouseWorldPos;

    private void Awake()
    {
        if (cam == null) cam = GetComponent<Camera>();
        if (cam == null) cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("CameraController2D: No Camera assigned or found on this GameObject, and no Main Camera in scene. Disabling camera controller.", this);
            enabled = false;
            return;
        }
        if (!cam.orthographic)
            Debug.LogWarning("Camera should be orthographic for 2D.");
    }

    private void Update()
    {
        if (cam == null) return;

        HandleZoom();
        HandlePan();
        ClampToBounds();
    }

    private void HandleZoom()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0f)
        {
            cam.orthographicSize = Mathf.Clamp(
                cam.orthographicSize - scroll * zoomSpeed,
                minZoom,
                maxZoom
            );
        }
    }

    private void HandlePan()
    {
        if (Input.GetMouseButtonDown(0))
        {
            lastMouseWorldPos = GetWorldMousePosition();
        }

        if (Input.GetMouseButton(0))
        {
            Vector3 currentMouseWorld = GetWorldMousePosition();
            Vector3 diff = dragFollowsMouse ? (currentMouseWorld - lastMouseWorldPos)
                                            : (lastMouseWorldPos - currentMouseWorld);
            if (cam != null)
            {
                cam.transform.position += diff * panSpeed;
            }
            lastMouseWorldPos = currentMouseWorld;
        }
    }

    private Vector3 GetWorldMousePosition()
    {
        // Raycast the mouse position onto the Z=0 plane for stable 2D panning
        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.forward, Vector3.zero); // world plane at Z = 0
        if (plane.Raycast(ray, out float enter))
        {
            Vector3 hit = ray.GetPoint(enter);
            return new Vector3(hit.x, hit.y, 0f);
        }
        // Fallback to orthographic conversion (should rarely happen)
        Vector3 mouseScreen = Input.mousePosition;
        mouseScreen.z = -cam.transform.position.z;
        Vector3 world = cam.ScreenToWorldPoint(mouseScreen);
        return new Vector3(world.x, world.y, 0f);
    }

    private void ClampToBounds()
    {
        if (confiner == null) return; // No clamping without a confiner

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;

        Bounds bounds = confiner.bounds;

        float minX = bounds.min.x + halfWidth;
        float maxX = bounds.max.x - halfWidth;
        float minY = bounds.min.y + halfHeight;
        float maxY = bounds.max.y - halfHeight;

        Vector3 pos = cam.transform.position;
        if (minX > maxX) pos.x = bounds.center.x;
        else pos.x = Mathf.Clamp(pos.x, minX, maxX);

        if (minY > maxY) pos.y = bounds.center.y;
        else pos.y = Mathf.Clamp(pos.y, minY, maxY);

        cam.transform.position = new Vector3(pos.x, pos.y, cam.transform.position.z);
    }

    private void OnValidate()
    {
        if (cam == null) cam = Camera.main;
        if (cam == null || confiner == null) return;

        float halfHeight = cam.orthographicSize;
        float halfWidth = halfHeight * cam.aspect;
        Bounds b = confiner.bounds;

        bool tooNarrow = (b.size.x < halfWidth * 2f);
        bool tooShort = (b.size.y < halfHeight * 2f);
        if (tooNarrow || tooShort)
        {
            Debug.LogWarning("CameraController2D: Confiner is smaller than the camera's viewport. Panning will be constrained or appear not to move. Enlarge the BoxCollider2D or zoom out less.", this);
        }
    }
}



