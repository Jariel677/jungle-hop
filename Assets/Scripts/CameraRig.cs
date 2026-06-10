using UnityEngine;

/// <summary>
/// Chase camera. Follows the runner from behind and above, damps lateral
/// motion, shakes on impact, and widens its field of view with speed so faster
/// running feels faster.
/// </summary>
public class CameraRig : MonoBehaviour
{
    public Transform target;
    public Vector3 offset = new Vector3(0f, 5.3f, -7.6f);
    public float smooth = 8.5f;

    const float BaseFov = 68f;

    Camera _cam;
    float _shake;
    float _fovKick;
    float _smoothFov = BaseFov;

    /// <summary>Adds an impact shake (decays automatically).</summary>
    public void Shake(float amount)
    {
        if (amount > _shake) _shake = amount;
    }

    /// <summary>Briefly widens the field of view (decays automatically) for a speed kick.</summary>
    public void Punch(float amount)
    {
        if (amount > _fovKick) _fovKick = amount;
    }

    void Start()
    {
        _cam = GetComponent<Camera>();
        if (target != null)
            transform.position = new Vector3(target.position.x * 0.4f, offset.y, target.position.z + offset.z);
    }

    void LateUpdate()
    {
        if (target == null) return;

        Vector3 tp = target.position;
        Vector3 desired = new Vector3(tp.x * 0.4f + offset.x, offset.y, tp.z + offset.z);
        Vector3 pos = Vector3.Lerp(transform.position, desired, smooth * Time.deltaTime);

        if (_shake > 0.0015f)
        {
            pos += Random.insideUnitSphere * _shake;
            _shake = Mathf.Lerp(_shake, 0f, 9f * Time.unscaledDeltaTime);
        }
        transform.position = pos;

        Vector3 look = new Vector3(tp.x * 0.5f, 1.6f, tp.z + 9f);
        Quaternion want = Quaternion.LookRotation(look - desired);
        transform.rotation = Quaternion.Slerp(transform.rotation, want, smooth * Time.deltaTime);

        if (_cam != null)
        {
            float speed = GameManager.Instance != null ? GameManager.Instance.CurrentSpeed : 0f;
            float baseFov = BaseFov + Mathf.Clamp(speed - 9f, 0f, 18f) * 0.85f;
            _smoothFov = Mathf.Lerp(_smoothFov, baseFov, 3f * Time.deltaTime);
            _fovKick = Mathf.Lerp(_fovKick, 0f, 5f * Time.deltaTime);
            _cam.fieldOfView = _smoothFov + _fovKick;
        }
    }
}
