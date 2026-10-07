using System.Collections;
using DG.Tweening;
using UnityEngine;
using UnityEngine.EventSystems;

[DisallowMultipleComponent]
public class BallSortGameController : MonoBehaviour
{
    public static BallSortGameController Instance { get; private set; }

    [Header("Scene")]
    [SerializeField] private Tube[] tubes;

    [Header("Tube Input")]
    [Tooltip("Optional. If empty, Camera.main is used.")]
    [SerializeField] private Camera inputCamera;

    [Tooltip("Layers checked when looking for a tube. Everything is allowed by default.")]
    [SerializeField] private LayerMask tubeClickLayers = ~0;

    private Tube selectedTube;
    private int selectedBallCount;
    private bool gameWon;
    private bool inputEnabled;
    private bool isTransferring;

    [Header("Ball Transfer Animation")]
    [SerializeField] private float ballMoveDuration = 0.18f;
    [SerializeField] private float delayBetweenBalls = 0.06f;

    // Prevents a central Physics2D raycast and any legacy OnMouseDown call
    // from handling the same press twice in one frame.
    private int lastHandledClickFrame = -1;

    public bool GameWon => gameWon;
    public bool InputEnabled => inputEnabled;

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
        SetInputEnabled(false);
        RefreshScene();
    }

    private void Update()
    {
        if (!inputEnabled || gameWon || isTransferring)
        {
            return;
        }

        if (!TryGetPointerDown(out Vector2 screenPosition, out int pointerId))
        {
            return;
        }

        // World tubes get priority over UI. This fixes UI graphics behind a tube
        // incorrectly blocking the tube because IsPointerOverGameObject returned true.
        if (TryGetTubeAtScreenPosition(screenPosition, out Tube clickedTube))
        {
            NotifyTubeClicked(clickedTube);
            return;
        }

        // No tube was physically hit. A real UI click should not cancel selection.
        if (!IsPointerOverUI(pointerId))
        {
            CancelSelection();
        }
    }

    private bool TryGetPointerDown(
        out Vector2 screenPosition,
        out int pointerId)
    {
#if UNITY_EDITOR || UNITY_STANDALONE || UNITY_WEBGL
        // Left mouse click OR right mouse click.
        if (Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1))
        {
            screenPosition = Input.mousePosition;
            pointerId = -1;
            return true;
        }
#endif

#if UNITY_ANDROID || UNITY_IOS
    if (Input.touchCount > 0)
    {
        Touch touch = Input.GetTouch(0);

        if (touch.phase == TouchPhase.Began)
        {
            screenPosition = touch.position;
            pointerId = touch.fingerId;
            return true;
        }
    }
#endif

        screenPosition = default;
        pointerId = -1;
        return false;
    }

    private bool TryGetTubeAtScreenPosition(
        Vector2 screenPosition,
        out Tube clickedTube)
    {
        clickedTube = null;

        Camera cameraToUse = inputCamera != null
            ? inputCamera
            : Camera.main;

        if (cameraToUse == null)
        {
            Debug.LogError(
                "Tube input needs a Camera. Assign Input Camera or tag the game camera MainCamera.",
                this
            );
            return false;
        }

        Ray screenRay = cameraToUse.ScreenPointToRay(screenPosition);

        RaycastHit2D[] hits = Physics2D.GetRayIntersectionAll(
            screenRay,
            Mathf.Infinity,
            tubeClickLayers
        );

        float nearestDistance = float.PositiveInfinity;

        for (int i = 0; i < hits.Length; i++)
        {
            Collider2D hitCollider = hits[i].collider;

            if (hitCollider == null)
            {
                continue;
            }

            // This also works when the collider belongs to a ball or another
            // child object inside the tube.
            Tube tube = hitCollider.GetComponentInParent<Tube>();

            if (tube == null || !tube.CompareTag("Tube"))
            {
                continue;
            }

            if (hits[i].distance < nearestDistance)
            {
                nearestDistance = hits[i].distance;
                clickedTube = tube;
            }
        }

        return clickedTube != null;
    }

    public void SetInputEnabled(bool enabled)
    {
        inputEnabled = enabled;

        if (!enabled)
        {
            isTransferring = false;
        }

        if (!enabled)
        {
            CancelSelection();
        }
    }

    public void NotifyTubeClicked(Tube clickedTube)
    {
        if (!inputEnabled || gameWon || isTransferring || clickedTube == null)
        {
            return;
        }

        if (lastHandledClickFrame == Time.frameCount)
        {
            return;
        }

        lastHandledClickFrame = Time.frameCount;
        HandleTubeClicked(clickedTube);
    }

    private void HandleTubeClicked(Tube clickedTube)
    {
        if (selectedTube == null)
        {
            SelectTube(clickedTube);
            return;
        }

        if (clickedTube == selectedTube)
        {
            CancelSelection();
            return;
        }

        if (clickedTube.IsLocked)
        {
            CancelSelection();
            return;
        }

        TryTransferTo(clickedTube);
    }

    private void SelectTube(Tube tube)
    {
        if (tube == null || tube.IsEmpty() || tube.IsLocked)
        {
            return;
        }

        selectedTube = tube;
        selectedBallCount = tube.GetTopSameColorCount();

        if (selectedBallCount <= 0)
        {
            selectedTube = null;
            return;
        }

        selectedTube.ShowTopSelection();
    }

    private void TryTransferTo(Tube targetTube)
    {
        if (selectedTube == null || targetTube == null)
        {
            CancelSelection();
            return;
        }

        Ball selectedTopBall = selectedTube.GetTopBall();

        if (selectedTopBall == null ||
            !targetTube.CanAccept(selectedTopBall.color))
        {
            CancelSelection();
            return;
        }

        int transferAmount =
            Mathf.Min(selectedBallCount, targetTube.FreeSpace);

        if (transferAmount <= 0)
        {
            CancelSelection();
            return;
        }

        Tube sourceTube = selectedTube;

        sourceTube.ClearSelectionVisual();
        selectedTube = null;
        selectedBallCount = 0;

        StartCoroutine(TransferBallsOneByOne(
            sourceTube,
            targetTube,
            transferAmount));
    }

    private IEnumerator TransferBallsOneByOne(
        Tube sourceTube,
        Tube targetTube,
        int transferAmount)
    {
        isTransferring = true;
        int movedCount = 0;

        for (int i = 0; i < transferAmount; i++)
        {
            Ball ball = sourceTube.RemoveTopBall();

            if (ball == null)
            {
                break;
            }

            if (!targetTube.AddBall(ball))
            {
                sourceTube.AddBall(ball);
                sourceTube.SnapBallsToSlots();
                break;
            }

            movedCount++;

            Tween moveTween = targetTube.AnimateBallToCurrentSlot(
                ball,
                ballMoveDuration);

            if (moveTween != null)
            {
                yield return moveTween.WaitForCompletion();
            }

            if (i < transferAmount - 1 && delayBetweenBalls > 0f)
            {
                yield return new WaitForSeconds(delayBetweenBalls);
            }
        }

        sourceTube.SnapBallsToSlots();
        targetTube.SnapBallsToSlots();

        sourceTube.RefreshLockedState();
        targetTube.RefreshLockedState();

        isTransferring = false;

        if (movedCount <= 0)
        {
            yield break;
        }

        bool won = CheckForWin();

        if (!won && LevelManager.Instance != null)
        {
            LevelManager.Instance.SaveCurrentLevelState();
        }
    }

    private void CancelSelection()
    {
        if (selectedTube != null)
        {
            selectedTube.ClearSelectionVisual();
        }

        selectedTube = null;
        selectedBallCount = 0;
    }

    private bool IsPointerOverUI(int pointerId)
    {
        if (EventSystem.current == null)
        {
            return false;
        }

        if (pointerId >= 0)
        {
            return EventSystem.current.IsPointerOverGameObject(pointerId);
        }

        return EventSystem.current.IsPointerOverGameObject();
    }

    public void RefreshAllTubeLocks()
    {
        if (tubes == null)
        {
            return;
        }

        for (int i = 0; i < tubes.Length; i++)
        {
            if (tubes[i] != null)
            {
                tubes[i].RefreshLockedState();
            }
        }
    }

    public void RefreshScene()
    {
        CancelSelection();
        gameWon = false;

        tubes = FindObjectsOfType<Tube>();

        RefreshAllTubeLocks();
        CheckForWin();
    }

    private bool CheckForWin()
    {
        if (gameWon || tubes == null || tubes.Length == 0)
        {
            return gameWon;
        }

        bool foundTube = false;

        for (int i = 0; i < tubes.Length; i++)
        {
            Tube tube = tubes[i];

            if (tube == null)
            {
                continue;
            }

            foundTube = true;

            if (!tube.IsEmpty() && !tube.IsLocked)
            {
                return false;
            }
        }

        if (!foundTube)
        {
            return false;
        }

        gameWon = true;
        inputEnabled = false;

        Debug.Log("Level Complete!");

        if (LevelManager.Instance != null)
        {
            LevelManager.Instance.RunHandleLevelCompleted();
            SoundManager.Instance.PlaySound("complete");
        }

        return true;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }
}
