# patch_exe_icon.ps1 - replace the exe's icon resources with app.ico frames.
# (twinBASIC leaves its default icon; project.iconForm needs a DESIGNER icon
# which the text-tree import cannot embed.)
# The whole flow (enumerate + rewrite) runs inside ONE C# block - per-call
# PowerShell P/Invoke round-trips produced ERROR_ENVVAR_NOT_FOUND (203) on
# every UpdateResourceW (see RESULTS). Pure C# works.
$ErrorActionPreference = 'Stop'
$exe = 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic\build\Build\SilentNotesTbP3_win32.exe'
$ico = 'D:\vibe coding\silentnotes\src\SilentNotes.TwinBasic\build\shellsrc\Resources\ICON\app.ico'

Add-Type -TypeDefinition @"
using System;
using System.Runtime.InteropServices;
public class IconPatch {
  public delegate bool EnumResNameProc(IntPtr hModule, IntPtr type, IntPtr name, IntPtr param);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern IntPtr BeginUpdateResourceW(string pFileName, bool bDeleteExistingResources);
  [DllImport("kernel32.dll", SetLastError=true)]
  public static extern bool EndUpdateResourceW(IntPtr hUpdate, bool fDiscard);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern bool UpdateResourceW(IntPtr hUpdate, IntPtr lpType, IntPtr lpName, ushort wLanguage, byte[] lpData, uint cbData);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern bool EnumResourceNamesW(IntPtr hModule, IntPtr type, EnumResNameProc cb, IntPtr param);
  [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)]
  public static extern IntPtr LoadLibraryExW(string name, IntPtr hFile, uint flags);
  [DllImport("kernel32.dll")] public static extern bool FreeLibrary(IntPtr h);

  public static string LastReport = "";

  // Replaces ALL icon resources with the frames of the .ico file:
  // existing RT_ICON ids are overwritten in order, surplus frames get new
  // ids, surplus old frames are deleted; the FIRST existing RT_GROUP_ICON
  // is overwritten with a directory pointing at the new frames.
  public static bool Patch(string exePath, string icoPath) {
    byte[] ico = System.IO.File.ReadAllBytes(icoPath);
    if (ico[0] != 0 || ico[1] != 0 || ico[2] != 1 || ico[3] != 0) throw new Exception("app.ico is not an .ico file");
    int count = ico[4] + ico[5] * 256;
    LastReport = "frames=" + count + "; ";

    // enumerate existing ids (datafile load, no free-then-begin race)
    int[] iconIds = new int[0];
    int[] groupIds = new int[0];
    IntPtr lib = LoadLibraryExW(exePath, IntPtr.Zero, 2);
    if (lib != IntPtr.Zero) {
      System.Collections.Generic.List<int> il = new System.Collections.Generic.List<int>();
      System.Collections.Generic.List<int> gl = new System.Collections.Generic.List<int>();
      EnumResNameProc cbI = delegate(IntPtr hm, IntPtr t, IntPtr n, IntPtr p) {
        if ((long)n < 65536L) il.Add((int)n);
        return true;
      };
      EnumResNameProc cbG = delegate(IntPtr hm, IntPtr t, IntPtr n, IntPtr p) {
        if ((long)n < 65536L) gl.Add((int)n);
        return true;
      };
      EnumResourceNamesW(lib, (IntPtr)3, cbI, IntPtr.Zero);
      EnumResourceNamesW(lib, (IntPtr)14, cbG, IntPtr.Zero);
      FreeLibrary(lib);
      iconIds = il.ToArray();
      groupIds = gl.ToArray();
    }
    LastReport += "oldIcons=" + iconIds.Length + " oldGroups=" + groupIds.Length + "; ";

    IntPtr h = BeginUpdateResourceW(exePath, false);
    if (h == IntPtr.Zero) { LastReport += "begin le=" + Marshal.GetLastWin32Error(); return false; }
    ushort lang = 0x0409;

    // overwrite existing RT_ICON ids in order
    int reused = Math.Min(count, iconIds.Length);
    for (int i = 0; i < reused; i++) {
      if (!WriteFrame(h, ico, i, iconIds[i], lang)) { EndUpdateResourceW(h, true); return false; }
    }
    // surplus OLD frames -> delete
    for (int i = reused; i < iconIds.Length; i++) {
      UpdateResourceW(h, (IntPtr)3, (IntPtr)iconIds[i], lang, null, 0);
    }
    // surplus NEW frames -> fresh ids from 1000
    for (int i = reused; i < count; i++) {
      if (!WriteFrame(h, ico, i, 1000 + i, lang)) { EndUpdateResourceW(h, true); return false; }
    }
    // the new frame ids in final order
    int[] finalIds = new int[count];
    for (int i = 0; i < count; i++) finalIds[i] = (i < reused) ? iconIds[i] : 1000 + i;

    // overwrite the first existing group; delete other groups
    if (groupIds.Length == 0) groupIds = new int[] { 1 };
    byte[] grp = BuildGroupDir(ico, count, finalIds);
    if (!UpdateResourceW(h, (IntPtr)14, (IntPtr)groupIds[0], lang, grp, (uint)grp.Length)) {
      LastReport += "group le=" + Marshal.GetLastWin32Error();
      EndUpdateResourceW(h, true);
      return false;
    }
    for (int i = 1; i < groupIds.Length; i++) {
      UpdateResourceW(h, (IntPtr)14, (IntPtr)groupIds[i], lang, null, 0);
    }
    if (!EndUpdateResourceW(h, false)) {
      LastReport += "end le=" + Marshal.GetLastWin32Error();
      return false;
    }
    LastReport += "group id=" + groupIds[0] + " -> OK";
    return true;
  }

  static bool WriteFrame(IntPtr h, byte[] ico, int frameIdx, int resId, ushort lang) {
    int off = 6 + frameIdx * 16;
    int len = BitConverter.ToInt32(ico, off + 8);
    int imgOff = BitConverter.ToInt32(ico, off + 12);
    byte[] frame = new byte[len];
    Array.Copy(ico, imgOff, frame, 0, len);
    if (!UpdateResourceW(h, (IntPtr)3, (IntPtr)resId, lang, frame, (uint)len)) {
      LastReport += "frame " + resId + " le=" + Marshal.GetLastWin32Error() + "; ";
      return false;
    }
    return true;
  }

  static byte[] BuildGroupDir(byte[] ico, int count, int[] resIds) {
    // GRPICONDIRENTRY (14 bytes) differs from ICONDIR (16): wBytesInRes is
    // a WORD (low 16 bits of the DWORD size) and wId sits at offset 10-11.
    byte[] grp = new byte[6 + count * 14];
    grp[2] = 1;                  // type=1 (icon)
    grp[4] = ico[4]; grp[5] = ico[5];   // count
    for (int i = 0; i < count; i++) {
      int src = 6 + i * 16;
      int dst = 6 + i * 14;
      Array.Copy(ico, src, grp, dst, 8);    // w,h,colors,reserved,planes,bitcount
      grp[dst + 8] = ico[src + 8];          // wBytesInRes (low word)
      grp[dst + 9] = ico[src + 9];
      grp[dst + 10] = (byte)(resIds[i] & 0xFF);   // wId
      grp[dst + 11] = (byte)((resIds[i] >> 8) & 0xFF);
    }
    return grp;
  }
}
"@

$running = Get-Process -Name 'SilentNotesTbP3_win32' -ErrorAction SilentlyContinue
if ($running -ne $null) {
  Stop-Process -Id $running.Id -Force
  Start-Sleep -Milliseconds 800
}
if (-not [IconPatch]::Patch($exe, $ico)) {
  throw ("ICON PATCH FAILED: " + [IconPatch]::LastReport)
}
Write-Output ("ICON PATCHED: " + [IconPatch]::LastReport)
exit 0
