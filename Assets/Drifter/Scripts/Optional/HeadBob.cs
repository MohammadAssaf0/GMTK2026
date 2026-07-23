using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public class HeadBob : MonoBehaviour
{
    [Header("Bobbing Settings")]
    public float baseBobSpeed = 4.5f;      // Frequency of bob
    public float baseBobAmount = 0.05f;    // Height of bob
    public float runMultiplier = 1.5f;     // Multiplier when running
    public float smooth = 8f;              // How fast it interpolates back to default

    [Header("References")]
    public CharacterController controller;

    private float defaultYPos;
    private float timer;

    void Start()
    {
        if (controller == null)
            controller = GetComponent<CharacterController>();

        defaultYPos = transform.localPosition.y;
    }

    void Update()
    {
        // Get movement speed from CharacterController
        float speed = new Vector3(controller.velocity.x, 0f, controller.velocity.z).magnitude;

        bool isMoving = speed > 0.1f;

        Vector3 localPos = transform.localPosition;

        if (isMoving)
        {
            // Scale bob amount and speed based on velocity
            float bobSpeed = baseBobSpeed + speed * runMultiplier * 0.2f;
            float bobAmount = baseBobAmount + speed * 0.02f;

            timer += Time.deltaTime * bobSpeed;
            float newY = defaultYPos + Mathf.Sin(timer) * bobAmount;

            localPos.y = Mathf.Lerp(localPos.y, newY, Time.deltaTime * smooth);
        }
        else
        {
            // Reset phase and return to resting position smoothly
            timer = 0f;
            localPos.y = Mathf.Lerp(localPos.y, defaultYPos, Time.deltaTime * smooth);
        }

        transform.localPosition = localPos;
    }
}