using UnityEngine;
using Fusion;
using UnityEngine.InputSystem;

[RequireComponent(typeof(CharacterController))]
public class PlayerMovement : NetworkBehaviour
{
    private CharacterController _controller;
    public Camera fpsCamera;
    public Animator animator;

    [Header("Rychlosti")]
    public float walkSpeed = 9f;
    public float sprintSpeed = 15f;
    public float crouchSpeed = 4.5f;
    public float movementSmoothness = 14f;

    [Header("Skok a gravitace")]
    public float jumpForce = 6f;
    public float gravity = -30f;
    public float coyoteTime = 0.15f;

    [Header("Myš")]
    public float mouseSensitivity = 0.15f;

    [Header("Crouch")]
    public float standHeight = 2f;
    public float crouchHeight = 1f;
    public float crouchTransitionSpeed = 10f;

    [Header("Head Bob")]
    public float walkBobFreq = 1.8f;
    public float sprintBobFreq = 2.8f;
    public float walkBobAmt = 0.035f;
    public float sprintBobAmt = 0.07f;
    public float crouchBobAmt = 0.015f;

    private float _yRotation = 0f;
    private float _verticalRotation = 0f;
    private float _velocityY = 0f;
    private Vector2 _targetMoveInput = Vector2.zero;
    private Vector2 _currentMoveInput = Vector2.zero;
    private bool _isSprinting = false;
    private bool _isCrouching = false;
    private bool _jumpRequested = false;
    private float _coyoteTimer = 0f;
    private Vector3 _cameraBaseLocalPos;
    private float _cameraTargetLocalY;
    private float _bobTimer = 0f;

    [HideInInspector] public float recoilOffset = 0f;

    public override void Spawned()
    {
        _controller = GetComponent<CharacterController>();
        _cameraBaseLocalPos = fpsCamera.transform.localPosition;
        _cameraTargetLocalY = _cameraBaseLocalPos.y;
        _yRotation = transform.eulerAngles.y;

        // Every player prefab has a HUD Canvas. Show it only for its owner.
        Canvas hudCanvas = GetComponentInChildren<Canvas>(true);
        if (hudCanvas != null)
            hudCanvas.gameObject.SetActive(HasStateAuthority);

        if (HasStateAuthority)
        {
            fpsCamera.gameObject.SetActive(true);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;

            if (Camera.main != null)
                Camera.main.gameObject.SetActive(false);
        }
        else
        {
            fpsCamera.gameObject.SetActive(false);
            _controller.enabled = false;
        }
    }

    void Update()
    {
        if (!HasStateAuthority) return;

        HandleMouse();
        HandleKeyboard();
        recoilOffset = Mathf.Lerp(recoilOffset, 0f, Time.deltaTime * 8f);
    }

    void LateUpdate()
    {
        if (!HasStateAuthority) return;

        HandleCrouchTransition();
        HandleHeadBob();
    }

    private void HandleMouse()
    {
        if (Mouse.current == null) return;

        Vector2 delta = Mouse.current.delta.ReadValue();

        _yRotation += delta.x * mouseSensitivity;
        _verticalRotation -= delta.y * mouseSensitivity;
        _verticalRotation = Mathf.Clamp(_verticalRotation, -90f, 90f);

        fpsCamera.transform.rotation = Quaternion.Euler(
            _verticalRotation - recoilOffset, _yRotation, 0f);

        transform.rotation = Quaternion.Euler(0f, _yRotation, 0f);
    }

    private void HandleKeyboard()
    {
        if (Keyboard.current == null) return;

        float w = Keyboard.current.wKey.isPressed ? 1f : 0f;
        float s = Keyboard.current.sKey.isPressed ? -1f : 0f;
        float a = Keyboard.current.aKey.isPressed ? -1f : 0f;
        float d = Keyboard.current.dKey.isPressed ? 1f : 0f;

        _targetMoveInput = new Vector2(a + d, w + s).normalized;
        _isSprinting = Keyboard.current.leftShiftKey.isPressed && !_isCrouching;

        if (Keyboard.current.cKey.wasPressedThisFrame)
            _isCrouching = !_isCrouching;

        if (Keyboard.current.spaceKey.wasPressedThisFrame)
            _jumpRequested = true;

        _currentMoveInput = Vector2.Lerp(
            _currentMoveInput,
            _targetMoveInput,
            Time.deltaTime * movementSmoothness);
    }

    private void HandleCrouchTransition()
    {
        float targetH = _isCrouching ? crouchHeight : standHeight;

        _controller.height = Mathf.Lerp(
            _controller.height,
            targetH,
            Time.deltaTime * crouchTransitionSpeed);

        Vector3 c = _controller.center;
        c.y = _controller.height / 2f;
        _controller.center = c;

        _cameraTargetLocalY = _isCrouching
            ? _cameraBaseLocalPos.y - (standHeight - crouchHeight) * 0.5f
            : _cameraBaseLocalPos.y;
    }

    private void HandleHeadBob()
    {
        bool moving = _currentMoveInput.magnitude > 0.1f && _controller.isGrounded;

        float freq = _isSprinting ? sprintBobFreq : walkBobFreq;
        float amt = _isCrouching ? crouchBobAmt
            : _isSprinting ? sprintBobAmt
            : walkBobAmt;

        if (moving)
            _bobTimer += Time.deltaTime * freq * Mathf.PI * 2f;
        else
            _bobTimer = Mathf.Lerp(_bobTimer, 0f, Time.deltaTime * 6f);

        float bobY = Mathf.Sin(_bobTimer) * amt;

        Vector3 camPos = fpsCamera.transform.localPosition;
        camPos.y = Mathf.Lerp(
            camPos.y,
            _cameraTargetLocalY + bobY,
            Time.smoothDeltaTime * 20f);

        fpsCamera.transform.localPosition = camPos;
    }

    public override void FixedUpdateNetwork()
    {
        if (!HasStateAuthority) return;

        bool grounded = Physics.Raycast(
            transform.position + Vector3.up * 0.1f,
            Vector3.down,
            0.3f);

        if (grounded)
            _coyoteTimer = coyoteTime;
        else
            _coyoteTimer -= Runner.DeltaTime;

        if (grounded && _velocityY < 0f)
            _velocityY = -2f;

        if (_jumpRequested)
        {
            if (_coyoteTimer > 0f)
            {
                _velocityY = jumpForce;
                _coyoteTimer = 0f;
            }

            _jumpRequested = false;
        }

        _velocityY += gravity * Runner.DeltaTime;

        float speed = _isCrouching ? crouchSpeed
            : _isSprinting ? sprintSpeed
            : walkSpeed;

        Quaternion yRot = Quaternion.Euler(0f, _yRotation, 0f);
        Vector3 forward = yRot * Vector3.forward;
        Vector3 right = yRot * Vector3.right;

        Vector3 move = (right * _currentMoveInput.x +
                        forward * _currentMoveInput.y) * speed;

        move.y = _velocityY;

        _controller.Move(move * Runner.DeltaTime);
        transform.rotation = Quaternion.Euler(0f, _yRotation, 0f);

        if (animator != null)
        {
            Vector3 hVel = new Vector3(
                _controller.velocity.x,
                0f,
                _controller.velocity.z);

            animator.SetFloat("Speed", hVel.magnitude);
        }
    }
}
