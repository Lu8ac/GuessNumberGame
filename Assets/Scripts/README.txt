================================================
  GUESS THE NUMBER — v3  Continuous Idle Tumble
================================================

FILES
-----
  GuessTheNumber.cs           Main game script
  DiceFaceTextureGenerator.cs Auto-generates pip textures at runtime

WHAT'S NEW IN V3
-----------------
  Dice now tumble CONTINUOUSLY from the moment the game starts.
  They do NOT stop until the player clicks Submit.

  IDLE    Both dice spin chaotically on randomised multi-axes.
          The axis slowly drifts over time so it never looks looped.
  SUBMIT  The tumble eases out (cubic) over ~1.6 seconds and lands
          on the correct face.
  WIN     Faces tinted gold, pips show the correct numbers.
  LOSE    Faces tinted red,  pips show the answer.
  WRONG   Dice keep tumbling — no interruption.
  RESET   Dice immediately snap back to idle chaotic tumble.

ANIMATION TUNING (on GameManager Inspector)
--------------------------------------------
  Idle Speed          280   deg/s while tumbling  (raise = faster spin)
  Axis Drift Speed    0.6   how chaotic the axis changes feel
  Land Duration       1.6   seconds to decelerate and land on Submit

================================================================
SCENE SETUP  (step by step)
================================================================

1. TWO DICE CUBES
   Hierarchy → right-click → 3D Object → Cube → rename "DiceA"
   Repeat for "DiceB"
   Suggested positions:
     DiceA:  X = -1.5,  Y = 0,  Z = 5
     DiceB:  X =  1.5,  Y = 0,  Z = 5

2. CANVAS UI
   Canvas (Screen Space – Overlay)
     BackgroundPanel   Image  color #1a237e  anchor stretch/stretch  all offsets 0
     HeaderText        TextMeshProUGUI
     GuessInputField   TMP_InputField
     SubmitButton      Button   OnClick → GuessTheNumber.SubmitGuess()
     ResetButton       Button   OnClick → GuessTheNumber.GameSetup()
     CatImage          Image

3. GAME MANAGER OBJECT
   Hierarchy → Create Empty → rename "GameManager"
   Add Component → GuessTheNumber
   Add Component → DiceFaceTextureGenerator
   (both scripts on the same object)

4. INSPECTOR WIRING  (select GameManager)

   GuessTheNumber
     Header Text           → HeaderText
     Guess Input           → GuessInputField
     Submit Button         → SubmitButton
     Reset Button          → ResetButton
     Cat Image             → CatImage
     Neutral Cat Sprite    → your neutral cat PNG
     Happy Cat Sprite      → your happy  cat PNG
     Sad Cat Sprite        → your sad    cat PNG
     Dice A                → DiceA
     Dice B                → DiceB
     Pip Textures [0..5]   LEAVE EMPTY  (auto-filled by generator)
     Dice Default Material → DiceDefault  (#CCCCCC)
     Dice Win Material     → DiceWin      (#FFD700  Metallic 0.6  Smooth 0.8)
     Dice Lose Material    → DiceLose     (#CC2222)

   DiceFaceTextureGenerator
     Texture Size          256   (use 512 for sharper dots on large screens)
     Face Color            White
     Pip Color             Black
     Pip Radius Fraction   0.09

5. BUTTON OnClick events
   SubmitButton → OnClick (+) → GameManager → GuessTheNumber.SubmitGuess()
   ResetButton  → OnClick (+) → GameManager → GuessTheNumber.GameSetup()

================================================================
MATERIALS
================================================================
  Create → Material for each, set in Inspector:

  DiceDefault   Base Map #CCCCCC   Metallic 0    Smoothness 0.5
  DiceWin       Base Map #FFD700   Metallic 0.6  Smoothness 0.8
  DiceLose      Base Map #CC2222   Metallic 0    Smoothness 0.3

================================================================
GAME RULES
================================================================
  Secret number range : 2 – 12  (sum of two classic 1–6 dice)
  Attempts            : 3
  Hint on wrong guess : "Too low!" or "Too high!"
  On win              : dice land showing the two face numbers that add up to the secret
  On lose             : dice reveal the answer faces in red

================================================================
DEFENSES
================================================================
  Empty input           → flashed warning, no attempt used
  Non-numeric input     → rejected, field cleared
  Out of range (< 2 or > 12) → rejected
  Submit after game ends → blocked until Reset
  Coroutine conflicts   → landing coroutines stopped before new ones start
  Memory                → runtime pip materials destroyed on component teardown

================================================
