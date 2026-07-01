using UnityEngine;

/// <summary>
/// CAMERA FOLLOW
/// หน้าที่: กล้องตาม Player แบบ smooth
/// อยู่บน: Main Camera
/// </summary>
public class CameraFollow : MonoBehaviour
{
    [Header("Target")]
    public Transform target;         // ← ลาก Player ใส่

    [Header("Settings")]
    public float smoothSpeed = 0.1f; // 0.1 = smooth มาก, 1.0 = ตามทันที
    public Vector3 offset    = new Vector3(0, 0, -10);

    void LateUpdate()
    {
        if (target == null) return;
        Vector3 dest = target.position + offset;
        transform.position = Vector3.Lerp(transform.position, dest, smoothSpeed);
    }
}
