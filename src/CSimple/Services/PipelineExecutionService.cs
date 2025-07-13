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
                  // Ultra-performance optimization: Connection pool for reused model instances
        private readonly Dictionary<string, Task> _modelWarmupTasks = new Dictionary<string, Task>();
        
        // Pre-computed execution metadata cache
        private readonly Dictionary<string, List<NodeViewModel>> _inputNodeCache = new Dictionary<string, List<NodeViewModel>>();
        private readonly Dictionary<string, string> _preparedInputCache = new Dictionary<string, string>();
        private bool _executionCacheValid = false;

        public PipelineExecutionService(EnsembleModelService ensembleModelService, Func<NodeViewModel, NeuralNetworkModel> findCorrespondingModelFunc)
        {
            _ensembleModelService = ensembleModelService ?? throw new ArgumentNullException(nameof(ensembleModelService));
            _findCorrespondingModelFunc = findCorrespondingModelFunc ?? throw new ArgumentNullException(nameof(findCorrespondingModelFunc));
        }

        /// <summary>
        /// Executes all model nodes in the pipeline with ultra-optimized performance
        /// </summary>
        public async Task<(int successCount, int skippedCount)> ExecuteAllModelsAsync(
            ObservableCollection<NodeViewModel> nodes,
            ObservableCollection<ConnectionViewModel> connections,
            int currentActionStep,
            Func<string, string, string, Task> showAlert = null)
        {
            var totalStopwatch = Stopwatch.StartNew();

            try
            {
                // Ultra-fast setup with improved caching
                _nodeCache.Clear();
                _ensembleModelService.ClearStepContentCache();
                
                // Clear execution-specific caches
                _inputNodeCache.Clear();
                _preparedInputCache.Clear();
                _executionCacheValid = false;

                // Pre-allocate all collections for better memory performance
                var modelNodes = new List<NodeViewModel>(nodes.Count);
                var modelLookupCache = new Dictionary<string, NeuralNetworkModel>(nodes.Count);
                var availableModelNodes = new List<NodeViewModel>(nodes.Count);
                
                // Single-pass processing for maximum efficiency
                foreach (var node in nodes)
                {
                    _nodeCache[node.Id] = node;
                    if (node.Type == NodeType.Model)
                    {
                        modelNodes.Add(node);
                        
                        // Immediately resolve model to avoid double lookup
                        var correspondingModel = _findCorrespondingModelFunc(node);
                        if (correspondingModel != null)
                        {
                            modelLookupCache[node.Id] = correspondingModel;
                            availableModelNodes.Add(node);
                        }
                    }
                }

                if (availableModelNodes.Count == 0)
                {
                    if (showAlert != null)
                        await showAlert("Info", "No executable model nodes found in the pipeline.", "OK");
                    return (0, 0);
                }

                // Pre-compute input relationships for all models at once
                PrecomputeInputRelationships(availableModelNodes, nodes, connections, currentActionStep);

                // Fast execution grouping with aggressive parallelization
                var executionGroups = BuildUltraOptimizedExecutionGroups(availableModelNodes, connections);

                int successCount = 0;
                int skippedCount = 0;

                // Hyper-optimized execution with minimal overhead
                foreach (var group in executionGroups)
                {
                    // Pre-filter executable models with cached connection counts
                    var executableModels = new List<NodeViewModel>(group.Count);
                    foreach (var modelNode in group)
                    {
                        if (modelLookupCache.ContainsKey(modelNode.Id) && 
                            CanExecuteModelNodeFast(modelNode, connections))
                        {
                            executableModels.Add(modelNode);
                        }
                    }
                    
                    if (executableModels.Count == 0)
                    {
                        skippedCount += group.Count;
                        continue;
                    }
                    
                    // Maximum parallelism execution with pre-computed inputs
                    var parallelTasks = executableModels.Select(async modelNode =>
                    {
                        var correspondingModel = modelLookupCache[modelNode.Id];
                        try
                        {
                            await ExecuteUltraOptimizedModelNodeAsync(modelNode, correspondingModel, nodes, connections, currentActionStep).ConfigureAwait(false);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    });
                    
                    // Execute all models in the group with maximum concurrency
                    var results = await Task.WhenAll(parallelTasks).ConfigureAwait(false);
                    
                    // Ultra-fast result counting
                    var batchSuccessCount = results.Count(success => success);
                    successCount += batchSuccessCount;
                    skippedCount += executableModels.Count - batchSuccessCount;
                }
                
                totalStopwatch.Stop();
                Debug.WriteLine($"🎉 [PipelineExecutionService] Completed: {successCount} successful, {skippedCount} skipped in {totalStopwatch.ElapsedMilliseconds}ms");

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
            
            // Pre-build model node ID hashset for faster lookups
            var modelNodeIds = new HashSet<string>(modelNodes.Count);
            foreach (var node in modelNodes)
            {
                modelNodeIds.Add(node.Id);
            }
            
            // Only consider ACTUAL model-to-model dependencies - use optimized filtering
            var actualModelConnections = new List<ConnectionViewModel>();
            foreach (var c in connections)
            {
                if (modelNodeIds.Contains(c.SourceNodeId) && modelNodeIds.Contains(c.TargetNodeId))
                {
                    actualModelConnections.Add(c);
                }
            }
            
            // If no model-to-model dependencies, run all in parallel (most common case)
            if (actualModelConnections.Count == 0)
            {
                return new List<List<NodeViewModel>> { modelNodes };
            }
            
            // Build dependency graph efficiently
            var dependencyLevels = new Dictionary<NodeViewModel, int>(modelNodes.Count);
            var dependencies = new Dictionary<string, HashSet<string>>(modelNodes.Count);
            
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
            
            // Calculate levels efficiently
            var visited = new HashSet<string>(modelNodes.Count);
            var modelNodeLookup = new Dictionary<string, NodeViewModel>(modelNodes.Count);
            foreach (var node in modelNodes)
            {
                modelNodeLookup[node.Id] = node;
            }
            
            foreach (var node in modelNodes)
            {
                if (!visited.Contains(node.Id))
                {
                    CalculateNodeLevelOptimized(node, dependencyLevels, dependencies, visited, modelNodeLookup);
                }
            }

            // Group by level
            var levelGroups = new List<List<NodeViewModel>>();
            var levelDict = new Dictionary<int, List<NodeViewModel>>();
            
            foreach (var kvp in dependencyLevels)
            {
                var level = kvp.Value;
                if (!levelDict.ContainsKey(level))
                {
                    levelDict[level] = new List<NodeViewModel>();
                }
                levelDict[level].Add(kvp.Key);
            }
            
            // Sort levels and create final groups
            var sortedLevels = levelDict.Keys.OrderBy(k => k);
            foreach (var level in sortedLevels)
            {
                levelGroups.Add(levelDict[level]);
            }
            
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
        private bool CanExecuteModelNode(NodeViewModel modelNode, ObservableCollection<ConnectionViewModel> connections, Dictionary<string, int> connectionCountCache = null)
        {
            // Quick check - only models can be executed
            if (modelNode.Type != NodeType.Model)
                return false;

            // For ensemble models, need at least 2 inputs
            if (modelNode.EnsembleInputCount > 1)
            {
                int connectedInputCount;
                if (connectionCountCache != null && connectionCountCache.TryGetValue(modelNode.Id, out connectedInputCount))
                {
                    return connectedInputCount >= 2;
                }
                
                // Fallback: count connections manually (slower path)
                connectedInputCount = 0;
                foreach (var c in connections)
                {
                    if (c.TargetNodeId == modelNode.Id)
                        connectedInputCount++;
                }
                connectionCountCache?.TryAdd(modelNode.Id, connectedInputCount);
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

        /// <summary>
        /// Pre-computes input relationships and prepared inputs for all models to avoid repeated work
        /// </summary>
        private void PrecomputeInputRelationships(List<NodeViewModel> modelNodes, ObservableCollection<NodeViewModel> nodes, 
            ObservableCollection<ConnectionViewModel> connections, int currentActionStep)
        {
            foreach (var modelNode in modelNodes)
            {
                // Cache input nodes
                var inputNodes = _ensembleModelService.GetConnectedInputNodes(modelNode, nodes, connections);
                _inputNodeCache[modelNode.Id] = inputNodes;
                
                // Pre-compute input if possible (for models without dependencies)
                if (inputNodes.All(n => n.Type == NodeType.Input))
                {
                    var preparedInput = _ensembleModelService.PrepareModelInput(modelNode, inputNodes, currentActionStep);
                    _preparedInputCache[modelNode.Id] = preparedInput;
                }
            }
            _executionCacheValid = true;
        }

        /// <summary>
        /// Ultra-optimized execution groups with more aggressive parallelization
        /// </summary>
        private List<List<NodeViewModel>> BuildUltraOptimizedExecutionGroups(List<NodeViewModel> modelNodes, ObservableCollection<ConnectionViewModel> connections)
        {
            // Quick check: if no connections at all, run everything in parallel
            if (connections.Count == 0)
            {
                Debug.WriteLine("📊 [BuildUltraOptimizedExecutionGroups] No dependencies found, executing all models in parallel");
                return new List<List<NodeViewModel>> { modelNodes };
            }
            
            // Build model node ID hashset for faster lookups
            var modelNodeIds = new HashSet<string>(modelNodes.Count);
            foreach (var node in modelNodes)
            {
                modelNodeIds.Add(node.Id);
            }
            
            // Only consider ACTUAL model-to-model dependencies
            var modelToModelDeps = new HashSet<string>();
            foreach (var c in connections)
            {
                if (modelNodeIds.Contains(c.SourceNodeId) && modelNodeIds.Contains(c.TargetNodeId))
                {
                    modelToModelDeps.Add($"{c.SourceNodeId}->{c.TargetNodeId}");
                }
            }
            
            // If no model-to-model dependencies, run all in parallel
            if (modelToModelDeps.Count == 0)
            {
                Debug.WriteLine("📊 [BuildUltraOptimizedExecutionGroups] No model-to-model dependencies, executing all models in parallel");
                return new List<List<NodeViewModel>> { modelNodes };
            }
            
            // For now, use simpler grouping - models with only input dependencies first, then dependent models
            var independentModels = new List<NodeViewModel>();
            var dependentModels = new List<NodeViewModel>();
            
            foreach (var modelNode in modelNodes)
            {
                var hasModelDependency = false;
                foreach (var dep in modelToModelDeps)
                {
                    if (dep.EndsWith($"->{modelNode.Id}"))
                    {
                        hasModelDependency = true;
                        break;
                    }
                }
                
                if (hasModelDependency)
                    dependentModels.Add(modelNode);
                else
                    independentModels.Add(modelNode);
            }
            
            var groups = new List<List<NodeViewModel>>();
            if (independentModels.Count > 0)
                groups.Add(independentModels);
            if (dependentModels.Count > 0)
                groups.Add(dependentModels);
            
            Debug.WriteLine($"📊 [BuildUltraOptimizedExecutionGroups] Created {groups.Count} groups: {independentModels.Count} independent, {dependentModels.Count} dependent");
            return groups;
        }

        /// <summary>
        /// Fast execution check with minimal overhead
        /// </summary>
        private bool CanExecuteModelNodeFast(NodeViewModel modelNode, ObservableCollection<ConnectionViewModel> connections)
        {
            if (modelNode.Type != NodeType.Model)
                return false;

            // For ensemble models, need at least 2 inputs
            if (modelNode.EnsembleInputCount > 1)
            {
                int inputCount = 0;
                foreach (var c in connections)
                {
                    if (c.TargetNodeId == modelNode.Id)
                    {
                        inputCount++;
                        if (inputCount >= 2) return true; // Early exit
                    }
                }
                return false;
            }
            
            return true;
        }

        /// <summary>
        /// Ultra-optimized model execution with pre-computed inputs
        /// </summary>
        private async Task ExecuteUltraOptimizedModelNodeAsync(NodeViewModel modelNode, NeuralNetworkModel correspondingModel,
            ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections, int currentActionStep)
        {
            try
            {
                // Use pre-computed input if available
                string input;
                if (_preparedInputCache.TryGetValue(modelNode.Id, out input))
                {
                    // Use cached input
                }
                else if (_inputNodeCache.TryGetValue(modelNode.Id, out var cachedInputNodes))
                {
                    // Use cached input nodes to prepare input
                    input = _ensembleModelService.PrepareModelInput(modelNode, cachedInputNodes, currentActionStep);
                }
                else
                {
                    // Fallback to normal execution
                    await _ensembleModelService.ExecuteSingleModelNodeAsync(modelNode, correspondingModel, nodes, connections, currentActionStep);
                    return;
                }

                // Direct model execution without redundant input preparation
                string result = await _ensembleModelService.ExecuteModelWithInput(correspondingModel, input).ConfigureAwait(false);
                
                // Fast result storage
                string resultContentType = _ensembleModelService.DetermineResultContentType(correspondingModel, result);
                int currentStep = currentActionStep + 1;
                modelNode.SetStepOutput(currentStep, resultContentType, result);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [ExecuteUltraOptimizedModelNodeAsync] Error executing model {modelNode.Name}: {ex.Message}");
                throw;
            }
        }

    }
}
