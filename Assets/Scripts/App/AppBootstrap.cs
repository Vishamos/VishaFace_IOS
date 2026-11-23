using UnityEngine;

// 앱 시작 설정과 구성 요소 생성. 씬을 고치지 않고 코드에서 붙인다.
public static class AppBootstrap
{
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Initialize()
    {
        // 방송 중 화면이 꺼지지 않게 한다. iOS에서는 isIdleTimerDisabled = YES와 같다 (사양 §4.3).
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
    }
}
