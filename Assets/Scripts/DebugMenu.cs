using TMPro;
using UnityEngine;

#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
using UnityEngine.UI;
#endif

public class DebugMenu : MonoBehaviour
{
    [Header("Input")]
#if ENABLE_INPUT_SYSTEM
    public Key resetKey = Key.R;
    public Key debugKey = Key.D;
#else
    public KeyCode resetKey = KeyCode.R;
    public KeyCode debugKey = KeyCode.D;
#endif
    private bool _pressedDebug;
    private bool _pressedReset;

    [Header("Managers")]
    [SerializeField] private BallSpawner ballManager;
    [SerializeField] private GameRulesManager gameManager;

    [Header("Text")]
    [SerializeField] private TextMeshProUGUI debugText;


    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        debugText = GetComponent<TextMeshProUGUI>();
    }

    // Update is called once per frame
    void Update()
    {
        //set debugtext to appropriate values
        debugText.text = @$"Debug Menu:
    speed: {ballManager.ActiveBall.GetComponent<Rigidbody>().linearVelocity.magnitude}
    xPos: {ballManager.ActiveBall.GetComponent<Transform>().position.x}
    yPos: {ballManager.ActiveBall.GetComponent<Transform>().position.y}
    zPos: {ballManager.ActiveBall.GetComponent<Transform>().position.z}";

        //enable or disable debugtext on debug key press
#if ENABLE_INPUT_SYSTEM
        var kb = Keyboard.current;
        _pressedDebug = kb != null && kb[debugKey].wasPressedThisFrame;
#else
        _pressedDebug = Input.GetKey(debugKey);
#endif
        if (_pressedDebug)
        {
            debugText.enabled = !debugText.enabled;
        }

        //reset the balls
#if ENABLE_INPUT_SYSTEM
        _pressedReset = kb != null && kb[resetKey].wasPressedThisFrame;
#else
        _pressedReset = Input.GetKey(resetKey);
#endif
        if (_pressedReset)
        {
            gameManager.StartRun();
        }
    }
}
