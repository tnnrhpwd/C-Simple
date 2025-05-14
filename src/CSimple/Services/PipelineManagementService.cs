using CSimple.Models;
using CSimple.ViewModels;
using System;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace CSimple.Services
{
    public class PipelineManagementService
    {
        private readonly FileService _fileService;
        private readonly NodeManagementService _nodeManagementService;

        public PipelineManagementService(FileService fileService, NodeManagementService nodeManagementService)
        {
            _fileService = fileService;
            _nodeManagementService = nodeManagementService;
        }

        public async Task LoadAvailablePipelinesAsync(ObservableCollection<string> AvailablePipelineNames)
        {
            try
            {
                var pipelines = await _fileService.ListPipelinesAsync();
                AvailablePipelineNames.Clear();
                if (pipelines != null)
                {
                    foreach (var pipeline in pipelines.OrderBy(p => p.Name))
                    {
                        AvailablePipelineNames.Add(pipeline.Name);
                    }
                }
                // Optionally set the SelectedPipelineName if needed within OrientPage itself
                // SelectedPipelineName = AvailablePipelineNames.FirstOrDefault();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Error loading available pipeline names in OrientPageViewModel: {ex.Message}");
            }
        }

        public async Task LoadPipelineAsync(string pipelineName, ObservableCollection<NodeViewModel> Nodes, ObservableCollection<ConnectionViewModel> Connections, Action InvalidateCanvas, string CurrentPipelineName, Func<string, Task> DisplayAlert, Action<string> SetCurrentPipelineName, Action<string> SetSelectedPipelineName, Action<string> OnPropertyChanged, Func<Task> UpdateNodeClassificationsAsync)
        {
            Debug.WriteLine($"Loading pipeline: {pipelineName}");
            var pipelineData = await _fileService.LoadPipelineAsync(pipelineName);
            if (pipelineData != null)
            {
                // Check if we are loading the same pipeline that's already current
                // and if the canvas is already empty (avoids unnecessary clearing/reloading)
                if (CurrentPipelineName == pipelineName && !Nodes.Any() && !Connections.Any() && pipelineData.Nodes.Count == 0 && pipelineData.Connections.Count == 0)
                {
                    Debug.WriteLine($"Pipeline '{pipelineName}' is already the current empty pipeline. Skipping reload.");
                    // Ensure CurrentPipelineName is set correctly even if skipping
                    if (CurrentPipelineName != pipelineName)
                    {
                        SetCurrentPipelineName(pipelineName);
                    }
                    // Ensure picker reflects the name
                    SetSelectedPipelineName(pipelineName);
                    OnPropertyChanged(nameof(pipelineName));
                    return; // Exit early
                }

                _nodeManagementService.LoadPipelineData(Nodes, Connections, pipelineData, InvalidateCanvas);
                SetCurrentPipelineName(pipelineData.Name);
                // Manually update the picker selection if needed, though binding should handle it
                SetSelectedPipelineName(pipelineName);
                OnPropertyChanged(nameof(pipelineName));
                Debug.WriteLine($"Successfully loaded pipeline: {pipelineName}");
                // After loading, update classifications based on the newly loaded nodes
                await UpdateNodeClassificationsAsync();
            }
            else
            {
                // Only show error if it's not the initial creation scenario
                if (!(CurrentPipelineName == pipelineName && !Nodes.Any() && !Connections.Any()))
                {
                    await DisplayAlert?.Invoke($"Failed to load pipeline '{pipelineName}'.");
                }
                else
                {
                    Debug.WriteLine($"Failed to load pipeline '{pipelineName}', but assuming it's the initial empty one being created.");
                }
            }
        }

        public async Task SaveCurrentPipelineAsync(string CurrentPipelineName, ObservableCollection<NodeViewModel> Nodes, ObservableCollection<ConnectionViewModel> Connections)
        {
            if (string.IsNullOrWhiteSpace(CurrentPipelineName))
            {
                // This shouldn't happen if CurrentPipelineName is managed correctly
                Debug.WriteLine("Cannot save pipeline with empty name.");
                return;
            }

            Debug.WriteLine($"Saving pipeline: {CurrentPipelineName}");
            var pipelineData = new PipelineData
            {
                Name = CurrentPipelineName,
                Nodes = Nodes.Select(n => new SerializableNode(n)).ToList(),
                Connections = Connections.Select(c => new SerializableConnection(c)).ToList()
            };
            await _fileService.SavePipelineAsync(pipelineData);
            // No need to reload list unless timestamp sorting is critical for immediate UI update
        }

        public async Task CreateNewPipelineAsync(ObservableCollection<string> AvailablePipelineNames, ObservableCollection<NodeViewModel> Nodes, ObservableCollection<ConnectionViewModel> Connections, Action InvalidateCanvas, Action ClearCanvas, Action<string> SetCurrentPipelineName, Action<string> OnPropertyChanged)
        {
            ClearCanvas();
            // Find a unique default name
            int counter = 1;
            string baseName = "Untitled Pipeline";
            string newName = baseName;
            while (AvailablePipelineNames.Contains(newName))
            {
                newName = $"{baseName} {counter++}";
            }
            SetCurrentPipelineName(newName);

            Debug.WriteLine($"Created new pipeline placeholder: {newName}");

            // Add default input nodes
            _nodeManagementService.AddDefaultInputNodes(Nodes, newName);

            // Add to list immediately so it can be selected later
            if (!AvailablePipelineNames.Contains(newName))
            {
                AvailablePipelineNames.Insert(0, newName); // Add to top
                OnPropertyChanged(nameof(AvailablePipelineNames)); // Notify UI about the change
            }
            // No saving or selecting here - handled by the command or initialization logic
            await Task.CompletedTask;
        }

        public async Task<bool> RenamePipelineAsync(string oldName, string newName)
        {
            if (string.IsNullOrWhiteSpace(oldName))
            {
                Debug.WriteLine("No pipeline selected to rename.");
                return false;
            }

            if (!string.IsNullOrWhiteSpace(newName) && newName != oldName)
            {
                if (true)
                {
                    if (AvailablePipelineNamesContain(newName))
                    {
                        Debug.WriteLine($"A pipeline named '{newName}' already exists.");
                        return false;
                    }
                }

                bool success = await _fileService.RenamePipelineAsync(oldName, newName);
                return success;
            }
            return false;
        }

        public async Task DeletePipelineAsync(string nameToDelete)
        {
            await _fileService.DeletePipelineAsync(nameToDelete);
        }

        private bool AvailablePipelineNamesContain(string newName)
        {
            throw new NotImplementedException();
        }
    }
}
