using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "New Level", menuName = "Ball Sort/Level")]
public class LevelData : ScriptableObject
{
    [Range(3, 7)]
    [SerializeField] private int numberOfColors = 3;

    [SerializeField] private Ball.BallColor[] selectedColors =
    {
        Ball.BallColor.Red,
        Ball.BallColor.Green,
        Ball.BallColor.Blue,
        Ball.BallColor.Yellow,
        Ball.BallColor.LightBlue,
        Ball.BallColor.Pink,
        Ball.BallColor.Orange,
        Ball.BallColor.Black,
        Ball.BallColor.Gray,
        Ball.BallColor.NeonPink,
        Ball.BallColor.NeonGreen,
        Ball.BallColor.NeonBlue,
        Ball.BallColor.Purple
    };

    [Header("Mystery Balls")]
    [Tooltip("How many filled tubes begin as mystery tubes. Clamped from zero to Number Of Colors.")]
    [SerializeField] private int mysteryTubeCount;

    [Tooltip("Sprite shown on unrevealed mystery balls. The original ball prefab and true color enum stay unchanged.")]
    [SerializeField] private Sprite mysteryBallSprite;

    [Header("Generated Layout")]
    [Tooltip("More attempts let the generator search for a better-mixed starting layout.")]
    [Range(25, 1000)]
    [SerializeField] private int generationAttempts = 250;

    [Tooltip("Enable this when you want the level layout to remain identical every time it is loaded.")]
    [SerializeField] private bool useFixedSeed = true;

    [SerializeField] private int fixedSeed = 12345;

    public int NumberOfColors => numberOfColors;
    public int MysteryTubeCount => mysteryTubeCount;
    public Sprite MysteryBallSprite => mysteryBallSprite;
    public int GenerationAttempts => generationAttempts;
    public bool UseFixedSeed => useFixedSeed;
    public int FixedSeed => fixedSeed;

    public bool TryGetSelectedColors(out Ball.BallColor[] colors, out string error)
    {
        colors = null;
        error = string.Empty;

        if (numberOfColors < 3 || numberOfColors > 7)
        {
            error = "A Ball Sort level must use between 3 and 7 color slots.";
            return false;
        }

        if (selectedColors == null || selectedColors.Length != numberOfColors)
        {
            error = "The Selected Colors array must contain exactly " +
                    numberOfColors + " colors.";
            return false;
        }

        if (mysteryTubeCount > 0 && mysteryBallSprite == null)
        {
            error = "Assign a Mystery Ball Sprite when Mystery Tube Count is greater than zero.";
            return false;
        }

        Dictionary<Ball.BallColor, int> colorUseCounts =
            new Dictionary<Ball.BallColor, int>();

        for (int i = 0; i < selectedColors.Length; i++)
        {
            Ball.BallColor selectedColor = selectedColors[i];

            if (!colorUseCounts.ContainsKey(selectedColor))
            {
                colorUseCounts[selectedColor] = 0;
            }

            colorUseCounts[selectedColor]++;

            if (colorUseCounts[selectedColor] > 2)
            {
                error = selectedColor + " can be used at most twice.";
                return false;
            }

            if (colorUseCounts[selectedColor] > 1 && numberOfColors <= 5)
            {
                error = "Duplicate colors are only allowed when the level has more than 5 color slots.";
                return false;
            }
        }

        colors = new Ball.BallColor[selectedColors.Length];
        Array.Copy(selectedColors, colors, selectedColors.Length);
        return true;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        int availableColorCount = Enum.GetValues(typeof(Ball.BallColor)).Length;

        numberOfColors = Mathf.Clamp(numberOfColors, 3, Mathf.Min(7, availableColorCount));
        mysteryTubeCount = Mathf.Clamp(mysteryTubeCount, 0, numberOfColors);

        if (selectedColors == null)
        {
            selectedColors = new Ball.BallColor[numberOfColors];
        }
        else if (selectedColors.Length != numberOfColors)
        {
            Array.Resize(ref selectedColors, numberOfColors);
        }

        generationAttempts = Mathf.Max(25, generationAttempts);
    }
#endif
}
