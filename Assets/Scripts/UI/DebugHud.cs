using System.Text;
using UnityEngine;
using UnityEngine.UI;

// 디버그 화면 (사양 §4.1, §4.2). 카메라 영상은 표시하지 않고 수치만 표시한다.
// 글자는 화면 갱신 부담을 줄이려고 0.1초마다 바꾼다. 화면 글자는 내장 폰트의 한글 표시가 확인되지 않아 영어로 쓴다.
//
// 배치 (기준 750×1334, 위에서부터 y)
//   170  FPS·추적 여부
//   230  머리 자세 3줄
//   340  블렌드셰이프 2열 × 26줄
public class DebugHud : MonoBehaviour
{
    private const float RefreshInterval = 0.1f;
    private const float FpsWindow = 0.5f;

    private FaceTracker tracker;
    private Text statusText;
    private Text headText;
    private readonly Text[] columns = new Text[2];
    private readonly StringBuilder sb = new StringBuilder(1024);
    private float nextRefresh;

    // FPS: FpsWindow 동안 그린 프레임 수로 계산
    private int fpsFrames;
    private float fpsWindowStart;
    private float fps;

    public static DebugHud Create(FaceTracker tracker)
    {
        var canvas = UiKit.CreateCanvas("HUD");
        var hud = canvas.gameObject.AddComponent<DebugHud>();
        hud.tracker = tracker;
        hud.Build();
        return hud;
    }

    private void Build()
    {
        // 화면 전체 검은 배경 (카메라 영상 대신)
        UiKit.Stretch(UiKit.CreateImage(transform, "Background", Color.black));

        statusText = UiKit.Place(UiKit.CreateText(transform, "Status", 28, TextAnchor.MiddleLeft), 20, 170, 710, 50);
        headText = UiKit.Place(UiKit.CreateText(transform, "Head", 22, TextAnchor.UpperLeft), 20, 230, 710, 100);

        const int rows = 26;
        for (int c = 0; c < columns.Length; c++)
        {
            columns[c] = UiKit.Place(UiKit.CreateText(transform, $"BlendShapes{c}", 22, TextAnchor.UpperLeft),
                20 + c * 360, 340, 350, rows * 37);
            columns[c].lineSpacing = 1.2f;
        }
    }

    private void Update()
    {
        MeasureFps();

        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + RefreshInterval;

        RefreshStatus(tracker.Frame);
        RefreshHead(tracker.Frame);
        RefreshBlendShapes(tracker.Frame);
    }

    private void MeasureFps()
    {
        fpsFrames++;
        float elapsed = Time.unscaledTime - fpsWindowStart;
        if (elapsed < FpsWindow) return;
        fps = fpsFrames / elapsed;
        fpsFrames = 0;
        fpsWindowStart = Time.unscaledTime;
    }

    private void RefreshStatus(FaceFrame frame)
    {
        sb.Clear();
        sb.Append("FPS ").Append(fps.ToString("F1"))
          .Append("   Face: ").Append(frame.Tracking ? "Tracking" : "Lost");
        statusText.text = sb.ToString();
        statusText.color = frame.Tracking ? Color.white : new Color(1f, 0.5f, 0.4f);
    }

    private void RefreshHead(FaceFrame frame)
    {
        Quaternion q = frame.HeadRotation;
        Vector3 e = q.eulerAngles;
        Vector3 p = frame.HeadPosition;

        sb.Clear();
        sb.Append("Rot(deg) x ").Append(Signed(e.x).ToString("F1"))
          .Append("  y ").Append(Signed(e.y).ToString("F1"))
          .Append("  z ").Append(Signed(e.z).ToString("F1")).Append('\n');
        sb.Append("Quat ").Append(q.x.ToString("F3")).Append(' ').Append(q.y.ToString("F3")).Append(' ')
          .Append(q.z.ToString("F3")).Append(' ').Append(q.w.ToString("F3")).Append('\n');
        sb.Append("Pos(m) x ").Append(p.x.ToString("F3"))
          .Append("  y ").Append(p.y.ToString("F3"))
          .Append("  z ").Append(p.z.ToString("F3"));
        headText.text = sb.ToString();
    }

    private void RefreshBlendShapes(FaceFrame frame)
    {
        int perColumn = (FaceBlendShapes.Count + columns.Length - 1) / columns.Length;
        for (int c = 0; c < columns.Length; c++)
        {
            sb.Clear();
            int end = Mathf.Min((c + 1) * perColumn, FaceBlendShapes.Count);
            for (int i = c * perColumn; i < end; i++)
            {
                sb.Append(i.ToString("00")).Append(' ').Append(FaceBlendShapes.Names[i])
                  .Append(' ').Append(frame.BlendShapes[i].ToString("0.00")).Append('\n');
            }
            columns[c].text = sb.ToString();
        }
    }

    // 0∼360 각도를 -180∼180으로 (부호 확인용)
    private static float Signed(float degrees) => degrees > 180f ? degrees - 360f : degrees;
}
