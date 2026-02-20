using UnityEngine;

/// <summary>
/// Drop target: on ball hit, moves down on Y by a fixed amount with smooth Lerp. When fully down,
/// you choose one: disable collider, swap to a flat collider, or use non-bouncy physics material.
/// Put this on the same GameObject that has the Collider the ball hits.
/// </summary>
public class DropTarget : MonoBehaviour
{
    public enum WhenFullyDownMode
    {
        DisableCollider,
        SwapToFlatCollider,
        NonBouncyMaterial
    }

    [Header("On Ball Hit — Drop It")]
    [Tooltip("Units to move down (world space). Set in Inspector per target. Movement uses Lerp for smooth animation.")]
    [SerializeField] private float unitsDropped = 0.5f;
    [Tooltip("Duration of the sink animation in seconds.")]
    [SerializeField] private float fallDuration = 0.4f;
    [Tooltip("Inclined board. If set, sink follows this transform's local down instead of world Y.")]
    [SerializeField] private Transform boardForSlope;

    [Header("Reset")]
    [Tooltip("Seconds after fully down before the target returns to its original position. 0 = never reset.")]
    [SerializeField] private float resetDelay = 10f;
    [Tooltip("How long the rise animation takes when the target comes back up (seconds).")]
    [SerializeField] [Range(0.05f, 2f)] private float returnDuration = 0.3f;

    [Header("When Fully Down")]
    [Tooltip("Disable collider = ball passes through. Swap to flat = use a separate flat collider. Non-bouncy = ball rolls over with no bounce.")]
    [SerializeField] private WhenFullyDownMode whenFullyDown = WhenFullyDownMode.NonBouncyMaterial;
    [Tooltip("For Swap To Flat Collider: assign a flat collider (e.g. child) to enable when down; main collider is disabled.")]
    [SerializeField] private Collider flatColliderWhenDown;
    [Tooltip("For Non-Bouncy Material: assign the board/floor physics material so the ball rolls without bouncing.")]
    [SerializeField] private PhysicsMaterial nonBouncyMaterial;

    private Vector3 _startPosition;
    private float _fallTimer;
    private bool _falling;
    private bool _hasTriggered;
    private Rigidbody _rb;
    private Collider _mainCollider;

    private float _resetTimer;
    private bool _returning;
    private Vector3 _returnStartPos;
    private float _returnTimer;
    private PhysicsMaterial _originalMaterial;

    private void Awake()
    {
        _rb = GetComponent<Rigidbody>();
        _mainCollider = GetComponent<Collider>();
    }

    private Vector3 GetTargetPosition()
    {
        if (boardForSlope != null)
        {
            Vector3 downSlope = boardForSlope.TransformDirection(Vector3.down).normalized;
            return _startPosition + downSlope * unitsDropped;
        }
        return _startPosition + Vector3.down * unitsDropped;
    }

    private void Update()
    {
        if (_falling)
        {
            _fallTimer += Time.deltaTime;
            float rawT = Mathf.Clamp01(_fallTimer / fallDuration);
            float t = rawT * rawT; // ease-in for smooth, predictable drop

            Vector3 targetPos = GetTargetPosition();
            Vector3 newPos = Vector3.Lerp(_startPosition, targetPos, t);

            if (_rb != null)
                _rb.position = newPos;
            else
                transform.position = newPos;

            if (rawT >= 1f)
            {
                _falling = false;
                if (_rb != null)
                    _rb.position = targetPos;
                else
                    transform.position = targetPos;
                ApplyWhenFullyDown();
                if (resetDelay > 0f)
                    _resetTimer = resetDelay;
            }
            return;
        }

        if (_returning)
        {
            _returnTimer += Time.deltaTime;
            float rawT = Mathf.Clamp01(_returnTimer / returnDuration);
            float t = rawT * rawT; // ease-in for smooth rise

            Vector3 newPos = Vector3.Lerp(_returnStartPos, _startPosition, t);
            if (_rb != null)
                _rb.position = newPos;
            else
                transform.position = newPos;

            if (rawT >= 1f)
            {
                _returning = false;
                if (_rb != null)
                    _rb.position = _startPosition;
                else
                    transform.position = _startPosition;
                FinishReturn();
            }
            return;
        }

        if (_hasTriggered && resetDelay > 0f && _resetTimer > 0f)
        {
            _resetTimer -= Time.deltaTime;
            if (_resetTimer <= 0f)
            {
                _returning = true;
                _returnStartPos = GetCurrentPosition();
                _returnTimer = 0f;
            }
        }
    }

    private void ApplyWhenFullyDown()
    {
        switch (whenFullyDown)
        {
            case WhenFullyDownMode.DisableCollider:
                if (_mainCollider != null)
                    _mainCollider.enabled = false;
                break;
            case WhenFullyDownMode.SwapToFlatCollider:
                if (_mainCollider != null)
                    _mainCollider.enabled = false;
                if (flatColliderWhenDown != null)
                    flatColliderWhenDown.enabled = true;
                break;
            case WhenFullyDownMode.NonBouncyMaterial:
                if (_mainCollider != null)
                {
                    _originalMaterial = _mainCollider.sharedMaterial;
                    if (nonBouncyMaterial != null)
                        _mainCollider.sharedMaterial = nonBouncyMaterial;
                }
                break;
        }
        DisableAwardComponents();
    }

    private void FinishReturn()
    {
        switch (whenFullyDown)
        {
            case WhenFullyDownMode.DisableCollider:
                if (_mainCollider != null)
                    _mainCollider.enabled = true;
                break;
            case WhenFullyDownMode.SwapToFlatCollider:
                if (flatColliderWhenDown != null)
                    flatColliderWhenDown.enabled = false;
                if (_mainCollider != null)
                    _mainCollider.enabled = true;
                break;
            case WhenFullyDownMode.NonBouncyMaterial:
                if (_mainCollider != null && _originalMaterial != null)
                    _mainCollider.sharedMaterial = _originalMaterial;
                break;
        }
        _hasTriggered = false;
        EnableAwardComponents();
    }

    private void EnableAwardComponents()
    {
        /*
        var pointAdder = GetComponent<PointAdder>();
        if (pointAdder != null) pointAdder.enabled = true;
        */

        var coinAdder = GetComponent<CoinAdder>();
        if (coinAdder != null) coinAdder.enabled = true;

        var multAdder = GetComponent<MultAdder>();
        if (multAdder != null) multAdder.enabled = true;
    }

    private void DisableAwardComponents()
    {
        /*
        var pointAdder = GetComponent<PointAdder>();
        if (pointAdder != null) pointAdder.enabled = false;
        */

        var coinAdder = GetComponent<CoinAdder>();
        if (coinAdder != null) coinAdder.enabled = false;

        var multAdder = GetComponent<MultAdder>();
        if (multAdder != null) multAdder.enabled = false;
    }

    private Vector3 GetCurrentPosition()
    {
        return _rb != null ? _rb.position : transform.position;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (_hasTriggered) return;
        if (!collision.collider.CompareTag("Ball")) return;

        _hasTriggered = true;
        DisableAwardComponents();
        _startPosition = GetCurrentPosition();
        _falling = true;
        _fallTimer = 0f;
    }

    private void OnTriggerEnter(Collider col)
    {
        if (_hasTriggered) return;
        if (!col.CompareTag("Ball")) return;

        _hasTriggered = true;
        DisableAwardComponents();
        _startPosition = GetCurrentPosition();
        _falling = true;
        _fallTimer = 0f;
    }
}
