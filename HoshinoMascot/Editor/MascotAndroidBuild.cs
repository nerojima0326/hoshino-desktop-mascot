#if UNITY_ANDROID
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using UnityEditor;
using UnityEditor.Android;
using UnityEngine;

namespace HoshinoMascot.EditorTools
{
    /// <summary>
    /// Android로 빌드할 때 홈 화면 띄우기(MascotOverlayService)에 필요한 권한과 서비스를
    /// 만들어진 AndroidManifest.xml에 넣습니다. 프로젝트의 매니페스트 파일은 건드리지 않습니다.
    /// (Android Build Support가 설치되어 있고 빌드 대상이 Android일 때만 컴파일됩니다)
    /// </summary>
    class MascotAndroidBuild : IPostGenerateGradleAndroidProject
    {
        const string AndroidNamespace = "http://schemas.android.com/apk/res/android";
        const string ServiceName = "com.hoshino.mascot.MascotOverlayService";

        public int callbackOrder => 100;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            string manifestPath = Path.Combine(path, "src", "main", "AndroidManifest.xml");
            if (!File.Exists(manifestPath))
            {
                Debug.LogWarning("[Hoshino Mascot] AndroidManifest.xml을 찾지 못해서 홈 화면 띄우기를 넣지 못했습니다: " + manifestPath);
                return;
            }
            if (!PlayerSettings.preserveFramebufferAlpha)
            {
                Debug.LogWarning("[Hoshino Mascot] Preserve Framebuffer Alpha가 꺼져 있어서 홈 화면에 띄우면 캐릭터 주변이 검게 나옵니다. " +
                                 "Tools > Hoshino Mascot > 4. Apply Android Settings 를 눌러 주세요.");
            }

            // Android 14부터 포그라운드 서비스에 종류를 적어야 하는데, 그 값(specialUse)은 compileSdk 34 이상에서만 빌드됩니다
            bool specialUse = CompileSdk(path) >= 34;

            var document = new XmlDocument { PreserveWhitespace = false };
            document.Load(manifestPath);
            XmlElement manifest = document.DocumentElement;
            AddPermission(document, manifest, "android.permission.SYSTEM_ALERT_WINDOW");
            AddPermission(document, manifest, "android.permission.FOREGROUND_SERVICE");
            AddPermission(document, manifest, "android.permission.POST_NOTIFICATIONS");
            if (specialUse) AddPermission(document, manifest, "android.permission.FOREGROUND_SERVICE_SPECIAL_USE");

            XmlElement application = manifest["application"];
            if (application == null)
            {
                application = document.CreateElement("application");
                manifest.AppendChild(application);
            }
            if (FindByName(application, "service", ServiceName) == null)
            {
                XmlElement service = document.CreateElement("service");
                SetAndroidAttribute(document, service, "name", ServiceName);
                // 앱 화면의 Unity와 따로 돌도록 별도 프로세스에서 Unity를 하나 더 띄웁니다
                SetAndroidAttribute(document, service, "process", ":overlay");
                SetAndroidAttribute(document, service, "exported", "false");
                if (specialUse)
                {
                    SetAndroidAttribute(document, service, "foregroundServiceType", "specialUse");
                    XmlElement property = document.CreateElement("property");
                    SetAndroidAttribute(document, property, "name", "android.app.PROPERTY_SPECIAL_USE_FGS_SUBTYPE");
                    SetAndroidAttribute(document, property, "value", "Desktop mascot character shown on the home screen");
                    service.AppendChild(property);
                }
                application.AppendChild(service);
            }
            var settings = new XmlWriterSettings { Indent = true, Encoding = new UTF8Encoding(false) };
            using (XmlWriter writer = XmlWriter.Create(manifestPath, settings)) document.Save(writer);
        }

        static int CompileSdk(string path)
        {
            string gradlePath = Path.Combine(path, "build.gradle");
            if (File.Exists(gradlePath))
            {
                Match match = Regex.Match(File.ReadAllText(gradlePath), @"compileSdk(?:Version)?\s*=?\s*(\d+)");
                if (match.Success) return int.Parse(match.Groups[1].Value);
            }
            // 못 읽었으면 대상 API 설정으로 짐작합니다 (Automatic이면 0)
            int target = (int)PlayerSettings.Android.targetSdkVersion;
#if UNITY_6000_0_OR_NEWER
            return target > 0 ? target : 34;
#else
            return target;
#endif
        }

        static void AddPermission(XmlDocument document, XmlElement manifest, string permission)
        {
            if (FindByName(manifest, "uses-permission", permission) != null) return;
            XmlElement element = document.CreateElement("uses-permission");
            SetAndroidAttribute(document, element, "name", permission);
            // 권한은 <application> 앞에 둡니다
            XmlElement application = manifest["application"];
            if (application != null) manifest.InsertBefore(element, application);
            else manifest.AppendChild(element);
        }

        static XmlElement FindByName(XmlElement parent, string tag, string name)
        {
            foreach (XmlNode node in parent.ChildNodes)
            {
                if (node is XmlElement element && element.Name == tag && element.GetAttribute("name", AndroidNamespace) == name)
                    return element;
            }
            return null;
        }

        static void SetAndroidAttribute(XmlDocument document, XmlElement element, string name, string value)
        {
            XmlAttribute attribute = document.CreateAttribute("android", name, AndroidNamespace);
            attribute.Value = value;
            element.Attributes.Append(attribute);
        }
    }
}
#endif
