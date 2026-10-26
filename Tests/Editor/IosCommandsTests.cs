using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class IosCommandsTests
    {
        private const string UDID = "00008120-000924542E200032";
        private const string BUNDLE_ID = "com.javienne.rouflac";
        private const string DEVICES = "{ \"info\" : { \"outcome\" : \"success\" }, \"result\" : { \"devices\" : [ " + "{ \"identifier\" : \"61D32D86-0A64-55F6-9D56-7AAEF3DAF7C3\", \"connectionProperties\" : { \"tunnelState\" : \"disconnected\", \"transportType\" : \"localNetwork\" }, \"deviceProperties\" : { \"name\" : \"iPad\", \"osVersionNumber\" : \"27.0.1\" }, \"hardwareProperties\" : { \"udid\" : \"00008120-000924542E200032\", \"marketingName\" : \"iPad (A16)\", \"productType\" : \"iPad15,7\", \"platform\" : \"iOS\", \"reality\" : \"physical\" } }, " + "{ \"identifier\" : \"D98B2C02-491F-4BD0-84B4-CED675C59142\", \"connectionProperties\" : { \"tunnelState\" : \"disconnected\" }, \"deviceProperties\" : { \"name\" : \"iPhone 18 Pro\" }, \"hardwareProperties\" : { \"udid\" : \"D98B2C02-491F-4BD0-84B4-CED675C59142\", \"reality\" : \"simulated\" } }, " + "{ \"identifier\" : \"AAAA\", \"connectionProperties\" : { \"tunnelState\" : \"unavailable\" }, \"deviceProperties\" : { \"name\" : \"Old iPhone\" }, \"hardwareProperties\" : { \"udid\" : \"BBBB\", \"reality\" : \"physical\" } } ] } }";
        private const string CONNECTION_FAILED = "{ \"error\" : { \"code\" : 4000, \"domain\" : \"com.apple.dt.CoreDeviceError\", \"userInfo\" : { \"NSLocalizedDescription\" : { \"string\" : \"A connection to this device could not be established.\" } } }, \"info\" : { \"outcome\" : \"failed\" } }";

        [TestCase("/Applications/Xcode-beta.app/Contents/Developer", true, "/Applications/Xcode-beta.app/Contents/Developer")]
        [TestCase(null, true, IosCommands.XCODE_DEVELOPER_DIR)]
        [TestCase("", false, null)]
        public void GetDeveloperDir_EnvironmentAndXcode_PrefersTheEnvironment(string fromEnvironment, bool xcodeInstalled, string expected)
        {
            Assert.That(IosCommands.GetDeveloperDir(fromEnvironment, xcodeInstalled), Is.EqualTo(expected));
        }

        [TestCase("5THUC8P43D", "DEVELOPMENT_TEAM = OTHER123;", "5THUC8P43D")]
        [TestCase("", "\t\t\t\tDEVELOPMENT_TEAM = OTHER123;\n", "OTHER123")]
        [TestCase(null, null, "")]
        public void GetTeam_PlayerSettingsAndProject_PrefersThePlayerSettings(string playerSettingsTeam, string xcodeProject, string expected)
        {
            Assert.That(IosCommands.GetTeam(playerSettingsTeam, xcodeProject), Is.EqualTo(expected));
        }

        [Test]
        public void SelectDevice_OnePhysicalReachable_PicksIt()
        {
            var device = IosCommands.SelectDevice(IosCommands.Parse(DEVICES).result.devices, null);

            Assert.That(device.hardwareProperties.udid, Is.EqualTo(UDID));
            Assert.That(IosCommands.GetModel(device), Is.EqualTo("iPad (A16) (iPad15,7)"));
            Assert.That(IosCommands.GetOsVersion(device), Is.EqualTo("iOS 27.0.1"));
        }

        [Test]
        public void GetReachable_Devices_KeepsThePhysicalOnesNotUnavailable()
        {
            var reachable = IosCommands.GetReachable(IosCommands.Parse(DEVICES).result.devices);

            Assert.That(reachable.Select(device => device.hardwareProperties.udid), Is.EqualTo(new[] { UDID }));
        }

        [Test]
        public void ToListedDevice_Device_NamesItAfterItsNameAndModel()
        {
            var device = IosCommands.ToListedDevice(IosCommands.Parse(DEVICES).result.devices[0]);

            Assert.That(device.Id, Is.EqualTo(UDID));
            Assert.That(device.Name, Is.EqualTo("iPad, iPad (A16)"));
        }

        [TestCase(UDID)]
        [TestCase("61D32D86-0A64-55F6-9D56-7AAEF3DAF7C3")]
        [TestCase("iPad")]
        public void SelectDevice_Requested_FindsItByUdidIdentifierOrName(string requested)
        {
            var device = IosCommands.SelectDevice(IosCommands.Parse(DEVICES).result.devices, requested);

            Assert.That(device.hardwareProperties.udid, Is.EqualTo(UDID));
        }

        [TestCase("D98B2C02-491F-4BD0-84B4-CED675C59142")]
        [TestCase("BBBB")]
        public void SelectDevice_SimulatorOrUnavailable_IsNotAvailable(string requested)
        {
            Assert.That(() => IosCommands.SelectDevice(IosCommands.Parse(DEVICES).result.devices, requested), Throws.TypeOf<MeasureException>().With.Message.Contains("is not available"));
        }

        [Test]
        public void SelectDevice_None_AsksToPlugOneIn()
        {
            Assert.That(() => IosCommands.SelectDevice(new DevicectlJson.Device[0], null), Throws.TypeOf<MeasureException>().With.Message.Contains("No iOS device"));
        }

        [Test]
        public void SelectDevice_Several_AsksToChooseOne()
        {
            var devices = IosCommands.Parse(DEVICES.Replace("\"simulated\"", "\"physical\"")).result.devices;

            Assert.That(() => IosCommands.SelectDevice(devices, null), Throws.TypeOf<MeasureException>().With.Message.Contains("Several"));
        }

        [TestCase("{ \"result\" : { \"deviceIdentifier\" : \"61D3\", \"passcodeRequired\" : true, \"unlockedSinceBoot\" : true } }", true)]
        [TestCase("{ \"result\" : { \"deviceIdentifier\" : \"61D3\", \"passcodeRequired\" : false, \"unlockedSinceBoot\" : true } }", false)]
        public void Parse_LockState_ReadsWhetherThePasscodeIsRequired(string json, bool expected)
        {
            Assert.That(IosCommands.Parse(json).result.passcodeRequired, Is.EqualTo(expected));
        }

        [Test]
        public void Parse_Launch_ReadsTheProcessIdentifier()
        {
            var json = "{ \"result\" : { \"launchOptions\" : { \"arguments\" : [ ] }, \"process\" : { \"auditToken\" : [ 4294967295, 501 ], \"executable\" : \"file:///Rouflac.app/Rouflac\", \"processIdentifier\" : 2341 } } }";

            Assert.That(IosCommands.Parse(json).result.process.processIdentifier, Is.EqualTo(2341));
        }

        [Test]
        public void GetError_Failure_GivesItsDescription()
        {
            Assert.That(IosCommands.GetError(IosCommands.Parse(CONNECTION_FAILED)), Is.EqualTo("A connection to this device could not be established."));
        }

        [Test]
        public void GetError_FailureWithoutDescription_GivesItsDomainAndCode()
        {
            var output = IosCommands.Parse("{ \"error\" : { \"code\" : 7000, \"domain\" : \"com.apple.dt.CoreDeviceError\" } }");

            Assert.That(IosCommands.GetError(output), Is.EqualTo("com.apple.dt.CoreDeviceError error 7000"));
        }

        [TestCase(DEVICES)]
        [TestCase("")]
        [TestCase(null)]
        public void GetError_SuccessOrNoOutput_GivesNull(string json)
        {
            Assert.That(IosCommands.GetError(IosCommands.Parse(json)), Is.Null);
        }

        [Test]
        public void IsConnectionError_ConnectionNotEstablished_IsTrue()
        {
            Assert.That(IosCommands.IsConnectionError(IosCommands.Parse(CONNECTION_FAILED)), Is.True);
        }

        [Test]
        public void IsConnectionError_OtherError_IsFalse()
        {
            Assert.That(IosCommands.IsConnectionError(IosCommands.Parse(CONNECTION_FAILED.Replace("4000", "7000"))), Is.False);
        }

        [Test]
        public void Devicectl_Command_WritesItsResultToTheJsonFileOnly()
        {
            var arguments = IosCommands.Devicectl("/work/devicectl.json", IosCommands.Launch(UDID, BUNDLE_ID));

            Assert.That(arguments, Is.EqualTo(new[] { "devicectl", "device", "process", "launch", "--device", UDID, "--terminate-existing", BUNDLE_ID, "--json-output", "/work/devicectl.json", "--quiet" }));
        }

        [Test]
        public void CopyFrom_StartupFile_ReadsItFromTheDataContainerOfTheApp()
        {
            var arguments = IosCommands.CopyFrom(UDID, BUNDLE_ID, IosCommands.STARTUP_FILE, "/work/startup.txt");

            Assert.That(arguments, Is.EqualTo(new[] { "device", "copy", "from", "--device", UDID, "--domain-type", "appDataContainer", "--domain-identifier", BUNDLE_ID, "--source", "Library/Caches/startup.txt", "--destination", "/work/startup.txt" }));
        }

        [Test]
        public void Terminate_ProcessIdentifier_GivesItInDigits()
        {
            Assert.That(IosCommands.Terminate(UDID, 2341), Is.EqualTo(new[] { "device", "process", "terminate", "--device", UDID, "--pid", "2341" }));
        }

        [Test]
        public void XcodeBuild_Project_BuildsForDevicesSignedByTheTeamWithoutArchive()
        {
            var arguments = IosCommands.XcodeBuild("/build/Rouflac/Unity-iPhone.xcodeproj", "/work/DerivedData", "5THUC8P43D");

            Assert.That(arguments, Is.EqualTo(new[] { "xcodebuild", "-project", "/build/Rouflac/Unity-iPhone.xcodeproj", "-scheme", "Unity-iPhone", "-configuration", "Release", "-destination", "generic/platform=iOS", "-derivedDataPath", "/work/DerivedData", "-allowProvisioningUpdates", "-quiet", "build", "DEVELOPMENT_TEAM=5THUC8P43D", "CODE_SIGN_STYLE=Automatic" }));
        }

        [Test]
        public void GetProductsFolder_DerivedData_IsTheFolderOfReleaseDeviceBuilds()
        {
            Assert.That(IosCommands.GetProductsFolder("/work/DerivedData"), Is.EqualTo(Path.Combine("/work/DerivedData", "Build", "Products", "Release-iphoneos")));
        }
    }
}