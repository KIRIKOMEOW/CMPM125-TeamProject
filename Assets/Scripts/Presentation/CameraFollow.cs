using UnityEngine;

// Moves the camera rig toward the player, leaning a little toward the mouse, clamped to the arena.
// CameraShake lives on the child camera, so this script never touches the shaken transform.
public class CameraFollow : MonoBehaviour
{
    [SerializeField] private Transform target;
    [SerializeField] private Camera viewCamera;
    [SerializeField] private float smoothTime = 0.12f;
    [SerializeField] private float aimLean = 1.5f;
    [SerializeField] private Vector2 arenaMin = new Vector2(-15f, -9f);
    [SerializeField] private Vector2 arenaMax = new Vector2(15f, 9f);

    private Vector3 velocity;

    private void LateUpdate()
    {
        if (target == null)
            return;

        Vector2 goal = target.position;
        if (viewCamera != null && GameManager.Instance != null && GameManager.Instance.State == GameState.Playing)
        {
            Vector2 mouse = viewCamera.ScreenToWorldPoint(Input.mousePosition);
            goal += Vector2.ClampMagnitude(mouse - goal, 4f) / 4f * aimLean;
        }

        if (viewCamera != null && viewCamera.orthographic)
        {
            float halfHeight = viewCamera.orthographicSize;
            float halfWidth = halfHeight * viewCamera.aspect;
            goal.x = ClampAxis(goal.x, arenaMin.x + halfWidth, arenaMax.x - halfWidth);
            goal.y = ClampAxis(goal.y, arenaMin.y + halfHeight, arenaMax.y - halfHeight);
        }

        Vector3 desired = new Vector3(goal.x, goal.y, transform.position.z);
        transform.position = Vector3.SmoothDamp(transform.position, desired, ref velocity, smoothTime, Mathf.Infinity, Time.unscaledDeltaTime);
    }

    private static float ClampAxis(float value, float min, float max)
    {
        return min > max ? (min + max) * 0.5f : Mathf.Clamp(value, min, max);
    }
}
