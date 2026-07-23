using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class FirstPersonController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    public float gravity = -9.81f;
    public float jumpHeight = 1.2f;

    [Header("Look Settings")]
    public float mouseSensitivity = 2f;
    public Transform playerCamera;

    [Header("Ground Check")]
    public Transform groundCheck;
    public float groundDistance = 0.4f;
    public LayerMask groundMask;

    private CharacterController _controller;
    private Vector2 _moveInput;
    private Vector2 _lookInput;
    private float _cameraPitch;
    
    private Vector3 _velocity;
    private bool _isGrounded;
    private bool _jumpInput;

    private void Start()
    {
        _controller = GetComponent<CharacterController>();
        Cursor.lockState = CursorLockMode.Locked; 
        Cursor.visible = false;
    }

    void OnMove(InputValue value)
    {
        _moveInput = value.Get<Vector2>();
    }

    void OnLook(InputValue value)
    {
        _lookInput = value.Get<Vector2>();
    }
    
    void OnJump(InputValue value)
    {
        _jumpInput = value.isPressed;
    }

    private void Update()
    {
        Look();
        MoveAndGravity();
    }

    void Look()
    {
        _cameraPitch -= _lookInput.y * mouseSensitivity;
        _cameraPitch = Mathf.Clamp(_cameraPitch, -90f, 90f);
        
        playerCamera.localRotation = Quaternion.Euler(_cameraPitch, 0f, 0f);
        transform.Rotate(Vector3.up * (_lookInput.x * mouseSensitivity));
    }

    void MoveAndGravity()
    {
        _isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundMask);

        if (_isGrounded && _velocity.y < 0)
        {
            _velocity.y = -2f;
        }

        var moveDirection = transform.right * _moveInput.x + transform.forward * _moveInput.y;
        _controller.Move(moveDirection * (moveSpeed * Time.deltaTime));
        
        if (_jumpInput && _isGrounded)
        {
            _velocity.y = Mathf.Sqrt(jumpHeight * -2f * gravity);
            _jumpInput = false;
        }

        _velocity.y += gravity * Time.deltaTime;
        _controller.Move(_velocity * Time.deltaTime);
    }
}