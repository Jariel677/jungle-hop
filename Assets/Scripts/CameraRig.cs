using UnityEngine;

/// <summary>
/// Chase camera. Follows the runner from behind and above, damping lateral
/// motion so lane switches and jumps stay readable.
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 5.3f, -7.6f);
    public float smooth = 8.5f;

    void Start()
    {
        if (target != null)
            transform.position = new Vector3(target.position.x * 0.4f, offset.y, target.position.z + offset.z);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 tp = target.position;
        Vector3 desired = new Vector3(tp.x * 0.4f + offset.x, offset.y, tp.z + offset.z);
        transform.position = Vector3.Lerp(transform.position, desired, smooth * Time.deltaTime);

        Vector3 look = new Vector3(tp.x * 0.5f, 1.6f, tp.z + 9f);
        Quaternion want = Quaternion.LookRotation(look - transform.position);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, smooth * Time.deltaTime);
    }
}
