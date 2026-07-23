using UnityEngine;

public class FootstepSounds : MonoBehaviour
{
    public bool enableFootstepSounds = true; // Checkbox to enable or disable footstep sounds
    public AudioClip[] grassFootsteps;
    public AudioClip[] woodFootsteps;
    public AudioClip[] concreteFootsteps;
    public float baseFootstepInterval = 0.5f;

    private float footstepTimer;
    private AudioSource audioSource;
    private CharacterController characterController;

    void Start()
    {
        audioSource = GetComponent<AudioSource>();
        characterController = GetComponent<CharacterController>();
        footstepTimer = 0f;
    }

    void Update()
    {
        if (enableFootstepSounds && characterController.isGrounded && (characterController.velocity.x != 0 || characterController.velocity.z != 0))
        {
            footstepTimer += Time.deltaTime;

            float footstepInterval = baseFootstepInterval;
            // Adjust footstep interval based on speed (optional)
            if (characterController.velocity.magnitude > 1f) // Check if running
            {
                footstepInterval /= 1.5f; // Increase interval for running
            }

            if (footstepTimer >= footstepInterval)
            {
                PlayFootstep();
                footstepTimer = 0f;
            }
        }
        else
        {
            footstepTimer = 0f; // Reset the timer if not moving or not grounded
        }
    }

    void PlayFootstep()
    {
        if (audioSource != null)
        {
            AudioClip clip = GetFootstepSound();
            if (clip != null)
            {
                audioSource.PlayOneShot(clip);
            }
        }
    }

    AudioClip GetFootstepSound()
    {
        // Check the type of surface we are currently on
        RaycastHit hit;
        if (Physics.Raycast(transform.position, -Vector3.up, out hit, 1f))
        {
            string surfaceTag = hit.collider.tag;
            switch (surfaceTag)
            {
                case "Grass":
                    return grassFootsteps[Random.Range(0, grassFootsteps.Length)];
                case "Wood":
                    return woodFootsteps[Random.Range(0, woodFootsteps.Length)];
                case "Concrete":
                    return concreteFootsteps[Random.Range(0, concreteFootsteps.Length)];
                // Add cases for more surfaces as needed
                default:
                    return null; // No sound if the surface is unrecognized
            }
        }
        return null; // Return null if no hit detected
    }
}