using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class PlayerMovementAdvanced : MonoBehaviour
{
    public bool CanMove { get; set; } = true;
    private bool IsGrounded => characterController.isGrounded;
    private bool ShouldJump => Input.GetKeyDown(jumpKey) && characterController.isGrounded;
    private bool IsSprinting => Input.GetKey(sprintKey) && characterController.isGrounded && canUseStamina && characterController.velocity != Vector3.zero;
    private float GetCurrentOffSet => IsSprinting ? baseStepSpeed * sprintStepMultiplier : baseStepSpeed;

    [Header("Functional Options")]
    [SerializeField] private bool canSprint = true;
    [SerializeField] private bool willSlideOnSlopes = true;
    [SerializeField] public bool useStamina = true;
    [SerializeField] private bool useHeat = true;
    [SerializeField] public bool useFootSteps = true;
    [SerializeField] private bool canUseHeadBob = true;

    [Header("References")]
    [SerializeField] private LayerMask whatIsGround;
    [SerializeField] private CharacterController characterController;
    [SerializeField] private CapsuleCollider capsuleCollider;
    [SerializeField] private GameObject cameraHolder;
    [SerializeField] private Camera cameraComp;
    [SerializeField] private UiController uiController;
    [SerializeField] private GameObject footsteps;
    [SerializeField] private AudioSource footstepAudioSource;
    [SerializeField] private AudioSource HeatAudioSource;
    [SerializeField] private Volume globalVolume;
    [SerializeField] private Vignette vignette;

    [Header("Controls")]
    [SerializeField] private KeyCode sprintKey = KeyCode.LeftShift;
    [SerializeField] private KeyCode jumpKey = KeyCode.Space;

    [Header("Look Parameters")]
    [SerializeField, Range(1, 10)] private float lookSpeedX;
    [SerializeField, Range(1, 10)] private float lookSpeedY;
    [SerializeField, Range(1, 100)] private float upperLookLimit;
    [SerializeField, Range(1, 100)] private float lowerLookLimit;
    private float rotationX;

    [Header("Headbob Parameters")]
    [SerializeField] private float walkBobSpeed;
    [SerializeField] private float walkBobAmount;
    [SerializeField] private float sprintBobSpeed;
    [SerializeField] private float sprintBobAmount;
    private float defaultYPos;
    private float timer;

    [Header("Movement Parameters")]
    [SerializeField] private float momentumDrag;
    [SerializeField] private float moveSpeed;
    [SerializeField] private float walkSpeed;
    [SerializeField] private float sprintSpeed;
    [SerializeField] private float slopeSpeed;
    private float moveDirectionY;
    private float moveDirectionZ;
    private float moveDirectionX;
    [HideInInspector] public Vector3 moveDirectionMomentum;
    private Vector2 currentInput;
    private Vector3 moveDirection;
    private Vector3 hitPointNormal;

    [Header("Jump Parameters")]
    [SerializeField] private float gravity;
    [SerializeField] private float upwardsGravityScale;
    [SerializeField] private float downwardsGravityScale;
    public float upwardsJumpForce;
    public float forwardsJumpForce;
    [SerializeField] private float jumpCooldown;
    private bool readyToJump;
    private bool exitingSlope;

    [Header("Stamina Parameters")]
    [SerializeField] private float maxStamina;
    [SerializeField] private float sprintingStaminaUseMultiplier = 5f;
    [SerializeField] private float timeBeforeStaminaRegenStarts = 5f;
    [SerializeField] private float staminaValueIncrement = 2f;
    [SerializeField] private float staminaTimeIncrement = 0.1f;
    public float currentStamina;
    [HideInInspector] public float displayStamina;
    public bool canUseStamina;
    private Coroutine regeneratingStamina;
    public static Action<PlayerMovementAdvanced, float> OnStaminaChange;

    [Header("Heat Parameters")]
    [SerializeField] private float maxHeat = 100f;
    [SerializeField] private float HeatUseMultiplier = 5f;
    [SerializeField] private float timeBeforeHeatRegenStarts = 5f;
    [SerializeField] private float HeatValueIncrement = 2f;
    [SerializeField] private float HeatTimeIncrement = 0.1f;
    [SerializeField, Range(0, 1)] private float vignetteIntensity;
    [SerializeField] private AudioClip[] SunburnClips;
    private bool inShadow;
    public float currentHeat;
    public float displayHeat;
    public float heatTimer;
    public float countDown;
    private Coroutine regeneratingHeat;
    public static Action<float> OnHeatChange;

    [Header("Footstep Parameters")]
    [SerializeField] private float baseStepSpeed = 0.5f;
    [SerializeField] private float sprintStepMultiplier = 0.6f;
    [SerializeField] private AudioClip[] DefaultClips;
    private float footstepTimer = 0;

    private bool IsGod;

    private void Awake()
    {
        whatIsGround = LayerMask.GetMask("whatIsGround");

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    private void Start()
    {
        SetUp();

        globalVolume.profile.TryGet(out vignette);
    }

    private void SetUp()
    {
        canUseStamina = true;
        currentStamina = maxStamina;
        currentHeat = maxHeat;
        defaultYPos = cameraComp.transform.localPosition.y;
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.O) && Input.GetKeyDown(KeyCode.P))
        {
            IsGod = true;
        }
        else if (Input.GetKeyUp(KeyCode.O) && Input.GetKeyUp(KeyCode.P))
        {
            IsGod = false;
        }

        if (IsGod)
        {
            walkSpeed = 30f;
            currentHeat = 100f;
        }
        else
        {
            walkSpeed = 10f;
        }

        if (CanMove)
        {
            vignette.intensity.value = vignetteIntensity;
            displayStamina = currentStamina;
            displayHeat = currentHeat;
            countDown = heatTimer;

            if (currentHeat >= 100f)
            {
                vignetteIntensity = Mathf.Lerp(vignetteIntensity, 0f, 0.1f);
            }
            else if (currentHeat >= 75f && currentHeat < 100f)
            {
                vignetteIntensity = Mathf.Lerp(vignetteIntensity, 0.15f, 0.1f);
                //vignetteIntensity = 0.15f;
            }
            else if (currentHeat >= 50f && currentHeat < 75f)
            {
                vignetteIntensity = Mathf.Lerp(vignetteIntensity, 0.3f, 0.1f);
                //vignetteIntensity = 0.3f;
            }
            else if (currentHeat >= 50f && currentHeat < 75f)
            {
                vignetteIntensity = Mathf.Lerp(vignetteIntensity, 0.45f, 0.1f);
                //vignetteIntensity = 0.45f;
            }
            else if (currentHeat >= 25f && currentHeat < 50f)
            {
                vignetteIntensity = Mathf.Lerp(vignetteIntensity, 0.6f, 0.1f);
                //vignetteIntensity = 0.6f;
            }
            else if (currentHeat > 0f && currentHeat < 25f)
            {
                vignetteIntensity = Mathf.Lerp(vignetteIntensity, 0.75f, 0.1f);
                //vignetteIntensity = 0.75f;
            }
            else if (currentHeat == 0f)
            {
                vignetteIntensity = Mathf.Lerp(vignetteIntensity, 1f, 0.1f);
                //vignetteIntensity = 1f;
            }

            vignette.intensity.Override(vignetteIntensity);

            if (currentHeat == 0)
            {
                heatTimer -= Time.deltaTime;

                if (heatTimer <= 0f)
                {
                    heatTimer = 0f;
                }
            }
            else if (currentHeat > 0f)
            {
                heatTimer += Time.deltaTime;

                if (heatTimer > 5f)
                {
                    heatTimer = 5f;
                }
            }

            if (GameManager.GameManagerInstance.GameLose == false)
            {
                HandleMovementInput();

                HandleCameraInput();

                HandleJump();

                HandleHeadbob();

                if (canSprint)
                {
                    HandleStamina();
                }

                if (useHeat)
                {
                    HandleHeat();
                }

                if (useFootSteps)
                {
                    HandleFootsteps();
                }

                if (heatTimer <= 0f)
                {
                    GameManager.GameManagerInstance.GameLose = true;
                }

                ApplyFinalMovements();
            }
        }
    }

    private void HandleMovementInput()
    {
        currentInput = new Vector2((IsSprinting ? sprintSpeed : walkSpeed) * Input.GetAxis("Horizontal"), (IsSprinting ? sprintSpeed : walkSpeed) * Input.GetAxis("Vertical"));

        moveDirection = (transform.forward * currentInput.y) + (transform.right * currentInput.x);
    }

    private void HandleCameraInput()
    {
        rotationX -= Input.GetAxis("Mouse Y") * lookSpeedY;
        rotationX = Mathf.Clamp(rotationX, -upperLookLimit, lowerLookLimit);
        cameraHolder.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, Input.GetAxis("Mouse X") * lookSpeedX, 0);
    }

    private void ApplyFinalMovements()
    {
        if (!IsGrounded)
        {
            if (characterController.velocity.y > 0f)
            {
                moveDirectionY -= gravity * upwardsGravityScale * 10 * Time.deltaTime;
            }

            if (characterController.velocity.y <= 0f)
            {
                moveDirectionY -= gravity * downwardsGravityScale * 10 * Time.deltaTime;
            }
        }

        if (characterController.velocity.y < -1f && IsGrounded)
        {
            moveDirectionY = 0f;
        }

        if (willSlideOnSlopes && IsSliding)
        {
            moveDirection += new Vector3(hitPointNormal.x, hitPointNormal.y, hitPointNormal.z) * slopeSpeed;
        }

        moveDirection.y = moveDirectionY;
        moveDirection.z += moveDirectionZ;
        moveDirection.x += moveDirectionX;

        moveDirection += moveDirectionMomentum;

        characterController.Move(moveDirection * Time.deltaTime);

        if (characterController.velocity.magnitude >= currentInput.y)
        {
            moveDirectionZ = 0f;
        }

        if (characterController.velocity.magnitude >= currentInput.x)
        {
            moveDirectionX = 0f;
        }

        if (moveDirectionMomentum.magnitude >= 0f)
        {
            moveDirectionMomentum -= momentumDrag * Time.deltaTime * moveDirectionMomentum;

            if (moveDirectionMomentum.magnitude <= 0.0f)
            {
                moveDirectionMomentum = Vector3.zero;
            }

            if (IsGrounded)
            {
                moveDirectionMomentum = Vector3.zero;
            }
        }
    }

    public void HandleJump()
    {
        if (ShouldJump && IsGrounded)
        {
            moveDirectionY = upwardsJumpForce;

            if (moveDirection.z > 0f)
            {
                moveDirectionZ = forwardsJumpForce;
            }
            else if (moveDirection.z < 0f)
            {
                moveDirectionZ = (forwardsJumpForce * -1);
            }
            else if (moveDirection.z == 0f)
            {
                moveDirectionZ = 0f;
            }

            if (moveDirection.x > 0f)
            {
                moveDirectionX = (forwardsJumpForce / 2);
            }
            else if (moveDirection.x < 0f)
            {
                moveDirectionX = ((forwardsJumpForce / 2) * -1);
            }
            else if (moveDirection.x == 0f)
            {
                moveDirectionX = 0f;
            }

            readyToJump = false;

            exitingSlope = true;

            Invoke(nameof(ResetJump), jumpCooldown);
        }
    }

    private void ResetJump()
    {
        readyToJump = true;

        exitingSlope = false;
    }

    private void HandleHeadbob()
    {
        if (canUseHeadBob)
        {
            if (!IsGrounded)
            {
                return;
            }

            if (Mathf.Abs(moveDirection.x) > 0.1f || Mathf.Abs(moveDirection.z) > 0.1f)
            {
                timer += Time.deltaTime * (IsSprinting ? sprintBobSpeed : walkBobSpeed);
                cameraComp.transform.localPosition = new Vector3(
                    cameraComp.transform.localPosition.x,
                    defaultYPos + Mathf.Sin(timer) * (IsSprinting ? sprintBobAmount : walkBobAmount),
                    cameraComp.transform.localPosition.z);
            }
        }
    }

    private void HandleFootsteps()
    {
        if (!IsGrounded)
        {
            return;
        }

        if (characterController.velocity == Vector3.zero)
        {
            return;
        }

        footstepTimer -= Time.deltaTime;

        if (footstepTimer <= 0)
        {
            if (Physics.Raycast(transform.position, Vector3.down, out RaycastHit hit, 5f))
            {
                footstepAudioSource.pitch = UnityEngine.Random.Range(1f, 1.5f);

                switch (hit.collider.tag)
                {
                    default:
                        footstepAudioSource.PlayOneShot(DefaultClips[UnityEngine.Random.Range(0, DefaultClips.Length - 1)]);
                        break;
                }
            }

            if (characterController.velocity.magnitude > 0f)
            {
                HeatAudioSource.pitch = UnityEngine.Random.Range(1f, 1.5f);
            }

            if (currentHeat >= 75f && currentHeat < 100f)
            {
                HeatAudioSource.PlayOneShot(SunburnClips[0]);
            }
            else if (currentHeat >= 50f && currentHeat < 75f)
            {
                HeatAudioSource.PlayOneShot(SunburnClips[1]);
            }
            else if (currentHeat >= 50f && currentHeat < 75f)
            {
                HeatAudioSource.PlayOneShot(SunburnClips[2]);
            }
            else if (currentHeat >= 25f && currentHeat < 50f)
            {
                HeatAudioSource.PlayOneShot(SunburnClips[3]);
            }
            else if (currentHeat > 0f && currentHeat < 25f)
            {
                HeatAudioSource.PlayOneShot(SunburnClips[4]);
            }
            else if (currentHeat == 0f)
            {
                HeatAudioSource.PlayOneShot(SunburnClips[5]);
            }

            footstepTimer = GetCurrentOffSet;
        }
    }

    private void HandleStamina()
    {
        if (IsSprinting)
        {
            if (regeneratingStamina != null)
            {
                StopCoroutine(regeneratingStamina);
                regeneratingStamina = null;
            }

            currentStamina -= sprintingStaminaUseMultiplier * Time.deltaTime;

            if (currentStamina < 0)
            {
                currentStamina = 0;
            }

            OnStaminaChange?.Invoke(this, currentStamina);

            if (currentStamina <= 0)
            {
                canUseStamina = false;
            }
        }

        if (!IsSprinting)
        {
            if (currentStamina < maxStamina && regeneratingStamina == null)
            {
                regeneratingStamina = StartCoroutine(RegenerateStamina());
            }
        }
    }

    private void HandleHeat()
    {
        if (Physics.Raycast(cameraComp.transform.position, Vector3.up, out RaycastHit hit, 50f))
        {
            if (hit.collider.CompareTag("Tree"))
            {
                inShadow = true;
            }
        }
        else
        {
            inShadow = false;
        }

        if (inShadow == false)
        {
            if (regeneratingHeat != null)
            {
                StopCoroutine(regeneratingHeat);
                regeneratingHeat = null;
            }

            currentHeat -= HeatUseMultiplier * Time.deltaTime;

            if (currentHeat < 0)
            {
                currentHeat = 0;
            }

            OnHeatChange?.Invoke(currentHeat);
        }

        if (inShadow == true)
        {
            regeneratingHeat = StartCoroutine(RegeneratingHeat());
        }
    }

    private bool IsSliding
    {
        get
        {
            if (IsGrounded && Physics.Raycast(transform.position, Vector3.down, out RaycastHit slopeHit, 2f))
            {
                hitPointNormal = slopeHit.normal;
                return Vector3.Angle(hitPointNormal, Vector3.up) > characterController.slopeLimit;
            }
            else
            {
                return false;
            }
        }
    }

    private IEnumerator RegenerateStamina()
    {
        yield return new WaitForSeconds(timeBeforeStaminaRegenStarts);
        WaitForSeconds timeToWait = new WaitForSeconds(staminaTimeIncrement);

        while (currentStamina < maxStamina)
        {
            if (currentStamina > 0)
            {
                canUseStamina = true;
            }

            currentStamina += staminaValueIncrement;

            if (currentStamina > maxStamina)
            {
                currentStamina = maxStamina;
            }

            OnStaminaChange?.Invoke(this, currentStamina);

            yield return timeToWait;
        }

        regeneratingStamina = null;
    }

    private IEnumerator RegeneratingHeat()
    {
        yield return new WaitForSeconds(timeBeforeHeatRegenStarts);
        WaitForSeconds timeToWait = new WaitForSeconds(HeatTimeIncrement);

        while (currentHeat < maxHeat)
        {
            if (inShadow == false)
            {
                break;
            }

            currentHeat += HeatValueIncrement;

            if (currentHeat > maxHeat)
            {
                currentHeat = maxHeat;
            }

            OnHeatChange?.Invoke(currentHeat);

            yield return timeToWait;
        }

        regeneratingHeat = null;
    }

    public void OnTriggerEnter(Collider other)
    {
        if (other.gameObject.CompareTag("Oasis"))
        {
            GameManager.GameManagerInstance.GameWin = true;
        }
    }
}