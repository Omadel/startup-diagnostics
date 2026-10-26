using System.Linq;
using NUnit.Framework;

namespace Javienne.StartupDiagnostics.Tests
{
    public class AndroidCommandsTests
    {
        private const string SERIAL = "25271FDF6004NP";

        [Test]
        public void ParseDevices_AdbDevices_ReadsEachSerialAndState()
        {
            var devices = AndroidCommands.ParseDevices("List of devices attached\n25271FDF6004NP\tdevice\nemulator-5554\tunauthorized\r\n\n");

            Assert.That(devices, Is.EqualTo(new[] { ("25271FDF6004NP", "device"), ("emulator-5554", "unauthorized") }));
        }

        [Test]
        public void ParseDevices_NoDevice_GivesNothing()
        {
            Assert.That(AndroidCommands.ParseDevices("List of devices attached\n\n"), Is.Empty);
        }

        [Test]
        public void ParseReadyDevices_AdbDevicesLong_NamesTheReadyOnesAfterTheirModel()
        {
            var output = "List of devices attached\n25271FDF6004NP         device usb:0-1 product:oriole model:Pixel_6 device:oriole transport_id:13\nR58M12ABCDE            unauthorized usb:0-2 transport_id:14\r\n\n";

            var devices = AndroidCommands.ParseReadyDevices(output);

            Assert.That(devices.Select(device => (device.Id, device.Name)), Is.EqualTo(new[] { (SERIAL, "Pixel 6") }));
        }

        [Test]
        public void ParseReadyDevices_NoModel_NamesItAfterItsSerial()
        {
            var devices = AndroidCommands.ParseReadyDevices("emulator-5554\tdevice\n");

            Assert.That(devices.Select(device => (device.Id, device.Name)), Is.EqualTo(new[] { ("emulator-5554", "emulator-5554") }));
        }

        [Test]
        public void SelectDevice_OneReady_PicksIt()
        {
            var devices = new[] { (SERIAL, AndroidCommands.STATE_DEVICE), ("other", "offline") };

            Assert.That(AndroidCommands.SelectDevice(devices, null), Is.EqualTo(SERIAL));
        }

        [Test]
        public void SelectDevice_RequestedAmongSeveral_PicksIt()
        {
            var devices = new[] { (SERIAL, AndroidCommands.STATE_DEVICE), ("other", AndroidCommands.STATE_DEVICE) };

            Assert.That(AndroidCommands.SelectDevice(devices, "other"), Is.EqualTo("other"));
        }

        [Test]
        public void SelectDevice_SeveralReady_AsksToChooseOne()
        {
            var devices = new[] { (SERIAL, AndroidCommands.STATE_DEVICE), ("other", AndroidCommands.STATE_DEVICE) };

            Assert.That(() => AndroidCommands.SelectDevice(devices, null), Throws.TypeOf<MeasureException>().With.Message.Contains("Several"));
        }

        [Test]
        public void SelectDevice_RequestedNotReady_SaysWhichAreReady()
        {
            var devices = new[] { (SERIAL, AndroidCommands.STATE_DEVICE) };

            Assert.That(() => AndroidCommands.SelectDevice(devices, "other"), Throws.TypeOf<MeasureException>().With.Message.Contains(SERIAL));
        }

        [Test]
        public void SelectDevice_OnlyUnauthorized_AsksToAllowDebugging()
        {
            var devices = new[] { (SERIAL, AndroidCommands.STATE_UNAUTHORIZED) };

            Assert.That(() => AndroidCommands.SelectDevice(devices, null), Throws.TypeOf<MeasureException>().With.Message.Contains("allow it on the device"));
        }

        [Test]
        public void SelectDevice_None_AsksToPlugOneIn()
        {
            Assert.That(() => AndroidCommands.SelectDevice(new (string, string)[0], null), Throws.TypeOf<MeasureException>().With.Message.Contains("No Android device"));
        }

        [TestCase("    mShowingLockscreen=true mShowingDream=false", true)]
        [TestCase("  isKeyguardShowing=true", true)]
        [TestCase("    mKeyguardShowing=true", true)]
        [TestCase("    mShowingLockscreen=false mShowingDream=false\n  isKeyguardShowing=false", false)]
        [TestCase("", false)]
        public void IsLocked_DumpsysWindow_TellsWhetherTheLockScreenShows(string dumpsys, bool expected)
        {
            Assert.That(AndroidCommands.IsLocked(dumpsys), Is.EqualTo(expected));
        }

        [Test]
        public void ParseActivity_Installed_GivesTheLastLine()
        {
            var output = "priority=0 preferredOrder=0 match=0x108000 specificIndex=-1 isDefault=false\ncom.javienne.rouflac/com.unity3d.player.UnityPlayerGameActivity\n";

            Assert.That(AndroidCommands.ParseActivity(output), Is.EqualTo("com.javienne.rouflac/com.unity3d.player.UnityPlayerGameActivity"));
        }

        [TestCase("No activity found\n")]
        [TestCase("")]
        public void ParseActivity_NotInstalled_GivesNull(string output)
        {
            Assert.That(AndroidCommands.ParseActivity(output), Is.Null);
        }

        [Test]
        public void ReadLaunchReport_AmStart_ReadsTheFirstFrameAndTheKindOfLaunch()
        {
            var launch = new Launch();

            AndroidCommands.ReadLaunchReport("Starting: Intent { cmp=com.javienne.rouflac/.Activity }\nStatus: ok\nLaunchState: COLD\nActivity: com.javienne.rouflac/.Activity\nTotalTime: 133\nWaitTime: 140\nComplete\n", launch);

            Assert.That(launch.OsFirstFrame, Is.EqualTo(133));
            Assert.That(launch.OsLaunchState, Is.EqualTo("COLD"));
        }

        [Test]
        public void ReadLaunchReport_NoReport_LeavesTheLaunchAsIs()
        {
            var launch = new Launch();

            AndroidCommands.ReadLaunchReport("Error: Activity not started\n", launch);

            Assert.That(launch.OsFirstFrame, Is.EqualTo(-1));
            Assert.That(launch.OsLaunchState, Is.Null);
        }

        [TestCase("Active default network: 151\n  NetworkAgentInfo{network{148}", MeasureResult.NETWORK_ONLINE)]
        [TestCase("Active default network: none\n", MeasureResult.NETWORK_OFFLINE)]
        [TestCase("Can't find service: connectivity", MeasureResult.NETWORK_UNKNOWN)]
        public void ParseNetwork_DumpsysConnectivity_TellsWhetherThereIsADefaultNetwork(string dumpsys, string expected)
        {
            Assert.That(AndroidCommands.ParseNetwork(dumpsys), Is.EqualTo(expected));
        }

        [Test]
        public void Shell_Command_RunsItOnTheDevice()
        {
            Assert.That(AndroidCommands.Shell(SERIAL, "am", "force-stop", "com.javienne.rouflac"), Is.EqualTo(new[] { "-s", SERIAL, "shell", "am", "force-stop", "com.javienne.rouflac" }));
        }

        [Test]
        public void GetDataFolder_Package_IsItsFilesFolderOnTheSharedStorage()
        {
            Assert.That(AndroidCommands.GetDataFolder("com.javienne.rouflac"), Is.EqualTo("/sdcard/Android/data/com.javienne.rouflac/files"));
        }

        [Test]
        public void PushAndPull_Files_CopyThemToAndFromTheDevice()
        {
            Assert.That(AndroidCommands.Push(SERIAL, "/work/request.txt", "/sdcard/request.txt"), Is.EqualTo(new[] { "-s", SERIAL, "push", "/work/request.txt", "/sdcard/request.txt" }));
            Assert.That(AndroidCommands.Pull(SERIAL, "/sdcard/capture.raw", "/project/capture.raw"), Is.EqualTo(new[] { "-s", SERIAL, "pull", "/sdcard/capture.raw", "/project/capture.raw" }));
        }

        [Test]
        public void BuildDeviceApks_Aab_MakesTheApksOfTheConnectedDevice()
        {
            var arguments = AndroidCommands.BuildDeviceApks("/tools/bundletool.jar", "/build/Game.aab", "/work/device.apks", "/sdk/adb", SERIAL);

            Assert.That(arguments, Is.EqualTo(new[] { "-jar", "/tools/bundletool.jar", "build-apks", "--bundle=/build/Game.aab", "--output=/work/device.apks", "--overwrite", "--connected-device", "--device-id=" + SERIAL, "--adb=/sdk/adb" }));
        }

        [Test]
        public void ReadUnityLog_Serial_DumpsTheUnityTagOnly()
        {
            Assert.That(AndroidCommands.ReadUnityLog(SERIAL), Is.EqualTo(new[] { "-s", SERIAL, "logcat", "-d", "-s", "Unity:V" }));
        }
    }
}