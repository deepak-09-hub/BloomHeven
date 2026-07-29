using UnityEngine;

[DisallowMultipleComponent]
public class Ball : MonoBehaviour
{
    public enum BallColor
    {
        Red,
        Green,
        Blue,
        Yellow,
        LightBlue,
        Pink,
        Orange,
        Black,
        Gray,
        NeonPink,
        NeonGreen,
        NeonBlue,
        Purple
    }

    public BallColor color;

    [Header("Mystery Visual")]
    [Tooltip("Optional. If empty, the SpriteRenderer on this object or a child is used.")]
    [SerializeField] private SpriteRenderer ballRenderer;

    private Sprite trueColorSprite;

    public bool IsMysteryHidden { get; private set; }

    private void Awake()
    {
        CacheRendererAndTrueSprite();
    }

    private void CacheRendererAndTrueSprite()
    {
        if (ballRenderer == null)
        {
            ballRenderer = GetComponent<SpriteRenderer>();
        }

        if (ballRenderer == null)
        {
            ballRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        if (ballRenderer != null && trueColorSprite == null)
        {
            trueColorSprite = ballRenderer.sprite;
        }
    }

    public void SetMysteryState(bool hidden, Sprite mysterySprite)
    {
        CacheRendererAndTrueSprite();

        IsMysteryHidden = hidden && mysterySprite != null;

        if (ballRenderer == null)
        {
            Debug.LogError("Ball needs a SpriteRenderer for the mystery-ball visual.", this);
            return;
        }

        ballRenderer.sprite = IsMysteryHidden
            ? mysterySprite
            : trueColorSprite;
    }

    public void RevealTrueColor()
    {
        CacheRendererAndTrueSprite();
        IsMysteryHidden = false;

        if (ballRenderer != null)
        {
            ballRenderer.sprite = trueColorSprite;
        }
    }
}
