using DG.Tweening;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

[RequireComponent(typeof(Button))]
public class ButtonScript : MonoBehaviour
{
    private UnityEvent _capturedEvent = new UnityEvent();

    public Button button;
    public bool completeAnimationBeforeInvoke = true;
    public bool changeButtonInteractableState = false;
    public bool isWorldSpaceBtn = false;

    [Header("Tween Animation Settings")]
    public float scaleMultiplier = .1f;
    public float duration = .4f;
    public int vibrato = 9;
    public float elasticity = 1;

    Vector3 initialScale = Vector3.zero;
    bool isInProcess = false;

    private void Awake()
    {
        if (button == null)
            button = GetComponent<Button>();
    }

    void Start()
    {
        // swap reference AFTER others added listeners
        _capturedEvent = button.onClick;
        button.onClick = new Button.ButtonClickedEvent();
        button.onClick.AddListener(ButtonClicked);
    }

    public void ButtonClicked()
    {
        if (!isInProcess)
        {
            isInProcess = true;

            if (changeButtonInteractableState)
                button.interactable = false;

            Vector3 initialScaleTemp = transform.localScale;
            if (initialScale.magnitude == 0)
                initialScale = initialScaleTemp;

            transform.localScale = initialScale;
            transform.DOPunchScale(-initialScale * scaleMultiplier, duration, vibrato, elasticity).OnComplete(() =>
            {
                if (changeButtonInteractableState)
                    button.interactable = true;

                isInProcess = false;
            });
            DOVirtual.DelayedCall((completeAnimationBeforeInvoke ? 0.25f : 0), () =>
            {
                _capturedEvent?.Invoke();

                button.onClick.RemoveListener(ButtonClicked);
                button.onClick.Invoke();
                button.onClick.AddListener(ButtonClicked);
            });
        }
    }
    private void OnMouseDown()
    {
        if (isWorldSpaceBtn)
        {
            ButtonClicked();
        }
    }
}