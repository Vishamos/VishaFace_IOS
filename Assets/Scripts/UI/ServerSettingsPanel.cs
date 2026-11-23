using UnityEngine;
using UnityEngine.UI;

// 서버 주소 설정 (사양 §4.5, §4.2 연결 상태 표시, worklist B1). UI는 코드로 만든다.
// - 저장 키는 Socket.HostKey / Socket.PortKey (PlayerPrefs, iOS에서는 UserDefaults).
// - 입력한 주소는 로그로 출력하지 않는다.
//
// 배치 (기준 750×1334, 위에서부터 y)
//    40  IP 입력 | 포트 입력 | Connect 버튼
//   110  연결 상태·에러 코드·입력 안내
public class ServerSettingsPanel : MonoBehaviour
{
    private Socket socket;
    private InputField hostInput;
    private InputField portInput;
    private Button applyButton;
    private Text statusText;
    private string notice = ""; // 입력값 오류 안내 (다음 상태 변화나 정상 저장 때 지움)

    public static ServerSettingsPanel Create(Transform parent, Socket socket)
    {
        var go = new GameObject("ServerSettings", typeof(RectTransform));
        go.transform.SetParent(parent, false);
        UiKit.Stretch(go.transform);

        var panel = go.AddComponent<ServerSettingsPanel>();
        panel.socket = socket;
        panel.Build();
        panel.LoadSaved();
        panel.applyButton.onClick.AddListener(panel.Apply);
        socket.StateChanged += panel.OnStateChanged;
        panel.RefreshStatus();
        return panel;
    }

    private void OnDestroy()
    {
        if (socket != null) socket.StateChanged -= OnStateChanged;
    }

    private void Build()
    {
        hostInput = UiKit.Place(UiKit.CreateInputField(transform, "HostInput", "Server IP", 28), 20, 40, 380, 60);
        hostInput.contentType = InputField.ContentType.Standard;
        hostInput.keyboardType = TouchScreenKeyboardType.URL; // 점(.) 입력이 쉬운 키보드
        hostInput.characterLimit = 64;

        portInput = UiKit.Place(UiKit.CreateInputField(transform, "PortInput", "Port", 28), 410, 40, 130, 60);
        portInput.contentType = InputField.ContentType.IntegerNumber; // 숫자 키패드
        portInput.characterLimit = 5;

        applyButton = UiKit.Place(UiKit.CreateButton(transform, "ConnectButton", "Connect", 28), 550, 40, 180, 60);

        statusText = UiKit.Place(UiKit.CreateText(transform, "ServerStatus", 26, TextAnchor.UpperLeft), 20, 110, 710, 55);
    }

    private void LoadSaved()
    {
        hostInput.text = PlayerPrefs.GetString(Socket.HostKey, "");
        portInput.text = PlayerPrefs.GetInt(Socket.PortKey, Socket.DefaultPort).ToString();
    }

    private void Apply()
    {
        string host = hostInput.text.Trim();
        if (host.Length == 0)
        {
            ShowNotice("Enter server IP");
            return;
        }
        if (!int.TryParse(portInput.text, out int port) || port < 1 || port > 65535)
        {
            ShowNotice("Port must be 1-65535");
            return;
        }

        PlayerPrefs.SetString(Socket.HostKey, host);
        PlayerPrefs.SetInt(Socket.PortKey, port);
        PlayerPrefs.Save();

        notice = "";
        socket.Reconnect();
        RefreshStatus();
    }

    private void OnStateChanged(Socket.SocketState state)
    {
        notice = "";
        RefreshStatus();
    }

    private void ShowNotice(string message)
    {
        notice = message;
        RefreshStatus();
    }

    private void RefreshStatus()
    {
        string text = "Server: " + socket.State;
        if (socket.LastErrorCode != 0) text += $" (error {socket.LastErrorCode})";
        if (notice.Length > 0) text += $"\n{notice}";
        statusText.text = text;
        statusText.color = socket.State == Socket.SocketState.Registered ? new Color(0.5f, 1f, 0.5f) : Color.white;
    }
}
