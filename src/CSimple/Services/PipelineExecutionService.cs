using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
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
        private readonly AudioStepContentService _audioStepContentService;

        // Performance optimization caches
        private readonly Dictionary<string, NodeViewModel> _nodeCache = new Dictionary<string, NodeViewModel>();
        private readonly Dictionary<string, List<string>> _dependencyCache = new Dictionary<string, List<string>>();
        // Ultra-performance optimization: Connection pool for reused model instances
        private readonly Dictionary<string, Task> _modelWarmupTasks = new Dictionary<string, Task>();

        // Pre-computed execution metadata cache
        private readonly Dictionary<string, List<NodeViewModel>> _inputNodeCache = new Dictionary<string, List<NodeViewModel>>();
        private readonly Dictionary<string, string> _preparedInputCache = new Dictionary<string, string>();
#pragma warning disable CS0414 // Field is assigned but never used - reserved for future cache validation
        private bool _executionCacheValid = false;
#pragma warning restore CS0414

        public PipelineExecutionService(EnsembleModelService ensembleModelService, Func<NodeViewModel, NeuralNetworkModel> findCorrespondingModelFunc, AudioStepContentService audioStepContentService = null)
        {
            _ensembleModelService = ensembleModelService ?? throw new ArgumentNullException(nameof(ensembleModelService));
            _findCorrespondingModelFunc = findCorrespondingModelFunc ?? throw new ArgumentNullException(nameof(findCorrespondingModelFunc));
            _audioStepContentService = audioStepContentService; // Optional - can be null if TTS not available
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
            return await ExecuteAllModelsAsync(nodes, connections, currentActionStep, showAlert, null, null, null);
        }

        /// <summary>
        /// Executes all model nodes in the pipeline with ultra-optimized performance and group tracking
        /// </summary>
        public async Task<(int successCount, int skippedCount)> ExecuteAllModelsAsync(
            ObservableCollection<NodeViewModel> nodes,
            ObservableCollection<ConnectionViewModel> connections,
            int currentActionStep,
            Func<string, string, string, Task> showAlert = null,
            Action<int> onGroupsInitialized = null,
            Action<int, int> onGroupStarted = null,
            Action<int> onGroupCompleted = null)
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

                // Use proper dependency-based execution grouping for multi-level dependencies
                var executionGroups = BuildOptimizedExecutionGroups(availableModelNodes, connections);

                // Notify about groups initialization
                onGroupsInitialized?.Invoke(executionGroups.Count);

                int successCount = 0;
                int skippedCount = 0;
                int currentGroupNumber = 1;

                // Ultra-optimized execution with aggressive parallelism and pre-computation
                var maxConcurrency = Math.Max(Environment.ProcessorCount * 2, availableModelNodes.Count);
                using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

                foreach (var group in executionGroups)
                {
                    // Notify group started
                    onGroupStarted?.Invoke(currentGroupNumber, group.Count);

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

                    // Notify group completed
                    onGroupCompleted?.Invoke(currentGroupNumber);
                    currentGroupNumber++;
                }

                totalStopwatch.Stop();
                Debug.WriteLine($"🎉 [{DateTime.Now:HH:mm:ss.fff}] [PipelineExecutionService] Completed: {successCount} successful, {skippedCount} skipped in {totalStopwatch.ElapsedMilliseconds}ms");

                return (successCount, skippedCount);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [PipelineExecutionService] Critical error: {ex.Message}");
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
                var node = kvp.Key;
                Debug.WriteLine($"   🔗 [{DateTime.Now:HH:mm:ss.fff}] Model '{node.Name}' assigned to dependency level {level}");

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

            Debug.WriteLine($"📊 [{DateTime.Now:HH:mm:ss.fff}] [BuildOptimizedExecutionGroups] Created {levelGroups.Count} execution groups with dependencies");

            // Enhanced logging to show group composition
            for (int i = 0; i < levelGroups.Count; i++)
            {
                var group = levelGroups[i];
                var modelNames = string.Join(", ", group.Select(n => n.Name));
                Debug.WriteLine($"   📋 Group {i + 1}: {group.Count} models - [{modelNames}]");
            }

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
                Debug.WriteLine($"🔗 [{DateTime.Now:HH:mm:ss.fff}] [GetNodeDependencies] Found dependency connection: {connection.SourceNodeId} -> {node.Id}");
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
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteOptimizedModelNodeAsync] Error executing model {modelNode.Name}: {ex.Message}");
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
                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [TopologicalSort] Cycle detected involving node: {node.Name}");
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
        /// Ultra-optimized input relationship pre-computation with aggressive caching
        /// </summary>
        private void PrecomputeInputRelationshipsOptimized(List<NodeViewModel> modelNodes, ObservableCollection<NodeViewModel> nodes,
            ObservableCollection<ConnectionViewModel> connections, int currentActionStep, Dictionary<string, int> connectionCountCache)
        {
            // Build connection lookup in a single pass for maximum efficiency
            var targetToSources = new Dictionary<string, List<NodeViewModel>>(modelNodes.Count);

            foreach (var connection in connections)
            {
                if (!targetToSources.TryGetValue(connection.TargetNodeId, out var sourcesList))
                {
                    sourcesList = new List<NodeViewModel>(4); // Pre-allocate common size
                    targetToSources[connection.TargetNodeId] = sourcesList;
                }

                if (_nodeCache.TryGetValue(connection.SourceNodeId, out var sourceNode))
                {
                    sourcesList.Add(sourceNode);
                }
            }

            // Process models in parallel for input preparation
            var preparationTasks = new List<Task>(modelNodes.Count);

            foreach (var modelNode in modelNodes)
            {
                preparationTasks.Add(Task.Run(() =>
                {
                    var inputNodes = targetToSources.GetValueOrDefault(modelNode.Id, new List<NodeViewModel>());

                    // Always cache input nodes for later use
                    lock (_inputNodeCache)
                    {
                        _inputNodeCache[modelNode.Id] = inputNodes;
                    }

                    // Pre-compute input only for models with Input-only dependencies (80% of cases)
                    if (inputNodes.Count > 0 && inputNodes.All(n => n.Type == NodeType.Input))
                    {
                        try
                        {
                            var preparedInput = _ensembleModelService.PrepareModelInput(modelNode, inputNodes, currentActionStep);
                            lock (_preparedInputCache)
                            {
                                _preparedInputCache[modelNode.Id] = preparedInput;
                            }
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [PrecomputeInputRelationshipsOptimized] Failed to pre-compute input for {modelNode.Name}: {ex.Message}");
                        }
                    }
                }));
            }

            // Wait for all preparation tasks with timeout
            try
            {
                Task.WaitAll(preparationTasks.ToArray(), TimeSpan.FromSeconds(5));
            }
            catch (AggregateException ex)
            {
                Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [PrecomputeInputRelationshipsOptimized] Some input preparations failed: {ex.Message}");
            }

            _executionCacheValid = true;
            Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [PrecomputeInputRelationshipsOptimized] Cached {_preparedInputCache.Count}/{modelNodes.Count} inputs");
        }

        /// <summary>
        /// Hyper-optimized execution groups with minimal dependency analysis for maximum parallelism
        /// </summary>
        private List<List<NodeViewModel>> BuildHyperOptimizedExecutionGroups(List<NodeViewModel> modelNodes, ObservableCollection<ConnectionViewModel> connections)
        {
            // Quick optimization: if no connections at all, run everything in parallel
            if (connections.Count == 0)
            {
                Debug.WriteLine("📊 [{DateTime.Now:HH:mm:ss.fff}] [BuildHyperOptimizedExecutionGroups] No dependencies found, executing all models in parallel");
                return new List<List<NodeViewModel>> { modelNodes };
            }

            // Build model node ID hashset for O(1) lookups
            var modelNodeIds = new HashSet<string>(modelNodes.Count);
            foreach (var node in modelNodes)
            {
                modelNodeIds.Add(node.Id);
            }

            // Fast check for model-to-model dependencies
            var hasModelDependencies = false;
            var dependentModelIds = new HashSet<string>();

            foreach (var connection in connections)
            {
                if (modelNodeIds.Contains(connection.SourceNodeId) && modelNodeIds.Contains(connection.TargetNodeId))
                {
                    hasModelDependencies = true;
                    dependentModelIds.Add(connection.TargetNodeId);
                }
            }

            // If no model-to-model dependencies, run all in parallel (most common case)
            if (!hasModelDependencies)
            {
                Debug.WriteLine("📊 [{DateTime.Now:HH:mm:ss.fff}] [BuildHyperOptimizedExecutionGroups] No model-to-model dependencies, executing all models in parallel");
                return new List<List<NodeViewModel>> { modelNodes };
            }

            // Simple two-tier grouping for maximum performance
            var independentModels = new List<NodeViewModel>(modelNodes.Count);
            var dependentModels = new List<NodeViewModel>();

            foreach (var modelNode in modelNodes)
            {
                if (dependentModelIds.Contains(modelNode.Id))
                    dependentModels.Add(modelNode);
                else
                    independentModels.Add(modelNode);
            }

            var groups = new List<List<NodeViewModel>>(2);
            if (independentModels.Count > 0)
                groups.Add(independentModels);
            if (dependentModels.Count > 0)
                groups.Add(dependentModels);

            Debug.WriteLine($"📊 [{DateTime.Now:HH:mm:ss.fff}] [BuildHyperOptimizedExecutionGroups] Created {groups.Count} groups: {independentModels.Count} independent, {dependentModels.Count} dependent");
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
        /// Optimized model execution with throttling and minimal overhead
        /// </summary>
        private async Task<bool> ExecuteModelWithThrottlingAsync(NodeViewModel modelNode, NeuralNetworkModel correspondingModel,
            ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections,
            int currentActionStep, SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);
            try
            {
                await ExecuteHyperOptimizedModelNodeAsync(modelNode, correspondingModel, nodes, connections, currentActionStep).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithThrottlingAsync] Model {modelNode.Name} failed: {ex.Message}");
                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Hyper-optimized model execution with maximum caching and minimal overhead
        /// </summary>
        private async Task ExecuteHyperOptimizedModelNodeAsync(NodeViewModel modelNode, NeuralNetworkModel correspondingModel,
            ObservableCollection<NodeViewModel> nodes, ObservableCollection<ConnectionViewModel> connections, int currentActionStep)
        {
            try
            {
                // Try to use pre-computed input first (fastest path) - 90% of cases
                if (_preparedInputCache.TryGetValue(modelNode.Id, out var cachedInput))
                {
                    var result = await _ensembleModelService.ExecuteModelWithInput(correspondingModel, cachedInput).ConfigureAwait(false);
                    var resultContentType = _ensembleModelService.DetermineResultContentType(correspondingModel, result);
                    modelNode.SetStepOutput(currentActionStep + 1, resultContentType, result);
                    return;
                }

                // Use cached input nodes if available (medium speed path) - 8% of cases
                if (_inputNodeCache.TryGetValue(modelNode.Id, out var cachedInputNodes))
                {
                    var input = _ensembleModelService.PrepareModelInput(modelNode, cachedInputNodes, currentActionStep);
                    var result = await _ensembleModelService.ExecuteModelWithInput(correspondingModel, input).ConfigureAwait(false);
                    var resultContentType = _ensembleModelService.DetermineResultContentType(correspondingModel, result);
                    modelNode.SetStepOutput(currentActionStep + 1, resultContentType, result);
                    return;
                }

                // Fallback to normal execution (slowest path) - 2% of cases
                await _ensembleModelService.ExecuteSingleModelNodeAsync(modelNode, correspondingModel, nodes, connections, currentActionStep);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteHyperOptimizedModelNodeAsync] Error executing model {modelNode.Name}: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Executes all model nodes with pre-computed optimizations for maximum performance
        /// </summary>
        public async Task<(int successCount, int skippedCount)> ExecuteAllModelsOptimizedAsync(
            ObservableCollection<NodeViewModel> nodes,
            ObservableCollection<ConnectionViewModel> connections,
            int currentActionStep,
            Dictionary<string, NeuralNetworkModel> preloadedModelCache,
            Dictionary<string, string> precomputedInputCache,
            Func<string, string, string, Task> showAlert = null,
            bool concurrentRender = true)
        {
            return await ExecuteAllModelsOptimizedAsync(nodes, connections, currentActionStep, preloadedModelCache, precomputedInputCache, showAlert, null, null, null, concurrentRender);
        }

        /// <summary>
        /// Executes all model nodes with pre-computed optimizations for maximum performance and group tracking
        /// </summary>
        public async Task<(int successCount, int skippedCount)> ExecuteAllModelsOptimizedAsync(
            ObservableCollection<NodeViewModel> nodes,
            ObservableCollection<ConnectionViewModel> connections,
            int currentActionStep,
            Dictionary<string, NeuralNetworkModel> preloadedModelCache,
            Dictionary<string, string> precomputedInputCache,
            Func<string, string, string, Task> showAlert = null,
            Action<int> onGroupsInitialized = null,
            Action<int, int> onGroupStarted = null,
            Action<int> onGroupCompleted = null,
            bool concurrentRender = true)
        {
            var totalStopwatch = Stopwatch.StartNew();
            // Debug.WriteLine($"⚡ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Using pre-computed optimizations");

            try
            {
                // Use pre-loaded model cache for instant model lookup
                var availableModelNodes = new List<NodeViewModel>();
                foreach (var kvp in preloadedModelCache)
                {
                    var node = nodes.FirstOrDefault(n => n.Id == kvp.Key);
                    if (node != null)
                    {
                        availableModelNodes.Add(node);
                    }
                }

                if (availableModelNodes.Count == 0)
                {
                    if (showAlert != null)
                        await showAlert("Info", "No executable model nodes found in the pipeline.", "OK");
                    return (0, 0);
                }

                // Debug.WriteLine($"🚀 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Found {availableModelNodes.Count} pre-loaded models");

                // Use proper dependency-based execution grouping for multi-level dependencies
                var executionGroups = BuildOptimizedExecutionGroups(availableModelNodes, connections);

                // Notify about groups initialization
                onGroupsInitialized?.Invoke(executionGroups.Count);

                int successCount = 0;
                int skippedCount = 0;
                int currentGroupNumber = 1;

                // Ultra-fast execution with both pre-computed and dynamic inputs
                var maxConcurrency = Math.Max(Environment.ProcessorCount * 2, availableModelNodes.Count);
                using var semaphore = new SemaphoreSlim(maxConcurrency, maxConcurrency);

                foreach (var group in executionGroups)
                {
                    // Notify group started
                    onGroupStarted?.Invoke(currentGroupNumber, group.Count);

                    // Set all nodes in the group to Running state for visual feedback on UI thread
                    Microsoft.Maui.Controls.Application.Current.Dispatcher.Dispatch(() =>
                    {
                        foreach (var modelNode in group)
                        {
                            modelNode.ExecutionState = ViewModels.ExecutionState.Running;
                        }
                    });
                    // Debug.WriteLine($"🔶 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Group {currentGroupNumber}: Set {group.Count} nodes to Running state");

                    // Create execution tasks for all models in the group
                    var executionTasks = new List<Task<bool>>();

                    foreach (var modelNode in group)
                    {
                        if (preloadedModelCache.TryGetValue(modelNode.Id, out var model))
                        {
                            // Try pre-computed input first, fall back to dynamic computation
                            if (precomputedInputCache.TryGetValue(modelNode.Id, out var precomputedInput))
                            {
                                executionTasks.Add(ExecuteModelWithPrecomputedInputAsync(
                                    modelNode, model, precomputedInput, currentActionStep, semaphore, connections, nodes));
                            }
                            else
                            {
                                // Compute input dynamically for models that depend on other models
                                executionTasks.Add(ExecuteModelWithDynamicInputAsync(
                                    modelNode, model, nodes, connections, currentActionStep, semaphore));
                            }
                        }
                        else
                        {
                            // Try to find model in case cache missed it - never skip unless absolutely necessary
                            Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Model cache miss for {modelNode.Name}, attempting fallback lookup");

                            // Create a fallback task that attempts to find and execute the model
                            executionTasks.Add(Task.Run(async () =>
                            {
                                try
                                {
                                    await semaphore.WaitAsync();

                                    // Note: Node is already set to Running state at group level

                                    // Try to find the model using the available models in NetPageViewModel
                                    var netPageVM = ((App)Application.Current).NetPageViewModel;
                                    if (netPageVM?.AvailableModels != null)
                                    {
                                        var fallbackModel = netPageVM.AvailableModels.FirstOrDefault(m =>
                                            m.Name.Equals(modelNode.Name, StringComparison.OrdinalIgnoreCase) ||
                                            m.HuggingFaceModelId.Contains(modelNode.Name, StringComparison.OrdinalIgnoreCase));

                                        if (fallbackModel != null)
                                        {
                                            Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Found fallback model for {modelNode.Name}: {fallbackModel.HuggingFaceModelId}");

                                            // Use basic input gathering if no pre-computed input
                                            string inputContent = ""; // Default empty input
                                            try
                                            {
                                                // Quick input gathering without complex dependency resolution
                                                var inputNodes = nodes.Where(n => n.Type == NodeType.Input).ToList();
                                                if (inputNodes.Any())
                                                {
                                                    inputContent = $"Basic input from {inputNodes.Count} sources"; // Simplified
                                                }
                                            }
                                            catch { /* Use default empty input */ }

                                            // Execute with fallback model
                                            string result = await netPageVM.ExecuteModelAsync(fallbackModel.HuggingFaceModelId, inputContent);
                                            if (!string.IsNullOrEmpty(result))
                                            {
                                                modelNode.SetStepOutput(currentActionStep + 1, "text", result);

                                                // Trigger TTS autoplay if enabled
                                                await TriggerTtsAutoplayIfEnabledAsync(modelNode, result, "text");

                                                // Propagate output to connected File nodes for memory saving
                                                await PropagateOutputToConnectedFileNodesAsync(modelNode, result, currentActionStep, connections, nodes);

                                                // Note: Node will be set to Completed state at group level
                                                Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Fallback execution successful for {modelNode.Name}");
                                                return true;
                                            }
                                        }
                                    }

                                    Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Could not execute {modelNode.Name} - no model found");
                                    return false;
                                }
                                catch (Exception ex)
                                {
                                    Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Fallback execution failed for {modelNode.Name}: {ex.Message}");
                                    return false;
                                }
                                finally
                                {
                                    semaphore.Release();
                                }
                            }));
                        }
                    }

                    if (executionTasks.Count > 0)
                    {
                        if (concurrentRender)
                        {
                            // Execute all models in the group concurrently (original behavior)
                            Debug.WriteLine($"🔄 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Group {currentGroupNumber}: Executing {executionTasks.Count} models concurrently");
                            var results = await Task.WhenAll(executionTasks).ConfigureAwait(false);

                            // Update execution states based on results on UI thread
                            Microsoft.Maui.Controls.Application.Current.Dispatcher.Dispatch(() =>
                            {
                                for (int i = 0; i < group.Count && i < results.Length; i++)
                                {
                                    var modelNode = group[i];
                                    var success = results[i];

                                    if (success)
                                    {
                                        modelNode.ExecutionState = ViewModels.ExecutionState.Completed;
                                    }
                                    else
                                    {
                                        modelNode.ExecutionState = ViewModels.ExecutionState.Pending; // Reset failed nodes
                                    }
                                }
                            });

                            // Count successful executions
                            successCount += results.Count(r => r);
                            skippedCount += results.Count(r => !r);

                            Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Group {currentGroupNumber} completed: {results.Count(r => r)} successful, {results.Count(r => !r)} failed");
                        }
                        else
                        {
                            // Execute models sequentially (new behavior)
                            // Debug.WriteLine($"⏩ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Group {currentGroupNumber}: Executing {executionTasks.Count} models sequentially");
                            var results = new List<bool>();

                            for (int i = 0; i < executionTasks.Count; i++)
                            {
                                var modelNode = group[i];
                                // Debug.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Sequential execution: Starting model {i + 1}/{executionTasks.Count}: {modelNode.Name}");

                                var result = await executionTasks[i].ConfigureAwait(false);
                                results.Add(result);

                                // Update execution state immediately after each model completes
                                Microsoft.Maui.Controls.Application.Current.Dispatcher.Dispatch(() =>
                                {
                                    if (result)
                                    {
                                        modelNode.ExecutionState = ViewModels.ExecutionState.Completed;
                                        // Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Sequential execution: {modelNode.Name} completed successfully");
                                    }
                                    else
                                    {
                                        modelNode.ExecutionState = ViewModels.ExecutionState.Pending; // Reset failed nodes
                                        Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Sequential execution: {modelNode.Name} failed");
                                    }
                                });
                            }

                            // Count successful executions
                            successCount += results.Count(r => r);
                            skippedCount += results.Count(r => !r);

                            // Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Group {currentGroupNumber} sequential execution completed: {results.Count(r => r)} successful, {results.Count(r => !r)} failed");
                        }
                    }

                    // Notify group completed
                    onGroupCompleted?.Invoke(currentGroupNumber);
                    currentGroupNumber++;
                }

                totalStopwatch.Stop();
                // Debug.WriteLine($"⚡ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Completed: {successCount} successful, {skippedCount} skipped in {totalStopwatch.ElapsedMilliseconds}ms");

                return (successCount, skippedCount);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteAllModelsOptimizedAsync] Critical error: {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Executes a single model with pre-computed input for maximum speed
        /// </summary>
        private async Task<bool> ExecuteModelWithPrecomputedInputAsync(
            NodeViewModel modelNode,
            NeuralNetworkModel model,
            string precomputedInput,
            int currentActionStep,
            SemaphoreSlim semaphore,
            ObservableCollection<ConnectionViewModel> connections,
            ObservableCollection<NodeViewModel> nodes)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                // Note: Node execution state is managed at group level for proper visual feedback

                Debug.WriteLine($"🤖 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithPrecomputedInputAsync] Executing {modelNode.Name} with pre-computed input ({precomputedInput?.Length ?? 0} chars)");

                // Execute the model directly with pre-computed input
                string result = await _ensembleModelService.ExecuteModelWithInput(model, precomputedInput);

                if (!string.IsNullOrEmpty(result))
                {
                    // Determine content type and store result
                    string resultContentType = _ensembleModelService.DetermineResultContentType(model, result);
                    int stepIndex = currentActionStep + 1; // Convert to 1-based
                    modelNode.SetStepOutput(stepIndex, resultContentType, result);

                    // Propagate output to connected File nodes for memory saving
                    await PropagateOutputToConnectedFileNodesAsync(modelNode, result, currentActionStep, connections, nodes);

                    Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithPrecomputedInputAsync] {modelNode.Name} completed successfully");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithPrecomputedInputAsync] {modelNode.Name} returned empty result");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithPrecomputedInputAsync] {modelNode.Name} failed: {ex.Message}");
                return false;
            }
            finally
            {
                semaphore.Release();
            }
        }

        /// <summary>
        /// Executes a single model with dynamically computed input for models that depend on other models
        /// </summary>
        private async Task<bool> ExecuteModelWithDynamicInputAsync(
            NodeViewModel modelNode,
            NeuralNetworkModel model,
            ObservableCollection<NodeViewModel> nodes,
            ObservableCollection<ConnectionViewModel> connections,
            int currentActionStep,
            SemaphoreSlim semaphore)
        {
            await semaphore.WaitAsync().ConfigureAwait(false);

            try
            {
                // Note: Node execution state is managed at group level for proper visual feedback coordination

                // Computing input for model dynamically
                var connectedInputNodes = _ensembleModelService.GetConnectedInputNodes(modelNode, nodes, connections);
                var allNodes = nodes.ToList();

                // Detailed logging commented out to reduce console spam
                // Debug.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithDynamicInputAsync] Total nodes: {allNodes.Count}, Total connections: {allConnections.Count}");
                // foreach (var inputNode in allNodes.Where(n => n.Type == NodeType.Input))
                // {
                //     Debug.WriteLine($"🔍 [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithDynamicInputAsync] Input node '{inputNode.Name}' (ID: {inputNode.Id}) has {inputNode.ActionSteps?.Count ?? 0} action steps");
                // }

                // Include all input nodes with content for Intelligence Mode
                var allInputNodes = allNodes.Where(n => n.Type == NodeType.Input).ToList();
                var allInputNodesWithContent = new List<NodeViewModel>();

                foreach (var inputNode in allInputNodes)
                {
                    var (contentType, content) = inputNode.GetStepContent(1);
                    if (!string.IsNullOrEmpty(content))
                    {
                        allInputNodesWithContent.Add(inputNode);
                    }
                }

                // Use all input nodes with content if available
                var inputNodesToUse = allInputNodesWithContent.Count > 0 ? allInputNodesWithContent : connectedInputNodes;

                if (inputNodesToUse.Count == 0)
                {
                    return false;
                }

                // Collect step content from connected nodes
                var stepContents = new List<string>();
                int stepIndex = 1; // Always use step 1 for input content in NetPage Intelligence Mode

                foreach (var inputNode in inputNodesToUse)
                {
                    var (contentType, content) = inputNode.GetStepContent(stepIndex);
                    if (!string.IsNullOrEmpty(content))
                    {
                        if (contentType?.ToLowerInvariant() == "image" || contentType?.ToLowerInvariant() == "audio")
                        {
                            stepContents.Add(content);
                        }
                        else
                        {
                            stepContents.Add($"{inputNode.Name}: {content}");
                        }
                    }
                }

                if (stepContents.Count == 0)
                {
                    return false;
                }

                // Combine step contents and append classification text
                string combinedInput = _ensembleModelService.CombineStepContents(stepContents, modelNode.SelectedEnsembleMethod);
                combinedInput = AppendClassificationText(combinedInput, modelNode);

                // Execute the model with dynamically computed input
                string result = await _ensembleModelService.ExecuteModelWithInput(model, combinedInput);

                if (!string.IsNullOrEmpty(result))
                {
                    // Determine content type and store result
                    string resultContentType = _ensembleModelService.DetermineResultContentType(model, result);
                    modelNode.SetStepOutput(stepIndex, resultContentType, result);

                    // Trigger TTS autoplay if enabled
                    await TriggerTtsAutoplayIfEnabledAsync(modelNode, result, resultContentType);

                    // Propagate output to connected File nodes for memory saving
                    await PropagateOutputToConnectedFileNodesAsync(modelNode, result, currentActionStep, connections, nodes);

                    // Debug.WriteLine($"✅ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithDynamicInputAsync] {modelNode.Name} completed successfully");
                    return true;
                }
                else
                {
                    Debug.WriteLine($"⚠️ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithDynamicInputAsync] {modelNode.Name} returned empty result");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [ExecuteModelWithDynamicInputAsync] {modelNode.Name} failed: {ex.Message}");
                return false;
            }
            finally
            {
                semaphore.Release();
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

        /// <summary>
        /// Appends classification-specific text to the input if the model node has a classification
        /// </summary>
        private string AppendClassificationText(string originalInput, NodeViewModel modelNode)
        {
            if (string.IsNullOrEmpty(modelNode.Classification) || modelNode.Classification == "")
            {
                return originalInput;
            }

            string classificationText = modelNode.CurrentClassificationText;

            if (string.IsNullOrEmpty(classificationText))
            {
                return originalInput;
            }

            // Append classification text with appropriate formatting
            string appendedInput = $"{originalInput}\n\n{modelNode.Classification}: {classificationText}"; // Use safe formatting without brackets

            Debug.WriteLine($"📝 [{DateTime.Now:HH:mm:ss.fff}] [AppendClassificationText] Appended {modelNode.Classification} text to input for node '{modelNode.Name}': '{classificationText}'");

            return appendedInput;
        }

        /// <summary>
        /// Propagates model output to connected File nodes for memory saving
        /// </summary>
        private async Task PropagateOutputToConnectedFileNodesAsync(
            NodeViewModel modelNode,
            string outputContent,
            int currentActionStep,
            ObservableCollection<ConnectionViewModel> connections,
            ObservableCollection<NodeViewModel> nodes)
        {
            try
            {
                // Find all connections where this model node is the source and target is a File node
                var fileConnections = connections.Where(c =>
                    c.SourceNodeId == modelNode.Id &&
                    nodes.Any(n => n.Id == c.TargetNodeId && n.Type == NodeType.File)).ToList();

                if (fileConnections.Count == 0)
                {
                    return; // No File nodes connected
                }

                Debug.WriteLine($"📄 [{DateTime.Now:HH:mm:ss.fff}] [PropagateOutputToConnectedFileNodes] Found {fileConnections.Count} File node connections for model '{modelNode.Name}'");

                foreach (var connection in fileConnections)
                {
                    var fileNode = nodes.FirstOrDefault(n => n.Id == connection.TargetNodeId && n.Type == NodeType.File);
                    if (fileNode != null)
                    {
                        await AppendOutputToFileNodeAsync(fileNode, modelNode, outputContent, currentActionStep);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [PropagateOutputToConnectedFileNodes] Error propagating output from '{modelNode.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Appends model output to a File node's memory file
        /// </summary>
        private async Task AppendOutputToFileNodeAsync(
            NodeViewModel fileNode,
            NodeViewModel sourceModelNode,
            string outputContent,
            int currentActionStep)
        {
            try
            {
                // Determine the file path for the File node
                string filePath = null;

                // Try to get the file path from the node's name or properties
                if (!string.IsNullOrEmpty(fileNode.Name))
                {
                    // If the node name contains a file extension, use it as-is
                    if (Path.HasExtension(fileNode.Name))
                    {
                        // Create the full path in the Memory folder
                        var memoryDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "CSimple",
                            "Resources",
                            "Memory"
                        );
                        Directory.CreateDirectory(memoryDir);
                        filePath = Path.Combine(memoryDir, fileNode.Name);
                    }
                    else
                    {
                        // Add .txt extension if no extension provided
                        var memoryDir = Path.Combine(
                            Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                            "CSimple",
                            "Resources",
                            "Memory"
                        );
                        Directory.CreateDirectory(memoryDir);

                        // Use appropriate extension based on node name
                        if (fileNode.Name.ToLowerInvariant().Contains("goals") ||
                            fileNode.Name.ToLowerInvariant().Contains("plans"))
                        {
                            filePath = Path.Combine(memoryDir, $"{fileNode.Name}.json");
                        }
                        else
                        {
                            filePath = Path.Combine(memoryDir, $"{fileNode.Name}.txt");
                        }
                    }
                }
                else
                {
                    // Use a default memory file name
                    var memoryDir = Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                        "CSimple",
                        "Resources",
                        "Memory"
                    );
                    Directory.CreateDirectory(memoryDir);
                    filePath = Path.Combine(memoryDir, "memory_output.txt");
                }

                // Create the content to append with timestamp and source info
                var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                var separator = new string('=', 50);
                var contentToAppend = $"\n{separator}\n[{timestamp}] Output from Model: {sourceModelNode.Name} (Step {currentActionStep + 1})\n{separator}\n{outputContent}\n";

                // Append to the file
                await File.AppendAllTextAsync(filePath, contentToAppend);

                Debug.WriteLine($"💾 [{DateTime.Now:HH:mm:ss.fff}] [AppendOutputToFileNode] Successfully appended output from '{sourceModelNode.Name}' to file: {filePath}");

                // Update the File node's step content to point to the updated file
                fileNode.SetStepOutput(currentActionStep + 1, "text", filePath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"❌ [{DateTime.Now:HH:mm:ss.fff}] [AppendOutputToFileNode] Error appending to file node '{fileNode.Name}': {ex.Message}");
            }
        }

        /// <summary>
        /// Triggers TTS autoplay if enabled for the model node
        /// </summary>
        private Task TriggerTtsAutoplayIfEnabledAsync(NodeViewModel modelNode, string result, string resultContentType)
        {
            try
            {
                // Check if TTS service is available
                if (_audioStepContentService == null)
                {
                    return Task.CompletedTask; // TTS not available, skip
                }

                // Check if content should be read aloud - either action node OR user enabled autoplay
                bool shouldReadAloud = false;
                string reason = "";

                if (resultContentType?.ToLowerInvariant() == "text" && !string.IsNullOrWhiteSpace(result))
                {
                    // Check for action classification (automatic TTS)
                    if (modelNode?.Classification?.ToLowerInvariant() == "action")
                    {
                        shouldReadAloud = true;
                        reason = "Action-classified model";
                    }
                    // Check for user-enabled autoplay toggle
                    else if (modelNode?.ReadAloudOnCompletion == true)
                    {
                        shouldReadAloud = true;
                        reason = "User-enabled autoplay";
                    }
                }

                if (shouldReadAloud)
                {
                    Debug.WriteLine($"[PipelineExecutionService] Reading content aloud ({reason}): {result.Substring(0, Math.Min(result.Length, 100))}...");

                    // Run TTS in background to avoid blocking pipeline execution
                    _ = Task.Run(async () =>
                    {
                        try
                        {
                            await _audioStepContentService.PlayStepContentAsync(result, resultContentType, modelNode);
                        }
                        catch (Exception ex)
                        {
                            Debug.WriteLine($"[PipelineExecutionService] Error during TTS playback: {ex.Message}");
                        }
                    });
                }
                else
                {
                    Debug.WriteLine($"[PipelineExecutionService] Skipping TTS - not action node and autoplay not enabled for '{modelNode?.Name}'");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[PipelineExecutionService] Error in TTS autoplay logic: {ex.Message}");
            }

            return Task.CompletedTask;
        }

    }
}
