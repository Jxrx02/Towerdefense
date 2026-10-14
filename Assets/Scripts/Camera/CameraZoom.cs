using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Smooth 2D-Kamera: folgt dem Helden, lässt sich per Drag/Scroll/Pinch frei bewegen
/// und gleitet nach einer Weile ohne Eingabe wieder zum Helden zurück.
/// Position wird jedes Frame hart gegen die Bounds geclamped -> kein Überschreiten mehr.
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraZoom : MonoBehaviour
{
    [Header("Referenzen")]
    [SerializeField] CameraBounds2D bounds;
    [SerializeField] Transform hero;

    [Header("Follow")]
    [Tooltip("Sekunden ohne Eingabe, bis die Kamera zum Helden zurückkehrt")]
    [SerializeField] float followDelay = 3f;
    [Tooltip("Größer = langsamer/weicher beim Folgen und Zurückgleiten")]
    [SerializeField] float followSmoothTime = 0.35f;
    [SerializeField] Vector2 followOffset;

    [Header("Pan")]
    [Tooltip("Kleine Glättung beim Ziehen")]
    [SerializeField] float panSmoothTime = 0.08f;
    [Tooltip("Ab dieser Mausbewegung (Pixel) zählt es als Drag und nicht als Klick")]
    [SerializeField] float dragThresholdPixels = 6f;

    [Header("Zoom (orthographicSize: kleiner = näher dran)")]
    [SerializeField] float minOrthoSize = 2f;
    [SerializeField] float maxOrthoSize = 10f;
    [SerializeField] float scrollZoomSpeed = 3f;
    [SerializeField] float pinchZoomSpeed = 0.005f;
    [SerializeField] float zoomSmoothTime = 0.12f;

    Camera cam;

    Vector3 desiredPos;
    Vector3 velocity;
    float targetOrtho;
    float orthoVelocity;

    bool following = true;
    float idleTimer;

    bool dragging;
    Vector3 lastMouse;
    float dragDistance;

    void Awake()
    {
        cam = GetComponent<Camera>();

        // Die PixelPerfectCamera überschreibt orthographicSize jedes Frame (LateUpdate),
        // rundet auf ganze PPU-Stufen und snappt -> Ruckeln. Für smoothes Zoomen deaktivieren.
        var ppc = GetComponent<PixelPerfectCamera>();
        if (ppc != null)
        {
            ppc.enabled = false;
            Debug.Log("CameraZoom: PixelPerfectCamera wurde deaktiviert, da sie smoothes Zoomen/Bewegen verhindert.");
        }

        if (hero == null)
        {
            var go = GameObject.FindWithTag("Player");
            if (go != null) hero = go.transform;
        }

        targetOrtho = Mathf.Clamp(cam.orthographicSize, minOrthoSize, maxOrthoSize);
        cam.orthographicSize = targetOrtho;

        // Startposition direkt auf den Helden setzen (kein Hinfliegen beim Start)
        Vector3 start = transform.position;
        if (hero != null) start = HeroTarget();
        desiredPos = Clamp(start, cam.orthographicSize);
        transform.position = desiredPos;
    }

    void Update()
    {
        HandleInput();
    }

    void LateUpdate()
    {
        // ---------- Zoom ----------
        float maxAllowed = maxOrthoSize;
        if (bounds != null)
            maxAllowed = Mathf.Min(maxAllowed, bounds.MaxOrthoSize(cam.aspect));
        targetOrtho = Mathf.Clamp(targetOrtho, minOrthoSize, Mathf.Max(minOrthoSize, maxAllowed));

        cam.orthographicSize = Mathf.SmoothDamp(
            cam.orthographicSize, targetOrtho, ref orthoVelocity, zoomSmoothTime);

        // ---------- Position ----------
        if (following && hero != null)
            desiredPos = HeroTarget();

        desiredPos = Clamp(desiredPos, cam.orthographicSize);

        float smooth = following ? followSmoothTime : panSmoothTime;
        Vector3 pos = Vector3.SmoothDamp(transform.position, desiredPos, ref velocity, smooth);

        // Harte Begrenzung mit der AKTUELLEN Zoomstufe -> kann nie über die Bounds hinaus
        transform.position = Clamp(pos, cam.orthographicSize);
    }

    // =====================================================================
    //  Input
    // =====================================================================
    void HandleInput()
    {
        bool interacted = false;

        // --- Mausrad ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f && !IsOverUI())
        {
            targetOrtho *= Mathf.Exp(-scroll * scrollZoomSpeed);
            interacted = true;
        }

        if (Input.touchCount >= 2)
        {
            // --- Pinch-Zoom ---
            dragging = false;

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            float prev = ((t0.position - t0.deltaPosition) - (t1.position - t1.deltaPosition)).magnitude;
            float curr = (t0.position - t1.position).magnitude;

            targetOrtho *= Mathf.Exp(-(curr - prev) * pinchZoomSpeed);
            interacted = true;
        }
        else
        {
            // --- Pan (Maus / ein Finger) ---
            if (Input.GetMouseButtonDown(0))
            {
                dragging = !IsOverUI();
                lastMouse = Input.mousePosition;
                dragDistance = 0f;
            }
            else if (Input.GetMouseButton(0) && dragging)
            {
                Vector3 delta = Input.mousePosition - lastMouse;
                lastMouse = Input.mousePosition;
                dragDistance += delta.magnitude;

                if (dragDistance > dragThresholdPixels)
                {
                    if (following)
                    {
                        // Aus dem Follow-Modus wechseln: ab jetzt von der echten Kameraposition aus ziehen
                        following = false;
                        desiredPos = transform.position;
                    }

                    // Pixel -> Welteinheiten. Unabhängig von der (geglätteten) Kameraposition,
                    // dadurch keine Rückkopplung/Jitter bei langsamer Bewegung.
                    float worldPerPixel = 2f * cam.orthographicSize / Screen.height;
                    desiredPos -= new Vector3(delta.x * worldPerPixel, delta.y * worldPerPixel, 0f);
                    desiredPos = Clamp(desiredPos, cam.orthographicSize);
                }
            }

            if (Input.GetMouseButtonUp(0))
                dragging = false;

            // Finger/Maus gedrückt halten (auch wenn still) zählt als Interaktion
            if (dragging && Input.GetMouseButton(0) && dragDistance > dragThresholdPixels)
                interacted = true;
        }

        // --- Idle-Timer: nach X Sekunden ohne Eingabe zurück zum Helden ---
        if (interacted)
        {
            idleTimer = 0f;
        }
        else if (!following)
        {
            idleTimer += Time.unscaledDeltaTime;
            if (idleTimer >= followDelay && hero != null)
                following = true;
        }
    }

    // =====================================================================
    //  Helpers
    // =====================================================================
    Vector3 HeroTarget()
    {
        return new Vector3(
            hero.position.x + followOffset.x,
            hero.position.y + followOffset.y,
            transform.position.z);
    }

    Vector3 Clamp(Vector3 pos, float orthoSize)
    {
        if (bounds == null) return pos;
        return bounds.Clamp(pos, orthoSize, cam.aspect);
    }

    static bool IsOverUI()
    {
        if (EventSystem.current == null) return false;

        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);

        return EventSystem.current.IsPointerOverGameObject();
    }
}