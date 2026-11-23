#if UNITY_IOS
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.iOS.Xcode;
using UnityEngine;

namespace VishaFaceI.EditorTools
{
    /// <summary>
    /// iOS 빌드 후 Xcode 프로젝트의 Info.plist에 로컬 네트워크 권한 문구를 넣는다 (사양 §5.8a).
    /// iOS 14 이상은 같은 LAN의 서버에 접속할 때 이 권한을 묻고, 거부하면 모든 접속이 실패한다.
    /// Unity 플레이어 설정에는 이 항목이 없어서 후처리로 넣는다. Append 빌드에서도 매번 같은 값으로 덮어쓴다.
    /// </summary>
    public class IosInfoPlistPostprocess : IPostprocessBuildWithReport
    {
        const string LocalNetworkUsageKey = "NSLocalNetworkUsageDescription";
        const string LocalNetworkUsageText = "같은 Wi-Fi의 중계 서버로 표정 데이터를 보내려면 로컬 네트워크 접근이 필요합니다.";

        public int callbackOrder => 0;

        public void OnPostprocessBuild(BuildReport report)
        {
            if (report.summary.platform != BuildTarget.iOS) return;

            var plistPath = Path.Combine(report.summary.outputPath, "Info.plist");
            var plist = new PlistDocument();
            plist.ReadFromFile(plistPath);
            plist.root.SetString(LocalNetworkUsageKey, LocalNetworkUsageText);
            plist.WriteToFile(plistPath);

            Debug.Log($"[IosBuild] Info.plist에 {LocalNetworkUsageKey} 추가");
        }
    }
}
#endif
