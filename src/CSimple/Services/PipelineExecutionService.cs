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
                var connectionCountCache = new Dictionary<string, int>(nodes.Count);
                
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

                // Pre-compute connection counts for fast execution validation
                foreach (var connection in connections)
                {
                    if (connectionCountCache.ContainsKey(connection.TargetNodeId))
                        connectionCountCache[connection.TargetNodeId]++;
                    else
                        connectionCountCache[connection.TargetNodeId] = 1;
                }

                if (availableModelNodes.Count == 0)
                {
                    if (showAlert != null)
                        await showAlert("Info", "No executable model nodes found in the pipeline.", "OK");
                    return (0, 0);
                }

                // Pre-compute input relationships for all models at once - with aggressive caching
                PrecomputeInputRelationshipsOptimized(availableModelNodes, nodes, connections, currentActionStep, connectionCountCache);

                // Fast execution grouping with aggressive parallelization
                var executionGroups = BuildHyperOptimizedExecutionGroups(availableModelNodes, connections);

                int successCount = 0;
                int skippedCount = 0;

                // Ultra-optimized execution with aggressive parallelism and pre-computation
                var maxConcurrency = Math.Max(Environment.ProcessorCount * 2, availableModelNodes.Count);
                using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

                foreach (var group in executionGroups)
                {
                    // Pre-filter and batch executable models with single-pass validation
                    var executableBatch = new List<(NodeViewModel node, NeuralNetworkModel model)>(group.Count);
                    foreach (var modelNode in group)
                    {
                        if (modelLookupCache.TryGetValue(modelNode.Id, out var model) && 
                            CanExecuteModelNodeHyperFast(modelNode, connectionCountCache))
                        {
                            executableBatch.Add((modelNode, model));
                        }
                    }
                    
                    if (executableBatch.Count == 0)
                    {
                        skippedCount += group.Count;
                        continue;
                    }
                    
                    // Create all tasks upfront for better memory allocation
                    var executionTasks = new Task<bool>[executableBatch.Count];
                    for (int i = 0; i < executableBatch.Count; i++)
                    {
                        var (modelNode, correspondingModel) = executableBatch[i];
                        executionTasks[i] = ExecuteModelWithThrottlingAsync(modelNode, correspondingModel, nodes, connections, currentActionStep, semaphore);
                    }
                    
                    // Execute all models with maximum concurrency
                    var results = await Task.WhenAll(executionTasks).ConfigureAwait(false);
                    
                    // Ultra-fast result aggregation with single pass
                    int batchSuccessCount = 0;
                    for (int i = 0; i < results.Length; i++)
                    {
                        if (results[i]) batchSuccessCount++;
                    }
                    
                    successCount += batchSuccessCount;
                    skippedCount += executableBatch.Count - batchSuccessCount;

                    // Minimal cache update for dependent groups only
                    if (executionGroups.Count > 1 && batchSuccessCount > 0)
                    {
                        UpdateCacheAfterExecution(executableBatch.Take(batchSuccessCount).Select(x => x.node).ToList(), currentActionStep);
                    }
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
        /// Pre-computes input relationships and prepared inputs with aggressive caching
        /// </summary>
        private void PrecomputeInputRelationshipsOptimized(List<NodeViewModel> modelNodes, ObservableCollection<NodeViewModel> nodes, 
            ObservableCollection<ConnectionViewModel> connections, int currentActionStep, Dictionary<string, int> connectionCountCache)
        {
            // Build a fast lookup for input-to-model connections
            var inputConnections = new Dictionary<string, List<NodeViewModel>>();
            
            foreach (var connection in connections)
            {
                if (!inputConnections.ContainsKey(connection.TargetNodeId))
                    inputConnections[connection.TargetNodeId] = new List<NodeViewModel>();
                
                var sourceNode = _nodeCache.GetValueOrDefault(connection.SourceNodeId);
                if (sourceNode != null)
                    inputConnections[connection.TargetNodeId].Add(sourceNode);
            }

            foreach (var modelNode in modelNodes)
            {
                // Cache input nodes using pre-built lookup
                var inputNodes = inputConnections.GetValueOrDefault(modelNode.Id) ?? new List<NodeViewModel>();
                _inputNodeCache[modelNode.Id] = inputNodes;
                
                // Pre-compute input for models that only depend on Input nodes (most common case)
                if (inputNodes.Count > 0 && inputNodes.All(n => n.Type == NodeType.Input))
                {
                    try
                    {
                        var preparedInput = _ensembleModelService.PrepareModelInput(modelNode, inputNodes, currentActionStep);
                        _preparedInputCache[modelNode.Id] = preparedInput;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"⚠️ [PrecomputeInputRelationshipsOptimized] Failed to pre-compute input for {modelNode.Name}: {ex.Message}");
                    }
                }
            }
            _executionCacheValid = true;
        }

        /// <summary>
        /// Hyper-optimized execution groups with smarter dependency analysis
        /// </summary>
        private List<List<NodeViewModel>> BuildHyperOptimizedExecutionGroups(List<NodeViewModel> modelNodes, ObservableCollection<ConnectionViewModel> connections)
        {
            // Quick check: if no connections at all, run everything in parallel
            if (connections.Count == 0)
            {
                Debug.WriteLine("📊 [BuildHyperOptimizedExecutionGroups] No dependencies found, executing all models in parallel");
                return new List<List<NodeViewModel>> { modelNodes };
            }
            
            // Build model node ID hashset for faster lookups
            var modelNodeIds = new HashSet<string>(modelNodes.Count);
            foreach (var node in modelNodes)
            {
                modelNodeIds.Add(node.Id);
            }
            
            // Analyze model-to-model dependencies more efficiently
            var modelDependencies = new Dictionary<string, HashSet<string>>();
            var hasModelDependencies = false;
            
            foreach (var connection in connections)
            {
                if (modelNodeIds.Contains(connection.SourceNodeId) && modelNodeIds.Contains(connection.TargetNodeId))
                {
                    hasModelDependencies = true;
                    if (!modelDependencies.ContainsKey(connection.TargetNodeId))
                        modelDependencies[connection.TargetNodeId] = new HashSet<string>();
                    modelDependencies[connection.TargetNodeId].Add(connection.SourceNodeId);
                }
            }
            
            // If no model-to-model dependencies, run all in parallel
            if (!hasModelDependencies)
            {
                Debug.WriteLine("📊 [BuildHyperOptimizedExecutionGroups] No model-to-model dependencies, executing all models in parallel");
                return new List<List<NodeViewModel>> { modelNodes };
            }
            
            // Simple two-level grouping for maximum parallelism
            var independentModels = new List<NodeViewModel>();
            var dependentModels = new List<NodeViewModel>();
            
            foreach (var modelNode in modelNodes)
            {
                if (modelDependencies.ContainsKey(modelNode.Id))
                    dependentModels.Add(modelNode);
                else
                    independentModels.Add(modelNode);
            }
            
            var groups = new List<List<NodeViewModel>>();
            if (independentModels.Count > 0)
                groups.Add(independentModels);
            if (dependentModels.Count > 0)
                groups.Add(dependentModels);
            
            Debug.WriteLine($"📊 [BuildHyperOptimizedExecutionGroups] Created {groups.Count} groups: {independentModels.Count} independent, {dependentModels.Count} dependent");
            return groups;
        }

        /// <summary>
        /// Hyper-fast execution check with cached connection counts
        /// </summary>
        private bool CanExecuteModelNodeHyperFast(NodeViewModel modelNode, Dictionary<string, int> connectionCountCache)
        {
            if (modelNode.Type != NodeType.Model)
                return false;

            // For ensemble models, need at least 2 inputs
            if (modelNode.EnsembleInputCount > 1)
            {
                return connectionCountCache.GetValueOrDefault(modelNode.Id, 0) >= 2;
            }
            
            return true;
        }

        /// <summary>
        /// Hyper-optimized model execution with maximum caching and minimal overhead
        /// </summary>
        private async Task ExecuteHyperOptimizedModelNodeAsync(NodeViewModel modelNode, NeuralNetworkModel correspondingModel,
            ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections, int currentActionStep)
        {
            try
            {
                // Try to use pre-computed input first (fastest path)
                if (_preparedInputCache.TryGetValue(modelNode.Id, out var cachedInput))
                {
                    // Direct model execution with cached input
                    string result = await _ensembleModelService.ExecuteModelWithInput(correspondingModel, cachedInput).ConfigureAwait(false);
                    
                    // Fast result storage
                    string resultContentType = _ensembleModelService.DetermineResultContentType(correspondingModel, result);
                    int currentStep = currentActionStep + 1;
                    modelNode.SetStepOutput(currentStep, resultContentType, result);
                    return;
                }

                // Use cached input nodes if available (medium speed path)
                if (_inputNodeCache.TryGetValue(modelNode.Id, out var cachedInputNodes))
                {
                    var input = _ensembleModelService.PrepareModelInput(modelNode, cachedInputNodes, currentActionStep);
                    string result = await _ensembleModelService.ExecuteModelWithInput(correspondingModel, input).ConfigureAwait(false);
                    
                    string resultContentType = _ensembleModelService.DetermineResultContentType(correspondingModel, result);
                    int currentStep = currentActionStep + 1;
                    modelNode.SetStepOutput(currentStep, resultContentType, result);
                    return;
                }

                // Fallback to normal execution (slowest path)
                await _ensembleModelService.ExecuteSingleModelNodeAsync(modelNode, correspondingModel, nodes, connections, currentActionStep);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [ExecuteHyperOptimizedModelNodeAsync] Error executing model {modelNode.Name}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Updates cache after execution for models that depend on other models
        /// </summary>
        private void UpdateCacheAfterExecution(List<NodeViewModel> executedModels, int currentActionStep)
        {
            foreach (var model in executedModels)
            {
                // Invalidate cached inputs for models that depend on this one
                var dependentModelIds = _inputNodeCache
                    .Where(kvp => kvp.Value.Any(n => n.Id == model.Id))
                    .Select(kvp => kvp.Key)
                    .ToList();

                foreach (var dependentId in dependentModelIds)
                {
                    _preparedInputCache.Remove(dependentId);
                }
            }
        }

    }
}
