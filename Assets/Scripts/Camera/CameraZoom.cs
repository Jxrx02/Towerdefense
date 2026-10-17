using TowerDefense;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Smooth 2D-Kamera mit Follow, Dead Zone, Look-Ahead, Tempo-Zoom, Pan, Zoom zum Cursor/Pinch-Mitte,
/// Rubber-Band an den Bounds, Fokus-Events, Camera Shake und optionalem Explore-Effekt (Volume).
///
/// Aufruf von außen (z. B. bei Treffern/Explosionen):
///   CameraZoom.Instance.ShakeCamera(0.3f, 0.25f);
///   CameraZoom.Instance.FocusOn(chestGameObject, 2f);
/// </summary>
[RequireComponent(typeof(Camera))]
public class CameraZoom : MonoBehaviour
{
    public static CameraZoom Instance { get; private set; }

    enum Mode { Follow, Free, Focus }

    [Header("Referenzen")]
    [SerializeField] CameraBounds2D bounds;
    private Transform hero;

    [Header("Follow")]
    [Tooltip("Sekunden ohne Eingabe, bis die Kamera zum Helden zurückkehrt")]
    [SerializeField] float followDelay = 3f;
    [Tooltip("Normale Glättung beim Folgen (größer = träger)")]
    [SerializeField] float followSmoothTime = 0.3f;
    [Tooltip("Anfangs-Glättung beim Zurückkehren (länger = sanfterer Start)")]
    [SerializeField] float returnSmoothTime = 0.9f;
    [Tooltip("Sekunden, in denen von returnSmoothTime auf followSmoothTime abgeblendet wird")]
    [SerializeField] float returnBlendDuration = 1.5f;
    [SerializeField] Vector2 followOffset;

    [Header("Dead Zone (Anteil der halben Bildgröße)")]
    [SerializeField] Vector2 deadZone = new Vector2(0.12f, 0.08f);

    [Header("Look-Ahead")]
    [Tooltip("Maximale Vorausschau in Welteinheiten")]
    [SerializeField] float lookAheadDistance = 1.5f;
    [Tooltip("Heldengeschwindigkeit (Einheiten/s), ab der die volle Vorausschau erreicht wird")]
    [SerializeField] float lookAheadFullSpeed = 5f;
    [SerializeField] float lookAheadSmoothTime = 0.6f;

    [Header("Pan")]
    [SerializeField] float panSmoothTime = 0.08f;
    [Tooltip("Ab dieser Mausbewegung (Pixel) zählt es als Drag und nicht als Klick")]
    [SerializeField] float dragThresholdPixels = 6f;

    [Header("Rubber-Band an den Bounds")]
    [Tooltip("Maximale Überdehnung als Anteil der orthographicSize")]
    [SerializeField] float rubberRange = 0.3f;
    [SerializeField] float rubberDragSmooth = 0.05f;
    [SerializeField] float rubberReleaseSmooth = 0.2f;

    [Header("Zoom (orthographicSize: kleiner = näher dran)")]
    [SerializeField] float minOrthoSize = 2f;
    [SerializeField] float maxOrthoSize = 10f;
    [SerializeField] float scrollZoomSpeed = 3f;
    [SerializeField] float pinchZoomSpeed = 0.005f;
    [SerializeField] float zoomSmoothTime = 0.12f;
    [Tooltip("An: zoomt im freien Modus zum Cursor/zur Pinch-Mitte. Im Follow-Modus wird immer zur Bildmitte gezoomt. Zoomen wechselt nie den Modus.")]
    [SerializeField] bool zoomToPointer = true;

    [Header("Tempo-Zoom (nur im Follow-Modus)")]
    [Tooltip("1 = aus. Z. B. 1.15 = beim Rennen 15% weiter herauszoomen")]
    [SerializeField] float runZoomOutFactor = 1.15f;
    [SerializeField] float runZoomFullSpeed = 6f;
    [SerializeField] float speedZoomSmoothTime = 1f;

    [Header("Fokus-Events")]
    [SerializeField] float focusSmoothTime = 0.6f;
    [Tooltip("Spieler-Eingaben werden während eines Fokus ignoriert")]
    [SerializeField] bool focusBlocksInput = true;
    [Tooltip("Spätestens nach dieser Zeit gilt das Ziel als erreicht und die Haltezeit beginnt")]
    [SerializeField] float focusMaxTravelTime = 2.5f;

    [Header("Shake")]
    [SerializeField] float shakeFrequency = 25f;

    [Header("Explore-Effekt (optional)")]
    [Tooltip("Globales Volume (Weight 0) mit Vignette/Color Adjustments. Wird im freien Modus eingeblendet.")]
    [SerializeField] Volume exploreVolume;
    [SerializeField] float exploreFadeTime = 0.35f;

    // ---------- State ----------
    Camera cam;
    float zPos;
    Mode mode = Mode.Follow;

    // Position
    Vector3 basePos;          // geglättete, geclampte Position (ohne Rubber/Shake)
    Vector3 desiredPos;
    Vector3 velocity;
    Vector3 anchor;           // logisches Follow-Ziel (Dead Zone)
    float idleTimer;
    float returnBlend = 1f;

    // Hero-Bewegung
    Vector2 lastHeroPos, heroVel, heroVelRef, lookAhead, lookAheadRef;
    bool heroPosInitialized;

    // Zoom
    float userOrtho;
    float orthoVel;
    float speedFactor = 1f, speedFactorVel;
    bool zoomAnchorValid;
    Vector2 zoomAnchorScreen;

    // Drag / Rubber
    bool dragging, dragPending;
    Vector3 lastMouse, dragRaw;
    float dragDistance;
    Vector3 rubber, rubberTarget, rubberVel;

    // Focus
    Transform focusTarget;
    float focusHoldLeft, focusElapsed;
    bool focusArrived;
    bool focusZoomActive;
    float savedUserOrtho;

    // Shake
    float shakeLeft, shakeTotal, shakeStrength, shakeSeed;

    // Explore-Effekt
    float exploreWeight, exploreVel;

    // =====================================================================
    //  Lifecycle
    // =====================================================================
    void Awake()
    {
        Instance = this;
        cam = GetComponent<Camera>();
        zPos = transform.position.z;

        // Die PixelPerfectCamera überschreibt orthographicSize jedes Frame und rundet auf PPU-Stufen.
        var ppc = GetComponent<PixelPerfectCamera>();
        if (ppc != null)
        {
            ppc.enabled = false;
            Debug.Log("CameraZoom: PixelPerfectCamera wurde deaktiviert, da sie smoothes Zoomen/Bewegen verhindert.");
        }

        if (hero == null)
        {
            var go = GameObject.FindAnyObjectByType<Hero>();
            if (go != null) hero = go.transform;
        }

        userOrtho = Mathf.Clamp(cam.orthographicSize, minOrthoSize, maxOrthoSize);
        cam.orthographicSize = userOrtho;

        Vector3 start = hero != null ? HeroPoint() : new Vector3(transform.position.x, transform.position.y, zPos);
        basePos = Clamp(start, cam.orthographicSize);
        desiredPos = basePos;
        anchor = start;
        transform.position = basePos;

        mode = hero != null ? Mode.Follow : Mode.Free;

        if (exploreVolume != null) exploreVolume.weight = 0f;
    }

    void OnDestroy()
    {
        if (Instance == this) Instance = null;
    }

    void Update()
    {
        HandleInput();
    }

    void LateUpdate()
    {
        float dt = Time.deltaTime;
        if (dt <= 0f) return;

        UpdateHeroMotion(dt);
        UpdateZoom(dt);
        UpdatePosition(dt);

        rubber = Vector3.SmoothDamp(rubber, rubberTarget, ref rubberVel,
            dragging ? rubberDragSmooth : rubberReleaseSmooth, Mathf.Infinity, dt);

        Vector3 shake = UpdateShake(dt);
        UpdateExploreEffect(dt);

        // Shake wird gegen die Bounds geclamped (mit aktueller Zoomstufe), sodass er nie darüber hinausgeht.
        // Nur das bewusste Rubber-Band beim Ziehen wird danach addiert.
        Vector3 shaken = Clamp(basePos + shake, cam.orthographicSize);

        transform.position = new Vector3(
            shaken.x + rubber.x,
            shaken.y + rubber.y,
            zPos);
    }

    // =====================================================================
    //  Öffentliche API
    // =====================================================================

    /// <summary>Kamera wackeln lassen. strength in Welteinheiten (typisch 0.1 – 0.5).</summary>
    public void ShakeCamera(float shakeDuration, float strength)
    {
        if (shakeDuration <= 0f || strength <= 0f) return;

        // Ein schwächerer Shake überschreibt keinen stärkeren, der noch läuft
        if (strength >= CurrentShakeAmplitude())
        {
            shakeTotal = shakeDuration;
            shakeLeft = shakeDuration;
            shakeStrength = strength;
            shakeSeed = Random.value * 100f;
        }
    }

    /// <summary>
    /// Kamera gleitet zum Ziel, bleibt focusDuration Sekunden dort (Zählung beginnt bei Ankunft)
    /// und kehrt dann sanft zum Helden zurück. Optional mit eigener Zoomstufe (orthographicSize).
    /// </summary>
    public void FocusOn(GameObject target, float focusDuration, float zoomOrthoSize = -1f)
    {
        if (target == null) return;

        if (mode == Mode.Focus) EndFocusZoom();

        focusTarget = target.transform;
        focusHoldLeft = Mathf.Max(0f, focusDuration);
        focusElapsed = 0f;
        focusArrived = false;

        dragging = false;
        dragPending = false;
        rubberTarget = Vector3.zero;
        zoomAnchorValid = false;

        if (zoomOrthoSize > 0f)
        {
            if (!focusZoomActive)
            {
                savedUserOrtho = userOrtho;
                focusZoomActive = true;
            }
            userOrtho = zoomOrthoSize;
        }

        mode = Mode.Focus;
    }

    /// <summary>Sofort (weich) zurück zum Helden, z. B. für einen UI-Button.</summary>
    public void ReturnToHero()
    {
        if (hero == null) return;
        if (mode == Mode.Focus) EndFocus();
        else EnterFollow();
    }

    // =====================================================================
    //  Input
    // =====================================================================
    void HandleInput()
    {
        if (mode == Mode.Focus && focusBlocksInput)
        {
            dragging = false;
            dragPending = false;
            return;
        }

        bool interacted = false;

        // --- Mausrad ---
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (Mathf.Abs(scroll) > 0.0001f && !IsOverUI())
        {
            userOrtho *= Mathf.Exp(-scroll * scrollZoomSpeed);
            BeginPointerZoom(Input.mousePosition);
            interacted = true;
        }

        if (Input.touchCount >= 2)
        {
            // --- Pinch-Zoom ---
            dragging = false;
            dragPending = false;
            rubberTarget = Vector3.zero;

            Touch t0 = Input.GetTouch(0);
            Touch t1 = Input.GetTouch(1);

            float prev = ((t0.position - t0.deltaPosition) - (t1.position - t1.deltaPosition)).magnitude;
            float curr = (t0.position - t1.position).magnitude;

            userOrtho *= Mathf.Exp(-(curr - prev) * pinchZoomSpeed);
            BeginPointerZoom((t0.position + t1.position) * 0.5f);
            interacted = true;
        }
        else
        {
            // --- Pan (Maus / ein Finger) ---
            if (Input.GetMouseButtonDown(0))
            {
                dragging = !IsOverUI();
                dragPending = dragging;
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
                    if (dragPending)
                    {
                        // Erster Drag-Frame: in den freien Modus wechseln (bricht auch einen Rücksprung ab)
                        dragPending = false;
                        EnterFree();
                        dragRaw = desiredPos;
                    }

                    // Pixel -> Welteinheiten, unabhängig von der geglätteten Kameraposition (kein Jitter)
                    float worldPerPixel = 2f * cam.orthographicSize / Screen.height;
                    dragRaw -= new Vector3(delta.x * worldPerPixel, delta.y * worldPerPixel, 0f);

                    desiredPos = Clamp(dragRaw, cam.orthographicSize);

                    // Überdehnung über die Bounds hinaus mit Widerstand (Rubber-Band)
                    Vector3 over = dragRaw - desiredPos;
                    float d = Mathf.Max(0.01f, cam.orthographicSize * rubberRange);
                    rubberTarget = new Vector3(Resist(over.x, d), Resist(over.y, d), 0f);
                }
            }

            if (Input.GetMouseButtonUp(0))
            {
                dragging = false;
                dragPending = false;
                rubberTarget = Vector3.zero;   // zurückfedern
            }

            if (dragging && Input.GetMouseButton(0))
                interacted = true;
        }

        // --- Idle-Timer: nach X Sekunden ohne Eingabe zurück zum Helden ---
        if (mode == Mode.Free)
        {
            if (interacted)
            {
                idleTimer = 0f;
            }
            else
            {
                idleTimer += Time.unscaledDeltaTime;
                if (idleTimer >= followDelay && hero != null)
                    EnterFollow();
            }
        }
    }

    void BeginPointerZoom(Vector2 screenPos)
    {
        // Zoomen wechselt nie in den freien Modus. Zum Cursor/zur Pinch-Mitte wird nur gezoomt,
        // wenn die Kamera durch Ziehen ohnehin schon frei ist. Im Follow-Modus zoomt sie zur Bildmitte.
        if (!zoomToPointer || mode != Mode.Free) return;
        zoomAnchorScreen = screenPos;
        zoomAnchorValid = true;
    }

    // =====================================================================
    //  Modus-Wechsel
    // =====================================================================
    void EnterFree()
    {
        if (mode == Mode.Free) return;

        if (mode == Mode.Focus)
        {
            EndFocusZoom();
            focusTarget = null;
        }

        mode = Mode.Free;
        desiredPos = basePos;   // Velocity bleibt erhalten -> weiches Abbremsen statt Ruck
        idleTimer = 0f;
    }

    void EnterFollow()
    {
        if (hero == null) return;

        mode = Mode.Follow;
        returnBlend = 0f;       // startet mit langer Glättung und blendet ab
        idleTimer = 0f;
        zoomAnchorValid = false;
        rubberTarget = Vector3.zero;
        anchor = HeroPoint();   // Held wird beim Zurückkehren wieder zentriert
    }

    void EndFocus()
    {
        EndFocusZoom();
        focusTarget = null;

        if (hero != null)
        {
            EnterFollow();
        }
        else
        {
            mode = Mode.Free;
            desiredPos = basePos;
            idleTimer = 0f;
        }
    }

    void EndFocusZoom()
    {
        if (!focusZoomActive) return;
        userOrtho = savedUserOrtho;
        focusZoomActive = false;
    }

    // =====================================================================
    //  Update-Teile (LateUpdate)
    // =====================================================================
    void UpdateHeroMotion(float dt)
    {
        if (hero == null)
        {
            heroVel = Vector2.zero;
            return;
        }

        Vector2 p = hero.position;
        Vector2 raw = heroPosInitialized ? (p - lastHeroPos) / dt : Vector2.zero;
        lastHeroPos = p;
        heroPosInitialized = true;

        // Teleport-Schutz: riesige Sprünge nicht als Geschwindigkeit werten
        if (raw.sqrMagnitude > 2500f) raw = Vector2.zero;

        heroVel = Vector2.SmoothDamp(heroVel, raw, ref heroVelRef, 0.12f, Mathf.Infinity, dt);

        Vector2 lookTarget = Vector2.ClampMagnitude(
            heroVel * (lookAheadDistance / Mathf.Max(0.01f, lookAheadFullSpeed)),
            lookAheadDistance);

        lookAhead = Vector2.SmoothDamp(lookAhead, lookTarget, ref lookAheadRef, lookAheadSmoothTime, Mathf.Infinity, dt);
    }

    void UpdateZoom(float dt)
    {
        float maxAllowed = maxOrthoSize;
        if (bounds != null)
            maxAllowed = Mathf.Min(maxAllowed, bounds.MaxOrthoSize(cam.aspect));
        maxAllowed = Mathf.Max(minOrthoSize, maxAllowed);

        userOrtho = Mathf.Clamp(userOrtho, minOrthoSize, maxAllowed);

        // Tempo-Zoom: beim Rennen etwas herauszoomen, beim Stehen wieder heran
        float speedTarget = 1f;
        if (mode == Mode.Follow && runZoomFullSpeed > 0.01f)
            speedTarget = Mathf.Lerp(1f, runZoomOutFactor, Mathf.Clamp01(heroVel.magnitude / runZoomFullSpeed));

        speedFactor = Mathf.SmoothDamp(speedFactor, speedTarget, ref speedFactorVel, speedZoomSmoothTime, Mathf.Infinity, dt);

        float target = Mathf.Clamp(userOrtho * speedFactor, minOrthoSize, maxAllowed);

        float oldOrtho = cam.orthographicSize;
        float newOrtho = Mathf.SmoothDamp(oldOrtho, target, ref orthoVel, zoomSmoothTime, Mathf.Infinity, dt);
        cam.orthographicSize = newOrtho;

        // Zoom zum Cursor/zur Pinch-Mitte: Weltpunkt unter dem Zeiger bleibt an derselben Bildschirmstelle
        if (zoomAnchorValid && mode == Mode.Free)
        {
            Vector2 ndc = new Vector2(
                zoomAnchorScreen.x / Screen.width - 0.5f,
                zoomAnchorScreen.y / Screen.height - 0.5f) * 2f;

            float dOrtho = oldOrtho - newOrtho;
            Vector3 shift = new Vector3(ndc.x * dOrtho * cam.aspect, ndc.y * dOrtho, 0f);
            basePos += shift;
            desiredPos += shift;
        }

        if (Mathf.Abs(newOrtho - target) < 0.001f)
            zoomAnchorValid = false;
    }

    void UpdatePosition(float dt)
    {
        if (mode == Mode.Follow && hero == null)
        {
            mode = Mode.Free;
            desiredPos = basePos;
        }

        float smooth = panSmoothTime;

        switch (mode)
        {
            case Mode.Follow:
                UpdateAnchor();
                desiredPos = new Vector3(anchor.x + lookAhead.x, anchor.y + lookAhead.y, zPos);

                returnBlend = Mathf.Min(1f, returnBlend + dt / Mathf.Max(0.01f, returnBlendDuration));
                smooth = Mathf.Lerp(returnSmoothTime, followSmoothTime, Mathf.SmoothStep(0f, 1f, returnBlend));
                break;

            case Mode.Focus:
                UpdateFocus(dt);
                smooth = focusSmoothTime;
                break;

            case Mode.Free:
                smooth = panSmoothTime;
                break;
        }

        float ortho = cam.orthographicSize;

        desiredPos = Clamp(desiredPos, ortho);
        basePos = Vector3.SmoothDamp(basePos, desiredPos, ref velocity, smooth, Mathf.Infinity, dt);

        // Harte Begrenzung mit der AKTUELLEN Zoomstufe (Rubber-Band wird separat addiert)
        basePos = Clamp(basePos, ortho);
    }

    void UpdateAnchor()
    {
        Vector3 h = HeroPoint();
        float hx = deadZone.x * cam.orthographicSize * cam.aspect;
        float hy = deadZone.y * cam.orthographicSize;

        float dx = h.x - anchor.x;
        if (dx > hx) anchor.x = h.x - hx;
        else if (dx < -hx) anchor.x = h.x + hx;

        float dy = h.y - anchor.y;
        if (dy > hy) anchor.y = h.y - hy;
        else if (dy < -hy) anchor.y = h.y + hy;
    }

    void UpdateFocus(float dt)
    {
        if (focusTarget == null)
        {
            EndFocus();
            return;
        }

        desiredPos = new Vector3(focusTarget.position.x, focusTarget.position.y, zPos);
        focusElapsed += dt;

        if (!focusArrived)
        {
            Vector3 clampedTarget = Clamp(desiredPos, cam.orthographicSize);
            float dist = ((Vector2)(clampedTarget - basePos)).magnitude;
            if (dist < cam.orthographicSize * 0.05f || focusElapsed > focusMaxTravelTime)
                focusArrived = true;
        }
        else
        {
            focusHoldLeft -= dt;
            if (focusHoldLeft <= 0f)
                EndFocus();
        }
    }

    Vector3 UpdateShake(float dt)
    {
        if (shakeLeft <= 0f) return Vector3.zero;

        shakeLeft = Mathf.Max(0f, shakeLeft - dt);
        float amp = CurrentShakeAmplitude();

        float t = Time.time * shakeFrequency;
        float x = (Mathf.PerlinNoise(shakeSeed, t) - 0.5f) * 2f * amp;
        float y = (Mathf.PerlinNoise(shakeSeed + 37.1f, t) - 0.5f) * 2f * amp;
        return new Vector3(x, y, 0f);
    }

    float CurrentShakeAmplitude()
    {
        if (shakeLeft <= 0f || shakeTotal <= 0f) return 0f;
        float t = shakeLeft / shakeTotal;
        return shakeStrength * t * t;   // weiches Ausklingen
    }

    void UpdateExploreEffect(float dt)
    {
        if (exploreVolume == null) return;

        float target = mode == Mode.Free ? 1f : 0f;
        exploreWeight = Mathf.SmoothDamp(exploreWeight, target, ref exploreVel, exploreFadeTime, Mathf.Infinity, dt);
        exploreVolume.weight = exploreWeight;
    }

    // =====================================================================
    //  Helpers
    // =====================================================================
    Vector3 HeroPoint()
    {
        return new Vector3(hero.position.x + followOffset.x, hero.position.y + followOffset.y, zPos);
    }

    Vector3 Clamp(Vector3 pos, float orthoSize)
    {
        if (bounds == null) return pos;
        return bounds.Clamp(pos, orthoSize, cam.aspect);
    }

    /// <summary>iOS-artiger Widerstand: je weiter über die Grenze, desto zäher.</summary>
    static float Resist(float overshoot, float range)
    {
        if (overshoot == 0f) return 0f;
        float s = Mathf.Sign(overshoot);
        float o = Mathf.Abs(overshoot);
        return s * (1f - 1f / (o * 0.55f / range + 1f)) * range;
    }

    static bool IsOverUI()
    {
        if (EventSystem.current == null) return false;

        if (Input.touchCount > 0)
            return EventSystem.current.IsPointerOverGameObject(Input.GetTouch(0).fingerId);

        return EventSystem.current.IsPointerOverGameObject();
    }
}