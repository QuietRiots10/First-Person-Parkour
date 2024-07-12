using UnityEngine;
using UnityEngine.InputSystem;

public class NewCameraController : MonoBehaviour
{
    [Tooltip("The GameObject for the camera to follow")]
    public GameObject PlayerHead;
    [Tooltip("The Player's LookDir object")]
    public GameObject LookDir;

    [Tooltip("Camera sensitivity (Default = 5)")]
    public float CameraSense;
    private float FixedSenseMultiplier = 0.01f; //So that the Sensitivity option doesnt have to be unnecesarily big or small

    private float MouseX = 0;
    private float MouseY = 0;

    private float TargetXRot;
    private float TargetYRot;

    void Update()
    {
        //Camera follows the player's head
        transform.position = PlayerHead.transform.position;

        //Find current look rotation
        Vector3 CurrentRot = transform.localRotation.eulerAngles;
        TargetXRot = CurrentRot.y + MouseX;

        //Set target rotations
        TargetYRot -= MouseY;
        TargetYRot = Mathf.Clamp(TargetYRot, -90f, 90f);

        //Set the camera's rotation
        transform.localRotation = Quaternion.Euler(TargetYRot, TargetXRot, 0);

        //Set the LookDir object to face in the direction of the camera
        LookDir.transform.localRotation = Quaternion.Euler(0, TargetXRot, 0);
    }
    public void OnLook(InputAction.CallbackContext context)
    {
        MouseX = context.ReadValue<Vector2>().x * FixedSenseMultiplier * CameraSense;
        MouseY = context.ReadValue<Vector2>().y * FixedSenseMultiplier * CameraSense;
    }
}
