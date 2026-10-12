using System.Runtime.InteropServices;

namespace XhznPlayer.Services;

public static class VolumeService
{
    public static float Get()
    {
        if (!TryGetVolume(out var vol)) return 0;
        try
        {
            vol.GetMasterVolumeLevelScalar(out var level);
            return level;
        }
        finally
        {
            Marshal.ReleaseComObject(vol);
        }
    }

    public static float Change(VolumeDelta delta)
    {
        if (!TryGetVolume(out var vol)) return Get();
        try
        {
            if (delta == VolumeDelta.Mute)
            {
                vol.GetMute(out var muted);
                vol.SetMute(!muted, Guid.Empty);
            }
            else
            {
                vol.GetMasterVolumeLevelScalar(out var level);
                var next = Math.Clamp(level + (delta == VolumeDelta.Up ? 0.04f : -0.04f), 0f, 1f);
                vol.SetMasterVolumeLevelScalar(next, Guid.Empty);
                vol.SetMute(next <= 0.0001f, Guid.Empty);
            }
            vol.GetMasterVolumeLevelScalar(out var now);
            return now;
        }
        finally
        {
            Marshal.ReleaseComObject(vol);
        }
    }

    private static bool TryGetVolume(out IAudioEndpointVolume volume)
    {
        volume = null!;
        object? enumeratorObj = null;
        object? deviceObj = null;
        try
        {
            enumeratorObj = Activator.CreateInstance(Type.GetTypeFromCLSID(new Guid("BCDE0395-E52F-467C-8E3D-C4579291692E"))!);
            var enumerator = (IMMDeviceEnumerator)enumeratorObj!;
            enumerator.GetDefaultAudioEndpoint(0, 0, out var device);
            deviceObj = device;
            device.Activate(typeof(IAudioEndpointVolume).GUID, 23, IntPtr.Zero, out var obj);
            volume = (IAudioEndpointVolume)obj;
            return true;
        }
        catch (Exception ex)
        {
            Logger.Warn("系统音量接口不可用: " + ex.Message);
            return false;
        }
        finally
        {
            if (deviceObj != null) Marshal.ReleaseComObject(deviceObj);
            if (enumeratorObj != null) Marshal.ReleaseComObject(enumeratorObj);
        }
    }

    [ComImport]
    [Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDeviceEnumerator
    {
        void EnumAudioEndpoints(int dataFlow, int dwStateMask, out IntPtr devices);
        void GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppDevice);
    }

    [ComImport]
    [Guid("D666063F-1587-4E43-81F1-B948E807363F")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IMMDevice
    {
        void Activate(in Guid iid, int dwClsCtx, IntPtr pActivationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    }

    [ComImport]
    [Guid("5CDF2C82-841E-4546-9722-0CF74078229A")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    private interface IAudioEndpointVolume
    {
        void RegisterControlChangeNotify(IntPtr pNotify);
        void UnregisterControlChangeNotify(IntPtr pNotify);
        void GetChannelCount(out uint count);
        void SetMasterVolumeLevel(float levelDb, Guid ctx);
        void SetMasterVolumeLevelScalar(float level, Guid ctx);
        void GetMasterVolumeLevel(out float levelDb);
        void GetMasterVolumeLevelScalar(out float level);
        void SetChannelVolumeLevel(uint ch, float levelDb, Guid ctx);
        void SetChannelVolumeLevelScalar(uint ch, float level, Guid ctx);
        void GetChannelVolumeLevel(uint ch, out float levelDb);
        void GetChannelVolumeLevelScalar(uint ch, out float level);
        void SetMute([MarshalAs(UnmanagedType.Bool)] bool mute, Guid ctx);
        void GetMute([MarshalAs(UnmanagedType.Bool)] out bool mute);
    }
}
