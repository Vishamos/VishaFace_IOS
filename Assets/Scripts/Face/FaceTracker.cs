using System;
using Unity.Collections;
using UnityEngine;
using UnityEngine.XR.ARFoundation;
using UnityEngine.XR.ARKit;
using UnityEngine.XR.ARSubsystems;

// 트래킹 계층 (사양 §3). ARKit 얼굴 추적 결과를 FaceFrame에 채운다. 네트워크는 모른다.
// - 매 프레임 LateUpdate에서 읽는다(AR Foundation이 Update에서 갱신한 뒤).
// - 추적이 끊기면 Tracking = false, 값은 마지막으로 추적한 값을 유지한다 (프로토콜 §5).
// - 머리 자세는 AR 카메라 기준 상대 자세 (프로토콜 §5a). 축·부호는 실기기 확인 필요.
// - 에디터에서는 ARKit 서브시스템이 없어 값이 갱신되지 않는다.
public class FaceTracker : MonoBehaviour
{
    public FaceFrame Frame { get; } = new FaceFrame();
    public event Action<FaceFrame> FrameUpdated;

    private ARFaceManager faceManager;
    private Transform cameraTransform;

    // ARKitBlendShapeLocation 값 → 프로토콜 인덱스. 이름으로 매핑해 enum 순서에 기대지 않는다.
    private static int[] locationToIndex;

    private void Start()
    {
        faceManager = FindAnyObjectByType<ARFaceManager>();
        var cameraManager = FindAnyObjectByType<ARCameraManager>();
        if (faceManager == null || cameraManager == null)
        {
            Debug.LogWarning("[FaceTracker] 씬에 ARFaceManager 또는 ARCameraManager가 없음");
            enabled = false;
            return;
        }
        cameraTransform = cameraManager.transform;
        BuildLocationMap();
    }

    private void LateUpdate()
    {
        ARFace face = null;
        foreach (var f in faceManager.trackables)
        {
            face = f;
            break; // 최대 1개 (MaximumFaceCount = 1)
        }

        bool tracking = face != null && face.trackingState == TrackingState.Tracking;
        if (tracking)
        {
            Frame.HeadRotation = Quaternion.Inverse(cameraTransform.rotation) * face.transform.rotation;
            Frame.HeadPosition = cameraTransform.InverseTransformPoint(face.transform.position);
            ReadBlendShapes(face.trackableId);
        }
        Frame.Tracking = tracking;

        FrameUpdated?.Invoke(Frame);
    }

    private void ReadBlendShapes(TrackableId faceId)
    {
        if (faceManager.subsystem is not ARKitFaceSubsystem arkit) return;

        using var coefficients = arkit.GetBlendShapeCoefficients(faceId, Allocator.Temp);
        foreach (var c in coefficients)
        {
            int location = (int)c.blendShapeLocation;
            if (location < 0 || location >= locationToIndex.Length) continue;
            int index = locationToIndex[location];
            if (index >= 0) Frame.BlendShapes[index] = c.coefficient;
        }
    }

    private static void BuildLocationMap()
    {
        if (locationToIndex != null) return;

        var locations = (ARKitBlendShapeLocation[])Enum.GetValues(typeof(ARKitBlendShapeLocation));
        int max = 0;
        foreach (var l in locations) max = Math.Max(max, (int)l);

        locationToIndex = new int[max + 1];
        Array.Fill(locationToIndex, -1);
        int mapped = 0;
        foreach (var l in locations)
        {
            int index = FaceBlendShapes.IndexOf(l.ToString());
            locationToIndex[(int)l] = index;
            if (index >= 0) mapped++;
        }
        if (mapped != FaceBlendShapes.Count)
            Debug.LogWarning($"[FaceTracker] 블렌드셰이프 매핑 {mapped}/{FaceBlendShapes.Count}");
    }
}
