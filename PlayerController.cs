using UnityEngine;
using System.Collections;
using UnityEngine.UI;

public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    public float moveSpeed = 5f;
    private Rigidbody rb;

    [Header("Mouse Look")]
    public float mouseSensitivity = 100f;
    public Transform cameraTransform;
    private float xRotation = 0f;

    [Header("Dashing")]
    public float dashSpeed = 20f;
    public float dashDuration = 0.2f;
    public float dashCooldown = 1f;
    private bool isDashing = false;
    private float dashCooldownTimer = 0f;

    [Header("Sliding")]
    public float slideSpeedMultiplier = 1.2f;
    public float slideSpeedDecay = 0.98f; // Slower decay for better momentum
    public float slideCooldown = 0.5f;
    private bool isSliding = false;
    private float slideCooldownTimer = 0f;
    private Vector3 slideMomentum;

    [Header("Jumping")]
    public float jumpForce = 5f;
    public float momentumJumpMultiplier = 1.5f;
    public float gravityMultiplier = 2f;
    private bool isGrounded;

    [Header("Momentum Tuning")]
    public float maxSlideSpeed = 50f; // Cap slide speed
    public float airControl = 0.3f; // Allow slight movement input while sliding/in air
    public float dashDuringSlideBoost = 1.5f; // Extra speed when dashing mid-slide

    [Header("UI")]
    public Text speedText; // Reference to UI Text element

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        Cursor.lockState = CursorLockMode.Locked;
    }

    void Update()
    {
        // Mouse Look
        float mouseX = Input.GetAxis("Mouse X") * mouseSensitivity * Time.deltaTime;
        float mouseY = Input.GetAxis("Mouse Y") * mouseSensitivity * Time.deltaTime;

        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraTransform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        transform.Rotate(Vector3.up * mouseX);

        // Dash input (now works during slides)
        if (Input.GetKeyDown(KeyCode.LeftShift) && !isDashing && dashCooldownTimer <= 0f)
        {
            StartCoroutine(Dash());
        }

        // Slide input
        if (Input.GetKeyDown(KeyCode.LeftControl) && !isSliding && slideCooldownTimer <= 0f)
        {
            StartSlide();
        }
        if (Input.GetKeyUp(KeyCode.LeftControl) && isSliding)
        {
            StopSlide();
        }

        // Jump input
        if (Input.GetKeyDown(KeyCode.Space) && isGrounded)
        {
            Jump();
        }

        // Cooldowns
        dashCooldownTimer = Mathf.Max(0f, dashCooldownTimer - Time.deltaTime);
        slideCooldownTimer = Mathf.Max(0f, slideCooldownTimer - Time.deltaTime);

        // Ground check
        isGrounded = Physics.Raycast(transform.position, Vector3.down, 1.1f);

        // Apply gravity
        if (!isGrounded)
        {
            rb.linearVelocity += Vector3.up * Physics.gravity.y * gravityMultiplier * Time.deltaTime;
        }

        // Update speed display
        UpdateSpeedUI();
    }

    void FixedUpdate()
    {
        if (isSliding)
        {
            // Apply decay to horizontal momentum
            slideMomentum = new Vector3(
                slideMomentum.x * slideSpeedDecay,
                slideMomentum.y,
                slideMomentum.z * slideSpeedDecay
            );

            // Allow steering and clamp speed
            float x = Input.GetAxis("Horizontal") * airControl;
            float z = Input.GetAxis("Vertical") * airControl;
            Vector3 move = transform.right * x + transform.forward * z;
            slideMomentum += move * moveSpeed * 0.5f; // Reduced steering power

            // Clamp horizontal speed only
            Vector3 horizontalMomentum = new Vector3(slideMomentum.x, 0, slideMomentum.z);
            horizontalMomentum = Vector3.ClampMagnitude(horizontalMomentum, maxSlideSpeed);
            slideMomentum = new Vector3(horizontalMomentum.x, slideMomentum.y, horizontalMomentum.z);

            rb.linearVelocity = slideMomentum;
        }
        else if (!isDashing)
        {
            // Normal WASD movement
            float x = Input.GetAxis("Horizontal");
            float z = Input.GetAxis("Vertical");
            Vector3 move = transform.right * x + transform.forward * z;
            rb.linearVelocity = move * moveSpeed + new Vector3(0, rb.linearVelocity.y, 0);
        }
    }

    void UpdateSpeedUI()
    {
        if (speedText != null)
        {
            // Convert m/s to km/h and display
            float speed = rb.linearVelocity.magnitude * 3.6f;
            speedText.text = $"Speed: {speed:F1} km/h";
        }
    }

    IEnumerator Dash()
    {
        isDashing = true;
        Vector3 dashDirection;

        // If sliding, dash in the slide direction + current input
        if (isSliding)
        {
            dashDirection = (slideMomentum.normalized + transform.forward * Input.GetAxisRaw("Vertical")).normalized;
            rb.linearVelocity = dashDirection * dashSpeed * dashDuringSlideBoost;
        }
        else
        {
            // Default dash logic
            dashDirection = (Input.GetAxisRaw("Horizontal") != 0 || Input.GetAxisRaw("Vertical") != 0)
                ? (transform.forward * Input.GetAxisRaw("Vertical") + transform.right * Input.GetAxisRaw("Horizontal")).normalized
                : transform.forward;
            rb.linearVelocity = dashDirection * dashSpeed + new Vector3(0, rb.linearVelocity.y, 0);
        }

        yield return new WaitForSeconds(dashDuration);

        // Preserve momentum after dash (don't reset if sliding)
        if (!isSliding)
        {
            rb.linearVelocity *= 0.8f; // Mild slowdown instead of hard reset
        }

        isDashing = false;
        dashCooldownTimer = dashCooldown;
    }

    void StartSlide()
    {
        isSliding = true;
        // Capture ALL current velocity (including dash/jump momentum)
        slideMomentum = rb.linearVelocity * slideSpeedMultiplier;
        slideMomentum.y = rb.linearVelocity.y; // Preserve gravity
    }

    void StopSlide()
    {
        isSliding = false;
        slideCooldownTimer = slideCooldown;
    }

    void Jump()
    {
        // Preserve ALL momentum (horizontal + vertical from slides/dashes)
        Vector3 jumpVelocity = rb.linearVelocity;
        jumpVelocity.y = jumpForce + (rb.linearVelocity.magnitude * momentumJumpMultiplier);
        rb.linearVelocity = jumpVelocity;

        // Exit slide state when jumping
        if (isSliding) StopSlide();
    }
}
