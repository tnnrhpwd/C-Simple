# Memory Files Directory

This directory stores memory files created for File/Memory nodes in the C-Simple pipeline editor.

Memory files are used to store outputs from model executions and can be used as persistent storage for AI model interactions.

## File Structure

- Memory files are typically text files (.txt)
- Each file contains timestamped outputs from connected model nodes
- Files can be manually edited to adjust memory content
- File paths are saved with pipeline configurations

## Usage

1. Add a "Memory (File)" node to your pipeline
2. Select the memory node
3. Click "Create New..." to create a new memory file
4. Or click "Select File..." to choose an existing file
5. Connect other nodes to the memory node to route outputs

## Example Memory File Content

```
# Memory File for Memory Node
Created: 2025-01-21 12:00:00
Node Type: File
Data Type: text

## Memory Contents
This file will store outputs from the 'Memory Node' node.

[2025-01-21 12:05:00] Model Output: Hello, this is a test output from the AI model.
[2025-01-21 12:06:15] Model Output: Another response from the model.
```
