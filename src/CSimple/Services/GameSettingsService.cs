namespace CSimple.Services
{
    public class GameSettingsService
    {
        private readonly ActionService _actionService;

        public GameSettingsService(ActionService actionService)
        {
            _actionService = actionService;
        }

        public bool GameOptimizedMode { get; set; } = false;
        public int MouseSensitivity { get; set; } = 100; // 1-200%
        public bool UseSmoothing { get; set; } = true;

        public void UpdateGameSettings(bool gameOptimizedMode, int mouseSensitivity, bool useSmoothing)
        {
            GameOptimizedMode = gameOptimizedMode;
            MouseSensitivity = Math.Clamp(mouseSensitivity, 1, 200);
            UseSmoothing = useSmoothing;

            InputSimulator.SetGameEnhancedMode(GameOptimizedMode, MouseSensitivity);

            // Update the action service settings (if already created)
            if (_actionService != null)
            {
                // Store these settings in the ActionService
                _actionService.UseInterpolation = UseSmoothing;
                _actionService.MovementSteps = GameOptimizedMode ? 20 : 10; // More steps in game mode
                _actionService.MovementDelayMs = GameOptimizedMode ? 1 : 2; // Faster in game mode
            }
        }
    }
}
