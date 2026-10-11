using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

[RequireComponent(typeof(XRBaseInteractable))]
public class HoverScaler : MonoBehaviour
{
    public float hoverScale = 1.1f;
    public float lerpSpeed = 12f;

    private Vector3 _originalScale;
    private Vector3 _targetScale;
    private XRBaseInteractable _interactable;

    void Awake()
    {
        _originalScale = transform.localScale;
        _targetScale = _originalScale;
        _interactable = GetComponent<XRBaseInteractable>();
        Debug.Log($"[HoverScaler] {name} Awake: interactable found = {_interactable != null}");
    }

    void OnEnable()
    {
        if (_interactable == null)
        {
            Debug.LogWarning($"[HoverScaler] {name} OnEnable: interactable is NULL, can't subscribe");
            return;
        }
        _interactable.hoverEntered.AddListener(OnHoverEntered);
        _interactable.hoverExited.AddListener(OnHoverExited);
        _interactable.selectEntered.AddListener(OnSelectEntered);
        Debug.Log($"[HoverScaler] {name} OnEnable: subscribed to hover+select events on {_interactable.GetType().Name}");
    }

    void OnDisable()
    {
        if (_interactable == null) return;
        _interactable.hoverEntered.RemoveListener(OnHoverEntered);
        _interactable.hoverExited.RemoveListener(OnHoverExited);
        _interactable.selectEntered.RemoveListener(OnSelectEntered);
    }

    void Update()
    {
        transform.localScale = Vector3.Lerp(transform.localScale, _targetScale, Time.deltaTime * lerpSpeed);
    }

    private void OnHoverEntered(UnityEngine.XR.Interaction.Toolkit.HoverEnterEventArgs args)
    {
        _targetScale = _originalScale * hoverScale;
        Debug.Log($"[HoverScaler] {name} ✅ HOVER ENTERED by '{args.interactorObject?.transform.name ?? "?"}'");
    }

    private void OnHoverExited(UnityEngine.XR.Interaction.Toolkit.HoverExitEventArgs args)
    {
        _targetScale = _originalScale;
        Debug.Log($"[HoverScaler] {name} ❌ HOVER EXITED by '{args.interactorObject?.transform.name ?? "?"}'");
    }

    private void OnSelectEntered(UnityEngine.XR.Interaction.Toolkit.SelectEnterEventArgs args)
    {
        Debug.Log($"[HoverScaler] {name} 🎯 SELECT ENTERED by '{args.interactorObject?.transform.name ?? "?"}'");
    }
}