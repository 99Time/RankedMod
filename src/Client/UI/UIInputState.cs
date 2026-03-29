namespace schrader
{
    internal static class UIInputState
    {
        public static bool isDraftUIOpen;
        public static bool isScoreboardOpen;
        public static bool isCursorLocked = true;

        public static bool ShouldCursorBeVisible()
        {
            return isDraftUIOpen || isScoreboardOpen;
        }

        public static void Sync(UIManager uiManager)
        {
            if (!uiManager)
            {
                return;
            }

            isScoreboardOpen = uiManager.Scoreboard != null && uiManager.Scoreboard.IsVisible;
            isCursorLocked = !uiManager.isMouseActive;
        }

        public static void Reset()
        {
            isDraftUIOpen = false;
            isScoreboardOpen = false;
            isCursorLocked = true;
        }
    }
}