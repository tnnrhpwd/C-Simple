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

                // Fast execution grouping
                var executionGroups = BuildOptimizedExecutionGroups(availableModelNodes, connections);

                int successCount = 0;
                int skippedCount = 0;

                // Ultra-fast execution with minimal logging overhead
                foreach (var group in executionGroups)
                {
                    // Pre-optimize connection count cache
                    var connectionCountCache = new Dictionary<string, int>(group.Count);
                    var connectionsArray = connections.ToArray(); // Use array for fast iteration
                    
                    foreach (var modelNode in group)
                    {
                        var inputCount = 0;
                        for (int i = 0; i < connectionsArray.Length; i++)
                        {
                            if (connectionsArray[i].TargetNodeId == modelNode.Id)
                                inputCount++;
                        }
                        connectionCountCache[modelNode.Id] = inputCount;
                    }

                    // Fast executable model filtering
                    var executableModels = new List<NodeViewModel>(group.Count);
                    foreach (var modelNode in group)
                    {
                        if (modelLookupCache.ContainsKey(modelNode.Id) && 
                            CanExecuteModelNode(modelNode, connections, connectionCountCache))
                        {
                            executableModels.Add(modelNode);
                        }
                    }
                    
                    if (executableModels.Count == 0)
                    {
                        skippedCount += group.Count;
                        continue;
                    }
                    
                    // Ultra-optimized parallel execution with aggressive concurrency
                    var parallelTasks = executableModels.Select(async modelNode =>
                    {
                        var correspondingModel = modelLookupCache[modelNode.Id];
                        try
                        {
                            await ExecuteOptimizedModelNodeAsync(modelNode, correspondingModel, nodes, connections, currentActionStep).ConfigureAwait(false);
                            return true;
                        }
                        catch
                        {
                            return false;
                        }
                    });
                    
                    // Execute all models in the group concurrently with max parallelism
                    var results = await Task.WhenAll(parallelTasks).ConfigureAwait(false);
                    
                    // Ultra-fast result counting using LINQ aggregation
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

    }
}
