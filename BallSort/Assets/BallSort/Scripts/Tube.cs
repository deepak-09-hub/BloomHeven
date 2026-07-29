using UnityEngine;
using DG.Tweening;
using System.Collections;

[DisallowMultipleComponent]
[RequireComponent(typeof(Collider2D))]
public class Tube : MonoBehaviour
{
    [Header("Ball Slots")]
    [Tooltip("Assign the slot transforms from bottom to top.")]
    [SerializeField] private Transform[] ballPositions;

    [SerializeField] private Ball[] balls;
    [SerializeField] private Ball ballPrefab;
    [Header("Completion Particles")]
    [Tooltip("Assign the child GameObject that contains the completion ParticleSystem.")]
    [SerializeField] private GameObject particles;

    private bool sceneStartupComplete;
    private bool completionParticlesPlayed;

    private bool initialized = false;

    [Header("Selection")]
    [SerializeField] private float selectedLiftDistance = 0.6f;

    private Vector3[] cachedSlotLocalPositions;

    public bool IsLocked { get; private set; }

    public int Capacity
    {
        get { return 4; }
    }

    public int BallCount
    {
        get
        {
            if (balls == null)
            {
                return 0;
            }

            int count = 0;

            for (int i = 0; i < balls.Length; i++)
            {
                if (balls[i] != null)
                {
                    count++;
                }
            }

            return count;
        }
    }

    public int FreeSpace
    {
        get { return Mathf.Max(0, Capacity - BallCount); }
    }

    private void Awake()
    {
        CacheSlotPositions();
        PrepareBallArray();
        SnapBallsToSlots();
        RefreshLockedState();

        StopCompletionParticles();
    }

    private void Start()
    {
        // LevelManager fills and restores tubes before Start runs. Waiting until
        // Start prevents particles from playing for tubes that were already solved
        // when a saved level was loaded.
        sceneStartupComplete = true;
    }

    public void NotifyClicked()
    {
        Debug.Log("Tube clicked: " + gameObject.name + ". Notifying BallSortGameController.");
        if (BallSortGameController.Instance != null)
        {
            BallSortGameController.Instance.NotifyTubeClicked(this);
        }
        else
        {
            Debug.LogError("No BallSortGameController exists in the scene.", this);
        }
    }

    private void CacheSlotPositions()
    {
        int capacity = ballPositions.Length;
        cachedSlotLocalPositions = new Vector3[capacity];

        for (int i = 0; i < capacity; i++)
        {
            if (ballPositions[i] == null)
            {
                Debug.LogError("A Ball Position is missing at index " + i + ".", this);
                continue;
            }

            cachedSlotLocalPositions[i] =
                transform.InverseTransformPoint(ballPositions[i].position);
        }
    }

    private void PrepareBallArray()
    {
        Ball[] configuredBalls = balls;
        balls = new Ball[Capacity];

        if (configuredBalls == null)
        {
            return;
        }

        int writeIndex = 0;

        for (int i = 0; i < configuredBalls.Length; i++)
        {
            Ball ball = configuredBalls[i];

            if (ball == null || writeIndex >= balls.Length)
            {
                continue;
            }

            balls[writeIndex] = ball;
            writeIndex++;
        }
    }

    public bool IsFull()
    {
        return Capacity > 0 && BallCount == Capacity;
    }

    public bool IsEmpty()
    {
        return BallCount == 0;
    }

    /// <summary>
    /// Returns the current balls from the bottom slot to the top slot.
    /// LevelManager uses this to save the exact board state.
    /// </summary>
    public Ball.BallColor[] GetBallColorsBottomToTop()
    {
        Ball.BallColor[] colors = new Ball.BallColor[BallCount];
        int writeIndex = 0;

        if (balls == null)
        {
            return colors;
        }

        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i] == null)
            {
                continue;
            }

            colors[writeIndex] = balls[i].color;
            writeIndex++;
        }

        return colors;
    }

    public Ball GetTopBall()
    {
        int topIndex = GetTopBallIndex();
        return topIndex >= 0 ? balls[topIndex] : null;
    }

    public int GetTopSameColorCount()
    {
        int topIndex = GetTopBallIndex();

        if (topIndex < 0)
        {
            return 0;
        }

        Ball.BallColor topColor = balls[topIndex].color;
        int count = 0;

        for (int i = topIndex; i >= 0; i--)
        {
            Ball ball = balls[i];

            if (ball == null ||
                ball.IsMysteryHidden ||
                ball.color != topColor)
            {
                break;
            }

            count++;
        }

        return count;
    }

    public bool CanAccept(Ball.BallColor colorToAccept)
    {
        if (IsLocked || FreeSpace <= 0)
        {
            return false;
        }

        Ball topBall = GetTopBall();
        return topBall == null || topBall.color == colorToAccept;
    }

    public bool AddBall(Ball ball)
    {
        if (ball == null)
            return false;

        if (BallCount >= Capacity)
            return false;

        balls[BallCount] = ball;

        ball.transform.SetParent(transform);

        return true;
    }

    public Ball RemoveTopBall()
    {
        int topIndex = GetTopBallIndex();

        if (topIndex < 0)
        {
            return null;
        }

        Ball removedBall = balls[topIndex];
        balls[topIndex] = null;

        RevealTopMysteryBall();
        return removedBall;
    }

    public void ShowTopSelection()
    {
        SnapBallsToSlots();

        int selectedCount = GetTopSameColorCount();
        int topIndex = GetTopBallIndex();

        if (selectedCount <= 0 || topIndex < 0)
        {
            return;
        }
        SoundManager.Instance.PlaySound("select");

        int firstSelectedIndex = topIndex - selectedCount + 1;
        Vector3 lift = transform.up * selectedLiftDistance;

        for (int i = firstSelectedIndex; i <= topIndex; i++)
        {
            if (balls[i] == null)
            {
                continue;
            }

            balls[i].transform.DOKill();

            balls[i].transform
                .DOMove(GetSlotWorldPosition(i) + lift, 0.15f)
                .SetEase(Ease.OutQuad);
        }
    }

    public void ClearSelectionVisual()
    {
        SnapBallsToSlots();
    }

    public Tween AnimateBallToCurrentSlot(Ball ball, float duration)
    {
        if (ball == null)
        {
            return null;
        }

        int slotIndex = -1;

        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i] == ball)
            {
                slotIndex = i;
                break;
            }
        }

        if (slotIndex < 0)
        {
            return null;
        }

        ball.transform.DOKill();
        return ball.transform
            .DOMove(GetSlotWorldPosition(slotIndex), Mathf.Max(0.01f, duration))
            .SetEase(Ease.OutQuad)
            .OnComplete(() => SoundManager.instance.playsound("deliverd"));
    }

    public void SnapBallsToSlots()
    {
        if (balls == null)
        {
            return;
        }

        for (int i = 0; i < balls.Length; i++)
        {
            Ball ball = balls[i];

            if (ball == null)
            {
                continue;
            }

            ball.transform.SetParent(transform, true);

            ball.transform.DOKill();

            ball.transform
                .DOMove(GetSlotWorldPosition(i), 0.2f)
                .SetEase(Ease.OutQuad);
        }
    }

    public void RefreshLockedState()
    {
        bool wasLocked = IsLocked;
        IsLocked = IsFull() && AllBallsHaveSameColor();

        if (IsLocked)
        {
            RevealAllBalls();

            // Play only when this tube changes from unsolved to solved during
            // gameplay. Do not replay it during loading or repeated refreshes.
            if (sceneStartupComplete && !wasLocked && !completionParticlesPlayed)
            {
                StartCoroutine(PlayCompletionParticles());

            }
        }
        else
        {
            RevealTopMysteryBall();
        }
    }

    private IEnumerator PlayCompletionParticles()
    {
        if (particles == null)
        {
           yield return null;
        }

        completionParticlesPlayed = true;

        if (!particles.activeSelf)
        {
            particles.SetActive(true);
        }

        ParticleSystem[] systems =
            particles.GetComponentsInChildren<ParticleSystem>(true);

        SoundManager.Instance.PlaySound("filled");

        for (int i = 0; i < systems.Length; i++)
        {
            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            systems[i].Play(true);

            yield return new WaitForSeconds(1f);

            systems[i].Stop();
        }
    }

    private void StopCompletionParticles()
    {
        if (particles == null)
        {
            return;
        }

        ParticleSystem[] systems =
            particles.GetComponentsInChildren<ParticleSystem>(true);

        for (int i = 0; i < systems.Length; i++)
        {
            systems[i].Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }
    }

    public bool[] GetMysteryHiddenStatesBottomToTop()
    {
        bool[] hiddenStates = new bool[BallCount];
        int writeIndex = 0;

        if (balls == null)
        {
            return hiddenStates;
        }

        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i] == null)
            {
                continue;
            }

            hiddenStates[writeIndex] = balls[i].IsMysteryHidden;
            writeIndex++;
        }

        return hiddenStates;
    }

    public void RevealTopMysteryBall()
    {
        Ball topBall = GetTopBall();

        if (topBall != null && topBall.IsMysteryHidden)
        {
            topBall.RevealTrueColor();
        }
    }

    public bool HasHiddenMysteryBalls()
    {
        if (balls == null)
        {
            return false;
        }

        for (int i = 0; i < balls.Length; i++)
        {
            if (balls[i] != null && balls[i].IsMysteryHidden)
            {
                return true;
            }
        }

        return false;
    }

    public int RevealAllMysteryBalls()
    {
        if (balls == null)
        {
            return 0;
        }

        int revealedCount = 0;

        for (int i = 0; i < balls.Length; i++)
        {
            Ball ball = balls[i];

            if (ball == null || !ball.IsMysteryHidden)
            {
                continue;
            }

            ball.RevealTrueColor();
            revealedCount++;
        }

        return revealedCount;
    }

    private void RevealAllBalls()
    {
        RevealAllMysteryBalls();
    }

    private bool AllBallsHaveSameColor()
    {
        if (!IsFull() || balls == null || balls.Length == 0 || balls[0] == null)
        {
            return false;
        }

        Ball.BallColor requiredColor = balls[0].color;

        for (int i = 1; i < balls.Length; i++)
        {
            if (balls[i] == null || balls[i].color != requiredColor)
            {
                return false;
            }
        }

        return true;
    }

    private int GetTopBallIndex()
    {
        if (balls == null)
        {
            return -1;
        }

        for (int i = balls.Length - 1; i >= 0; i--)
        {
            if (balls[i] != null)
            {
                return i;
            }
        }

        return -1;
    }

    private Vector3 GetSlotWorldPosition(int index)
    {
        if (cachedSlotLocalPositions == null ||
            index < 0 ||
            index >= cachedSlotLocalPositions.Length)
        {
            return transform.position;
        }

        return transform.TransformPoint(cachedSlotLocalPositions[index]);
    }

    public void Initialize(TubeData data)
    {
        balls = new Ball[Capacity];

        for (int i = 0; i < data.balls.Length; i++)
        {
            Ball newBall = Instantiate(ballPrefab, transform);

            newBall.color = data.balls[i];

            balls[i] = newBall;
        }

        SnapBallsToSlots();
        RefreshLockedState();

        initialized = true;
    }
}
