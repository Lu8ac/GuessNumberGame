using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// GuessTheNumber — Dual Dice Edition with Continuous Idle Tumble
///
/// Dice behaviour:
///   IDLE    → both dice tumble chaotically on random multi-axes, looping forever
///   SUBMIT  → tumble speed ramps DOWN over ~1.5s, lands on target face
///   WIN     → face tinted gold,  pip shows correct numbers
///   LOSE    → face tinted red,   pip shows the answer
///   RESET   → dice snap back to idle tumble immediately
///
/// SCENE SETUP
/// ───────────
/// Canvas
///   ├─ BackgroundPanel   Image  #1a237e  stretch/stretch
///   ├─ HeaderText        TextMeshProUGUI       → headerText
///   ├─ GuessInputField   TMP_InputField        → guessInput
///   ├─ SubmitButton      Button → SubmitGuess  → submitButton
///   ├─ ResetButton       Button → GameSetup    → resetButton
///   └─ CatImage          Image                 → catImage
///
/// 3-D Scene
///   ├─ DiceA   Cube  position (-1.5, 0, 5)     → diceA
///   └─ DiceB   Cube  position ( 1.5, 0, 5)     → diceB
///
/// Also attach DiceFaceTextureGenerator.cs to GameManager.
/// </summary>
public class GuessTheNumber : MonoBehaviour
{
    // ── Inspector: UI ──────────────────────────────────────────────────────
    [Header("UI References")]
    public TextMeshProUGUI headerText;
    public TMP_InputField  guessInput;
    public Button          submitButton;
    public Button          resetButton;
    public Image           catImage;

    [Header("Cat Sprites")]
    public Sprite neutralCatSprite;
    public Sprite happyCatSprite;
    public Sprite sadCatSprite;

    // ── Inspector: Dice ────────────────────────────────────────────────────
    [Header("Dice GameObjects")]
    public GameObject diceA;
    public GameObject diceB;

    [Header("Pip Textures  (index 0 = face‑1 … index 5 = face‑6)")]
    [Tooltip("Leave empty — DiceFaceTextureGenerator fills these automatically")]
    public Texture2D[] pipTextures = new Texture2D[6];

    [Header("Dice Materials")]
    public Material diceDefaultMaterial;
    public Material diceWinMaterial;
    public Material diceLoseMaterial;

    // ── Tuning knobs ───────────────────────────────────────────────────────
    [Header("Animation Tuning")]
    [Tooltip("Rotation speed (deg/s) while idle tumbling")]
    public float idleSpeed = 280f;

    [Tooltip("How fast the idle tumble axis drifts — higher = more chaotic")]
    public float axisDriftSpeed = 0.6f;

    [Tooltip("Seconds the dice take to slow down and land after Submit")]
    public float landDuration = 1.6f;

    // ── Constants ──────────────────────────────────────────────────────────
    private const int MinDie      = 1;
    private const int MaxDie      = 6;
    private const int MinSum      = 2;
    private const int MaxSum      = 12;
    private const int MaxAttempts = 3;

    // Face‑up Euler angles: FaceUp[n] orients the cube so face n points upward.
    // Adjust if your pip textures appear on wrong faces.
    private static readonly Quaternion[] FaceUp = new Quaternion[7]
    {
        Quaternion.identity,
        Quaternion.Euler(  0,   0,   0),   // 1
        Quaternion.Euler(  0,   0,  90),   // 2
        Quaternion.Euler(  0,   0, -90),   // 3
        Quaternion.Euler(-90,   0,   0),   // 4
        Quaternion.Euler( 90,   0,   0),   // 5
        Quaternion.Euler(180,   0,   0),   // 6
    };

    // ── Runtime state ──────────────────────────────────────────────────────
    private int  secretNumber;
    private int  attemptsRemaining;
    private bool gameOver;

    // Per-die tumble state (updated each frame in Update)
    private DieState stateA = new DieState();
    private DieState stateB = new DieState();

    // Coroutine handles
    private Coroutine landCoroutineA;
    private Coroutine landCoroutineB;

    // Pip materials built at runtime
    private Material[] pipMaterials;

    // ── Helper class (reference type so lambdas can capture it directly) ───
    private class DieState
    {
        public bool    isLanding;    // true while the landing coroutine runs
        public bool    hasLanded;    // true once fully landed (game over state)
        public Vector3 tumbleAxis;   // current rotation axis
        public Vector3 axisTarget;   // axis we are drifting toward
        public float   axisTimer;    // counts down to next axis change
    }

    // ──────────────────────────────────────────────────────────────────────
    // Unity lifecycle
    // ──────────────────────────────────────────────────────────────────────

    private void Start()
    {
        if (guessInput != null)
            guessInput.contentType = TMP_InputField.ContentType.IntegerNumber;

        BuildPipMaterials();
        GameSetup();
    }

    private void Update()
    {
        // Drive idle tumble every frame for both dice
        if (!stateA.hasLanded && !stateA.isLanding && diceA != null)
            TumbleIdle(diceA, stateA);

        if (!stateB.hasLanded && !stateB.isLanding && diceB != null)
            TumbleIdle(diceB, stateB);
    }

    private void OnDestroy()
    {
        if (pipMaterials != null)
            foreach (var m in pipMaterials)
                if (m != null) Destroy(m);
    }

    // ──────────────────────────────────────────────────────────────────────
    // PUBLIC METHODS
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Resets all state and starts idle tumble on both dice.
    /// Called on Start and by the Reset button.
    /// </summary>
    public void GameSetup()
    {
        secretNumber      = Random.Range(MinSum, MaxSum + 1);
        attemptsRemaining = MaxAttempts;
        gameOver          = false;

        UpdateHeader($"I'm thinking of a number between {MinSum} and {MaxSum}.\n" +
                     $"You have {attemptsRemaining} attempts to guess it...");
        ClearInputField();
        SetCatSprite(neutralCatSprite);
        SetSubmitInteractable(true);

        // Stop any landing coroutines and restart idle tumble
        StopLandingCoroutines();
        InitDieState(stateA, diceA);
        InitDieState(stateB, diceB);

        Debug.Log($"[GuessTheNumber] New game. Secret = {secretNumber}");
    }

    /// <summary>
    /// Validates input and evaluates the guess.
    /// Called by the Submit button.
    /// </summary>
    public void SubmitGuess()
    {
        if (gameOver)
        {
            FlashHeader("Game over! Press Reset Game to play again.");
            return;
        }

        string raw = guessInput.text.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            FlashHeader("Please enter a number first!");
            return;
        }

        if (!int.TryParse(raw, out int playerGuess))
        {
            FlashHeader("That's not a valid number. Try again!");
            ClearInputField();
            return;
        }

        if (playerGuess < MinSum || playerGuess > MaxSum)
        {
            FlashHeader($"Enter a number between {MinSum} and {MaxSum}.");
            ClearInputField();
            return;
        }

        if (playerGuess == secretNumber)
            HandleWin();
        else
            HandleWrongGuess(playerGuess);
    }

    // ──────────────────────────────────────────────────────────────────────
    // PRIVATE — Game logic
    // ──────────────────────────────────────────────────────────────────────

    private void HandleWin()
    {
        gameOver = true;
        PickWinningFaces(secretNumber, out int fA, out int fB);

        UpdateHeader($"You won!  {fA} + {fB} = {secretNumber}!");
        SetCatSprite(happyCatSprite);
        SetSubmitInteractable(false);

        BeginLanding(diceA, fA, true,  ref landCoroutineA, stateA);
        BeginLanding(diceB, fB, true,  ref landCoroutineB, stateB);

        Debug.Log($"[GuessTheNumber] WIN  {fA}+{fB}={secretNumber}");
    }

    private void HandleWrongGuess(int playerGuess)
    {
        attemptsRemaining--;

        if (attemptsRemaining <= 0)
        {
            gameOver = true;
            PickWinningFaces(secretNumber, out int fA, out int fB);

            UpdateHeader($"You lose!  The answer was {fA} + {fB} = {secretNumber}.\nBetter luck next time!");
            SetCatSprite(sadCatSprite);
            SetSubmitInteractable(false);

            BeginLanding(diceA, fA, false, ref landCoroutineA, stateA);
            BeginLanding(diceB, fB, false, ref landCoroutineB, stateB);

            Debug.Log($"[GuessTheNumber] LOSE  answer was {secretNumber}");
        }
        else
        {
            string hint = playerGuess < secretNumber ? "Too low!" : "Too high!";
            UpdateHeader($"Incorrect!  {hint}\n" +
                         $"You have {attemptsRemaining} attempt{(attemptsRemaining == 1 ? "" : "s")} remaining.");
            ClearInputField();
            // Dice keep tumbling — no change needed
            Debug.Log($"[GuessTheNumber] Wrong {playerGuess}. Left: {attemptsRemaining}");
        }
    }

    // ──────────────────────────────────────────────────────────────────────
    // PRIVATE — Dice tumble (runs in Update)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Initialises a die's tumble state and resets its GameObject.
    /// </summary>
    private void InitDieState(DieState s, GameObject die)
    {
        s.isLanding  = false;
        s.hasLanded  = false;
        s.tumbleAxis = RandomTumbleAxis();
        s.axisTarget = RandomTumbleAxis();
        s.axisTimer  = Random.Range(0.4f, 1.2f);

        if (die != null)
        {
            die.transform.rotation = Random.rotation;
            var rend = die.GetComponent<Renderer>();
            if (rend != null)
            {
                int randomFace = Random.Range(0, 6);
                if (pipMaterials != null && randomFace < pipMaterials.Length && pipMaterials[randomFace] != null)
                    rend.material = pipMaterials[randomFace];
                else if (diceDefaultMaterial != null)
                    rend.material = diceDefaultMaterial;
            }
        }
    }

    /// <summary>
    /// Called from Update — rotates the die on a slowly drifting random axis,
    /// producing the chaotic tumble feel.
    /// </summary>
    private void TumbleIdle(GameObject die, DieState s)
    {
        // Drift the axis toward the target over time
        s.axisTimer -= Time.deltaTime;
        if (s.axisTimer <= 0f)
        {
            s.axisTarget = RandomTumbleAxis();
            s.axisTimer  = Random.Range(0.5f, 1.5f);
        }

        s.tumbleAxis = Vector3.Slerp(s.tumbleAxis, s.axisTarget,
                                     axisDriftSpeed * Time.deltaTime).normalized;

        die.transform.Rotate(s.tumbleAxis, idleSpeed * Time.deltaTime, Space.World);
    }

    /// <summary>
    /// Returns a random axis biased toward multi-axis tumble (no pure Y spins).
    /// </summary>
    private Vector3 RandomTumbleAxis()
    {
        return new Vector3(
            Random.Range(0.3f, 1f)  * (Random.value > 0.5f ? 1 : -1),
            Random.Range(0.2f, 0.8f) * (Random.value > 0.5f ? 1 : -1),
            Random.Range(0.1f, 0.6f) * (Random.value > 0.5f ? 1 : -1)
        ).normalized;
    }

    // ──────────────────────────────────────────────────────────────────────
    // PRIVATE — Landing sequence
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Marks a die as landing and starts the landing coroutine.
    /// </summary>
    private void BeginLanding(
        GameObject die, int targetFace, bool isWin,
        ref Coroutine coroutine, DieState state)
    {
        if (die == null) return;
        if (coroutine != null) StopCoroutine(coroutine);
        state.isLanding = true;
        coroutine = StartCoroutine(LandingSequence(die, targetFace, isWin,
                                                    () => state.isLanding = false,
                                                    () => state.hasLanded = true));
    }

    /// <summary>
    /// Landing coroutine:
    ///   Phase 1 — decelerate from current rotation toward target face (ease-out cubic)
    ///   Phase 2 — apply pip texture and result color
    ///   Phase 3 — tiny bounce settle
    /// </summary>
    private IEnumerator LandingSequence(
        GameObject die, int targetFace, bool isWin,
        System.Action onLandingDone,
        System.Action onSettled)
    {
        Quaternion startRot  = die.transform.rotation;
        Quaternion targetRot = FaceUp[Mathf.Clamp(targetFace, 1, 6)];

        float elapsed = 0f;

        while (elapsed < landDuration)
        {
            float t     = elapsed / landDuration;
            float eased = 1f - Mathf.Pow(1f - t, 3f);   // cubic ease-out
            die.transform.rotation = Quaternion.Slerp(startRot, targetRot, eased);
            elapsed += Time.deltaTime;
            yield return null;
        }

        die.transform.rotation = targetRot;

        // Small bounce: overshoot 8° then settle
        float bounceTime    = 0f;
        float bounceDur     = 0.18f;
        float bounceAngle   = 8f;
        Vector3 bounceAxis  = die.transform.right;

        while (bounceTime < bounceDur)
        {
            float t       = bounceTime / bounceDur;
            float offset  = Mathf.Sin(t * Mathf.PI) * bounceAngle * (1f - t);
            die.transform.rotation = targetRot * Quaternion.AngleAxis(offset, bounceAxis);
            bounceTime += Time.deltaTime;
            yield return null;
        }

        die.transform.rotation = targetRot;

        // Apply pip + color
        ApplyPipMaterial(die, targetFace, isWin);

        onLandingDone?.Invoke();
        onSettled?.Invoke();
    }

    // ──────────────────────────────────────────────────────────────────────
    // PRIVATE — Material helpers
    // ──────────────────────────────────────────────────────────────────────

    private void ApplyPipMaterial(GameObject die, int faceValue, bool isWin)
    {
        var rend = die?.GetComponent<Renderer>();
        if (rend == null) return;

        int idx = Mathf.Clamp(faceValue - 1, 0, 5);
        Material source = isWin ? diceWinMaterial : diceLoseMaterial;

        if (pipMaterials != null && idx < pipMaterials.Length && pipMaterials[idx] != null && source != null)
        {
            // Copy win/lose material entirely to keep metallic/smoothness settings,
            // then overlay the pip texture on top
            Material m = new Material(source);
            m.mainTexture = pipMaterials[idx].mainTexture;
            rend.material = m;
        }
        else
        {
            rend.material = source != null ? source : (isWin ? diceWinMaterial : diceLoseMaterial);
        }
    }

    /// <summary>Builds runtime materials from assigned pip textures.</summary>
    public void BuildPipMaterials()
    {
        // Base pip materials off diceDefaultMaterial for idle tumble appearance
        pipMaterials = new Material[6];
        for (int i = 0; i < 6; i++)
        {
            if (pipTextures != null && i < pipTextures.Length && pipTextures[i] != null)
            {
                Material baseMat = diceDefaultMaterial != null ? diceDefaultMaterial
                    : new Material(Shader.Find("Universal Render Pipeline/Lit"));
                pipMaterials[i]             = new Material(baseMat);
                pipMaterials[i].mainTexture = pipTextures[i];
            }
        }
    }

    private void StopLandingCoroutines()
    {
        if (landCoroutineA != null) { StopCoroutine(landCoroutineA); landCoroutineA = null; }
        if (landCoroutineB != null) { StopCoroutine(landCoroutineB); landCoroutineB = null; }
    }

    // ──────────────────────────────────────────────────────────────────────
    // PRIVATE — Dice combination logic
    // ──────────────────────────────────────────────────────────────────────

    private void PickWinningFaces(int target, out int faceA, out int faceB)
    {
        var pairs = new List<(int, int)>();
        for (int a = MinDie; a <= MaxDie; a++)
        {
            int b = target - a;
            if (b >= MinDie && b <= MaxDie)
                pairs.Add((a, b));
        }
        var pick = pairs[Random.Range(0, pairs.Count)];
        faceA = pick.Item1;
        faceB = pick.Item2;
    }

    // ──────────────────────────────────────────────────────────────────────
    // PRIVATE — UI helpers
    // ──────────────────────────────────────────────────────────────────────

    private void UpdateHeader(string msg)
    {
        if (headerText != null) headerText.text = msg;
    }

    private void FlashHeader(string warning)
    {
        if (headerText != null) StartCoroutine(FlashCoroutine(warning));
    }

    private IEnumerator FlashCoroutine(string warning)
    {
        string saved = headerText.text;
        headerText.text = warning;
        yield return new WaitForSeconds(1.5f);
        if (!gameOver) headerText.text = saved;
    }

    private void ClearInputField()
    {
        if (guessInput == null) return;
        guessInput.text = string.Empty;
        guessInput.ActivateInputField();
    }

    private void SetSubmitInteractable(bool value)
    {
        if (submitButton != null) submitButton.interactable = value;
    }

    private void SetCatSprite(Sprite sprite)
    {
        if (catImage == null || sprite == null) return;
        catImage.sprite  = sprite;
        catImage.enabled = true;
    }
}
