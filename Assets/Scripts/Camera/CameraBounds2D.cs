using UnityEngine;

public class CameraBounds2D : MonoBehaviour
{
    public Vector2 scaleBound = new Vector2(1, 1);
    public Vector2 offset;

    /// <summary>
    /// Begrenzt eine Kamera-Position so, dass die komplette Kamera-Sicht innerhalb der Bounds bleibt.
    /// Ist die Sicht größer als die Bounds, wird auf der jeweiligen Achse zentriert
    /// (verhindert min > max und damit das Springen).
    /// </summary>
    public Vector3 Clamp(Vector3 pos, float orthoSize, float aspect)
    {
        float halfW = orthoSize * aspect;
        float halfH = orthoSize;

        Vector2 center = (Vector2)transform.position + offset;

        pos.x = ClampAxis(pos.x, center.x - scaleBound.x * 0.5f + halfW, center.x + scaleBound.x * 0.5f - halfW, center.x);
        pos.y = ClampAxis(pos.y, center.y - scaleBound.y * 0.5f + halfH, center.y + scaleBound.y * 0.5f - halfH, center.y);
        return pos;
    }

    /// <summary>Größte orthographicSize, bei der die Sicht noch komplett in die Bounds passt.</summary>
    public float MaxOrthoSize(float aspect)
    {
        return Mathf.Min(scaleBound.y * 0.5f, scaleBound.x * 0.5f / aspect);
    }

    static float ClampAxis(float value, float min, float max, float center)
    {
        return min > max ? center : Mathf.Clamp(value, min, max);
    }
}