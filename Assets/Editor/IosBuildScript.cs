using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace VishaFaceI.EditorTools
{
    /// <summary>
    /// iOS용 Xcode 프로젝트를 만드는 빌드 진입점.
    /// Mac의 tools/build_ios.sh가 배치 모드(-executeMethod)로 호출한다. 에디터 메뉴에서도 실행할 수 있다.
    /// 서명 정보(Team ID)는 여기서 다루지 않는다. ProjectSettings에 저장되면 공개 저장소에 올라가기 때문에
    /// xcodebuild 단계에서 명령줄 인자로만 넘긴다.
    /// </summary>
    public static class IosBuildScript
    {
        const string DefaultOutputPath = "Builds/iOS";
        const string LogPrefix = "[IosBuild]";

        /// <summary>
        /// 배치 모드 진입점. 성공하면 종료 코드 0, 실패하면 1로 Unity를 끝낸다.
        /// 인자: -outputPath &lt;경로&gt; (생략하면 Builds/iOS)
        /// </summary>
        public static void BuildFromCommandLine()
        {
            var exitCode = 1;
            try
            {
                var outputPath = GetCommandLineValue("-outputPath") ?? DefaultOutputPath;
                exitCode = Build(outputPath) ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError($"{LogPrefix} 예외로 빌드 중단: {e}");
            }
            EditorApplication.Exit(exitCode);
        }

        [MenuItem("VishaFace/Build iOS Xcode Project")]
        static void BuildFromMenu()
        {
            Build(DefaultOutputPath);
        }

        static bool Build(string outputPath)
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.iOS)
            {
                Debug.Log($"{LogPrefix} 빌드 대상을 iOS로 전환합니다.");
                if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.iOS, BuildTarget.iOS))
                {
                    Debug.LogError($"{LogPrefix} iOS로 전환 실패. iOS Build Support 모듈이 설치됐는지 확인하세요.");
                    return false;
                }
            }

            var scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();
            if (scenes.Length == 0)
            {
                Debug.LogError($"{LogPrefix} Build Profiles에 켜진 씬이 없습니다.");
                return false;
            }

            // 이전 Xcode 프로젝트가 있으면 덮어쓰지 않고 이어 쓴다(Append).
            // 바뀌지 않은 파일이 유지되어 Xcode 증분 빌드가 빨라진다.
            var options = BuildOptions.None;
            var canAppend = BuildPipeline.BuildCanBeAppended(BuildTarget.iOS, outputPath);
            if (canAppend == CanAppendBuild.Yes)
            {
                options |= BuildOptions.AcceptExternalModificationsToPlayer;
            }

            Debug.Log($"{LogPrefix} 시작: 출력={outputPath}, 씬={string.Join(", ", scenes)}, Append={canAppend == CanAppendBuild.Yes}");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outputPath,
                target = BuildTarget.iOS,
                targetGroup = BuildTargetGroup.iOS,
                options = options,
            });

            var summary = report.summary;
            var succeeded = summary.result == BuildResult.Succeeded;
            var message = $"{LogPrefix} 결과={summary.result}, 에러={summary.totalErrors}, 경고={summary.totalWarnings}, 소요={summary.totalTime}";
            if (succeeded)
            {
                Debug.Log(message);
            }
            else
            {
                Debug.LogError(message);
            }
            return succeeded;
        }

        /// <summary>명령줄에서 "-이름 값" 형태의 값을 찾는다. 없으면 null.</summary>
        static string GetCommandLineValue(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
            {
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase))
                {
                    return args[i + 1];
                }
            }
            return null;
        }
    }
}
