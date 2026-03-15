using System;
using UnityEngine;

namespace VehicleDynamics
{
    public struct RuntimeDebugLine
    {
        public Vector3 start;
        public Vector3 end;
        public Color color;

        public RuntimeDebugLine(Vector3 start, Vector3 end, Color color)
        {
            this.start = start;
            this.end = end;
            this.color = color;
        }
    }
    public static class RuntimeDebugDraw
    {
        public static void DrawCross(Vector3 center, float size, Color color, Action<Vector3, Vector3, Color> drawLine)
        {
            drawLine(center - Vector3.right * size, center + Vector3.right * size, color);
            drawLine(center - Vector3.up * size, center + Vector3.up * size, color);
            drawLine(center - Vector3.forward * size, center + Vector3.forward * size, color);
        }

        public static void DrawCircle(Vector3 center, Vector3 normal, float radius, Color color, Action<Vector3, Vector3, Color> drawLine, int segments = 36)
        {
            Quaternion rotation = Quaternion.FromToRotation(Vector3.up, normal.normalized);
            Vector3 previousPoint = center + rotation * (Vector3.forward * radius);

            for (int i = 1; i <= segments; i++)
            {
                float angle = i * 360f / segments;
                Vector3 nextPoint = center + rotation * (Quaternion.Euler(0f, angle, 0f) * (Vector3.forward * radius));
                drawLine(previousPoint, nextPoint, color);
                previousPoint = nextPoint;
            }
        }

        public static void DrawCoilSpring(
            Vector3 startPoint,
            Vector3 endPoint,
            Vector3 rightDirection,
            Vector3 forwardDirection,
            Action<Vector3, Vector3, Color> drawLine,
            Color color,
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

                Vector3 currentPoint = currentBasePoint
                    + amplitude * Mathf.Cos(currentProgress * turns * 2f * Mathf.PI) * rightDirection
                    + amplitude * Mathf.Sin(currentProgress * turns * 2f * Mathf.PI) * forwardDirection;

                Vector3 nextPoint = nextBasePoint
                    + amplitude * Mathf.Cos(nextProgress * turns * 2f * Mathf.PI) * rightDirection
                    + amplitude * Mathf.Sin(nextProgress * turns * 2f * Mathf.PI) * forwardDirection;

                drawLine(currentPoint, nextPoint, color);
            }
        }
    }
}