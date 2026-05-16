using UnityEngine;

public class CameraController : MonoBehaviour
{
    public float mouseSensitivity = 100f;
    public Transform playerBody;

    [Header("しゃがみカメラ設定")]
    public float standCameraY = 0.8f;
    public float crouchCameraY = 0.3f;

    private float xRotation = 0f;
    private PlayerController playerController;

    void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        playerController = playerBody.GetComponent<PlayerController>();
    }

    void Update()
    {
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -80f, 80f);

        transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        playerBody.Rotate(Vector3.up * mouseX);
    }
}
