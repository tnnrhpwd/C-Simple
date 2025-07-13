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
{        /// <summary>
        /// Service for handling pipeline execution, including "Run All Models" functionality
        /// with dependency resolution, topological sorting, and parallel execution
        /// </summary>
        public class PipelineExecutionService
        {
            private readonly EnsembleModelService _ensembleModelService;
            private readonly Func<NodeViewModel, NeuralNetworkModel> _findCorrespondingModelFunc;
            
            // Performance optimization caches
            private readonly Dictionary<string, NodeViewModel> _nodeCache = new Dictionary<string, NodeViewModel>();
            private readonly Dictionary<string, List<string>> _dependencyCache = new Dictionary<string, List<string>>();

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
                // Clear caches for fresh execution
                _nodeCache.Clear();
                _dependencyCache.Clear();

                // Step 1: Get all model nodes and build node cache
                var step1Stopwatch = Stopwatch.StartNew();
                var modelNodes = nodes.Where(n => n.Type == NodeType.Model).ToList();
                
                // Build node lookup cache once for the entire execution
                foreach (var node in nodes)
                {
                    _nodeCache[node.Id] = node;
                }
                
                step1Stopwatch.Stop();
                Debug.WriteLine($"⏱️ [Timing] Step 1 - Get model nodes and cache: {step1Stopwatch.ElapsedMilliseconds}ms");

                if (modelNodes.Count == 0)
                {
                    if (showAlert != null)
                        await showAlert("Info", "No model nodes found in the pipeline.", "OK");
                    return (0, 0);
                }

                Debug.WriteLine($"📊 [PipelineExecutionService] Found {modelNodes.Count} model nodes to process");

                // Step 2: Pre-cache model lookups and validate availability
                var step2Stopwatch = Stopwatch.StartNew();
                var modelLookupCache = new Dictionary<string, NeuralNetworkModel>();
                var availableModelNodes = new List<NodeViewModel>();
                
                foreach (var modelNode in modelNodes)
                {
                    var correspondingModel = _findCorrespondingModelFunc(modelNode);
                    if (correspondingModel != null)
                    {
                        modelLookupCache[modelNode.Id] = correspondingModel;
                        availableModelNodes.Add(modelNode);
                    }
                }
                step2Stopwatch.Stop();
                Debug.WriteLine($"⏱️ [Timing] Step 2 - Build model lookup cache: {step2Stopwatch.ElapsedMilliseconds}ms");

                // Step 3: Build execution groups based on dependencies (use availableModelNodes only)
                var step3Stopwatch = Stopwatch.StartNew();
                var executionGroups = BuildOptimizedExecutionGroups(availableModelNodes, connections);
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

                    // Create tasks for TRUE parallel execution
                    var executableModels = group.Where(modelNode => CanExecuteModelNode(modelNode, connections) && modelLookupCache.ContainsKey(modelNode.Id)).ToList();
                    
                    if (executableModels.Count == 0)
                    {
                        skippedCount += group.Count;
                        continue;
                    }
                    
                    Debug.WriteLine($"� [PipelineExecutionService] Starting TRUE parallel execution for {executableModels.Count} models");
                    
                    // Execute all models in the group truly in parallel using Task.WhenAll
                    var parallelTasks = executableModels.Select(async modelNode =>
                    {
                        try
                        {
                            var correspondingModel = modelLookupCache[modelNode.Id];
                            await ExecuteOptimizedModelNodeAsync(modelNode, correspondingModel, nodes, connections, currentActionStep);
                            return (success: true, modelNode);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"❌ [PipelineExecutionService] Model '{modelNode.Name}' failed: {ex.Message}");
                            return (success: false, modelNode);
                        }
                    });
                    
                    var results = await Task.WhenAll(parallelTasks);
                    var batchSuccessCount = results.Count(r => r.success);
                    var batchFailedCount = results.Count(r => !r.success);

                    groupStopwatch.Stop();
                    
                    // Simplified logging for performance
                    Debug.WriteLine($"✅ [PipelineExecutionService] Group {groupIndex + 1} completed: {batchSuccessCount} successful, {batchFailedCount} failed in {groupStopwatch.ElapsedMilliseconds}ms");

                    successCount += batchSuccessCount;
                    skippedCount += group.Count - batchSuccessCount - batchFailedCount;
                    groupIndex++;
                }
                step4Stopwatch.Stop();
                
                totalStopwatch.Stop();
                Debug.WriteLine($"🎉 [PipelineExecutionService] Pipeline completed: {successCount} successful, {skippedCount} skipped in {totalStopwatch.ElapsedMilliseconds}ms");
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
            // Quick check: if no connections at all, run everything in parallel
            if (connections.Count == 0)
            {
                return new List<List<NodeViewModel>> { modelNodes };
            }
            
            // Only consider ACTUAL model-to-model dependencies - use cached lookups
            var actualModelConnections = new List<ConnectionViewModel>();
            var modelNodeIds = new HashSet<string>(modelNodes.Select(m => m.Id));
            
            foreach (var c in connections)
            {
                if (modelNodeIds.Contains(c.SourceNodeId) && modelNodeIds.Contains(c.TargetNodeId))
                {
                    actualModelConnections.Add(c);
                }
            }
            
            // If no model-to-model dependencies, run all in parallel
            if (actualModelConnections.Count == 0)
            {
                Debug.WriteLine($"🚀 [BuildOptimizedExecutionGroups] No model dependencies - all {modelNodes.Count} models in parallel");
                return new List<List<NodeViewModel>> { modelNodes };
            }
            
            // Build dependency graph only when needed
            var dependencyLevels = new Dictionary<NodeViewModel, int>();
            var dependencies = new Dictionary<string, HashSet<string>>();
            
            // Initialize all nodes
            foreach (var node in modelNodes)
            {
                dependencies[node.Id] = new HashSet<string>();
            }
            
            // Build dependencies from actual model connections
            foreach (var connection in actualModelConnections)
            {
                dependencies[connection.TargetNodeId].Add(connection.SourceNodeId);
            }
            
            // Calculate levels
            var visited = new HashSet<string>();
            var modelNodeLookup = modelNodes.ToDictionary(n => n.Id, n => n);
            
            foreach (var node in modelNodes)
            {
                if (!visited.Contains(node.Id))
                {
                    CalculateNodeLevelOptimized(node, dependencyLevels, dependencies, visited, modelNodeLookup);
                }
            }

            // Group by level
            var levelGroups = dependencyLevels.GroupBy(kvp => kvp.Value)
                                              .OrderBy(g => g.Key)
                                              .Select(g => g.Select(kvp => kvp.Key).ToList())
                                              .ToList();
            
            Debug.WriteLine($"📊 [BuildOptimizedExecutionGroups] Created {levelGroups.Count} execution groups with dependencies");
            return levelGroups;
        }

        /// <summary>
        /// Calculates the dependency level of a node using iterative approach
        /// </summary>
        private void CalculateNodeLevelOptimized(NodeViewModel node, Dictionary<NodeViewModel, int> dependencyLevels,
                                                Dictionary<string, HashSet<string>> dependencies, HashSet<string> visited, 
                                                Dictionary<string, NodeViewModel> modelNodeLookup)
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
                            // Use cached node lookup instead of FirstOrDefault
                            if (modelNodeLookup.TryGetValue(depId, out var depNode) && dependencyLevels.ContainsKey(depNode))
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
                            if (modelNodeLookup.TryGetValue(depId, out var depNode) && !visited.Contains(depId))
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
                                if (modelNodeLookup.TryGetValue(depId, out var depNode) && dependencyLevels.ContainsKey(depNode))
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
            // Quick check - only models can be executed
            if (modelNode.Type != NodeType.Model)
                return false;

            // For ensemble models, need at least 2 inputs
            if (modelNode.EnsembleInputCount > 1)
            {
                var connectedInputCount = connections.Count(c => c.TargetNodeId == modelNode.Id);
                return connectedInputCount >= 2;
            }

            // Regular models can always be executed
            return true;
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
