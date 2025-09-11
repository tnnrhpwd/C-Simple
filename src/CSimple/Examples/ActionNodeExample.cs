using System;
using System.Threading.Tasks;
using CSimple.Services;

namespace CSimple.Examples
{
    /// <summary>
    /// Example demonstrating the enhanced action classified nodes system
    /// </summary>
    public class ActionNodeExample
    {
        private readonly ActionStringGenerationService _actionStringService;
        private readonly ActionExecutionService _actionExecutionService;

        public ActionNodeExample()
        {
            _actionStringService = new ActionStringGenerationService();
            _actionExecutionService = new ActionExecutionService();
        }

        /// <summary>
        /// Example of processing action model output
        /// </summary>
        public async Task ProcessActionModelOutputExample()
        {
            // Simulate action model outputs
            var actionModelOutputs = new[]
            {
                "double click on the Minecraft window",
                "click the start button",
                "press the enter key",
                "type 'Hello World'",
                "right click on desktop",
                "scroll down in browser"
            };

            var planContext = "Goal: Launch Minecraft and create a new world\nPlan: 1. Open Minecraft 2. Click 'Create New World' 3. Enter world name";

            foreach (var output in actionModelOutputs)
            {
                Console.WriteLine($"\n=== Processing Action Model Output ===");
                Console.WriteLine($"Output: {output}");
                Console.WriteLine($"Context: {planContext}");

                try
                {
                    // Generate executable action string
                    var actionString = await _actionStringService.GenerateExecutableActionString(output, planContext);
                    
                    if (!string.IsNullOrEmpty(actionString))
                    {
                        Console.WriteLine($"Generated Action String: {actionString}");
                        
                        // Execute the action (in a real scenario)
                        // var success = await _actionExecutionService.ExecuteActionStringAsync(actionString);
                        // Console.WriteLine($"Execution Result: {success}");
                        
                        Console.WriteLine("✅ Action generation successful");
                    }
                    else
                    {
                        Console.WriteLine("❌ Could not generate action string");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ Error: {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Example of OrientPage pipeline usage
        /// </summary>
        public async Task OrientPagePipelineExample()
        {
            Console.WriteLine("\n=== OrientPage Pipeline Example ===");
            
            // Simulate pipeline with Goal → Plan → Action flow
            Console.WriteLine("1. Goal Model Output: 'Launch Minecraft and start playing'");
            Console.WriteLine("2. Plan Model Output: 'Step 1: Double click Minecraft icon\\nStep 2: Wait for loading\\nStep 3: Click Single Player\\nStep 4: Create New World'");
            Console.WriteLine("3. Action Model Output: 'double click on Minecraft window'");
            
            // In the enhanced system, this would happen automatically:
            var actionOutput = "double click on Minecraft window";
            var planContext = "Step 1: Double click Minecraft icon\nStep 2: Wait for loading\nStep 3: Click Single Player\nStep 4: Create New World";
            
            var actionString = await _actionStringService.GenerateExecutableActionString(actionOutput, planContext);
            Console.WriteLine($"4. Generated Action String: {actionString}");
            Console.WriteLine("5. Action automatically executed when ActionsEnabled=true");
        }

        /// <summary>
        /// Example of NetPage pipeline chat usage
        /// </summary>
        public async Task NetPagePipelineChatExample()
        {
            Console.WriteLine("\n=== NetPage Pipeline Chat Example ===");
            
            Console.WriteLine("User Input: 'Please open calculator and calculate 5+3'");
            Console.WriteLine("Pipeline processes through Goal → Plan → Action models...");
            
            // Simulate action model output in pipeline
            var pipelineActionOutput = "click on calculator button";
            var actionString = await _actionStringService.GenerateExecutableActionString(pipelineActionOutput);
            
            Console.WriteLine($"Pipeline Generated: {actionString}");
            Console.WriteLine("Pipeline Chat: '🎯 Generated action string from model output'");
            Console.WriteLine("Pipeline Chat: '✅ Action executed successfully'");
        }
    }

    /// <summary>
    /// Demonstrates coordinate resolution capabilities
    /// </summary>
    public class CoordinateResolutionExample
    {
        private readonly WindowDetectionService _windowService;
        private readonly ScreenAnalysisService _screenService;

        public CoordinateResolutionExample()
        {
            _windowService = new WindowDetectionService();
            _screenService = new ScreenAnalysisService();
        }

        public async Task DemonstrateCoordinateResolution()
        {
            Console.WriteLine("\n=== Coordinate Resolution Examples ===");

            // Window-based coordinate resolution
            var minecraftCenter = await _windowService.GetWindowCenterAsync("Minecraft");
            if (minecraftCenter.HasValue)
            {
                Console.WriteLine($"Minecraft window center: {minecraftCenter.Value}");
            }

            // UI element detection
            var buttonLocation = await _screenService.FindUIElementAsync("start button", "desktop context");
            if (buttonLocation.HasValue)
            {
                Console.WriteLine($"Start button location: {buttonLocation.Value}");
            }

            // Common resolution scenarios
            var resolutionExamples = new[]
            {
                ("click on Chrome window", "Window detection → Chrome center coordinates"),
                ("click the OK button", "UI analysis → Button coordinates"),
                ("right click on desktop", "Screen bounds → Desktop area"),
                ("click in text field", "UI analysis → Input field coordinates")
            };

            foreach (var (command, explanation) in resolutionExamples)
            {
                Console.WriteLine($"'{command}' → {explanation}");
            }
        }
    }
}
