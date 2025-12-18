using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactors;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

public class XRPushInteractor : MonoBehaviour
{
    [Header("Ray (hover source)")]
    public XRRayInteractor rayInteractor;

    [Header("Push movement reference")]
    public Transform pushTransform;

    public XRInteractionManager interactionManager;

    [Tooltip("트리거 입력 액션(RightHand Select)")]
    public InputActionProperty grabAction;

    [Header("Haptics (direct)")]
    public XRNode hapticNode = XRNode.RightHand;
    [Range(0f, 1f)] public float hoverHapticAmplitude = 0.25f;
    [Range(0.01f, 0.3f)] public float hoverHapticDuration = 0.06f;
    [Range(0f, 1f)] public float focusTickAmplitude = 0.18f;
    [Range(0.01f, 0.2f)] public float focusTickDuration = 0.04f;

    [Header("Raycast candidates")]
    public LayerMask hitMask = ~0;
    public float maxDistance = 20f;
    public int maxHits = 64;

    [Header("Push gesture")]
    public float stepDistance = 0.03f;
    public float backStepDistance = 0.03f;

    [Header("Visuals (material swap)")]
    public Material transparentMaterial;
    public Material highlightMaterial;

    [Header("UI Lift (layer == UI)")]
    public float uiLiftY = 0.9f;

    private RaycastHit[] _hits;
    private readonly List<Candidate> _candidates = new(64);
    private readonly HashSet<Transform> _usedRoots = new();

    private int _focusIndex = 0;
    private int _prevFocusIndex = -1;
    private Vector3 _lastPushPos;
    private float _accum;

    private readonly Dictionary<Renderer, Material[]> _origMats = new();
    private readonly HashSet<Renderer> _changedRenderers = new();

    private int _uiLayer;
    private readonly HashSet<Transform> _uiLifted = new(); //UI가 "올라간 상태"인지 추적(중복 이동 방지)

    [SerializeField] private UIIndexSelector indexSelector;

    private struct Candidate
    {
        public Transform root;
        public XRBaseInteractable interactable;
        public Renderer[] renderers;

        // -----------------------------
        // (기존) UI의 경우 원래 위치 저장
        // public Vector3 originalPosition;
        // -----------------------------

        public bool isUI; // layer == UI 여부
    }

    void Awake()
    {
        rayInteractor = GetComponent<XRRayInteractor>();
        _hits = new RaycastHit[Mathf.Max(8, maxHits)];

        _uiLayer = LayerMask.NameToLayer("UI");

        if (!pushTransform)
            pushTransform = rayInteractor.transform;

        if (!interactionManager)
            interactionManager = FindFirstObjectByType<XRInteractionManager>();
    }

    void OnEnable()
    {
        rayInteractor.hoverEntered.AddListener(OnHoverEntered);
        rayInteractor.hoverExited.AddListener(OnHoverExited);

        if (grabAction.action != null)
            grabAction.action.Enable();
    }

    void OnDisable()
    {
        rayInteractor.hoverEntered.RemoveListener(OnHoverEntered);
        rayInteractor.hoverExited.RemoveListener(OnHoverExited);

        if (grabAction.action != null)
            grabAction.action.Disable();

        ResetAllVisuals(); //머티리얼 원복 + UI 내려주기까지

        // ✅ (추가) Hover가 끝나거나 비활성화될 때 UIIndexSelector도 정리하고 싶으면 사용
        if (indexSelector) indexSelector.ResetAll();
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        ResetPush();
        SendHaptics(hoverHapticAmplitude, hoverHapticDuration);
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        ResetPush();
        ResetAllVisuals();

        // ✅ (추가) Hover가 끝나면 선택 UI도 해제하고 싶으면
        if (indexSelector) indexSelector.ResetAll();
    }

    void Update()
    {
        if (!rayInteractor)
        {
            Debug.Log("0");
            return;
        }

        if (rayInteractor.interactablesHovered.Count == 0)
        {
            ResetAllVisuals();
            Debug.Log("1");

            // ✅ (추가) Hover가 없으면 UIIndexSelector도 해제하고 싶으면
            if (indexSelector) indexSelector.ResetAll();

            return;
        }

        var rayTf = rayInteractor.rayOriginTransform ? rayInteractor.rayOriginTransform : rayInteractor.transform;
        Vector3 origin = rayTf.position;
        Vector3 dir = rayTf.forward;

        BuildCandidates(origin, dir);
        if (_candidates.Count == 0)
        {
            ResetAllVisuals();
            Debug.Log("2");

            // ✅ (추가) 후보가 없으면 UIIndexSelector도 해제하고 싶으면
            if (indexSelector) indexSelector.ResetAll();

            return;
        }

        _focusIndex = Mathf.Clamp(_focusIndex, 0, _candidates.Count - 1);

        // push 계산
        Vector3 pushPos = pushTransform.position;
        float forwardMove = Vector3.Dot(pushPos - _lastPushPos, dir);
        _lastPushPos = pushPos;
        _accum += forwardMove;

        while (_accum >= stepDistance)
        {
            _accum -= stepDistance;
            if (_focusIndex < _candidates.Count - 1) _focusIndex++;
        }

        while (_accum <= -backStepDistance)
        {
            _accum += backStepDistance;
            if (_focusIndex > 0) _focusIndex--;
        }

        if (_focusIndex != _prevFocusIndex)
        {
            _prevFocusIndex = _focusIndex;

            if (_candidates.Count > 0 && _focusIndex >= 0 && _focusIndex < _candidates.Count)
                Debug.Log($"[XRPushInteractor] Focus: {_candidates[_focusIndex].root.name}");

            SendHaptics(focusTickAmplitude, focusTickDuration);

            // ----------------------------------------------------
            // ✅ (추가) "UI를 선택하게 되는 경우" -> indexSelector.SetIndex 호출
            // ----------------------------------------------------
            NotifyIndexSelectorOnFocusChanged();
        }

        ApplyVisuals();
    }

    /// <summary>
    /// 포커스가 바뀌는 순간에만 호출.
    /// UI 레이어가 포커스되면 indexSelector.SetIndex(현재 포커스 인덱스) 호출.
    /// </summary>
    private void NotifyIndexSelectorOnFocusChanged()
    {
        if (!indexSelector) return;
        if (_candidates.Count == 0) return;
        if (_focusIndex < 0 || _focusIndex >= _candidates.Count) return;

        var focused = _candidates[_focusIndex];

        if (focused.isUI)
        {
            // ✅ 요구사항: UI를 선택(포커스)하게 되는 경우 인덱스 전달
            indexSelector.SetIndex(_focusIndex);
        }
        else
        {
            // (선택) UI가 아닌 걸로 포커스가 바뀌면 기존 UI 선택 해제
            // 원하면 주석 해제
            // indexSelector.ResetAll();
        }
    }

    private void SendHaptics(float amplitude, float duration)
    {
        var device = InputDevices.GetDeviceAtXRNode(hapticNode);
        if (!device.isValid) return;

        if (device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse && caps.numChannels > 0)
            device.SendHapticImpulse(0u, amplitude, duration);
    }

    private void ResetPush()
    {
        _accum = 0f;
        _lastPushPos = pushTransform ? pushTransform.position : transform.position;
        _focusIndex = 0;
        _prevFocusIndex = -1;
    }

    private void BuildCandidates(Vector3 origin, Vector3 dir)
    {
        _candidates.Clear();
        _usedRoots.Clear();

        int hitCount = Physics.RaycastNonAlloc(
            new Ray(origin, dir),
            _hits,
            maxDistance,
            hitMask,
            QueryTriggerInteraction.Ignore
        );

        if (hitCount <= 0) return;

        Array.Sort(_hits, 0, hitCount, RaycastHitDistanceComparer.Instance);

        for (int i = 0; i < hitCount; i++)
        {
            var col = _hits[i].collider;
            if (!col) continue;

            var interactable = col.GetComponentInParent<XRBaseInteractable>();
            if (!interactable) continue;

            var root = interactable.transform;
            if (_usedRoots.Contains(root)) continue;
            _usedRoots.Add(root);

            bool isUI = (_uiLayer != -1) && (root.gameObject.layer == _uiLayer);

            var rends = root.GetComponentsInChildren<Renderer>(true);

            // UI는 Renderer 없어도 후보 등록 가능, 일반은 Renderer 필수
            if (!isUI && (rends == null || rends.Length == 0)) continue;

            _candidates.Add(new Candidate
            {
                root = root,
                interactable = interactable,
                renderers = rends,
                isUI = isUI
            });
        }
    }

    private void ApplyVisuals()
    {
        ClearMaterialsOnly();

        // 1) 포커스 이전 일반 오브젝트는 투명 처리
        for (int i = 0; i < _focusIndex; i++)
        {
            var c = _candidates[i];
            if (!c.isUI)
                ApplyMaterial(c, transparentMaterial);
        }

        // 2) 포커스 대상 처리
        var focused = _candidates[_focusIndex];
        if (focused.isUI)
        {
            LiftUIOnce(focused.root); //선택되면 한번만 +Y
        }
        else
        {
            ApplyMaterial(focused, highlightMaterial);
        }

        // 3) 현재 포커스가 아닌 UI는 내려주기
        foreach (var t in new List<Transform>(_uiLifted))
        {
            if (!t) { _uiLifted.Remove(t); continue; }

            if (t != focused.root)
                DropUIOnce(t);
        }
    }

    private void LiftUIOnce(Transform t)
    {
        if (!t) return;
        if (_uiLifted.Contains(t)) return;

        t.position += Vector3.up * uiLiftY;
        _uiLifted.Add(t);
    }

    private void DropUIOnce(Transform t)
    {
        if (!t) return;
        if (!_uiLifted.Contains(t)) return;

        t.position -= Vector3.up * uiLiftY;
        _uiLifted.Remove(t);
    }

    private void ApplyMaterial(Candidate c, Material mat)
    {
        if (!mat) return;
        if (c.renderers == null || c.renderers.Length == 0) return;

        foreach (var r in c.renderers)
        {
            if (!r) continue;

            if (!_origMats.ContainsKey(r))
                _origMats[r] = r.materials;

            var mats = r.materials;
            var newMats = new Material[mats.Length];
            for (int i = 0; i < newMats.Length; i++)
                newMats[i] = mat;

            r.materials = newMats;
            _changedRenderers.Add(r);
        }
    }

    private void ClearMaterialsOnly()
    {
        if (_changedRenderers.Count == 0) return;

        foreach (var r in new List<Renderer>(_changedRenderers))
        {
            if (!r) continue;
            if (_origMats.TryGetValue(r, out var orig))
                r.materials = orig;
        }
        _changedRenderers.Clear();
    }

    private void ResetAllVisuals()
    {
        ClearMaterialsOnly();

        foreach (var t in new List<Transform>(_uiLifted))
        {
            if (!t) continue;
            DropUIOnce(t);
        }
        _uiLifted.Clear();
    }

    private class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}


