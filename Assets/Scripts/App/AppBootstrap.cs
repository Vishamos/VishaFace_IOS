using UnityEngine;

// 앱 시작 설정과 구성 요소 생성. 씬을 고치지 않고 코드에서 붙인다.
public static class AppBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // 방송 중 화면이 꺼지지 않게 한다. iOS에서는 isIdleTimerDisabled = YES와 같다 (사양 §4.3).
        Screen.sleepTimeout = SleepTimeout.NeverSleep;

        // 목표 60fps (Unity iOS 기본 30). 발열로 유지되지 않으면 30으로 낮춘다 (사양 §3.4).
        Application.targetFrameRate = 60;

        var app = new GameObject("App");
        var tracker = app.AddComponent<FaceTracker>();
        var socket = app.AddComponent<Socket>();

        var hud = DebugHud.Create(tracker);
        ServerSettingsPanel.Create(hud.transform, socket);
    }
}
