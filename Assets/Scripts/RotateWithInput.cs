using UnityEngine;

public class RotateWithInput : MonoBehaviour
{
    [Tooltip("Rotation speed in degrees per second")]
    [SerializeField] private float rotationSpeed = 100f;

    private void Update()
    {
        float direction = 0f;

        if (Input.GetKey(KeyCode.A)) direction = -1f;
        else if (Input.GetKey(KeyCode.D)) direction = 1f;

        if (direction != 0f)
            transform.Rotate(Vector3.up * direction * rotationSpeed * Time.deltaTime);
    }
}
