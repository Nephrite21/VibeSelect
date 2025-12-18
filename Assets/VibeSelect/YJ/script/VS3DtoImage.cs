using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class VS3DtoImage : MonoBehaviour
{
    [Header("Output UI")]
    [SerializeField] private Image targetImage;

    [Header("Capture Settings")]
    [SerializeField] private int textureSize = 512;
    [SerializeField] private float padding = 1.2f;              // 카메라 거리 여유(>1)
    [SerializeField] private Color backgroundColor = new Color(0, 0, 0, 0); // 투명 배경
    [SerializeField] private int previewLayer = 30;             // 미리보기 전용 레이어(프로젝트에 추가 권장)

    [Header("Optional Look")]
    [SerializeField] private Vector3 modelEulerOffset = Vector3.zero; // 캡처용 회전 오프셋
    [SerializeField] private bool addLight = true;

    private Coroutine running;

    public void CaptureOnceToUI(GameObject source)
    {
        if (running != null) StopCoroutine(running);
        running = StartCoroutine(CaptureRoutine(source));
    }

    private IEnumerator CaptureRoutine(GameObject source)
    {
        if (source == null)
        {
            Debug.LogWarning("source가 null");
            yield break;
        }

        if (targetImage == null)
        {
            yield break;
        }

        // 임시 루트 (씬 밖 멀리멀리)
        var root = new GameObject("~PreviewRoot");
        root.hideFlags = HideFlags.HideAndDontSave;
        root.transform.position = new Vector3(10000f, 10000f, 10000f);

        // 대상 복제(원본 건드리지 않기 위해)
        var clone = Instantiate(source, root.transform);
        clone.name = "~PreviewClone";
        clone.transform.localPosition = Vector3.zero;
        clone.transform.localRotation = Quaternion.Euler(modelEulerOffset);
        clone.transform.localScale = source.transform.lossyScale; // 대략 유지
        SetLayerRecursively(clone, previewLayer);

        // 렌더러 bounds 확보
        if (!TryGetBounds(clone, out Bounds bounds))
        {
            Debug.LogWarning("Renderer 없.");
            DestroyImmediate(root);
            yield break;
        }

        // 임시 카메라
        var camGO = new GameObject("~PreviewCam");
        camGO.hideFlags = HideFlags.HideAndDontSave;
        camGO.transform.SetParent(root.transform, false);

        var cam = camGO.AddComponent<Camera>();
        cam.enabled = false;
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = backgroundColor;
        cam.orthographic = false;
        cam.cullingMask = 1 << previewLayer;
        cam.nearClipPlane = 0.01f;
        cam.farClipPlane = 1000f;
        cam.fieldOfView = 30f;

        // 라이트(필요하면)
        Light light = null;
        if (addLight)
        {
            var lightGO = new GameObject("~PreviewLight");
            lightGO.hideFlags = HideFlags.HideAndDontSave;
            lightGO.transform.SetParent(root.transform, false);
            lightGO.transform.position = bounds.center + new Vector3(1f, 1.5f, -1.5f) * bounds.extents.magnitude;

            light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.2f;
            light.transform.rotation = Quaternion.LookRotation((bounds.center - lightGO.transform.position).normalized, Vector3.up);
        }

        // RenderTexture 생성
        var rt = new RenderTexture(textureSize, textureSize, 24, RenderTextureFormat.ARGB32);
        rt.Create();
        cam.targetTexture = rt;

        // 카메라 프레이밍
        FrameCameraToBounds(cam, bounds, padding);

        // 한 프레임 대
        yield return null;

        // 렌더
        cam.Render();

        // 픽셀 읽기
        var prevActive = RenderTexture.active;
        RenderTexture.active = rt;

        var tex = new Texture2D(textureSize, textureSize, TextureFormat.RGBA32, false, false);
        tex.ReadPixels(new Rect(0, 0, textureSize, textureSize), 0, 0);
        tex.Apply();

        RenderTexture.active = prevActive;

        // Sprite로 변환해서 UI에 넣기.
        var sprite = Sprite.Create(tex, new Rect(0, 0, textureSize, textureSize), new Vector2(0.5f, 0.5f), 100f);
        targetImage.sprite = sprite;
        targetImage.preserveAspect = true;
        targetImage.color = Color.white; // 알파 표시 위해

        // 정리~~제발~~
        cam.targetTexture = null;
        rt.Release();
        DestroyImmediate(rt);
        DestroyImmediate(root);

        running = null;
    }

    private static void SetLayerRecursively(GameObject obj, int layer)
    {
        foreach (var t in obj.GetComponentsInChildren<Transform>(true))
            t.gameObject.layer = layer;
    }

    private static bool TryGetBounds(GameObject obj, out Bounds bounds)
    {
        var renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers == null || renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        for (int i = 1; i < renderers.Length; i++)
            bounds.Encapsulate(renderers[i].bounds);

        return true;
    }

    private static void FrameCameraToBounds(Camera cam, Bounds bounds, float padding)
    {
        var center = bounds.center;
        var radius = bounds.extents.magnitude;

        // 카메라를 -Z에서 바라보도록
        cam.transform.rotation = Quaternion.identity;
        cam.transform.position = center + Vector3.back * 2f;

        // FOV 기반 거리 계산
        float fovRad = cam.fieldOfView * Mathf.Deg2Rad;
        float distance = (radius / Mathf.Sin(fovRad * 0.5f)) * padding;

        cam.transform.position = center + Vector3.back * distance;
        cam.transform.LookAt(center);
    }
}
