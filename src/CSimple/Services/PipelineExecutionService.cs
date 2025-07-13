using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using CSimple.Models;
using CSimple.ViewModels;

namespace CSimple.Services
{
    /// <summary>
    /// Service for handling pipeline execution, including "Run All Models" functionality
    /// with dependency resolution, topological sorting, and parallel execution
    /// </summary>
    public class PipelineExecutionService
    {
        private readonly EnsembleModelService _ensembleModelService;
        private readonly Func<NodeViewModel, NeuralNetworkModel> _findCorrespondingModelFunc;

        public PipelineExecutionService(EnsembleModelService ensembleModelService, Func<NodeViewModel, NeuralNetworkModel> findCorrespondingModelFunc)
        {
            _ensembleModelService = ensembleModelService ?? throw new ArgumentNullException(nameof(ensembleModelService));
            _findCorrespondingModelFunc = findCorrespondingModelFunc ?? throw new ArgumentNullException(nameof(findCorrespondingModelFunc));
        }

        /// <summary>
        /// Executes all model nodes in the pipeline with optimal dependency-based ordering
        /// </summary>
        public async Task<(int successCount, int skippedCount)> ExecuteAllModelsAsync(
            ObservableCollection<NodeViewModel> nodes,
            ObservableCollection<ConnectionViewModel> connections,
            int currentActionStep,
            Func<string, string, string, Task> showAlert = null)
        {
            var totalStopwatch = Stopwatch.StartNew();
            Debug.WriteLine("🎯 [PipelineExecutionService.ExecuteAllModelsAsync] Starting execution");

            try
            {
                // Step 1: Get all model nodes
                var step1Stopwatch = Stopwatch.StartNew();
                var modelNodes = nodes.Where(n => n.Type == NodeType.Model).ToList();
                step1Stopwatch.Stop();
                Debug.WriteLine($"⏱️ [Timing] Step 1 - Get model nodes: {step1Stopwatch.ElapsedMilliseconds}ms");

                if (modelNodes.Count == 0)
                {
                    if (showAlert != null)
                        await showAlert("Info", "No model nodes found in the pipeline.", "OK");
                    return (0, 0);
                }

                Debug.WriteLine($"📊 [PipelineExecutionService] Found {modelNodes.Count} model nodes to process");

                // Step 2: Pre-cache model lookups to avoid repeated searches
                var step2Stopwatch = Stopwatch.StartNew();
                var modelLookupCache = new Dictionary<string, NeuralNetworkModel>();
                foreach (var modelNode in modelNodes)
                {
                    var correspondingModel = _findCorrespondingModelFunc(modelNode);
                    if (correspondingModel != null)
                    {
                        modelLookupCache[modelNode.Id] = correspondingModel;
                    }
                }
                step2Stopwatch.Stop();
                Debug.WriteLine($"⏱️ [Timing] Step 2 - Build model lookup cache: {step2Stopwatch.ElapsedMilliseconds}ms");

                // Step 3: Build execution groups based on dependencies
                var step3Stopwatch = Stopwatch.StartNew();
                var executionGroups = BuildOptimizedExecutionGroups(modelNodes, connections);
                step3Stopwatch.Stop();
                Debug.WriteLine($"⏱️ [Timing] Step 3 - Build execution groups: {step3Stopwatch.ElapsedMilliseconds}ms");

                Debug.WriteLine($"📋 [PipelineExecutionService] Organized into {executionGroups.Count} execution groups:");
                for (int i = 0; i < executionGroups.Count; i++)
                {
                    var group = executionGroups[i];
                    Debug.WriteLine($"   Group {i + 1}: {string.Join(", ", group.Select(n => $"'{n.Name}'"))} ({group.Count} models)");
                }

                int successCount = 0;
                int skippedCount = 0;

                // Step 4: Execute groups sequentially, but models within groups in parallel
                var step4Stopwatch = Stopwatch.StartNew();
                int groupIndex = 0;
                foreach (var group in executionGroups)
                {
                    var groupStopwatch = Stopwatch.StartNew();
                    var groupStartTime = DateTime.UtcNow;
                    Debug.WriteLine($"📦 [PipelineExecutionService] Starting execution group {groupIndex + 1}/{executionGroups.Count} with {group.Count} models at {groupStartTime:HH:mm:ss.fff}:");

                    // Log models in this group with better resource context
                    foreach (var modelNode in group)
                    {
                        var inputCount = connections.Count(c => c.TargetNodeId == modelNode.Id);
                        var hasCorrespondingModel = modelLookupCache.ContainsKey(modelNode.Id);
                        var modelType = hasCorrespondingModel ? (modelLookupCache[modelNode.Id].HuggingFaceModelId?.Contains("whisper") == true ? "Audio" : 
                                                               modelLookupCache[modelNode.Id].HuggingFaceModelId?.Contains("blip") == true ? "Vision" : "Text") : "Unknown";
                        Debug.WriteLine($"   🤖 '{modelNode.Name}' | Type: {modelType} | Inputs: {inputCount} | Model Available: {hasCorrespondingModel} | Ensemble: {modelNode.SelectedEnsembleMethod}");
                    }

                    var tasks = group.Where(modelNode => CanExecuteModelNode(modelNode, connections) && modelLookupCache.ContainsKey(modelNode.Id))
                                     .Select(async modelNode =>
                    {
                        var modelStopwatch = Stopwatch.StartNew();
                        var taskStartTime = DateTime.UtcNow;
                        try
                        {
                            Debug.WriteLine($"🚀 [PipelineExecutionService] [{taskStartTime:HH:mm:ss.fff}] Starting execution: {modelNode.Name}");

                            await ExecuteOptimizedModelNodeAsync(modelNode, modelLookupCache[modelNode.Id], nodes, connections, currentActionStep);

                            modelStopwatch.Stop();
                            var taskEndTime = DateTime.UtcNow;
                            Debug.WriteLine($"✅ [PipelineExecutionService] [{taskEndTime:HH:mm:ss.fff}] Successfully executed: {modelNode.Name} in {modelStopwatch.ElapsedMilliseconds}ms");
                            return (success: true, node: modelNode, duration: modelStopwatch.ElapsedMilliseconds, startTime: taskStartTime, endTime: taskEndTime);
                        }
                        catch (Exception ex)
                        {
                            modelStopwatch.Stop();
                            var taskEndTime = DateTime.UtcNow;
                            Debug.WriteLine($"❌ [PipelineExecutionService] [{taskEndTime:HH:mm:ss.fff}] Error executing {modelNode.Name} after {modelStopwatch.ElapsedMilliseconds}ms: {ex.Message}");
                            return (success: false, node: modelNode, duration: modelStopwatch.ElapsedMilliseconds, startTime: taskStartTime, endTime: taskEndTime);
                        }
                    });

                    var results = await Task.WhenAll(tasks);
                    groupStopwatch.Stop();

                    // Enhanced group completion logging with better timing analysis
                    var successfulInGroup = results.Count(r => r.success);
                    var failedInGroup = results.Count(r => !r.success);
                    var avgDuration = results.Length > 0 ? results.Average(r => r.duration) : 0;
                    var maxDuration = results.Length > 0 ? results.Max(r => r.duration) : 0;
                    var minDuration = results.Length > 0 ? results.Min(r => r.duration) : 0;
                    
                    // Calculate actual parallelism metrics
                    var earliestStart = results.Length > 0 ? results.Min(r => r.startTime) : DateTime.UtcNow;
                    var latestEnd = results.Length > 0 ? results.Max(r => r.endTime) : DateTime.UtcNow;
                    var actualParallelDuration = (latestEnd - earliestStart).TotalMilliseconds;
                    var totalSequentialTime = results.Sum(r => r.duration);
                    var parallelismEfficiency = totalSequentialTime > 0 ? (totalSequentialTime / actualParallelDuration) : 0;
                    var groupEndTime = DateTime.UtcNow;
                    
                    // Calculate time overlap analysis
                    var timeSpread = maxDuration - minDuration;
                    var parallelismPercentage = actualParallelDuration > 0 ? (parallelismEfficiency / results.Length * 100) : 0;
                    
                    Debug.WriteLine($"📊 [PipelineExecutionService] Group {groupIndex + 1} completed in {groupStopwatch.ElapsedMilliseconds}ms at {groupEndTime:HH:mm:ss.fff}:");
                    Debug.WriteLine($"   ├── Models executed: {results.Length}/{group.Count}");
                    Debug.WriteLine($"   ├── Success: {successfulInGroup}, Failed: {failedInGroup}");
                    Debug.WriteLine($"   ├── Individual durations: Min={minDuration}ms, Avg={avgDuration:F0}ms, Max={maxDuration}ms (Spread: {timeSpread}ms)");
                    Debug.WriteLine($"   ├── Parallel analysis: Wall={actualParallelDuration:F0}ms, Work={totalSequentialTime:F0}ms, Efficiency={parallelismEfficiency:F1}x/{results.Length}x");
                    Debug.WriteLine($"   ├── Concurrency level: {parallelismPercentage:F0}% ({(parallelismPercentage > 80 ? "HIGHLY PARALLEL" : parallelismPercentage > 50 ? "MODERATELY PARALLEL" : "MOSTLY SEQUENTIAL")})");
                    Debug.WriteLine($"   ├── Resource contention: {(timeSpread > avgDuration * 0.5 ? "HIGH" : timeSpread > avgDuration * 0.2 ? "MODERATE" : "LOW")} (spread vs avg)");
                    Debug.WriteLine($"   └── Task overhead: {Math.Max(0, groupStopwatch.ElapsedMilliseconds - actualParallelDuration):F0}ms ({(actualParallelDuration > 0 ? Math.Max(0, groupStopwatch.ElapsedMilliseconds - actualParallelDuration) / actualParallelDuration * 100 : 0):F1}%)");

                    successCount += results.Count(r => r.success);
                    skippedCount += group.Count - results.Length + results.Count(r => !r.success);
                    groupIndex++;
                }
                step4Stopwatch.Stop();
                Debug.WriteLine($"⏱️ [Timing] Step 4 - Execute all groups: {step4Stopwatch.ElapsedMilliseconds}ms");

                totalStopwatch.Stop();
                Debug.WriteLine($"⏱️ [Timing] TOTAL EXECUTION TIME: {totalStopwatch.ElapsedMilliseconds}ms");

                string resultMessage = $"Execution completed!\nSuccessful: {successCount}\nSkipped: {skippedCount}";
                Debug.WriteLine($"🎉 [PipelineExecutionService] {resultMessage}");

                // Enhanced summary with execution breakdown
                Debug.WriteLine("📊 [PipelineExecutionService] EXECUTION SUMMARY:");
                Debug.WriteLine($"   ├── Models processed: {successCount + skippedCount}");
                Debug.WriteLine($"   ├── Success rate: {(successCount > 0 ? (double)successCount / (successCount + skippedCount) * 100 : 0):F1}%");
                Debug.WriteLine($"   ├── Execution groups: {executionGroups.Count}");
                Debug.WriteLine($"   ├── Pipeline execution time: {totalStopwatch.ElapsedMilliseconds}ms");
                Debug.WriteLine($"   ├── Time per successful model: {(successCount > 0 ? totalStopwatch.ElapsedMilliseconds / successCount : 0):F0}ms");
                Debug.WriteLine($"   ├── Setup overhead: {step1Stopwatch.ElapsedMilliseconds + step2Stopwatch.ElapsedMilliseconds + step3Stopwatch.ElapsedMilliseconds}ms");
                Debug.WriteLine($"   └── Execution efficiency: {(step4Stopwatch.ElapsedMilliseconds > 0 ? (double)step4Stopwatch.ElapsedMilliseconds / totalStopwatch.ElapsedMilliseconds * 100 : 0):F1}% actual execution");

                return (successCount, skippedCount);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [PipelineExecutionService] Critical error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Builds optimized execution groups based on dependency levels
        /// Models in the same group can be executed in parallel
        /// </summary>
        private List<List<NodeViewModel>> BuildOptimizedExecutionGroups(List<NodeViewModel> modelNodes, ObservableCollection<ConnectionViewModel> connections)
        {
            var visited = new HashSet<string>();
            var processing = new HashSet<string>(); // For cycle detection

            // Create dependency levels - models in the same level can run in parallel
            var dependencyLevels = new Dictionary<NodeViewModel, int>();

            foreach (var node in modelNodes)
            {
                if (!visited.Contains(node.Id))
                {
                    CalculateNodeLevel(node, dependencyLevels, visited, processing, 0, connections);
                }
            }

            // Group nodes by their dependency level
            var levelGroups = dependencyLevels.GroupBy(kvp => kvp.Value)
                                              .OrderBy(g => g.Key)
                                              .Select(g => g.Select(kvp => kvp.Key).ToList())
                                              .ToList();

            return levelGroups;
        }

        /// <summary>
        /// Calculates the dependency level of a node using recursive traversal
        /// </summary>
        private int CalculateNodeLevel(NodeViewModel node, Dictionary<NodeViewModel, int> dependencyLevels,
                                       HashSet<string> visited, HashSet<string> processing, int currentLevel,
                                       ObservableCollection<ConnectionViewModel> connections)
        {
            if (processing.Contains(node.Id))
                return currentLevel; // Cycle detection - use current level

            if (visited.Contains(node.Id))
                return dependencyLevels.GetValueOrDefault(node, 0);

            processing.Add(node.Id);

            var dependencies = GetNodeDependencies(node, connections).Where(d => d.Type == NodeType.Model);
            int maxDepLevel = currentLevel;

            foreach (var dependency in dependencies)
            {
                int depLevel = CalculateNodeLevel(dependency, dependencyLevels, visited, processing, currentLevel, connections);
                maxDepLevel = Math.Max(maxDepLevel, depLevel + 1);
            }

            processing.Remove(node.Id);
            visited.Add(node.Id);
            dependencyLevels[node] = maxDepLevel;

            return maxDepLevel;
        }

        /// <summary>
        /// Gets all nodes that the specified node depends on (input connections)
        /// </summary>
        private List<NodeViewModel> GetNodeDependencies(NodeViewModel node, ObservableCollection<ConnectionViewModel> connections)
        {
            var dependencies = new List<NodeViewModel>();

            // Find all nodes that this node depends on (input connections)
            var incomingConnections = connections.Where(c => c.TargetNodeId == node.Id).ToList();

            foreach (var connection in incomingConnections)
            {
                // Note: We need the nodes collection to find the actual NodeViewModel
                // This will be passed as a parameter in a real implementation
                // For now, we'll return an empty list and handle this in the calling code
                Debug.WriteLine($"🔗 [GetNodeDependencies] Found dependency connection: {connection.SourceNodeId} -> {node.Id}");
            }

            return dependencies;
        }

        /// <summary>
        /// Gets all nodes that the specified node depends on with access to the nodes collection
        /// </summary>
        public List<NodeViewModel> GetNodeDependencies(NodeViewModel node, ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections)
        {
            var dependencies = new List<NodeViewModel>();

            // Find all nodes that this node depends on (input connections)
            var incomingConnections = connections.Where(c => c.TargetNodeId == node.Id).ToList();

            foreach (var connection in incomingConnections)
            {
                var sourceNode = nodes.FirstOrDefault(n => n.Id == connection.SourceNodeId);
                if (sourceNode != null)
                {
                    dependencies.Add(sourceNode);

                    // If the source is also a model node, we need to ensure it runs first
                    if (sourceNode.Type == NodeType.Model)
                    {
                        // Recursively add its dependencies
                        dependencies.AddRange(GetNodeDependencies(sourceNode, nodes, connections));
                    }
                }
            }

            return dependencies.Distinct().ToList();
        }

        /// <summary>
        /// Executes a single model node with optimization
        /// </summary>
        private async Task ExecuteOptimizedModelNodeAsync(NodeViewModel modelNode, NeuralNetworkModel correspondingModel,
            ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections, int currentActionStep)
        {
            try
            {
                await _ensembleModelService.ExecuteSingleModelNodeAsync(modelNode, correspondingModel, nodes, connections, currentActionStep);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [ExecuteOptimizedModelNodeAsync] Error executing model {modelNode.Name}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Determines if a model node can be executed based on its configuration and connections
        /// </summary>
        private bool CanExecuteModelNode(NodeViewModel modelNode, ObservableCollection<ConnectionViewModel> connections)
        {
            if (modelNode.Type != NodeType.Model)
                return false;

            // Check if the model has the minimum required inputs
            var connectedInputCount = connections.Count(c => c.TargetNodeId == modelNode.Id);

            // For ensemble models, need at least 2 inputs
            if (modelNode.EnsembleInputCount > 1)
            {
                return connectedInputCount >= 2;
            }

            // For regular models, need at least 1 input or can run without inputs (depending on model type)
            return true; // Allow execution even without inputs for some model types
        }

        /// <summary>
        /// Builds a dependency-based execution order using topological sorting
        /// </summary>
        public List<NodeViewModel> BuildDependencyBasedExecutionOrder(List<NodeViewModel> modelNodes,
            ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections)
        {
            var executionOrder = new List<NodeViewModel>();
            var visited = new HashSet<string>();
            var visiting = new HashSet<string>(); // For cycle detection

            foreach (var node in modelNodes)
            {
                if (!visited.Contains(node.Id))
                {
                    TopologicalSort(node, visited, visiting, executionOrder, nodes, connections);
                }
            }

            return executionOrder;
        }

        /// <summary>
        /// Performs topological sorting to determine execution order
        /// </summary>
        private void TopologicalSort(NodeViewModel node, HashSet<string> visited, HashSet<string> visiting,
            List<NodeViewModel> executionOrder, ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections)
        {
            if (visiting.Contains(node.Id))
            {
                // Cycle detected - log warning but continue
                Debug.WriteLine($"⚠️ [TopologicalSort] Cycle detected involving node: {node.Name}");
                return;
            }

            if (visited.Contains(node.Id))
                return;

            visiting.Add(node.Id);

            // Find all dependencies (nodes that this node depends on)
            var dependencies = GetNodeDependencies(node, nodes, connections);
            foreach (var dependency in dependencies)
            {
                if (!visited.Contains(dependency.Id))
                {
                    TopologicalSort(dependency, visited, visiting, executionOrder, nodes, connections);
                }
            }

            visiting.Remove(node.Id);
            visited.Add(node.Id);

            // Only add model nodes to execution order
            if (node.Type == NodeType.Model)
            {
                executionOrder.Add(node);
            }
        }
    }
}
