namespace Cliptok.Constants
{
    public class VcRedistConstants
    {
        public static readonly VcRedist[] VcRedists =
        {
            new(
                2015,
                140,
                new()
                {
                    { RedistArch.X64, "https://aka.ms/vs/17/release/vc_redist.x64.exe" },
                    { RedistArch.X86, "https://aka.ms/vs/17/release/vc_redist.x86.exe" },
                    { RedistArch.Arm64, "https://aka.ms/vs/17/release/vc_redist.arm64.exe" },
                }
            ),

            new(
                2013,
                120,
                new()
                {
                    { RedistArch.X64, "https://aka.ms/highdpimfc2013x64enu" },
                    { RedistArch.X86, "https://aka.ms/highdpimfc2013x86enu" },
                }
            ),

            new(
                2012,
                110,
                new()
                {
                    { RedistArch.X64, "https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x64.exe" },
                    { RedistArch.X86, "https://download.microsoft.com/download/1/6/B/16B06F60-3B20-4FF2-B699-5E9B7962F9AE/VSU_4/vcredist_x86.exe" },
                }
            ),

            new(
                2010,
                100,
                new()
                {
                    { RedistArch.X64, "https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x64.exe" },
                    { RedistArch.X86, "https://download.microsoft.com/download/1/6/5/165255E7-1014-4D0A-B094-B6A430A6BFFC/vcredist_x86.exe" },
                }
            ),

            new(
                2008,
                90,
                new()
                {
                    { RedistArch.X64, "https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x64.exe" },
                    { RedistArch.X86, "https://download.microsoft.com/download/5/D/8/5D8C65CB-C849-4025-8E95-C3966CAFD8AE/vcredist_x86.exe" },
                }
            ),

            new(
                2005,
                80,
                new()
                {
                    { RedistArch.X64 | RedistArch.X86, "https://www.microsoft.com/download/details.aspx?id=26347" },
                }
            )
        };
    }

    public readonly record struct VcRedist(int Year, int Version, Dictionary<RedistArch, string> DownloadUrls);

    [Flags]
    public enum RedistArch
    {
        X64 = 1,
        X86 = 2,
        Arm64 = 4
    }
}
