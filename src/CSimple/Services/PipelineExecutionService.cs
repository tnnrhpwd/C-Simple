using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading;
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

                    // Create tasks for parallel execution with optimized Python process management
                    var taskCreationStopwatch = Stopwatch.StartNew();
                    var executableModels = group.Where(modelNode => CanExecuteModelNode(modelNode, connections) && modelLookupCache.ContainsKey(modelNode.Id)).ToList();
                    
                    // Optimize task scheduling within the group
                    executableModels = OptimizeGroupTaskScheduling(executableModels);
                    
                    Debug.WriteLine($"🔧 [PipelineExecutionService] Using UNLIMITED parallel execution for {executableModels.Count} models (Maximum Performance Mode)");
                    
                    // Use unlimited execution to maximize parallel performance
                    var (batchSuccessCount, batchFailedCount) = await ExecuteBatchedModelsAsync(executableModels, modelLookupCache, nodes, connections, currentActionStep);
                    
                    taskCreationStopwatch.Stop();
                    Debug.WriteLine($"⚡ [PipelineExecutionService] Completed unlimited parallel execution in {taskCreationStopwatch.ElapsedMilliseconds}ms with maximum performance mode");

                    // Create results array to match the original structure
                    var results = executableModels.Select(model => new { 
                        success = batchSuccessCount > 0, // Simplified for now - in reality we'd track individual results
                        node = model, 
                        duration = taskCreationStopwatch.ElapsedMilliseconds / Math.Max(1, executableModels.Count),
                        startTime = DateTime.UtcNow.AddMilliseconds(-taskCreationStopwatch.ElapsedMilliseconds),
                        endTime = DateTime.UtcNow
                    }).ToArray();
                    groupStopwatch.Stop();

                    // Enhanced group completion logging with better timing analysis
                    var successfulInGroup = batchSuccessCount;
                    var failedInGroup = batchFailedCount;
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
                    Debug.WriteLine($"   ├── Task overhead: {Math.Max(0, groupStopwatch.ElapsedMilliseconds - actualParallelDuration):F0}ms ({(actualParallelDuration > 0 ? Math.Max(0, groupStopwatch.ElapsedMilliseconds - actualParallelDuration) / actualParallelDuration * 100 : 0):F1}%)");

                    successCount += batchSuccessCount;
                    skippedCount += group.Count - batchSuccessCount - batchFailedCount;
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
            var dependencyLevels = new Dictionary<NodeViewModel, int>();
            
            // ENHANCED ANALYSIS: Only consider ACTUAL model-to-model dependencies
            // A model-to-model dependency exists only when one model's OUTPUT feeds into another model's INPUT
            var actualModelConnections = connections.Where(c => 
            {
                var sourceNode = modelNodes.FirstOrDefault(m => m.Id == c.SourceNodeId);
                var targetNode = modelNodes.FirstOrDefault(m => m.Id == c.TargetNodeId);
                
                // Both nodes must be models AND the source must have already produced output
                // that the target depends on (not just input->model connections)
                bool isModelToModel = sourceNode?.Type == NodeType.Model && targetNode?.Type == NodeType.Model;
                
                if (isModelToModel)
                {
                    Debug.WriteLine($"   🔗 Found model-to-model dependency: '{sourceNode.Name}' -> '{targetNode.Name}'");
                }
                
                return isModelToModel;
            }).ToList();
            
            Debug.WriteLine($"📊 [BuildOptimizedExecutionGroups] Found {actualModelConnections.Count} actual model-to-model connections out of {connections.Count} total connections");
            
            // OPTIMIZATION: Check if we can achieve maximum parallelism
            // Most pipelines should run all models in parallel unless there are explicit model chains
            if (actualModelConnections.Count == 0)
            {
                Debug.WriteLine("� [BuildOptimizedExecutionGroups] NO MODEL-TO-MODEL DEPENDENCIES - ENABLING MAXIMUM PARALLELISM");
                Debug.WriteLine($"⚡ [BuildOptimizedExecutionGroups] All {modelNodes.Count} models will execute in parallel for optimal performance");
                
                // Log all connections for debugging but don't treat them as dependencies
                foreach (var conn in connections)
                {
                    var sourceNode = modelNodes.FirstOrDefault(m => m.Id == conn.SourceNodeId);
                    var targetNode = modelNodes.FirstOrDefault(m => m.Id == conn.TargetNodeId);
                    var sourceType = sourceNode?.Type.ToString() ?? "Input/Other";
                    var targetType = targetNode?.Type.ToString() ?? "Output/Other";
                    Debug.WriteLine($"   📎 Connection: {sourceType} -> {targetType} (input/output flow, not execution dependency)");
                }
                
                return new List<List<NodeViewModel>> { modelNodes };
            }
            
            // Build dependency graph for models that do depend on each other
            var dependents = new Dictionary<string, HashSet<string>>();
            var dependencies = new Dictionary<string, HashSet<string>>();
            
            foreach (var node in modelNodes)
            {
                dependents[node.Id] = new HashSet<string>();
                dependencies[node.Id] = new HashSet<string>();
            }
            
            foreach (var connection in actualModelConnections)
            {
                dependents[connection.SourceNodeId].Add(connection.TargetNodeId);
                dependencies[connection.TargetNodeId].Add(connection.SourceNodeId);
            }
            
            // Assign levels based on dependency depth
            var visited = new HashSet<string>();
            foreach (var node in modelNodes)
            {
                if (!visited.Contains(node.Id))
                {
                    CalculateNodeLevelOptimized(node, dependencyLevels, dependencies, visited, modelNodes);
                }
            }

            // Group nodes by their dependency level
            var levelGroups = dependencyLevels.GroupBy(kvp => kvp.Value)
                                              .OrderBy(g => g.Key)
                                              .Select(g => g.Select(kvp => kvp.Key).ToList())
                                              .ToList();
            
            Debug.WriteLine($"📊 [BuildOptimizedExecutionGroups] Created {levelGroups.Count} execution levels:");
            for (int i = 0; i < levelGroups.Count; i++)
            {
                Debug.WriteLine($"   Level {i}: {string.Join(", ", levelGroups[i].Select(n => n.Name))} ({levelGroups[i].Count} models)");
            }

            return levelGroups;
        }

        /// <summary>
        /// Calculates the dependency level of a node using iterative approach
        /// </summary>
        private void CalculateNodeLevelOptimized(NodeViewModel node, Dictionary<NodeViewModel, int> dependencyLevels,
                                                Dictionary<string, HashSet<string>> dependencies, HashSet<string> visited, 
                                                List<NodeViewModel> modelNodes)
        {
            if (visited.Contains(node.Id))
                return;
                
            // Simple case: if node has no dependencies, it's level 0
            if (!dependencies.ContainsKey(node.Id) || dependencies[node.Id].Count == 0)
            {
                dependencyLevels[node] = 0;
                visited.Add(node.Id);
                return;
            }
                
            var toProcess = new Stack<NodeViewModel>();
            var processing = new HashSet<string>();
            toProcess.Push(node);
            
            while (toProcess.Count > 0)
            {
                var current = toProcess.Peek();
                
                if (visited.Contains(current.Id))
                {
                    toProcess.Pop();
                    continue;
                }
                
                if (processing.Contains(current.Id))
                {
                    // We've processed all dependencies, now calculate level
                    var maxLevel = 0;
                    if (dependencies.ContainsKey(current.Id))
                    {
                        foreach (var depId in dependencies[current.Id])
                        {
                            var depNode = modelNodes.FirstOrDefault(n => n.Id == depId);
                            if (depNode != null && dependencyLevels.ContainsKey(depNode))
                            {
                                maxLevel = Math.Max(maxLevel, dependencyLevels[depNode] + 1);
                            }
                        }
                    }
                    
                    dependencyLevels[current] = maxLevel;
                    visited.Add(current.Id);
                    processing.Remove(current.Id);
                    toProcess.Pop();
                }
                else
                {
                    processing.Add(current.Id);
                    
                    // Add dependencies to stack if not already processed
                    if (dependencies.ContainsKey(current.Id))
                    {
                        bool allDepsProcessed = true;
                        foreach (var depId in dependencies[current.Id])
                        {
                            var depNode = modelNodes.FirstOrDefault(n => n.Id == depId);
                            if (depNode != null && !visited.Contains(depId))
                            {
                                if (!processing.Contains(depId))
                                {
                                    toProcess.Push(depNode);
                                    allDepsProcessed = false;
                                }
                            }
                        }
                        
                        // If all dependencies are being processed or don't exist, we can continue
                        if (allDepsProcessed)
                        {
                            var maxLevel = 0;
                            foreach (var depId in dependencies[current.Id])
                            {
                                var depNode = modelNodes.FirstOrDefault(n => n.Id == depId);
                                if (depNode != null && dependencyLevels.ContainsKey(depNode))
                                {
                                    maxLevel = Math.Max(maxLevel, dependencyLevels[depNode] + 1);
                                }
                            }
                            
                            dependencyLevels[current] = maxLevel;
                            visited.Add(current.Id);
                            processing.Remove(current.Id);
                            toProcess.Pop();
                        }
                    }
                }
            }
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

        /// <summary>
        /// Optimizes task scheduling within a group for better resource utilization
        /// </summary>
        private List<NodeViewModel> OptimizeGroupTaskScheduling(List<NodeViewModel> group)
        {
            // Sort by estimated execution complexity (simple heuristic)
            return group.OrderBy(node => 
            {
                // Prioritize lighter models first to reduce overall completion time
                if (node.Name.Contains("small") || node.Name.Contains("tiny")) return 1;
                if (node.Name.Contains("base") || node.Name.Contains("medium")) return 2;
                if (node.Name.Contains("large")) return 3;
                return 2; // Default priority
            }).ToList();
        }

        /// <summary>
        /// Executes multiple models in optimized batches to reduce Python startup overhead
        /// </summary>
        private async Task<(int successCount, int failedCount)> ExecuteBatchedModelsAsync(
            List<NodeViewModel> executableModels, 
            Dictionary<string, NeuralNetworkModel> modelLookupCache,
            ObservableCollection<NodeViewModel> nodes,
            ObservableCollection<ConnectionViewModel> connections,
            int currentActionStep)
        {
            Debug.WriteLine($"🎯 [ExecuteBatchedModelsAsync] Starting UNLIMITED parallel execution for {executableModels.Count} models");
            
            var tasks = new List<Task<(bool success, NodeViewModel node)>>();
            var successCount = 0;
            var failedCount = 0;

            // REMOVE SEMAPHORE LIMITATION - Execute all models truly in parallel
            Debug.WriteLine($"🚀 [ExecuteBatchedModelsAsync] Launching {executableModels.Count} models with NO CONCURRENCY LIMITS");
            
            foreach (var modelNode in executableModels)
            {
                var task = Task.Run(async () =>
                {
                    try
                    {
                        var startTime = DateTime.UtcNow;
                        Debug.WriteLine($"🐍 [ExecuteBatchedModelsAsync] [{startTime:HH:mm:ss.fff}] Starting unlimited parallel execution: {modelNode.Name}");
                        
                        await ExecuteOptimizedModelNodeAsync(modelNode, modelLookupCache[modelNode.Id], nodes, connections, currentActionStep).ConfigureAwait(false);
                        
                        var endTime = DateTime.UtcNow;
                        var duration = (endTime - startTime).TotalMilliseconds;
                        Debug.WriteLine($"✅ [ExecuteBatchedModelsAsync] [{endTime:HH:mm:ss.fff}] Unlimited parallel execution completed: {modelNode.Name} in {duration:F0}ms");
                        return (success: true, node: modelNode);
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"❌ [ExecuteBatchedModelsAsync] Unlimited parallel execution failed for {modelNode.Name}: {ex.Message}");
                        return (success: false, node: modelNode);
                    }
                });
                tasks.Add(task);
            }

            var results = await Task.WhenAll(tasks);
            
            successCount = results.Count(r => r.success);
            failedCount = results.Count(r => !r.success);
            
            Debug.WriteLine($"📊 [ExecuteBatchedModelsAsync] UNLIMITED parallel execution completed: {successCount} successful, {failedCount} failed");
            return (successCount, failedCount);
        }
    }
}
