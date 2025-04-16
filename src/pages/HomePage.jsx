import React, { useState } from 'react';
import VoiceAssistant from '../components/VoiceAssistant';
import '../styles/voiceAssistant.css';

function HomePage() {
  const [voiceAssistantOpen, setVoiceAssistantOpen] = useState(false);

  // Assuming there's an existing toggle for AI assistant
  const handleAIToggleChange = (isOn) => {
    // Keep any existing functionality
    // ...existing code...
    
    // Toggle the voice assistant modal
    setVoiceAssistantOpen(isOn);
  };

  return (
    <div>
      {/* Assuming there's an existing AI toggle component */}
      <Toggle 
        label="AI Assistant" 
        onChange={handleAIToggleChange}
        // ...any other existing props...
      />
      
      {/* Add the voice assistant component */}
      <VoiceAssistant 
        isOpen={voiceAssistantOpen} 
        onClose={() => setVoiceAssistantOpen(false)} 
      />
    </div>
  );
}

export default HomePage;