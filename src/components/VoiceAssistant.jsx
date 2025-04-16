import React, { useState, useEffect } from 'react';
import voiceAssistant from '../services/voiceAssistant';
import modelManager, { MODEL_CATEGORIES } from '../services/modelManager';

const VoiceAssistant = ({ isOpen, onClose }) => {
  const [isListening, setIsListening] = useState(false);
  const [transcript, setTranscript] = useState('');
  const [response, setResponse] = useState('');
  const [activeModels, setActiveModels] = useState({});
  const [availableModels, setAvailableModels] = useState({});
  const [tab, setTab] = useState('chat');

  useEffect(() => {
    // Load available and installed models when component mounts
    const loadModels = async () => {
      try {
        const installed = await modelManager.getInstalledModels();
        const categorized = {};
        
        Object.keys(MODEL_CATEGORIES).forEach(category => {
          categorized[category] = installed.filter(m => m.category === category);
        });
        
        setAvailableModels(categorized);
        
        // Set default models if available
        const defaultModels = {};
        Object.keys(MODEL_CATEGORIES).forEach(category => {
          if (categorized[category] && categorized[category].length > 0) {
            defaultModels[category.toLowerCase()] = categorized[category][0];
          }
        });
        
        setActiveModels(defaultModels);
      } catch (error) {
        console.error("Failed to load models:", error);
      }
    };
    
    loadModels();
  }, []);

  useEffect(() => {
    // Initialize voice assistant when active models change
    if (Object.keys(activeModels).length >= 4) {
      voiceAssistant.initialize(activeModels);
    }
  }, [activeModels]);

  const handleToggleListening = () => {
    if (isListening) {
      voiceAssistant.stopListening();
      setIsListening(false);
    } else {
      voiceAssistant.startListening(
        (text) => setTranscript(text),
        (reply) => setResponse(reply)
      );
      setIsListening(true);
    }
  };

  const handleDownloadModel = async (modelId, category) => {
    try {
      const model = await modelManager.downloadModel(modelId, category);
      setAvailableModels(prev => ({
        ...prev,
        [category]: [...(prev[category] || []), model]
      }));
    } catch (error) {
      console.error(`Failed to download model ${modelId}:`, error);
    }
  };

  const handleSelectModel = (model, category) => {
    setActiveModels(prev => ({
      ...prev,
      [category.toLowerCase()]: model
    }));
  };

  if (!isOpen) return null;

  return (
    <div className="voice-assistant-modal">
      <div className="voice-assistant-header">
        <h2>Voice Assistant</h2>
        <button onClick={onClose}>Close</button>
      </div>
      
      <div className="voice-assistant-tabs">
        <button 
          className={tab === 'chat' ? 'active' : ''} 
          onClick={() => setTab('chat')}
        >
          Chat
        </button>
        <button 
          className={tab === 'models' ? 'active' : ''} 
          onClick={() => setTab('models')}
        >
          Models
        </button>
      </div>
      
      {tab === 'chat' ? (
        <div className="voice-assistant-chat">
          <div className="transcript-area">
            {transcript ? <p><strong>You:</strong> {transcript}</p> : <p>Say something...</p>}
            {response && <p><strong>Assistant:</strong> {response}</p>}
          </div>
          
          <button 
            className={`listen-button ${isListening ? 'listening' : ''}`}
            onClick={handleToggleListening}
          >
            {isListening ? 'Stop Listening' : 'Start Listening'}
          </button>
        </div>
      ) : (
        <div className="voice-assistant-models">
          {Object.entries(MODEL_CATEGORIES).map(([categoryKey, category]) => (
            <div key={categoryKey} className="model-category">
              <h3>{category.name}</h3>
              <p>{category.description}</p>
              
              <h4>Selected Model</h4>
              <div className="selected-model">
                {activeModels[categoryKey.toLowerCase()] ? (
                  <p>{activeModels[categoryKey.toLowerCase()].id}</p>
                ) : (
                  <p>No model selected</p>
                )}
              </div>
              
              <h4>Available Models</h4>
              <div className="available-models">
                {availableModels[categoryKey] && availableModels[categoryKey].length > 0 ? (
                  availableModels[categoryKey].map(model => (
                    <div key={model.id} className="model-item">
                      <span>{model.id}</span>
                      <button onClick={() => handleSelectModel(model, categoryKey)}>
                        Select
                      </button>
                    </div>
                  ))
                ) : (
                  <p>No models installed</p>
                )}
              </div>
              
              <h4>Recommended Models</h4>
              <div className="recommended-models">
                {category.recommendedModels.map(model => (
                  <div key={model.id} className="model-item">
                    <div>
                      <strong>{model.id}</strong>
                      <p>{model.description} ({model.size})</p>
                    </div>
                    <button onClick={() => handleDownloadModel(model.id, categoryKey)}>
                      Download
                    </button>
                  </div>
                ))}
              </div>
            </div>
          ))}
        </div>
      )}
    </div>
  );
};

export default VoiceAssistant;
