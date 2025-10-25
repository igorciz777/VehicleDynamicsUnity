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
}
