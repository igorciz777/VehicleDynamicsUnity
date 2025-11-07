using UnityEngine;

public class CustomGizmos
{
    public static void DrawCircle(Vector3 center, Vector3 normal, float radius, Color color, int segments = 36)
    {
        Gizmos.color = color;
        Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal.normalized);
        Vector3 previousPoint = center + rotation * (Vector3.forward * radius);

        for (int i = 1; i <= segments; i++)
        {
            float angle = i * 360f / segments;
            Vector3 nextPoint = center + rotation * (Quaternion.Euler(0, angle, 0) * (Vector3.forward * radius));
            Gizmos.DrawLine(previousPoint, nextPoint);
            previousPoint = nextPoint;
        }
    }
    public static void DrawCoilSpringGizmo(
        Vector3 startPoint,
        Vector3 endPoint,
        Vector3 rightDirection,
        Vector3 forwardDirection,
        int segments = 100,
        float turns = 10f,
        float amplitude = 0.05f,
        float startOffset = 0f,
        float endOffset = 0f)
    {
        Vector3 springDirection = (endPoint - startPoint).normalized;
        float totalLength = Vector3.Distance(startPoint, endPoint);
        float usableLength = totalLength - startOffset - endOffset;

        for (int i = 0; i < segments; i++)
        {
            float currentProgress = (float)i / segments;
            float nextProgress = (float)(i + 1) / segments;

            float currentDistance = startOffset + usableLength * currentProgress;
            float nextDistance = startOffset + usableLength * nextProgress;

            Vector3 currentBasePoint = startPoint + springDirection * currentDistance;
            Vector3 nextBasePoint = startPoint + springDirection * nextDistance;

            // Add coil oscillation
            Vector3 currentPoint = currentBasePoint
                + amplitude * Mathf.Cos(currentProgress * turns * 2f * Mathf.PI) * rightDirection
                + amplitude * Mathf.Sin(currentProgress * turns * 2f * Mathf.PI) * forwardDirection;

            Vector3 nextPoint = nextBasePoint
                + amplitude * Mathf.Cos(nextProgress * turns * 2f * Mathf.PI) * rightDirection
                + amplitude * Mathf.Sin(nextProgress * turns * 2f * Mathf.PI) * forwardDirection;

            Gizmos.DrawLine(currentPoint, nextPoint);
        }
    }
}
