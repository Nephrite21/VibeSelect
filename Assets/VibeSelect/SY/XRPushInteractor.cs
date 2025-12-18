using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit;

public class XRPushInteractor : MonoBehaviour
{
    [Header("Ray (hover source)")]
    public UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor rayInteractor;

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

    private RaycastHit[] _hits;
    private readonly List<Candidate> _candidates = new(64);
    private readonly HashSet<Transform> _usedRoots = new();

    private int _focusIndex = 0;
    private int _prevFocusIndex = -1;
    private Vector3 _lastPushPos;
    private float _accum;

    private readonly Dictionary<Renderer, Material[]> _origMats = new();
    private readonly HashSet<Renderer> _changedRenderers = new();

    private struct Candidate
    {
        public Transform root;
        public UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable interactable;
        public Renderer[] renderers;
    }

    void Awake()
    {
        rayInteractor = GetComponent<UnityEngine.XR.Interaction.Toolkit.Interactors.XRRayInteractor>();
        _hits = new RaycastHit[Mathf.Max(8, maxHits)];

        if (!pushTransform)
            pushTransform = rayInteractor.transform;

        if (!interactionManager)
            interactionManager = FindFirstObjectByType<XRInteractionManager>();
    }

    void OnEnable()
    {
        rayInteractor.hoverEntered.AddListener(OnHoverEntered);
        rayInteractor.hoverExited.AddListener(_ => ResetPush());

        if (grabAction.action != null)
            grabAction.action.Enable();
    }

    void OnDisable()
    {
        rayInteractor.hoverEntered.RemoveListener(OnHoverEntered);
        rayInteractor.hoverExited.RemoveListener(_ => ResetPush());

        if (grabAction.action != null)
            grabAction.action.Disable();

        ClearVisuals();
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        ResetPush();
        SendHaptics(hoverHapticAmplitude, hoverHapticDuration);
    }

    void Update()
    {
        if (!rayInteractor) return;

        if (rayInteractor.interactablesHovered.Count == 0)
        {
            ClearVisuals();
            return;
        }

        var rayTf = rayInteractor.rayOriginTransform ? rayInteractor.rayOriginTransform : rayInteractor.transform;
        Vector3 origin = rayTf.position;
        Vector3 dir = rayTf.forward;

        BuildCandidates(origin, dir);
        if (_candidates.Count == 0)
        {
            ClearVisuals();
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

            // ✅ 현재 focus된 오브젝트 이름 출력
            if (_candidates.Count > 0 && _focusIndex >= 0 && _focusIndex < _candidates.Count)
                Debug.Log($"[XRPushInteractor] Focus: {_candidates[_focusIndex].root.name}");

            SendHaptics(focusTickAmplitude, focusTickDuration);
        }
        ApplyVisuals();

        // (NearFar 호출 제거됨)
        // 트리거 눌렀을 때 동작이 필요하면 여기에서 구현하면 됨.
        // if (grabAction.action != null && grabAction.action.WasPressedThisFrame()) { ... }
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

            var interactable = col.GetComponentInParent<UnityEngine.XR.Interaction.Toolkit.Interactables.XRBaseInteractable>();
            if (!interactable) continue;

            var root = interactable.transform;
            if (_usedRoots.Contains(root)) continue;
            _usedRoots.Add(root);

            var rends = root.GetComponentsInChildren<Renderer>(true);
            if (rends == null || rends.Length == 0) continue;

            _candidates.Add(new Candidate
            {
                root = root,
                interactable = interactable,
                renderers = rends
            });
        }
    }

    private void ApplyVisuals()
    {
        ClearVisuals();

        for (int i = 0; i < _focusIndex; i++)
            ApplyMaterial(_candidates[i], transparentMaterial);

        ApplyMaterial(_candidates[_focusIndex], highlightMaterial);
    }

    private void ApplyMaterial(Candidate c, Material mat)
    {
        if (!mat) return;

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

    private void ClearVisuals()
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

    private class RaycastHitDistanceComparer : IComparer<RaycastHit>
    {
        public static readonly RaycastHitDistanceComparer Instance = new();
        public int Compare(RaycastHit a, RaycastHit b) => a.distance.CompareTo(b.distance);
    }
}
