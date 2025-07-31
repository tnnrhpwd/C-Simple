using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CSimple.Models;
using CSimple.ViewModels;

namespace CSimple.Services
{
    public interface IPipelineExecutionValidationService
    {
        Task<string> ExecuteCurrentPipelineAsync(
            ObservableCollection<NodeViewModel> nodes,
            ObservableCollection<ConnectionViewModel> connections,
            string currentPipelineName,
            string promptOverride = null);

        Task<string> ExecutePipelineByNameAsync(
            string pipelineName,
            string initialInput,
            FileService fileService,
            Func<string, Task> loadPipelineByName,
            Func<string> getCurrentPipelineName,
            Func<int> getNodesCount);

        bool HasAnyNodes(ObservableCollection<NodeViewModel> nodes);
        bool HasAnyConnections(ObservableCollection<ConnectionViewModel> connections);
        List<NodeViewModel> GetTextModelNodes(ObservableCollection<NodeViewModel> nodes);
        bool ValidatePipelineStructure(ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections);
    }

    public class PipelineExecutionValidationService : IPipelineExecutionValidationService
    {
        private readonly object _nodesLock = new object();
        private readonly object _connectionsLock = new object();

        public bool HasAnyNodes(ObservableCollection<NodeViewModel> nodes)
        {
            lock (_nodesLock)
            {
                return nodes.Any();
            }
        }

        public bool HasAnyConnections(ObservableCollection<ConnectionViewModel> connections)
        {
            lock (_connectionsLock)
            {
                return connections.Any();
            }
        }

        public List<NodeViewModel> GetTextModelNodes(ObservableCollection<NodeViewModel> nodes)
        {
            lock (_nodesLock)
            {
                return nodes.Where(n => n.Type == NodeType.Model && n.DataType == "text").ToList();
            }
        }

        public bool ValidatePipelineStructure(ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections)
        {
            if (!HasAnyNodes(nodes) || !HasAnyConnections(connections))
            {
                return false;
            }

            var inputNodes = nodes.Where(n => n.Type == NodeType.Input).ToList();
            var modelNodes = nodes.Where(n => n.Type == NodeType.Model).ToList();

            // Validate that we have at least one input and one model
            if (!inputNodes.Any() || !modelNodes.Any())
            {
                return false;
            }

            // Validate that there are connections between inputs and models
            var hasValidConnections = connections.Any(c =>
                inputNodes.Any(input => input.Id == c.SourceNodeId) &&
                modelNodes.Any(model => model.Id == c.TargetNodeId));

            return hasValidConnections;
        }

        /// <summary>
        /// Executes the currently loaded pipeline, optionally injecting a prompt into the final text model.
        /// NOTE: This is a simulation and does not run actual models.
        /// </summary>
        /// <param name="nodes">The nodes collection</param>
        /// <param name="connections">The connections collection</param>
        /// <param name="currentPipelineName">The current pipeline name</param>
        /// <param name="promptOverride">A specific prompt to add to the final text model's input.</param>
        /// <returns>The simulated output string from the final node, or an error message.</returns>
        public async Task<string> ExecuteCurrentPipelineAsync(
            ObservableCollection<NodeViewModel> nodes,
            ObservableCollection<ConnectionViewModel> connections,
            string currentPipelineName,
            string promptOverride = null)
        {
            Debug.WriteLine($"Executing pipeline '{currentPipelineName}' with prompt override: '{promptOverride}'");

            if (!HasAnyNodes(nodes) || !HasAnyConnections(connections))
            {
                return "Error: Pipeline is empty or has no connections.";
            }

            // --- Simulation Logic ---
            // This needs to be replaced with actual graph traversal and model execution.
            // For now, we'll make assumptions based on common patterns:
            // 1. Find Input nodes.
            // 2. Find Model nodes directly connected FROM Input nodes (Interpreters).
            // 3. Find a Model node connected FROM multiple Interpreters (Combiner/Final Text Model).

            var inputNodes = nodes.Where(n => n.Type == NodeType.Input).ToList();
            if (!inputNodes.Any()) return "Error: No input nodes found.";

            var interpreterOutputs = new Dictionary<string, string>(); // NodeId -> Simulated Output
            var interpreterNodes = new List<NodeViewModel>();

            // Simulate interpreter models processing inputs
            foreach (var inputNode in inputNodes)
            {
                var connectedModelIds = connections
                    .Where(c => c.SourceNodeId == inputNode.Id)
                    .Select(c => c.TargetNodeId);

                foreach (var modelId in connectedModelIds)
                {
                    var modelNode = nodes.FirstOrDefault(n => n.Id == modelId && n.Type == NodeType.Model);
                    if (modelNode != null && !interpreterOutputs.ContainsKey(modelNode.Id))
                    {
                        // Simulate output based on input type
                        string simulatedOutput = $"Interpreted {inputNode.DataType ?? "data"} from '{inputNode.Name}' via '{modelNode.Name}'.";
                        interpreterOutputs.Add(modelNode.Id, simulatedOutput);
                        if (!interpreterNodes.Contains(modelNode))
                        {
                            interpreterNodes.Add(modelNode);
                        }
                        Debug.WriteLine($"Simulated output for interpreter '{modelNode.Name}': {simulatedOutput}");
                    }
                }
            }

            if (!interpreterNodes.Any()) return "Error: No interpreter models found connected to inputs.";

            // Find the final combiner/text model (connected FROM interpreters)
            NodeViewModel finalModel = null;
            var textModelNodes = GetTextModelNodes(nodes);
            foreach (var potentialFinalNode in textModelNodes)
            {
                var incomingConnections = connections
                    .Where(c => c.TargetNodeId == potentialFinalNode.Id)
                    .Select(c => c.SourceNodeId);

                // Check if this node receives input from *all* identified interpreters
                bool receivesFromAllInterpreters = interpreterNodes.All(interp => incomingConnections.Contains(interp.Id));

                // Or check if it receives from *any* interpreter (simpler assumption)
                bool receivesFromAnyInterpreter = interpreterNodes.Any(interp => incomingConnections.Contains(interp.Id));

                // Let's assume the final node is the first text model connected to *any* interpreter
                if (receivesFromAnyInterpreter)
                {
                    finalModel = potentialFinalNode;
                    Debug.WriteLine($"Identified potential final model: '{finalModel.Name}'");
                    break;
                }
            }

            if (finalModel == null)
            {
                // Fallback: Find *any* model connected from an interpreter if no text model found - thread-safe version
                List<NodeViewModel> nodesCopy;
                List<ConnectionViewModel> connectionsCopy;

                lock (_nodesLock)
                {
                    nodesCopy = nodes.ToList();
                }

                lock (_connectionsLock)
                {
                    connectionsCopy = connections.ToList();
                }

                finalModel = nodesCopy.FirstOrDefault(n => n.Type == NodeType.Model &&
                    connectionsCopy.Any(c => c.TargetNodeId == n.Id && interpreterNodes.Any(interp => interp.Id == c.SourceNodeId)));
                if (finalModel != null)
                {
                    Debug.WriteLine($"Identified fallback final model (non-text?): '{finalModel.Name}'");
                }
            }

            if (finalModel == null) return "Error: Could not identify a final processing model connected to interpreters.";

            // Simulate final model execution
            var combinedInput = new StringBuilder();
            combinedInput.AppendLine($"Processing request for model '{finalModel.Name}':");

            // Gather inputs from connected interpreters
            var finalModelInputs = connections
                   .Where(c => c.TargetNodeId == finalModel.Id)
                   .Select(c => c.SourceNodeId);

            foreach (var inputId in finalModelInputs)
            {
                if (interpreterOutputs.TryGetValue(inputId, out var output))
                {
                    combinedInput.AppendLine($"- Input: {output}");
                }
                else
                {
                    // Maybe connected directly from an input node?
                    var directInputNode = nodes.FirstOrDefault(n => n.Id == inputId && n.Type == NodeType.Input);
                    if (directInputNode != null)
                    {
                        combinedInput.AppendLine($"- Direct Input: Raw {directInputNode.DataType ?? "data"} from '{directInputNode.Name}'.");
                    }
                }
            }

            if (!string.IsNullOrWhiteSpace(promptOverride))
            {
                combinedInput.AppendLine($"- Specific Prompt: {promptOverride}");
            }

            // Simulate API call or local execution delay
            await Task.Delay(1500); // Simulate processing time

            string finalOutput = $"Simulated result from '{finalModel.Name}': Based on the inputs ({interpreterNodes.Count} sources) and the prompt, the suggested improvement is to [Simulated AI Suggestion - Refine workflow for {finalModel.Name}].";
            Debug.WriteLine($"Final simulated output: {finalOutput}");

            return finalOutput;
        }

        // Method to execute a pipeline by name
        public async Task<string> ExecutePipelineByNameAsync(
            string pipelineName,
            string initialInput,
            FileService fileService,
            Func<string, Task> loadPipelineByName,
            Func<string> getCurrentPipelineName,
            Func<int> getNodesCount)
        {
            Debug.WriteLine($"Attempting to execute pipeline: {pipelineName} with input: {initialInput}");
            await loadPipelineByName(pipelineName); // Load the specified pipeline first

            if (getCurrentPipelineName() != pipelineName || getNodesCount() == 0)
            {
                return $"Error: Failed to load pipeline '{pipelineName}' before execution.";
            }

            // Pipeline execution would be handled by the calling code using ExecuteCurrentPipelineAsync
            return "Pipeline loaded successfully. Use ExecuteCurrentPipelineAsync to run.";
        }
    }
}
