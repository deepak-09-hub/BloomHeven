using DG.Tweening;
using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class LevelManager : MonoBehaviour
{
    public static LevelManager Instance { get; private set; }

    private const int BallsPerTube = 4;
    private const int EmptyTubeCount = 3;

    private const string CurrentLevelKey = "BallSort_CurrentLevel";
    private const string SoundKey = "BallSort_Sound";
    private const string MusicKey = "BallSort_Music";
    private const string AddedTubeKeyPrefix = "BallSort_AddedTube_";
    private const string LevelStateKeyPrefix = "BallSort_LevelState_";
    private const string RevealBallsKeyPrefix = "BallSort_RevealBalls_";
    private const string AddTubePowerUpCountKey = "BallSort_AddTubePowerUpCount";
    private const string RevealBallsPowerUpCountKey = "BallSort_RevealBallsPowerUpCount";

    private const int AddTubeRewardInterval = 20;
    private const int RevealBallsRewardInterval = 10;

    [Header("Levels")]
    [Tooltip("Optional handmade levels. After these are completed, endless procedural levels are generated automatically.")]
    [SerializeField] private LevelData[] levels;

    [Header("Endless Procedural Levels")]
    [Tooltip("Mystery sprite used by automatically generated levels.")]
    [SerializeField] private Sprite proceduralMysteryBallSprite;

    [Tooltip("Base seed keeps each procedural level identical when it is reopened.")]
    [SerializeField] private int proceduralBaseSeed = 24680;

    [Tooltip("How many levels pass before another color slot is introduced, up to seven.")]
    [Min(1)]
    [SerializeField] private int levelsPerColorIncrease = 4;

    [Tooltip("First procedural level that may contain mystery tubes.")]
    [Min(1)]
    [SerializeField] private int mysteryStartsAtLevel = 5;

    [Tooltip("Mystery levels repeat at this interval. Set to 1 to allow them on every level after the starting level.")]
    [Min(1)]
    [SerializeField] private int mysteryLevelInterval = 3;

    [Header("Prefabs")]
    [SerializeField] private Tube tubePrefab;
    [SerializeField] private Tube tempLastTubePrefab;

    [Header("Hierarchy")]
    [SerializeField] private Transform tubeParent;

    [Header("Layout")]
    [SerializeField] private float tubeSpacing = 2.5f;
    [SerializeField] private float rowSpacing = 4f;

    [Header("Ball Prefabs")]
    [SerializeField] private Ball redBallPrefab;
    [SerializeField] private Ball greenBallPrefab;
    [SerializeField] private Ball blueBallPrefab;
    [SerializeField] private Ball yellowBallPrefab;
    [SerializeField] private Ball lightBlueBallPrefab;
    [SerializeField] private Ball pinkBallPrefab;
    [SerializeField] private Ball orangeBallPrefab;
    [SerializeField] private Ball blackBallPrefab;
    [SerializeField] private Ball grayBallPrefab;
    [SerializeField] private Ball neonPinkBallPrefab;
    [SerializeField] private Ball neonGreenBallPrefab;
    [SerializeField] private Ball neonBlueBallPrefab;
    [SerializeField] private Ball purpleBallPrefab;

    [Header("UI")]
    [SerializeField] private GameObject startPanal;
    [SerializeField] private GameObject Title;
    [SerializeField] private GameObject gameplayPanal;
    [SerializeField] private GameObject settingsPanal;
    [SerializeField] private Button play;
    [SerializeField] private Button next;
    [SerializeField] private Button home;
    [SerializeField] private Button exit;
    [SerializeField] private Button settings;
    [SerializeField] private Button sound;
    [SerializeField] private Button music;
    [SerializeField] private Button closeSettings;
    [SerializeField] private Button addTube;
    [SerializeField] private Button revealBalls;
    [SerializeField] private TMP_Text addTubePowerUpCountText;
    [SerializeField] private TMP_Text revealBallsPowerUpCountText;
    [SerializeField] private TMP_Text levelNumberText;

    [Header("Next Level Panel")]
    [SerializeField] private GameObject nextlevelPanal;
    [SerializeField] private GameObject logo;
    [SerializeField] private GameObject star;
    [SerializeField] private GameObject starParticles;
    [SerializeField] private GameObject[] nextLevelParicles;
    private bool isPlayingParticles = false;

    private readonly List<Tube> spawnedTubes = new List<Tube>();

    private Transform generatedLevelRoot;
    private int currentLevelIndex;
    private int pendingNextLevelIndex = -1;
    private bool levelRunning;
    private bool isSoundOn = false;
    private bool isMusicOn = false;
    private bool addedTube = false;
    private int addTubePowerUpCount;
    private int revealBallsPowerUpCount;

    public int CurrentLevelIndex => currentLevelIndex;
    public int CurrentLevelNumber => currentLevelIndex + 1;
    public int LevelCount => int.MaxValue;
    private int HandmadeLevelCount => levels == null ? 0 : levels.Length;
    private bool IsCurrentLevelHandmade => currentLevelIndex < HandmadeLevelCount;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void Start()
    {
        play.onClick.AddListener(StartGame);
        next.onClick.AddListener(LoadNextLevel);
        home.onClick.AddListener(Home);
        exit.onClick.AddListener(Exit);
        settings.onClick.AddListener(OpenSettings);
        closeSettings.onClick.AddListener(CloseSettings);
        sound.onClick.AddListener(Sound);
        music.onClick.AddListener(Music);
        addTube.onClick.AddListener(AddTube);

        if (revealBalls != null)
        {
            revealBalls.onClick.AddListener(RevealMysteryBalls);
        }

        LoadSavedLevelIndex();
        LoadPowerUpInventory();
        UpdateLevelNumberText();
        RefreshPowerUpUI();

        SetObjectActive(startPanal, true);
        AnimateTitle();
        SetObjectActive(gameplayPanal, false);
        SetObjectActive(nextlevelPanal, false);
        LoadSettings();

        if (BallSortGameController.Instance != null)
        {
            BallSortGameController.Instance.SetInputEnabled(false);
        }
    }

    public void StartGame()
    {
        if (!ValidateBasicReferences())
        {
            return;
        }

        SoundManager.Instance.PlaySound("btn");

        pendingNextLevelIndex = -1;
        SetObjectActive(startPanal, false);
        SetObjectActive(gameplayPanal, true);
        SetObjectActive(nextlevelPanal, false);

        SpawnCurrentLevel(true);
    }

    private void OpenSettings()
    {
        SoundManager.Instance.PlaySound("btn");
        SetObjectActive(settingsPanal, true);
    }

    private void CloseSettings()
    {
        SoundManager.Instance.PlaySound("btn");
        SetObjectActive(settingsPanal, false);
    }

    private void Sound()
    {
        isSoundOn = !isSoundOn;

        SoundAndMusicButtonTween(true, isSoundOn);

        PlayerPrefs.SetInt(SoundKey, isSoundOn ? 1 : 0);
        PlayerPrefs.Save();

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetSoundEnabled(isSoundOn);
        }
    }

    private void Music()
    {
        isMusicOn = !isMusicOn;

        SoundAndMusicButtonTween(false, isMusicOn);

        PlayerPrefs.SetInt(MusicKey, isMusicOn ? 1 : 0);
        PlayerPrefs.Save();

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetMusicEnabled(isMusicOn);
        }
    }

    private void LoadSettings()
    {
        isSoundOn = PlayerPrefs.GetInt(SoundKey, 1) == 1;
        isMusicOn = PlayerPrefs.GetInt(MusicKey, 1) == 1;

        SoundAndMusicButtonTween(true, isSoundOn);
        SoundAndMusicButtonTween(false, isMusicOn);

        if (SoundManager.Instance != null)
        {
            SoundManager.Instance.SetSoundEnabled(isSoundOn);
            SoundManager.Instance.SetMusicEnabled(isMusicOn);
        }
    }
    private string GetAddedTubeKey(int levelIndex)
    {
        return AddedTubeKeyPrefix + levelIndex;
    }

    private string GetRevealBallsKey(int levelIndex)
    {
        return RevealBallsKeyPrefix + levelIndex;
    }

    private bool WasRevealBallsUsed(int levelIndex)
    {
        return PlayerPrefs.GetInt(GetRevealBallsKey(levelIndex), 0) == 1;
    }

    public void RevealMysteryBalls()
    {
        if (!CanUseRevealBallsPowerUp())
        {
            RefreshPowerUpUI();
            return;
        }
        SoundManager.Instance.PlaySound("btn");

        int revealedCount = 0;

        for (int i = 0; i < spawnedTubes.Count; i++)
        {
            Tube tube = spawnedTubes[i];

            if (tube != null)
            {
                revealedCount += tube.RevealAllMysteryBalls();
            }
        }

        // Do not consume the power-up when there was nothing to reveal.
        if (revealedCount <= 0)
        {
            RefreshPowerUpUI();
            return;
        }

        revealBallsPowerUpCount--;
        PlayerPrefs.SetInt(RevealBallsPowerUpCountKey, revealBallsPowerUpCount);
        PlayerPrefs.SetInt(GetRevealBallsKey(currentLevelIndex), 1);
        PlayerPrefs.Save();

        SaveCurrentLevelState();
        RefreshPowerUpUI();

        if (BallSortGameController.Instance != null)
        {
            BallSortGameController.Instance.RefreshScene();
        }
    }

    private void AddTube()
    {
        if (!CanUseAddTubePowerUp())
        {
            RefreshPowerUpUI();
            return;
        }
        SoundManager.Instance.PlaySound("btn");

        addedTube = true;
        addTubePowerUpCount--;

        if (tempLastTubePrefab != null)
        {
            tempLastTubePrefab.gameObject.SetActive(true);
        }

        PlayerPrefs.SetInt(AddTubePowerUpCountKey, addTubePowerUpCount);
        PlayerPrefs.SetInt(GetAddedTubeKey(currentLevelIndex), 1);
        PlayerPrefs.Save();

        SaveCurrentLevelState();
        RefreshPowerUpUI();

        if (BallSortGameController.Instance != null)
        {
            BallSortGameController.Instance.RefreshScene();
        }
    }

    private void LoadPowerUpInventory()
    {
        addTubePowerUpCount = Mathf.Max(
            0,
            PlayerPrefs.GetInt(AddTubePowerUpCountKey, 0));

        revealBallsPowerUpCount = Mathf.Max(
            0,
            PlayerPrefs.GetInt(RevealBallsPowerUpCountKey, 0));
    }

    private bool CanUseAddTubePowerUp()
    {
        return levelRunning &&
               addTubePowerUpCount > 0 &&
               !addedTube &&
               !WasAddTubeUsed(currentLevelIndex) &&
               tempLastTubePrefab != null;
    }

    private bool CanUseRevealBallsPowerUp()
    {
        return levelRunning &&
               revealBallsPowerUpCount > 0 &&
               !WasRevealBallsUsed(currentLevelIndex) &&
               HasHiddenMysteryBalls();
    }

    private bool WasAddTubeUsed(int levelIndex)
    {
        return PlayerPrefs.GetInt(GetAddedTubeKey(levelIndex), 0) == 1;
    }

    private bool HasHiddenMysteryBalls()
    {
        for (int i = 0; i < spawnedTubes.Count; i++)
        {
            if (spawnedTubes[i] != null &&
                spawnedTubes[i].HasHiddenMysteryBalls())
            {
                return true;
            }
        }

        return false;
    }

    private void RefreshPowerUpUI()
    {
        if (addTubePowerUpCountText != null)
        {
            addTubePowerUpCountText.text = addTubePowerUpCount.ToString();
        }

        if (revealBallsPowerUpCountText != null)
        {
            revealBallsPowerUpCountText.text =
                revealBallsPowerUpCount.ToString();
        }

        if (addTube != null)
        {
            addTube.interactable = CanUseAddTubePowerUp();
        }

        if (revealBalls != null)
        {
            revealBalls.interactable = CanUseRevealBallsPowerUp();
        }
    }

    private void AwardPowerUpsForCompletedLevel()
    {
        int completedLevelNumber = CurrentLevelNumber;
        bool inventoryChanged = false;

        if (completedLevelNumber % AddTubeRewardInterval == 0)
        {
            addTubePowerUpCount++;
            inventoryChanged = true;
        }

        if (completedLevelNumber % RevealBallsRewardInterval == 0)
        {
            revealBallsPowerUpCount++;
            inventoryChanged = true;
        }

        if (inventoryChanged)
        {
            PlayerPrefs.SetInt(
                AddTubePowerUpCountKey,
                addTubePowerUpCount);

            PlayerPrefs.SetInt(
                RevealBallsPowerUpCountKey,
                revealBallsPowerUpCount);

            PlayerPrefs.Save();
        }

        RefreshPowerUpUI();
    }

    private void SoundAndMusicButtonTween(bool isSound, bool turnOn)
    {
        RectTransform r1 = sound.transform.GetComponent<RectTransform>();
        RectTransform r2 = music.transform.GetComponent<RectTransform>();
        Image i1 = sound.GetComponent<Image>();
        Image i2 = sound.transform.GetChild(0).GetComponent<Image>();
        Image i3 = music.GetComponent<Image>();
        Image i4 = music.transform.GetChild(0).GetComponent<Image>();


        if (turnOn)
        {
            if (isSound)
            {

                r1.DOAnchorPosX(33f, 0.3f);
                i1.DOFade(1, 0.3f);
                i2.DOFade(0, 0.3f);
            }
            else if (!isSound)
            {

                r2.DOAnchorPosX(33f, 0.3f);
                i3.DOFade(1, 0.3f);
                i4.DOFade(0, 0.3f);
            }
        }
        else if (!turnOn)
        {
            if (isSound)
            {

                r1.DOAnchorPosX(-34f, 0.3f);
                i1.DOFade(0, 0.3f);
                i2.DOFade(1, 0.3f);
            }
            else if (!isSound)
            {

                r2.DOAnchorPosX(-34f, 0.3f);
                i3.DOFade(0, 0.3f);
                i4.DOFade(1, 0.3f);
            }
        }
    }

    private void Home()
    {
        SoundManager.Instance.PlaySound("btn");
        SaveCurrentLevelState(); int nextIndex = pendingNextLevelIndex >= 0
            ? pendingNextLevelIndex
            : currentLevelIndex + 1;

        isPlayingParticles = false;
        starParticles.transform.GetComponent<ParticleSystem>().Stop();
        starParticles.SetActive(false);
        for (int i = 0; i < nextLevelParicles.Length; i++)
        {
            nextLevelParicles[i].transform.GetComponent<ParticleSystem>().Stop();
            nextLevelParicles[i].SetActive(false);
        }

        currentLevelIndex = nextIndex;
        pendingNextLevelIndex = -1;

        SaveCurrentLevelIndex();
        DeleteSavedLevelState(currentLevelIndex);
        PlayerPrefs.DeleteKey(GetRevealBallsKey(currentLevelIndex));
        
        SetObjectActive(startPanal, true);
        AnimateTitle();
        SetObjectActive(gameplayPanal, false);
        SetObjectActive(nextlevelPanal, false);

        UpdateLevelNumberText();
        RefreshPowerUpUI();
    }

    private void Exit()
    {
        SoundManager.Instance.PlaySound("btn");
        SaveCurrentLevelState();
        ClearPreviouslyGeneratedLevel();
        SetObjectActive(startPanal, true);
        AnimateTitle();
        SetObjectActive(gameplayPanal, false);
        SetObjectActive(nextlevelPanal, false);
    }

    /// <summary>
    /// Assign this to the Next button's OnClick event.
    /// </summary>
    public void LoadNextLevel()
    {
        if (!ValidateBasicReferences())
        {
            return;
        }

        SoundManager.Instance.PlaySound("btn");

        int nextIndex = pendingNextLevelIndex >= 0
            ? pendingNextLevelIndex
            : currentLevelIndex + 1;

        currentLevelIndex = nextIndex;
        pendingNextLevelIndex = -1;
        addedTube = false;

        isPlayingParticles = false;
        starParticles.transform.GetComponent<ParticleSystem>().Stop();
        starParticles.SetActive(false);
        for (int i = 0; i < nextLevelParicles.Length; i++)
        {
            nextLevelParicles[i].transform.GetComponent<ParticleSystem>().Stop();
            nextLevelParicles[i].SetActive(false);
        }

        SaveCurrentLevelIndex();
        DeleteSavedLevelState(currentLevelIndex);

        SetObjectActive(gameplayPanal, true);
        SetObjectActive(nextlevelPanal, false);

        UpdateLevelNumberText();
        SpawnCurrentLevel(false);
    }


    private void SpawnCurrentLevel(bool loadSavedState)
    {
        if (!ValidateBasicReferences())
        {
            return;
        }

        if (!TryGetCurrentLevelDefinition(
                out Ball.BallColor[] levelColors,
                out int mysteryTubeCount,
                out Sprite mysterySprite,
                out int generationAttempts,
                out int generationSeed,
                out string levelId,
                out string validationError))
        {
            Debug.LogError(validationError, this);
            return;
        }

        if (!ValidateBallPrefabs(levelColors))
        {
            return;
        }

        levelRunning = false;

        if (BallSortGameController.Instance != null)
        {
            BallSortGameController.Instance.SetInputEnabled(false);
        }

        ClearPreviouslyGeneratedLevel();

        List<Ball.BallColor[]> tubeLayouts;
        List<bool[]> tubeMysteryStates;

        if (loadSavedState &&
            TryLoadSavedLevelState(
                levelId,
                levelColors,
                out tubeLayouts,
                out tubeMysteryStates))
        {
            Debug.Log("Loaded saved state for Level " + CurrentLevelNumber + ".", this);
        }
        else
        {
            tubeLayouts = CreateNewLevelLayouts(
                levelColors,
                mysteryTubeCount,
                generationAttempts,
                generationSeed,
                out tubeMysteryStates);
        }

        if (WasRevealBallsUsed(currentLevelIndex))
        {
            RevealAllSavedMysteryStates(tubeMysteryStates);
        }

        Transform parent = tubeParent != null ? tubeParent : transform;

        generatedLevelRoot = new GameObject(
            "Generated Level " + CurrentLevelNumber).transform;
        generatedLevelRoot.SetParent(parent, false);

        spawnedTubes.Clear();

        int lastTube = tubeLayouts.Count - 1;

        for (int tubeIndex = 0; tubeIndex < tubeLayouts.Count; tubeIndex++)
        {
            Tube tube = Instantiate(
                tubePrefab,
                GetTubePosition(tubeIndex, tubeLayouts.Count),
                Quaternion.identity,
                generatedLevelRoot);

            tube.name = "Tube " + (tubeIndex + 1);
            spawnedTubes.Add(tube);

            FillTube(
                tube,
                tubeLayouts[tubeIndex],
                tubeMysteryStates[tubeIndex],
                mysterySprite);

            tube.SnapBallsToSlots();
            tube.RefreshLockedState();

            if (tubeIndex == lastTube)
            {
                tempLastTubePrefab = tube;
                addedTube = PlayerPrefs.GetInt(GetAddedTubeKey(currentLevelIndex), 0) == 1;
                tube.gameObject.SetActive(addedTube);
            }
        }

        UpdateLevelNumberText();
        SaveCurrentLevelIndex();
        levelRunning = true;
        RefreshPowerUpUI();

        if (BallSortGameController.Instance != null)
        {
            BallSortGameController.Instance.SetInputEnabled(true);
            BallSortGameController.Instance.RefreshScene();
        }
        else
        {
            Debug.LogWarning(
                "The level was generated, but no BallSortGameController exists in the scene.",
                this);
        }
    }

    private bool TryGetCurrentLevelDefinition(
        out Ball.BallColor[] colors,
        out int mysteryTubeCount,
        out Sprite mysterySprite,
        out int generationAttempts,
        out int generationSeed,
        out string levelId,
        out string error)
    {
        colors = null;
        mysteryTubeCount = 0;
        mysterySprite = null;
        generationAttempts = 250;
        generationSeed = 0;
        levelId = string.Empty;
        error = string.Empty;

        if (IsCurrentLevelHandmade)
        {
            LevelData data = levels[currentLevelIndex];

            if (data == null)
            {
                error = "The handmade LevelData entry at index " + currentLevelIndex + " is empty.";
                return false;
            }

            if (!data.TryGetSelectedColors(out colors, out error))
            {
                return false;
            }

            mysteryTubeCount = data.MysteryTubeCount;
            mysterySprite = data.MysteryBallSprite;
            generationAttempts = data.GenerationAttempts;
            generationSeed = data.UseFixedSeed
                ? data.FixedSeed
                : unchecked(Environment.TickCount ^ GetInstanceID());
            levelId = "Handmade_" + data.name;
            return true;
        }

        int proceduralIndex = currentLevelIndex - HandmadeLevelCount;
        generationSeed = unchecked(proceduralBaseSeed + currentLevelIndex * 7919);
        colors = CreateProceduralColorSlots(proceduralIndex, generationSeed);
        mysteryTubeCount = GetProceduralMysteryTubeCount(proceduralIndex, colors.Length);
        mysterySprite = proceduralMysteryBallSprite;
        generationAttempts = Mathf.Clamp(200 + proceduralIndex * 20, 200, 1000);
        levelId = "Procedural_" + currentLevelIndex;

        if (mysteryTubeCount > 0 && mysterySprite == null)
        {
            error = "Assign Procedural Mystery Ball Sprite in LevelManager because this generated level uses mystery tubes.";
            return false;
        }

        return true;
    }

    private Ball.BallColor[] CreateProceduralColorSlots(int proceduralIndex, int seed)
    {
        int colorSlotCount = Mathf.Clamp(
            3 + proceduralIndex / Mathf.Max(1, levelsPerColorIncrease),
            3,
            7);

        Ball.BallColor[] palette =
            (Ball.BallColor[])Enum.GetValues(typeof(Ball.BallColor));
        System.Random random = new System.Random(seed);
        Shuffle(palette, random);

        int duplicateCount = 0;

        // Duplicates are only allowed on boards with six or seven color slots.
        // They appear gradually, and no actual color is ever used more than twice.
        if (colorSlotCount > 5)
        {
            duplicateCount = colorSlotCount == 6
                ? (proceduralIndex % 2)
                : 1 + (proceduralIndex % 2);
        }

        int uniqueCount = colorSlotCount - duplicateCount;
        Ball.BallColor[] result = new Ball.BallColor[colorSlotCount];

        for (int i = 0; i < uniqueCount; i++)
        {
            result[i] = palette[i];
        }

        for (int i = 0; i < duplicateCount; i++)
        {
            result[uniqueCount + i] = result[i % uniqueCount];
        }

        Shuffle(result, random);
        return result;
    }

    private int GetProceduralMysteryTubeCount(int proceduralIndex, int colorSlotCount)
    {
        int displayedLevel = currentLevelIndex + 1;

        if (displayedLevel < mysteryStartsAtLevel ||
            (displayedLevel - mysteryStartsAtLevel) % Mathf.Max(1, mysteryLevelInterval) != 0)
        {
            return 0;
        }

        int difficultyTier = 1 + proceduralIndex / 8;
        return Mathf.Clamp(difficultyTier, 1, Mathf.Min(3, colorSlotCount));
    }

    private void RevealAllSavedMysteryStates(List<bool[]> mysteryStates)
    {
        if (mysteryStates == null)
        {
            return;
        }

        for (int tubeIndex = 0; tubeIndex < mysteryStates.Count; tubeIndex++)
        {
            bool[] tubeStates = mysteryStates[tubeIndex];

            if (tubeStates == null)
            {
                continue;
            }

            for (int ballIndex = 0; ballIndex < tubeStates.Length; ballIndex++)
            {
                tubeStates[ballIndex] = false;
            }
        }
    }

    private List<Ball.BallColor[]> CreateNewLevelLayouts(
        Ball.BallColor[] levelColors,
        int mysteryTubeCount,
        int generationAttempts,
        int seed,
        out List<bool[]> mysteryStates)
    {
        List<Ball.BallColor[]> filledLayouts =
            GenerateMixedTubeLayouts(
                levelColors,
                generationAttempts,
                seed);

        List<Ball.BallColor[]> allLayouts =
            new List<Ball.BallColor[]>(
                filledLayouts.Count + EmptyTubeCount);

        allLayouts.AddRange(filledLayouts);

        for (int i = 0; i < EmptyTubeCount; i++)
        {
            allLayouts.Add(Array.Empty<Ball.BallColor>());
        }

        mysteryStates = CreateInitialMysteryStates(
            allLayouts,
            mysteryTubeCount,
            unchecked(seed ^ 0x4D595354));

        return allLayouts;
    }

    private List<bool[]> CreateInitialMysteryStates(
        List<Ball.BallColor[]> layouts,
        int mysteryTubeCount,
        int seed)
    {
        List<bool[]> states = new List<bool[]>(layouts.Count);

        for (int i = 0; i < layouts.Count; i++)
        {
            states.Add(new bool[layouts[i].Length]);
        }

        int filledTubeCount = Mathf.Max(0, layouts.Count - EmptyTubeCount);
        mysteryTubeCount = Mathf.Clamp(mysteryTubeCount, 0, filledTubeCount);

        List<int> candidates = new List<int>(filledTubeCount);

        for (int i = 0; i < filledTubeCount; i++)
        {
            candidates.Add(i);
        }

        System.Random random = new System.Random(seed);

        for (int i = candidates.Count - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            int temporary = candidates[i];
            candidates[i] = candidates[swapIndex];
            candidates[swapIndex] = temporary;
        }

        for (int i = 0; i < mysteryTubeCount; i++)
        {
            int tubeIndex = candidates[i];
            bool[] tubeStates = states[tubeIndex];

            // Bottom-to-top array: hide every ball except the current top ball.
            for (int ballIndex = 0;
                 ballIndex < tubeStates.Length - 1;
                 ballIndex++)
            {
                tubeStates[ballIndex] = true;
            }
        }

        return states;
    }

    /// <summary>
    /// Called by BallSortGameController after every successful move.
    /// </summary>
    public void SaveCurrentLevelState()
    {
        if (!levelRunning ||
            spawnedTubes.Count == 0 ||
            !ValidateCurrentLevelIndex())
        {
            return;
        }

        SavedLevelState savedState = new SavedLevelState
        {
            levelIndex = currentLevelIndex,
            levelName = GetCurrentLevelId(),
            tubes = new SavedTubeState[spawnedTubes.Count]
        };

        for (int i = 0; i < spawnedTubes.Count; i++)
        {
            Ball.BallColor[] colors =
                spawnedTubes[i] != null
                    ? spawnedTubes[i].GetBallColorsBottomToTop()
                    : Array.Empty<Ball.BallColor>();

            int[] colorValues = new int[colors.Length];

            for (int colorIndex = 0;
                 colorIndex < colors.Length;
                 colorIndex++)
            {
                colorValues[colorIndex] = (int)colors[colorIndex];
            }

            bool[] mysteryHidden =
                spawnedTubes[i] != null
                    ? spawnedTubes[i].GetMysteryHiddenStatesBottomToTop()
                    : Array.Empty<bool>();

            savedState.tubes[i] = new SavedTubeState
            {
                colors = colorValues,
                mysteryHidden = mysteryHidden
            };
        }

        string json = JsonUtility.ToJson(savedState);

        PlayerPrefs.SetString(GetLevelStateKey(currentLevelIndex), json);
        PlayerPrefs.SetInt(CurrentLevelKey, currentLevelIndex);
        PlayerPrefs.Save();
    }

    /// <summary>
    /// Called by BallSortGameController when all non-empty tubes are solved.
    /// </summary>
    public void RunHandleLevelCompleted()
    {
        StartCoroutine(HandleLevelCompleted());
    }

    private IEnumerator HandleLevelCompleted()
    {
        yield return new WaitForSeconds(0.5f);
        if (!levelRunning)
        {
            yield break;
        }

        levelRunning = false;
        AwardPowerUpsForCompletedLevel();

        DeleteSavedLevelState(currentLevelIndex);
        PlayerPrefs.DeleteKey(GetRevealBallsKey(currentLevelIndex));

        pendingNextLevelIndex = currentLevelIndex + 1;

        bool hasNextLevel = true;

        // Endless mode always has another deterministic procedural level.
        // Saving this immediately means Continue opens the next level even if
        // the app is closed while the win panel is visible.
        PlayerPrefs.SetInt(CurrentLevelKey, pendingNextLevelIndex);
        PlayerPrefs.Save();
        ClearPreviouslyGeneratedLevel();
        SetObjectActive(startPanal, false);
        //SetObjectActive(gameplayPanal, false);
        RunNextLevelPanalRoutine();
        SetObjectActive(next.gameObject, hasNextLevel);

        if (BallSortGameController.Instance != null)
        {
            BallSortGameController.Instance.SetInputEnabled(false);
        }
    }

    private void RunNextLevelPanalRoutine()
    {
        StartCoroutine(ShowNextLevelPanal());
    }

    private IEnumerator ShowNextLevelPanal()
    {
        RectTransform r = logo.GetComponent<RectTransform>();
        nextlevelPanal.SetActive(false);
        next.gameObject.SetActive(false);
        next.gameObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        home.gameObject.SetActive(false);
        home.gameObject.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
        r.anchoredPosition = new Vector2(r.anchoredPosition.x, 600f);
        star.transform.localScale = new Vector3(0f, 0f, 0f);

        yield return new WaitForSeconds(0.1f);
        next.gameObject.SetActive(true);
        home.gameObject.SetActive(true);
        nextlevelPanal.SetActive(true);
        PlayNextLevelParticles();
        r.DOAnchorPosY(-151f, 0.8f).SetEase(Ease.OutBack);
        star.transform.DORotate(new Vector3(0f, 0f, 2160f), 0.8f, RotateMode.FastBeyond360).SetEase(Ease.Linear);
        star.transform.DOScale(new Vector3(1f, 1f, 1f), 0.8f).SetEase(Ease.OutBack);
        yield return new WaitForSeconds(0.5f);
        next.gameObject.transform.GetComponent<Image>().DOFade(1f, 0.2f).SetEase(Ease.Linear);
        home.gameObject.transform.GetComponent<Image>().DOFade(1f, 0.2f).SetEase(Ease.Linear);
    }

    private void PlayNextLevelParticles()
    {
        StartCoroutine(PlayNextLevelParticlesRoutine());
    }

    private IEnumerator PlayNextLevelParticlesRoutine()
    {
        starParticles.SetActive(true);
        starParticles.transform.GetComponent<ParticleSystem>().Play();
        for(int i = 0; i < nextLevelParicles.Length; i++)
        {
            nextLevelParicles[i].SetActive(true);
        }
        isPlayingParticles = true;
        while (isPlayingParticles)
        {
            for (int i = 0; i < nextLevelParicles.Length; i++)
            {
                nextLevelParicles[i].transform.GetComponent<ParticleSystem>().Play();
            }

            yield return new WaitForSeconds(1f);
        }
    }

    private bool TryLoadSavedLevelState(
        string levelId,
        Ball.BallColor[] allowedColors,
        out List<Ball.BallColor[]> tubeLayouts,
        out List<bool[]> mysteryStates)
    {
        tubeLayouts = null;
        mysteryStates = null;

        string key = GetLevelStateKey(currentLevelIndex);

        if (!PlayerPrefs.HasKey(key))
        {
            return false;
        }

        string json = PlayerPrefs.GetString(key, string.Empty);

        if (string.IsNullOrWhiteSpace(json))
        {
            DeleteSavedLevelState(currentLevelIndex);
            return false;
        }

        SavedLevelState savedState;

        try
        {
            savedState = JsonUtility.FromJson<SavedLevelState>(json);
        }
        catch (Exception exception)
        {
            Debug.LogWarning(
                "Could not read the saved level state. A fresh level will " +
                "be generated.\n" + exception.Message,
                this);

            DeleteSavedLevelState(currentLevelIndex);
            return false;
        }

        if (!IsSavedStateValid(savedState, levelId, allowedColors))
        {
            Debug.LogWarning(
                "The saved board no longer matches this LevelData asset. " +
                "A fresh board will be generated.",
                this);

            DeleteSavedLevelState(currentLevelIndex);
            return false;
        }

        tubeLayouts =
            new List<Ball.BallColor[]>(savedState.tubes.Length);
        mysteryStates = new List<bool[]>(savedState.tubes.Length);

        for (int tubeIndex = 0;
             tubeIndex < savedState.tubes.Length;
             tubeIndex++)
        {
            int[] savedColors = savedState.tubes[tubeIndex].colors;
            Ball.BallColor[] colors =
                new Ball.BallColor[savedColors.Length];

            for (int colorIndex = 0;
                 colorIndex < savedColors.Length;
                 colorIndex++)
            {
                colors[colorIndex] =
                    (Ball.BallColor)savedColors[colorIndex];
            }

            tubeLayouts.Add(colors);

            bool[] savedMystery = savedState.tubes[tubeIndex].mysteryHidden;
            bool[] hiddenStates = new bool[savedMystery.Length];
            Array.Copy(savedMystery, hiddenStates, savedMystery.Length);
            mysteryStates.Add(hiddenStates);
        }

        return true;
    }

    private bool IsSavedStateValid(
        SavedLevelState savedState,
        string levelId,
        Ball.BallColor[] allowedColors)
    {
        if (savedState == null ||
            savedState.levelIndex != currentLevelIndex ||
            savedState.levelName != levelId ||
            savedState.tubes == null ||
            savedState.tubes.Length != allowedColors.Length + EmptyTubeCount)
        {
            return false;
        }

        HashSet<int> allowedColorValues = new HashSet<int>();
        Dictionary<int, int> colorCounts = new Dictionary<int, int>();

        Dictionary<int, int> expectedColorCounts = new Dictionary<int, int>();

        for (int i = 0; i < allowedColors.Length; i++)
        {
            int colorValue = (int)allowedColors[i];
            allowedColorValues.Add(colorValue);
            colorCounts[colorValue] = 0;

            if (!expectedColorCounts.ContainsKey(colorValue))
            {
                expectedColorCounts[colorValue] = 0;
            }

            expectedColorCounts[colorValue] += BallsPerTube;
        }

        for (int tubeIndex = 0;
             tubeIndex < savedState.tubes.Length;
             tubeIndex++)
        {
            SavedTubeState tubeState = savedState.tubes[tubeIndex];

            if (tubeState == null ||
                tubeState.colors == null ||
                tubeState.colors.Length > BallsPerTube ||
                tubeState.mysteryHidden == null ||
                tubeState.mysteryHidden.Length != tubeState.colors.Length)
            {
                return false;
            }

            if (tubeState.mysteryHidden.Length > 0 &&
                tubeState.mysteryHidden[tubeState.mysteryHidden.Length - 1])
            {
                // The top ball must always be visible.
                return false;
            }

            for (int colorIndex = 0;
                 colorIndex < tubeState.colors.Length;
                 colorIndex++)
            {
                int colorValue = tubeState.colors[colorIndex];

                if (!allowedColorValues.Contains(colorValue))
                {
                    return false;
                }

                colorCounts[colorValue]++;
            }
        }

        foreach (int colorValue in allowedColorValues)
        {
            if (colorCounts[colorValue] != expectedColorCounts[colorValue])
            {
                return false;
            }
        }

        return true;
    }

    private void FillTube(
        Tube tube,
        Ball.BallColor[] colors,
        bool[] mysteryHidden,
        Sprite mysterySprite)
    {
        if (colors == null || colors.Length > BallsPerTube)
        {
            Debug.LogError(
                "A tube cannot contain more than four balls.",
                this);
            return;
        }

        if (mysteryHidden == null || mysteryHidden.Length != colors.Length)
        {
            Debug.LogError(
                "Mystery state must match the number of balls in the tube.",
                this);
            return;
        }

        for (int i = 0; i < colors.Length; i++)
        {
            Ball prefab = GetBallPrefab(colors[i]);

            if (prefab == null)
            {
                Debug.LogError(
                    "No ball prefab is assigned for " + colors[i] + ".",
                    this);
                return;
            }

            Ball ball = Instantiate(prefab, tube.transform);
            ball.color = colors[i];
            ball.SetMysteryState(mysteryHidden[i], mysterySprite);

            if (!tube.AddBall(ball))
            {
                Debug.LogError(
                    "Could not add a ball to " + tube.name + ".",
                    tube);
                Destroy(ball.gameObject);
                return;
            }
        }

        tube.RevealTopMysteryBall();
    }

    private void LoadSavedLevelIndex()
    {
        currentLevelIndex = PlayerPrefs.GetInt(CurrentLevelKey, 0);

        currentLevelIndex = Mathf.Max(0, currentLevelIndex);
    }

    private void SaveCurrentLevelIndex()
    {
        PlayerPrefs.SetInt(CurrentLevelKey, currentLevelIndex);
        PlayerPrefs.Save();
    }

    private bool ValidateLevelList()
    {
        if (levels == null)
        {
            return true;
        }

        for (int i = 0; i < levels.Length; i++)
        {
            if (levels[i] != null)
            {
                continue;
            }

            Debug.LogError(
                "The handmade Levels array has an empty entry at index " + i + ".",
                this);
            return false;
        }

        return true;
    }

    private bool ValidateCurrentLevelIndex()
    {
        return currentLevelIndex >= 0;
    }

    private bool ValidateBasicReferences()
    {
        if (!ValidateLevelList())
        {
            return false;
        }

        if (!ValidateCurrentLevelIndex())
        {
            Debug.LogError("The current level index is invalid.", this);
            return false;
        }

        if (tubePrefab == null)
        {
            Debug.LogError("No Tube prefab is assigned.", this);
            return false;
        }

        return true;
    }

    private string GetCurrentLevelId()
    {
        if (IsCurrentLevelHandmade && levels[currentLevelIndex] != null)
        {
            return "Handmade_" + levels[currentLevelIndex].name;
        }

        return "Procedural_" + currentLevelIndex;
    }

    private bool ValidateBallPrefabs(Ball.BallColor[] colors)
    {
        for (int i = 0; i < colors.Length; i++)
        {
            if (GetBallPrefab(colors[i]) != null)
            {
                continue;
            }

            Debug.LogError(
                "No ball prefab is assigned for " + colors[i] + ".",
                this);
            return false;
        }

        return true;
    }

    private void DeleteSavedLevelState(int levelIndex)
    {
        PlayerPrefs.DeleteKey(GetLevelStateKey(levelIndex));
    }

    private string GetLevelStateKey(int levelIndex)
    {
        return LevelStateKeyPrefix + levelIndex;
    }

    private void UpdateLevelNumberText()
    {
        if (levelNumberText != null)
        {
            levelNumberText.text = "Level " + CurrentLevelNumber;
        }
    }

    private void SetObjectActive(GameObject target, bool active)
    {
        if (target != null)
        {
            target.SetActive(active);
        }
    }

    private void ClearPreviouslyGeneratedLevel()
    {
        spawnedTubes.Clear();

        if (generatedLevelRoot == null)
        {
            return;
        }

        generatedLevelRoot.gameObject.SetActive(false);

        if (Application.isPlaying)
        {
            Destroy(generatedLevelRoot.gameObject);
        }
        else
        {
            DestroyImmediate(generatedLevelRoot.gameObject);
        }

        generatedLevelRoot = null;
    }

    private Vector3 GetTubePosition(int index, int totalTubes)
    {
        int firstRowCount = Mathf.CeilToInt(totalTubes / 2f);
        bool isFirstRow = index < firstRowCount;

        int rowIndex = isFirstRow ? 0 : 1;
        int indexInRow = isFirstRow
            ? index
            : index - firstRowCount;

        int tubesInThisRow = isFirstRow
            ? firstRowCount
            : totalTubes - firstRowCount;

        float startX =
            -(tubesInThisRow - 1) * tubeSpacing * 0.5f;

        float x = startX + indexInRow * tubeSpacing;
        float y = rowIndex == 0
            ? rowSpacing * 0.5f
            : -rowSpacing * 0.5f;

        return new Vector3(x, y, 0f);
    }

    private List<Ball.BallColor[]> GenerateMixedTubeLayouts(
        Ball.BallColor[] colors,
        int attempts,
        int seed)
    {
        System.Random random = new System.Random(seed);
        Ball.BallColor[][] bestLayout = null;
        int bestPenalty = int.MaxValue;

        attempts = Mathf.Max(1, attempts);

        for (int attempt = 0; attempt < attempts; attempt++)
        {
            Ball.BallColor[][] candidate =
                CreateLayerShuffledLayout(colors, random);

            if (ContainsSingleColorTube(candidate))
            {
                continue;
            }

            int penalty = CalculateLayoutPenalty(candidate);

            if (penalty >= bestPenalty)
            {
                continue;
            }

            bestPenalty = penalty;
            bestLayout = CloneLayout(candidate);
        }

        if (bestLayout == null)
        {
            bestLayout = CreateFallbackMixedLayout(colors, random);
        }

        List<Ball.BallColor[]> result =
            new List<Ball.BallColor[]>(bestLayout.Length);

        for (int i = 0; i < bestLayout.Length; i++)
        {
            result.Add(bestLayout[i]);
        }

        return result;
    }

    private Ball.BallColor[][] CreateLayerShuffledLayout(
        Ball.BallColor[] colors,
        System.Random random)
    {
        int filledTubeCount = colors.Length;
        Ball.BallColor[][] layout =
            new Ball.BallColor[filledTubeCount][];

        for (int tubeIndex = 0;
             tubeIndex < filledTubeCount;
             tubeIndex++)
        {
            layout[tubeIndex] = new Ball.BallColor[BallsPerTube];
        }

        for (int slotIndex = 0;
             slotIndex < BallsPerTube;
             slotIndex++)
        {
            Ball.BallColor[] shuffledLayer =
                new Ball.BallColor[colors.Length];
            Array.Copy(colors, shuffledLayer, colors.Length);
            Shuffle(shuffledLayer, random);

            for (int tubeIndex = 0;
                 tubeIndex < filledTubeCount;
                 tubeIndex++)
            {
                layout[tubeIndex][slotIndex] = shuffledLayer[tubeIndex];
            }
        }

        return layout;
    }

    private int CalculateLayoutPenalty(Ball.BallColor[][] layout)
    {
        int penalty = 0;

        for (int tubeIndex = 0;
             tubeIndex < layout.Length;
             tubeIndex++)
        {
            Ball.BallColor[] tube = layout[tubeIndex];
            HashSet<Ball.BallColor> uniqueColors =
                new HashSet<Ball.BallColor>();
            Dictionary<Ball.BallColor, int> colorCounts =
                new Dictionary<Ball.BallColor, int>();

            for (int slotIndex = 0;
                 slotIndex < tube.Length;
                 slotIndex++)
            {
                Ball.BallColor color = tube[slotIndex];
                uniqueColors.Add(color);

                if (!colorCounts.ContainsKey(color))
                {
                    colorCounts[color] = 0;
                }

                colorCounts[color]++;

                if (slotIndex > 0 &&
                    tube[slotIndex - 1] == color)
                {
                    penalty += 8;
                }
            }

            penalty += (BallsPerTube - uniqueColors.Count) * 12;

            int largestSameColorCount = 0;

            foreach (KeyValuePair<Ball.BallColor, int> pair in colorCounts)
            {
                largestSameColorCount = Mathf.Max(
                    largestSameColorCount,
                    pair.Value);
            }

            if (largestSameColorCount >= 3)
            {
                penalty += (largestSameColorCount - 2) * 30;
            }

            if (tube[BallsPerTube - 1] == tube[BallsPerTube - 2])
            {
                penalty += 15;
            }
        }

        return penalty;
    }

    private bool ContainsSingleColorTube(Ball.BallColor[][] layout)
    {
        for (int tubeIndex = 0;
             tubeIndex < layout.Length;
             tubeIndex++)
        {
            Ball.BallColor firstColor = layout[tubeIndex][0];
            bool allSame = true;

            for (int slotIndex = 1;
                 slotIndex < BallsPerTube;
                 slotIndex++)
            {
                if (layout[tubeIndex][slotIndex] == firstColor)
                {
                    continue;
                }

                allSame = false;
                break;
            }

            if (allSame)
            {
                return true;
            }
        }

        return false;
    }

    private Ball.BallColor[][] CreateFallbackMixedLayout(
        Ball.BallColor[] colors,
        System.Random random)
    {
        Ball.BallColor[] shuffledColors =
            new Ball.BallColor[colors.Length];
        Array.Copy(colors, shuffledColors, colors.Length);
        Shuffle(shuffledColors, random);

        Ball.BallColor[][] layout =
            new Ball.BallColor[shuffledColors.Length][];

        for (int tubeIndex = 0;
             tubeIndex < shuffledColors.Length;
             tubeIndex++)
        {
            layout[tubeIndex] = new Ball.BallColor[BallsPerTube];

            for (int slotIndex = 0;
                 slotIndex < BallsPerTube;
                 slotIndex++)
            {
                int colorIndex =
                    (tubeIndex + slotIndex) % shuffledColors.Length;

                layout[tubeIndex][slotIndex] =
                    shuffledColors[colorIndex];
            }
        }

        return layout;
    }

    private Ball.BallColor[][] CloneLayout(Ball.BallColor[][] source)
    {
        Ball.BallColor[][] clone =
            new Ball.BallColor[source.Length][];

        for (int i = 0; i < source.Length; i++)
        {
            clone[i] = new Ball.BallColor[source[i].Length];
            Array.Copy(source[i], clone[i], source[i].Length);
        }

        return clone;
    }

    private void Shuffle<T>(T[] values, System.Random random)
    {
        for (int i = values.Length - 1; i > 0; i--)
        {
            int swapIndex = random.Next(i + 1);
            T temporary = values[i];
            values[i] = values[swapIndex];
            values[swapIndex] = temporary;
        }
    }

    private Ball GetBallPrefab(Ball.BallColor color)
    {
        switch (color)
        {
            case Ball.BallColor.Red:
                return redBallPrefab;

            case Ball.BallColor.Green:
                return greenBallPrefab;

            case Ball.BallColor.Blue:
                return blueBallPrefab;

            case Ball.BallColor.Yellow:
                return yellowBallPrefab;

            case Ball.BallColor.LightBlue:
                return lightBlueBallPrefab;

            case Ball.BallColor.Pink:
                return pinkBallPrefab;

            case Ball.BallColor.Orange:
                return orangeBallPrefab;

            case Ball.BallColor.Black:
                return blackBallPrefab;

            case Ball.BallColor.Gray:
                return grayBallPrefab;

            case Ball.BallColor.NeonPink:
                return neonPinkBallPrefab;

            case Ball.BallColor.NeonGreen:
                return neonGreenBallPrefab;

            case Ball.BallColor.NeonBlue:
                return neonBlueBallPrefab;

            case Ball.BallColor.Purple:
                return purpleBallPrefab;

            default:
                return null;
        }
    }

    private void AnimateTitle()
    {
        if (Title != null)
        {
            RectTransform titleRect = Title.GetComponent<RectTransform>();

            titleRect.DOKill();
            titleRect.anchoredPosition = new Vector2(0f, 1300f);
            titleRect.localScale = Vector3.one;

            titleRect.DOAnchorPosY(689f, 1f)
                .SetEase(Ease.OutBack)
                .OnComplete(() =>
                {
                    titleRect.DOScale(0.8f, 0.8f)
                        .SetLoops(-1, LoopType.Yoyo);
                });
        }
    }

    private void OnApplicationPause(bool paused)
    {
        if (paused)
        {
            SaveCurrentLevelState();
        }
    }

    private void OnApplicationQuit()
    {
        SaveCurrentLevelState();
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    [Serializable]
    private class SavedLevelState
    {
        public int levelIndex;
        public string levelName;
        public SavedTubeState[] tubes;
    }

    [Serializable]
    private class SavedTubeState
    {
        public int[] colors;
        public bool[] mysteryHidden;
    }
}
