using System.Runtime.InteropServices;

namespace AudioSwitch.Audio.Interop;

// Raw COM interop for setting per-endpoint spatial-audio format via IPropertyStore.
// NAudio wraps PropertyStore but only exposes read access; write requires opening
// the property store with STGM_READWRITE and calling SetValue + Commit ourselves.
//
// Property: PKEY_AudioEndpoint_Spatial_Audio_Format
//   fmtid = {F19F064D-082C-4E27-BC73-6882A1BB8E4C}, pid = 5
//   type  = VT_UI4 (DWORD); value = format id (see SpatialAudioFormatRegistry).

[ComImport]
[Guid("BCDE0395-E52F-467C-8E3D-C4579291692E")]
internal class MMDeviceEnumeratorClass { }

[ComImport]
[Guid("A95664D2-9614-4F35-A746-DE8DB63617E6")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDeviceEnumerator
{
    [PreserveSig] int EnumAudioEndpoints(int dataFlow, int stateMask, out IntPtr ppDevices);
    [PreserveSig] int GetDefaultAudioEndpoint(int dataFlow, int role, out IMMDevice ppEndpoint);
    [PreserveSig] int GetDevice([MarshalAs(UnmanagedType.LPWStr)] string pwstrId, out IMMDevice ppDevice);
    [PreserveSig] int RegisterEndpointNotificationCallback(IntPtr pClient);
    [PreserveSig] int UnregisterEndpointNotificationCallback(IntPtr pClient);
}

[ComImport]
[Guid("D666063F-1587-4E43-81F1-B948E807363F")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IMMDevice
{
    [PreserveSig] int Activate(ref Guid iid, int clsCtx, IntPtr activationParams, [MarshalAs(UnmanagedType.IUnknown)] out object ppInterface);
    [PreserveSig] int OpenPropertyStore(int stgmAccess, out IPropertyStore ppProperties);
    [PreserveSig] int GetId([MarshalAs(UnmanagedType.LPWStr)] out string ppstrId);
    [PreserveSig] int GetState(out int pdwState);
}

[ComImport]
[Guid("886D8EEB-8CF2-4446-8D02-CDBA1DBDCF99")]
[InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
internal interface IPropertyStore
{
    [PreserveSig] int GetCount(out uint cProps);
    [PreserveSig] int GetAt(uint iProp, out PROPERTYKEY pkey);
    [PreserveSig] int GetValue(ref PROPERTYKEY key, out PROPVARIANT pv);
    [PreserveSig] int SetValue(ref PROPERTYKEY key, ref PROPVARIANT propvar);
    [PreserveSig] int Commit();
}

[StructLayout(LayoutKind.Sequential)]
internal struct PROPERTYKEY
{
    public Guid fmtid;
    public uint pid;
}

// Minimal PROPVARIANT covering VT_EMPTY and VT_UI4 (the only types we touch
// for the spatial-format DWORD). 16 bytes header + 8 bytes value = 24 bytes
// on x86, 24 on x64 (variant size is documented as 16 on x86 / 24 on x64;
// we use the larger layout which works on both since trailing bytes are unused).
[StructLayout(LayoutKind.Explicit, Size = 24)]
internal struct PROPVARIANT
{
    [FieldOffset(0)] public ushort vt;
    [FieldOffset(8)] public uint uintValue;
    [FieldOffset(8)] public IntPtr pointerValue;
}

internal static class SpatialAudioInterop
{
    public const int STGM_READ = 0x00000000;
    public const int STGM_READWRITE = 0x00000002;

    public const ushort VT_EMPTY = 0;
    public const ushort VT_UI4 = 19;

    public static readonly PROPERTYKEY PKEY_AudioEndpoint_Spatial_Audio_Format = new()
    {
        fmtid = new Guid("F19F064D-082C-4E27-BC73-6882A1BB8E4C"),
        pid = 5,
    };

    [DllImport("ole32.dll")]
    public static extern int PropVariantClear(ref PROPVARIANT pvar);
}
