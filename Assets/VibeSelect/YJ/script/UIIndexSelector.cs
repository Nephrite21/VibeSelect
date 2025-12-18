using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class UIIndexSelector : MonoBehaviour
{
    [Header("Assign UI root GameObjects here")]
    [SerializeField] private List<GameObject> uiRoots = new List<GameObject>(3);

    [Header("Scales")]
    [SerializeField] private float selectedXYScale = 0.02f;
    [SerializeField] private float normalXYScale = 0.01f;

    [Header("Scale Lerp")]
    [SerializeField] private float scaleLerpSpeed = 5f; // 클수록 빨리 도달(초당)

    private int _currentIndex = -1;

    private readonly Dictionary<Transform, Coroutine> _scaleRoutines = new();

    public void SetIndex(int index)
    {
        if (uiRoots == null || uiRoots.Count == 0) return;

        if (index < 0 || index >= uiRoots.Count)
        {
            Debug.LogWarning($"[UIIndexSelector] Index out of range: {index}");
            return;
        }

        if (_currentIndex == index) return;

        // 이전 선택 해제
        if (_currentIndex >= 0 && _currentIndex < uiRoots.Count)
            ApplyStateLerp(_currentIndex, selected: false);

        // 새 선택 적용
        ApplyStateLerp(index, selected: true);

        _currentIndex = index;
    }

    private void ApplyStateLerp(int index, bool selected)
    {
        var go = uiRoots[index];
        if (!go) return;

        // 1) Toggle first child active
        if (go.transform.childCount > 0)
        {
            var firstChild = go.transform.GetChild(0).gameObject;
            firstChild.SetActive(selected);
        }
        else
        {
            Debug.LogWarning($"[UIIndexSelector] '{go.name}' has no children to toggle (index {index}).");
        }

        // 2) Lerp localScale XY (keep Z as-is)
        var t = go.transform;
        float xy = selected ? selectedXYScale : normalXYScale;
        Vector3 target = new Vector3(xy, xy, t.localScale.z);

        StartScaleLerp(t, target);
    }

    private void StartScaleLerp(Transform t, Vector3 target)
    {
        if (!t) return;

        if (_scaleRoutines.TryGetValue(t, out var running) && running != null)
            StopCoroutine(running);

        _scaleRoutines[t] = StartCoroutine(ScaleLerpRoutine(t, target));
    }

    private IEnumerator ScaleLerpRoutine(Transform t, Vector3 target)
    {
        while (t && (t.localScale - target).sqrMagnitude > 0.0000005f)
        {
            t.localScale = Vector3.MoveTowards(t.localScale, target, scaleLerpSpeed * Time.deltaTime);
            yield return null;
        }

        if (t) t.localScale = target;

        if (t && _scaleRoutines.ContainsKey(t))
            _scaleRoutines[t] = null;
    }

    // -----------------------------
    // (기존) 즉시 스케일 변경 버전(원하면 참고/백업용)
    // private void ApplyState(int index, bool selected)
    // {
    //     var go = uiRoots[index];
    //     if (!go) return;
    //
    //     if (go.transform.childCount > 0)
    //     {
    //         var firstChild = go.transform.GetChild(0).gameObject;
    //         firstChild.SetActive(selected);
    //     }
    //     else
    //     {
    //         Debug.LogWarning($"[UIIndexSelector] '{go.name}' has no children to toggle (index {index}).");
    //     }
    //
    //     var t = go.transform;
    //     var s = t.localScale;
    //     float xy = selected ? selectedXYScale : normalXYScale;
    //     t.localScale = new Vector3(xy, xy, s.z);
    // }
    // -----------------------------

    [ContextMenu("Reset All To Normal")]
    public void ResetAll()
    {
        if (uiRoots == null) return;

        // 모두 해제(lerp 적용)
        for (int i = 0; i < uiRoots.Count; i++)
            ApplyStateLerp(i, selected: false);

        _currentIndex = -1;
    }
}

