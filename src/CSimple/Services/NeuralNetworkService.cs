using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CSimple.Models;

namespace CSimple.Services
{
    public class NeuralNetworkService
    {
        private readonly List<NeuralModel> _models = new List<NeuralModel>();
        private bool _isInitialized = false;

        public NeuralNetworkService()
        {
            InitializeDefaultModels();
        }

        private void InitializeDefaultModels()
        {
            if (_isInitialized)
                return;

            try
            {
                // Add sample models
                _models.Add(new NeuralModel
                {
                    Name = "General Assistant v1.0",
                    Description = "Basic model for general purpose assistance",
                    Architecture = "Multilayer Perceptron",
                    Accuracy = 0.85,
                    TrainingEpochs = 50,
                    LearningRate = 0.01,
                    BatchSize = 32,
                    DropoutRate = 0.2,
                    UsesScreenData = true,
                    UsesAudioData = true,
                    UsesTextData = true,
                    TrainingDataPoints = 3500,
                    IsActive = true
                });

                _models.Add(new NeuralModel
                {
                    Name = "Image Recognition Model",
                    Description = "Specialized for image pattern recognition",
                    Architecture = "Convolutional Neural Network",
                    Accuracy = 0.78,
                    TrainingEpochs = 100,
                    LearningRate = 0.001,
                    BatchSize = 64,
                    DropoutRate = 0.3,
                    UsesScreenData = true,
                    UsesAudioData = false,
                    UsesTextData = false,
                    TrainingDataPoints = 2000,
                    LastTrainedDate = DateTime.Now.AddDays(-1),
                    IsActive = false
                });

                _isInitialized = true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error initializing models: {ex.Message}");
                // Log error or handle exception as needed
            }
        }

        // Get all available models
        public List<NeuralModel> GetAllModels()
        {
            return _models.ToList(); // Return a copy to prevent external modification
        }

        // Get active models
        public List<NeuralModel> GetActiveModels()
        {
            return _models.Where(m => m.IsActive).ToList();
        }

        // Get model by ID
        public NeuralModel GetModelById(string id)
        {
            return _models.FirstOrDefault(m => m.Id == id);
        }

        // Add a new model
        public bool AddModel(NeuralModel model)
        {
            if (model == null)
                return false;

            try
            {
                if (string.IsNullOrEmpty(model.Id))
                    model.Id = Guid.NewGuid().ToString();

                _models.Add(model);
                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error adding model: {ex.Message}");
                return false;
            }
        }

        // Train a model (simulated)
        public async Task<(bool Success, double Accuracy)> TrainModelAsync(NeuralModel model, IProgress<double> progress = null)
        {
            if (model == null)
                return (false, 0);

            try
            {
                // Simulate training process
                for (int i = 1; i <= 5; i++)
                {
                    await Task.Delay(1000);
                    progress?.Report(i / 5.0);
                }

                // Update model properties
                model.LastTrainedDate = DateTime.Now;
                model.Accuracy = Math.Min(model.Accuracy + 0.05, 0.99);
                model.TrainingDataPoints += 500;

                return (true, model.Accuracy);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error training model: {ex.Message}");
                return (false, model.Accuracy);
            }
        }

        // Activate/deactivate model
        public bool SetModelActiveState(string modelId, bool active)
        {
            try
            {
                var model = _models.FirstOrDefault(m => m.Id == modelId);
                if (model != null)
                {
                    model.IsActive = active;
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error setting model state: {ex.Message}");
                return false;
            }
        }

        // Get average accuracy of all models
        public double GetAverageAccuracy()
        {
            if (_models.Count == 0) return 0;

            return _models.Average(m => m.Accuracy);
        }

        // Get total training data points
        public int GetTotalDataPoints()
        {
            return _models.Sum(m => m.TrainingDataPoints);
        }

        // Delete a model
        public bool DeleteModel(string modelId)
        {
            try
            {
                var model = _models.FirstOrDefault(m => m.Id == modelId);
                if (model != null)
                {
                    _models.Remove(model);
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error deleting model: {ex.Message}");
                return false;
            }
        }
    }
}
