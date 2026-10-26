using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using CodexQuotaTray.Codex;

namespace CodexQuotaTray.App;

internal static class ExecutableTrust
{
    public static VerifiedExecutable? Discover()
    {
        string root = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "OpenAI", "Codex", "bin");
        if (!Directory.Exists(root)) return null;
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(root).OrderByDescending(Directory.GetLastWriteTimeUtc).Take(20))
            {
                string path = Path.Combine(dir, "codex.exe");
                if (File.Exists(path) && IsOpenAiSigned(path)) return Fingerprint(path);
            }
        }
        catch (IOException) { }
        catch (UnauthorizedAccessException) { }
        return null;
    }
    public static VerifiedExecutable Fingerprint(string path)
    {
        path = Path.GetFullPath(path);
        if (!path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) throw new IOException();
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read);
        return new(path, Convert.ToHexString(SHA256.HashData(stream)));
    }
    private static bool IsOpenAiSigned(string path)
    {
        IntPtr filePointer = IntPtr.Zero;
        IntPtr pathPointer = Marshal.StringToCoTaskMemUni(path);
        var data = new TrustData();
        Guid action = new("00AAC56B-CD44-11d0-8CC2-00C04FC295EE");
        try
        {
            var file = new FileInfoNative { Size = (uint)Marshal.SizeOf<FileInfoNative>(), Path = pathPointer };
            filePointer = Marshal.AllocHGlobal(Marshal.SizeOf<FileInfoNative>());
            Marshal.StructureToPtr(file, filePointer, false);
            data = new TrustData { Size = (uint)Marshal.SizeOf<TrustData>(), UiChoice = 2, UnionChoice = 1, File = filePointer, StateAction = 1, ProviderFlags = 0x1000 | 0x10 };
            if (WinVerifyTrust(new IntPtr(-1), ref action, ref data) != 0) return false;
#pragma warning disable SYSLIB0057
            using var cert = new X509Certificate2(X509Certificate.CreateFromSignedFile(path));
#pragma warning restore SYSLIB0057
            return cert.GetNameInfo(X509NameType.SimpleName, false) is "OpenAI OpCo, LLC" or "OpenAI, L.L.C.";
        }
        catch (CryptographicException) { return false; }
        finally
        {
            if (data.StateData != IntPtr.Zero) { data.StateAction = 2; WinVerifyTrust(new IntPtr(-1), ref action, ref data); }
            if (filePointer != IntPtr.Zero) Marshal.FreeHGlobal(filePointer);
            Marshal.FreeCoTaskMem(pathPointer);
        }
    }
    [DllImport("wintrust.dll", ExactSpelling = true)]
    private static extern int WinVerifyTrust(IntPtr hwnd, ref Guid action, ref TrustData data);
    [StructLayout(LayoutKind.Sequential)]
    private struct FileInfoNative { public uint Size; public IntPtr Path, File, Subject; }
    [StructLayout(LayoutKind.Sequential)]
    private struct TrustData
    {
        public uint Size; public IntPtr Policy, Sip; public uint UiChoice, Revocation, UnionChoice;
        public IntPtr File; public uint StateAction; public IntPtr StateData, Url;
        public uint ProviderFlags, UiContext; public IntPtr SignatureSettings;
    }
}
