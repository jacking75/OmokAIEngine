using System.Runtime.InteropServices;
using OmokGame;

// 고DPI 모니터에서 흐릿함 방지: MonoGame 창 생성 전에 DPI awareness 설정.
DpiHelper.EnableDpiAwareness();

using var game = new Game1();
game.Run();

internal static class DpiHelper
{
    [DllImport("user32.dll")]
    private static extern bool SetProcessDPIAware();

    public static void EnableDpiAwareness()
    {
        try { SetProcessDPIAware(); } catch { /* 비-Windows: 무시 */ }
    }
}
