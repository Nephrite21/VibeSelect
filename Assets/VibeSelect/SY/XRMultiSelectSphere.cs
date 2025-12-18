using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

public class XRMultiSelectSphere : MonoBehaviour
{
    [Header("Hands (Transforms)")]
    public Transform leftHand;
    public Transform rightHand;

    [Header("Head (Camera) for forward offset")]
    public Transform head;
    public float forwardOffset = 0.15f;

    [Header("Disable Ray Interaction while MultiSelect is active")]
    public Behaviour leftRayInteractor;     // XRRayInteractor
    public Behaviour rightRayInteractor;    // XRRayInteractor
    public Behaviour leftRayVisual;         // XRInteractorLineVisual(있으면)
    public Behaviour rightRayVisual;        // XRInteractorLineVisual(있으면)

    [Header("Find & disable XRPushInteractor under controller roots")]
    public Transform leftControllerRoot;    // LeftHand Controller 루트 (여기 아래에서 XRPushInteractor 찾음)
    public Transform rightControllerRoot;   // RightHand Controller 루트

    [Header("Visual Sphere (optional)")]
    public Transform sphereVisual;
    public Material sphereMaterial;         // 구체 전용 머티리얼(투명)
    public float minRadius = 0.06f;
    public float maxRadius = 0.60f;

    [Header("Selection")]
    public LayerMask selectableMask;
    public int maxOverlaps = 128;
    public QueryTriggerInteraction triggerInteraction = QueryTriggerInteraction.Ignore;

    [Header("Highlight (Material Swap)")]
    public Material highlightMaterial;      // 오브젝트 하이라이트용(구체와 다르게!)

    [Header("Haptics (Quest 2)")]
    public XRNode leftHapticNode = XRNode.LeftHand;
    public XRNode rightHapticNode = XRNode.RightHand;
    [Range(0f, 1f)] public float enterAmp = 0.2f;
    [Range(0.01f, 0.3f)] public float enterDur = 0.05f;
    [Range(0f, 1f)] public float exitAmp = 0.12f;
    [Range(0.01f, 0.3f)] public float exitDur = 0.03f;

    [Header("Debug")]
    public bool debugLogs = false;

    private Collider[] _overlapBuf;
    private bool _active;

    private Vector3 _center;
    private float _radius;

    private readonly HashSet<Transform> _current = new();
    private readonly HashSet<Transform> _prev = new();

    private readonly Dictionary<Renderer, Material[]> _origMats = new();
    private readonly HashSet<Renderer> _changedRenderers = new();

    // Ray enable 상태 저장(복구용)
    private bool _leftRayPrevEnabled = true;
    private bool _rightRayPrevEnabled = true;
    private bool _leftVisualPrevEnabled = true;
    private bool _rightVisualPrevEnabled = true;

    // ✅ XRPushInteractor 캐시 + 이전 상태 저장
    private Behaviour _leftPushInteractor;
    private Behaviour _rightPushInteractor;
    private bool _leftPushPrevEnabled = true;
    private bool _rightPushPrevEnabled = true;

    void Awake()
    {
        _overlapBuf = new Collider[Mathf.Max(8, maxOverlaps)];

        if (sphereVisual)
        {
            sphereVisual.gameObject.SetActive(false);

            if (sphereMaterial)
            {
                var mr = sphereVisual.GetComponentInChildren<MeshRenderer>(true);
                if (mr) mr.sharedMaterial = sphereMaterial;
            }
        }

        CachePushInteractors();
    }

    void Update()
    {
        if (!leftHand || !rightHand) return;

        bool lGrip = ReadGrip(XRNode.LeftHand);
        bool rGrip = ReadGrip(XRNode.RightHand);
        bool gripBoth = lGrip && rGrip;

        if (gripBoth && !_active) Begin();
        if (!gripBoth && _active) EndAndClearSelection();

        if (!_active) return;

        UpdateSphere();
        UpdateSelection();
    }

    // -------- Mode control --------
    private void Begin()
    {
        _active = true;
        _current.Clear();
        _prev.Clear();

        if (sphereVisual) sphereVisual.gameObject.SetActive(true);

        CachePushInteractors();
        DisableRays(true);

        if (debugLogs) Debug.Log("[MultiSelect] BEGIN (rays + XRPushInteractor disabled)");
    }

    private void EndAndClearSelection()
    {
        _active = false;

        if (sphereVisual) sphereVisual.gameObject.SetActive(false);

        // 선택 종료이므로 하이라이트 전부 해제
        foreach (var t in _current)
            SetHighlighted(t, false);

        _current.Clear();
        _prev.Clear();

        DisableRays(false);

        if (debugLogs) Debug.Log("[MultiSelect] END (rays + XRPushInteractor restored)");
    }

    // ✅ 컨트롤러 루트 아래에서 XRPushInteractor를 정확히 찾아 캐시
    private void CachePushInteractors()
    {
        if (_leftPushInteractor == null && leftControllerRoot != null)
            _leftPushInteractor = leftControllerRoot.GetComponentInChildren<XRPushInteractor>(true);

        if (_rightPushInteractor == null && rightControllerRoot != null)
            _rightPushInteractor = rightControllerRoot.GetComponentInChildren<XRPushInteractor>(true);
    }

    private void DisableRays(bool disable)
    {
        // 현재 enabled 상태 저장 → 나중에 원복
        if (disable)
        {
            if (leftRayInteractor) _leftRayPrevEnabled = leftRayInteractor.enabled;
            if (rightRayInteractor) _rightRayPrevEnabled = rightRayInteractor.enabled;
            if (leftRayVisual) _leftVisualPrevEnabled = leftRayVisual.enabled;
            if (rightRayVisual) _rightVisualPrevEnabled = rightRayVisual.enabled;

            // ✅ XRPushInteractor도 현재 상태 저장
            if (_leftPushInteractor) _leftPushPrevEnabled = _leftPushInteractor.enabled;
            if (_rightPushInteractor) _rightPushPrevEnabled = _rightPushInteractor.enabled;
        }

        if (leftRayInteractor) leftRayInteractor.enabled = disable ? false : _leftRayPrevEnabled;
        if (rightRayInteractor) rightRayInteractor.enabled = disable ? false : _rightRayPrevEnabled;

        if (leftRayVisual) leftRayVisual.enabled = disable ? false : _leftVisualPrevEnabled;
        if (rightRayVisual) rightRayVisual.enabled = disable ? false : _rightVisualPrevEnabled;

        // ✅ XRPushInteractor disable/restore
        if (_leftPushInteractor) _leftPushInteractor.enabled = disable ? false : _leftPushPrevEnabled;
        if (_rightPushInteractor) _rightPushInteractor.enabled = disable ? false : _rightPushPrevEnabled;
    }

    // -------- Sphere update --------
    private void UpdateSphere()
    {
        Vector3 mid = (leftHand.position + rightHand.position) * 0.5f;

        Vector3 fwd;
        if (head != null)
        {
            fwd = head.forward;
            fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-5f) fwd = Vector3.forward;
            fwd.Normalize();
        }
        else
        {
            fwd = (leftHand.forward + rightHand.forward) * 0.5f;
            if (fwd.sqrMagnitude < 1e-5f) fwd = Vector3.forward;
            fwd.Normalize();
        }

        _center = mid + fwd * forwardOffset;

        float dist = Vector3.Distance(leftHand.position, rightHand.position);
        _radius = Mathf.Clamp(dist * 0.5f, minRadius, maxRadius);

        if (sphereVisual)
        {
            sphereVisual.position = _center;
            sphereVisual.localScale = Vector3.one * (_radius * 2f);
        }
    }

    // -------- Selection update --------
    private void UpdateSelection()
    {
        _prev.Clear();
        foreach (var t in _current) _prev.Add(t);
        _current.Clear();

        int count = Physics.OverlapSphereNonAlloc(_center, _radius, _overlapBuf, selectableMask, triggerInteraction);

        for (int i = 0; i < count; i++)
        {
            var col = _overlapBuf[i];
            if (!col) continue;

            var root = col.attachedRigidbody ? col.attachedRigidbody.transform : col.transform;
            _current.Add(root);
        }

        foreach (var t in _current)
        {
            if (_prev.Contains(t)) continue;
            SetHighlighted(t, true);
            SendHapticsBoth(enterAmp, enterDur);
            if (debugLogs) Debug.Log($"[MultiSelect] Enter: {t.name}");
        }

        foreach (var t in _prev)
        {
            if (_current.Contains(t)) continue;
            SetHighlighted(t, false);
            SendHapticsBoth(exitAmp, exitDur);
            if (debugLogs) Debug.Log($"[MultiSelect] Exit: {t.name}");
        }
    }

    // -------- Highlight --------
    private void SetHighlighted(Transform root, bool on)
    {
        if (!highlightMaterial || !root) return;

        var renderers = root.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0) return;

        foreach (var r in renderers)
        {
            if (!r) continue;

            if (on)
            {
                if (!_origMats.ContainsKey(r))
                    _origMats[r] = r.materials;

                var mats = r.materials;
                var newMats = new Material[mats.Length];
                for (int i = 0; i < newMats.Length; i++)
                    newMats[i] = highlightMaterial;

                r.materials = newMats;
                _changedRenderers.Add(r);
            }
            else
            {
                if (_origMats.TryGetValue(r, out var orig))
                    r.materials = orig;

                _changedRenderers.Remove(r);
            }
        }
    }

    // -------- Grip read (direct) --------
    private bool ReadGrip(XRNode node)
    {
        var dev = InputDevices.GetDeviceAtXRNode(node);
        if (!dev.isValid) return false;

        if (dev.TryGetFeatureValue(CommonUsages.gripButton, out bool pressed))
            return pressed;

        return false;
    }

    // -------- Haptics --------
    private void SendHapticsBoth(float amp, float dur)
    {
        SendHaptics(leftHapticNode, amp, dur);
        SendHaptics(rightHapticNode, amp, dur);
    }

    private void SendHaptics(XRNode node, float amp, float dur)
    {
        var device = InputDevices.GetDeviceAtXRNode(node);
        if (!device.isValid) return;

        if (device.TryGetHapticCapabilities(out var caps) && caps.supportsImpulse && caps.numChannels > 0)
            device.SendHapticImpulse(0u, amp, dur);
    }

    void OnDrawGizmosSelected()
    {
        if (!_active) return;
        Gizmos.DrawWireSphere(_center, _radius);
    }
}
